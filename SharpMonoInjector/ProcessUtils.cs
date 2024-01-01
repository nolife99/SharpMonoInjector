using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SharpMonoInjector;

public static class ProcessUtils
{ 
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool IsWow64Process2(nint hProcess, out ushort processMachine, out ushort nativeMachine);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool IsWow64Process(nint hProcess, out bool wow64Process);
    static bool isTargetx64;

    public static IEnumerable<ExportedFunction> GetExportedFunctions(nint handle, nint mod)
    {
        using ProcessMemory memory = new(handle);

        var exportDir = mod + memory.Read<int>(mod + memory.Read<int>(mod + 0x3C) + 0x18 + (Is64BitProcess(handle) ? 0x70 : 0x60));
        var names = mod + memory.Read<int>(exportDir + 0x20);
        var ordinals = mod + memory.Read<int>(exportDir + 0x24);
        var funcs = mod + memory.Read<int>(exportDir + 0x1C);

        for (var i = 0; i < memory.Read<int>(exportDir + 0x18); ++i)
        {
            var addr = mod + memory.Read<int>(funcs + memory.Read<short>(ordinals + i * 2) * 4);
            if (addr != 0) yield return new(memory.ReadString(mod + memory.Read<int>(names + i * 4), 32, Encoding.ASCII), addr);
        }
    }
    public unsafe static bool GetMonoModule(nint handle, out nint monoModule)
    {
        if (!Native.EnumProcessModulesEx(handle, 0, 0, out var bytesNeeded))
            throw new InjectorException("Failed to get process module count", new Win32Exception(Marshal.GetLastWin32Error()));

        var count = bytesNeeded / (Is64BitProcess(handle) ? 8 : 4);
        var ptrs = stackalloc nint[count];

        if (!Native.EnumProcessModulesEx(handle, (nint)ptrs, bytesNeeded, out _))
            throw new InjectorException("Failed to enumerate process modules", new Win32Exception(Marshal.GetLastWin32Error()));

        const int MAX_PATH = 260;
        var path = stackalloc sbyte[MAX_PATH * Marshal.SizeOf<char>()];

        for (var i = 0; i < count; ++i) try
        {
            if (new string(path, 0, Native.GetModuleFileNameEx(handle, ptrs[i], (nint)path, MAX_PATH)).Contains("mono", StringComparison.OrdinalIgnoreCase))
            {
                if (!Native.GetModuleInformation(handle, ptrs[i], out var info, bytesNeeded))
                    throw new InjectorException("Failed to get module information", new Win32Exception(Marshal.GetLastWin32Error()));

                if (GetExportedFunctions(handle, info).Any(f => f.Name == "mono_get_root_domain"))
                {
                    monoModule = info;
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine("[ProcessUtils] GetMono - ERROR: " + e.Message);
        }

        monoModule = 0;
        return false;
    }
    public static bool Is64BitProcess(nint handle)
    {
        try
        {
            if (!Environment.Is64BitOperatingSystem) return false;

            var OSVer = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion", "ProductName", null);
            Console.WriteLine(OSVer);

            if (OSVer.Contains("Windows 10"))
            {
                #region Win10
        
                isTargetx64 = false;
                if (handle != 0)
                {
                    IsWow64Process2(handle, out var pMachine, out _);

                    if (pMachine == 332) isTargetx64 = false;
                    else isTargetx64 = true;

                    return isTargetx64;
                }
        
                #endregion
            }

            #region Win7

            IsWow64Process(handle, out bool isTargetWOWx64);
            return !isTargetWOWx64;

            #endregion  
        }
        catch (Exception e) 
        { 
            Trace.WriteLine("[ProcessUtils] is64Bit - ERROR: " + e.Message); 
        }
        return true;
    }
    public static bool AntivirusInstalled()
    {
        try
        {
            List<string> avs = [];
            var defenderFlag = false;

            using ManagementObjectSearcher searcher = new(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", "SELECT * FROM AntivirusProduct");
            using var instances = searcher.Get();

            if (instances.Count > 0)
            {
                Trace.WriteLine("Antivirus Installed: True");
                StringBuilder installedAVs = new("Installed Antivirus:\n");

                foreach (var av in instances)
                {
                    var avInstalled = ((string)av.GetPropertyValue("pathToSignedProductExe")).Replace("//", "") + " " + (string)av.GetPropertyValue("pathToSignedReportingExe");
                    installedAVs.AppendLine("   " + avInstalled);
                    avs.Add(avInstalled.ToLower());
                }
                Trace.WriteLine(installedAVs.ToString());
            }
            else Trace.WriteLine("Antivirus Installed: False");

            Parallel.ForEach(Process.GetProcesses(), p => Parallel.ForEach(avs, detect =>
            {
                if (detect.EndsWith(p.ProcessName + ".exe", StringComparison.OrdinalIgnoreCase)) Trace.WriteLine("Antivirus Running: " + detect);
            }));

            if (defenderFlag) return false;
            else return instances.Count > 0;
        }
        catch (Exception e)
        {
            Trace.WriteLine("Error checking for Antivirus: " + e.Message);
        }
        return false;
    }
}