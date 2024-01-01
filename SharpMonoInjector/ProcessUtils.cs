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
            var address = mod + memory.Read<int>(funcs + memory.Read<short>(ordinals + i * 2) * 4);
            if (address != 0) yield return new(memory.ReadString(mod + memory.Read<int>(names + i * 4), 32, Encoding.ASCII), address);
        }
    }
    public unsafe static bool GetMonoModule(nint handle, out nint monoModule)
    {
        if (!Native.EnumProcessModulesEx(handle, 0, 0, out var bytesNeeded))
            throw new InjectorException("Failed to enumerate process modules", new Win32Exception(Marshal.GetLastWin32Error()));

        var count = bytesNeeded / nint.Size;
        var ptrs = stackalloc nint[count];

        if (!Native.EnumProcessModulesEx(handle, (nint)ptrs, bytesNeeded, out _))
            throw new InjectorException("Failed to enumerate process modules", new Win32Exception(Marshal.GetLastWin32Error()));

        var path = stackalloc sbyte[520];
        for (var i = 0; i < count; ++i) try
        {
            var len = Native.GetModuleFileNameEx(handle, ptrs[i], (nint)path, 260);
            if (new string(path, 0, len).IndexOf("mono", StringComparison.OrdinalIgnoreCase) > -1)
            {
                if (!Native.GetModuleInformation(handle, ptrs[i], out var info, nint.Size * count))
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
            Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "[ProcessUtils] GetMono - ERROR: " + e.Message);
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
                    ushort pMachine = 0, nMachine = 0;
                    try
                    {
                        if (!IsWow64Process2(handle, out pMachine, out nMachine)) { /*handle error*/ }

                        if (pMachine == 332) isTargetx64 = false;
                        else isTargetx64 = true;

                        return isTargetx64;
                    }
                    catch { /* Will try the Win7 method */ }
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
            Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "[ProcessUtils] is64Bit - ERROR: " + e.Message); 
        }
        return true;
    }
    public static bool AntivirusInstalled()
    {
        #region Pre-Windows 7
        /* 
        try
        {
            var defenderFlag = false;
            var wmipathstr = @"\\" + Environment.MachineName + @"\root\SecurityCenter";

            var searcher = new ManagementObjectSearcher(wmipathstr, "SELECT * FROM AntivirusProduct");
            var instances = searcher.Get();

            if (instances.Count > 0)
            {
                Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "AntiVirus Installed: True");
                var installedAVs = "Installed AntiVirus':\r\n";

                foreach (var av in instances)
                {
                    installedAVs += av.GetText(TextFormat.WmiDtd20) + "\r\n";
                    var AVInstalled = ((string)av.GetPropertyValue("pathToSignedProductExe")).Replace("//", "") + " " + (string)av.GetPropertyValue("pathToSignedReportingExe");
                    installedAVs += "   " + AVInstalled + "\r\n";

                    if (((string)av.GetPropertyValue("pathToSignedProductExe")).StartsWith("windowsdefender") && ((string)av.GetPropertyValue("pathToSignedReportingExe")).EndsWith("Windows Defender\\MsMpeng.exe")) defenderFlag = true;
                }
                Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", installedAVs);
            }
            else Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "AntiVirus Installed: False");

            if (defenderFlag) return false;
            else return instances.Count > 0;
        }
        catch (Exception e)
        {
            Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "Error Checking for AV: " + e.Message);
        }
        */
        #endregion

        try
        {
            List<string> avs = [];
            var defenderFlag = false;
            var wmipathstr = @"\\" + Environment.MachineName + @"\root\SecurityCenter2";

            ManagementObjectSearcher searcher = new(wmipathstr, "SELECT * FROM AntivirusProduct");
            var instances = searcher.Get();

            if (instances.Count > 0)
            {
                Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "AntiVirus Installed: True");
                StringBuilder installedAVs = new("Installed AntiVirus':\r\n");

                foreach (var av in instances)
                {
                    var AVInstalled = ((string)av.GetPropertyValue("pathToSignedProductExe")).Replace("//", "") + " " + (string)av.GetPropertyValue("pathToSignedReportingExe");
                    installedAVs.AppendLine("   " + AVInstalled);
                    avs.Add(AVInstalled.ToLower());
                }
                Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", installedAVs.ToString());
            }
            else Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "AntiVirus Installed: False");

            Parallel.ForEach(Process.GetProcesses(), p => avs.ForEach(detectedAV =>
            {
                if (detectedAV.EndsWith(p.ProcessName.ToLower() + ".exe")) Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "AntiVirus Running: " + detectedAV);
            }));

            if (defenderFlag) return false;
            else return instances.Count > 0;
        }
        catch (Exception e)
        {
            Trace.WriteLine(Environment.CurrentDirectory + "\\DebugLog.txt", "Error Checking for AV: " + e.Message);
        }
        return false;
    }
}