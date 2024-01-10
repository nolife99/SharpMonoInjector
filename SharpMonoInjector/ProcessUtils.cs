using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using Microsoft.Win32;

namespace SharpMonoInjector;

public static class ProcessUtils
{
    internal static IEnumerable<ExportedFunction> GetExportedFunctions(Process proc, nint mod)
    {
        using ProcessMemory memory = new(proc);

        var exportDir = mod + memory.Read<int>(mod + memory.Read<int>(mod + 0x3C) + 0x18 + (Is64BitProcess(proc) ? 0x70 : 0x60));
        var names = mod + memory.Read<int>(exportDir + 0x20);
        var ordinals = mod + memory.Read<int>(exportDir + 0x24);
        var funcs = mod + memory.Read<int>(exportDir + 0x1C);

        for (var i = 0; i < memory.Read<int>(exportDir + 0x18); ++i)
        {
            var addr = mod + memory.Read<int>(funcs + memory.Read<short>(ordinals + i * 2) * 4);
            if (addr != 0) yield return new(memory.ReadString(mod + memory.Read<int>(names + i * 4), 32, Encoding.ASCII), addr);
        }
    }
    public unsafe static bool GetMonoModule(Process process, out nint monoModule)
    {
        if (!Native.EnumProcessModulesEx(process.SafeHandle, 0, 0, out var bytesNeeded))
            throw new InjectorException("Failed to get process module count", new Win32Exception());

        var count = bytesNeeded / (Is64BitProcess(process) ? 8 : 4);
        var ptrs = stackalloc nint[count];

        if (!Native.EnumProcessModulesEx(process.SafeHandle, (nint)ptrs, bytesNeeded, out _))
            throw new InjectorException("Failed to enumerate process modules", new Win32Exception());

        const int MAX_PATH = 260;
        var path = stackalloc sbyte[MAX_PATH];

        for (var i = 0; i < count; ++i) try
        {
            if (new string(path, 0, Native.GetModuleFileNameExA(process.SafeHandle, ptrs[i], (nint)path, MAX_PATH)).Contains("mono", StringComparison.OrdinalIgnoreCase))
            {
                if (!Native.GetModuleInformation(process.SafeHandle, ptrs[i], out var info, bytesNeeded)) throw new InjectorException("Failed to get module information", new Win32Exception());
                if (GetExportedFunctions(process, info).Any(x => x.Name == "mono_get_root_domain"))
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

    static bool is64;
    internal static bool Is64BitProcess(Process proc)
    {
        try
        {
            if (!Environment.Is64BitOperatingSystem) return false;

            var OSVer = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion", "ProductName", null);
            if (OSVer.Contains("Windows 10"))
            {
                Native.IsWow64Process2(proc.SafeHandle, out var pMachine, out _);

                if (pMachine == 332) is64 = false;
                else is64 = true;

                return is64;
            }

            #region Win7

            Native.IsWow64Process(proc.SafeHandle, out bool isWOW64);
            return !isWOW64;

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
            using ManagementObjectSearcher searcher = new(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", "SELECT * FROM AntivirusProduct");
            using var instances = searcher.Get();

            if (instances.Count > 0)
            {
                Trace.WriteLine("Antivirus Installed: True");
                StringBuilder installedAVs = new("Installed Antivirus:\n");

                instances.AsParallel().Cast<ManagementBaseObject>().ForAll(av =>
                {
                    using (av)
                    {
                        var avInstalled = ((string)av.GetPropertyValue("pathToSignedProductExe")).Replace("//", "") + " " + (string)av.GetPropertyValue("pathToSignedReportingExe");
                        avs.Add(avInstalled.ToLower());
                        lock (installedAVs) installedAVs.AppendLine("   " + avInstalled);
                    }
                });
                Trace.WriteLine(installedAVs.ToString());
            }
            else Trace.WriteLine("Antivirus Installed: False");

            Process.GetProcesses().AsParallel().ForAll(p =>
            {
                using (p) avs.AsParallel().ForAll(detect =>
                {
                    if (detect.EndsWith(p.ProcessName + ".exe", StringComparison.OrdinalIgnoreCase)) Trace.WriteLine("Antivirus Running: " + detect);
                });
            });
            return instances.Count > 0;
        }
        catch (Exception e)
        {
            Trace.WriteLine("Error checking for Antivirus: " + e.ToString());
        }
        return false;
    }
}