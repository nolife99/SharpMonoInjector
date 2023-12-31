using Microsoft.Win32;
using SharpMonoInjector.Gui.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

namespace SharpMonoInjector.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModel
{
    public MainWindowViewModel()
    {
        AVAlert = ProcessUtils.AntivirusInstalled();
        if (AVAlert) AVColor = "#FFA00668";
        else AVColor = "#FF21AC40";

        RefreshCommand = new(ExecuteRefreshCommand, CanExecuteRefreshCommand);
        BrowseCommand = new(ExecuteBrowseCommand);
        InjectCommand = new(ExecuteInjectCommand, CanExecuteInjectCommand);
        EjectCommand = new(ExecuteEjectCommand, CanExecuteEjectCommand);
        CopyStatusCommand = new(ExecuteCopyStatusCommand);
    }

    #region Commands

    public RelayCommand RefreshCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand InjectCommand { get; }
    public RelayCommand EjectCommand { get; }
    public RelayCommand CopyStatusCommand { get; }

    void ExecuteCopyStatusCommand(object param) => Clipboard.SetText(Status);
    bool CanExecuteRefreshCommand(object param) => !IsRefreshing;

    async void ExecuteRefreshCommand(object param)
    {
        File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "[MainWindowViewModel] - ExecuteRefresh Entered\r\n");
        IsRefreshing = true;
        Status = "Refreshing processes";

        List<MonoProcess> processes = [];
        File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "[MainWindowViewModel] - Setting Process Access Rights:\r\n\tPROCESS_QUERY_INFORMATION\r\n\tPROCESS_VM_READ\r\n");
        File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "[MainWindowViewModel] - Checking Processes for Mono\r\n");

        await Task.Run(() =>
        {
            var cp = Environment.ProcessId;
            foreach (var p in Process.GetProcesses()) try
            {
                if (GetProcessUser(p) != null && p.Id != cp)
                {
                    nint handle;
                    if ((handle = Native.OpenProcess(ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ, false, p.Id)) != 0)
                    {
                        File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "\t" + p.ProcessName + ".exe\r\n");
                        if (ProcessUtils.GetMonoModule(handle, out var mono))
                        {
                            File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "\t\tMono found in process: " + p.ProcessName + ".exe\r\n");
                            processes.Add(new()
                            {
                                MonoModule = mono,
                                Id = p.Id,
                                Name = p.ProcessName
                            });
                            break;
                        }
                        Native.CloseHandle(handle);
                    }
                }
            }
            catch (Exception e) 
            { 
                File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "    ERROR SCANNING: " + p.ProcessName + " - " + e.Message + "\r\n"); 
            }
            File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "FINISHED SCANNING PROCESSES...\r\n");
        });

        Processes = processes;
        if (_processes.Count > 0)
        {
            Status = "Processes refreshed";
            SelectedProcess = _processes[0];
        }
        else
        {
            Status = "No Mono process found";
            File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "No Mono process found:\r\n");
        }
        IsRefreshing = false;
    }

    void ExecuteBrowseCommand(object param)
    {
        OpenFileDialog ofd = new()
        {
            Filter = "Dynamic Link Library|*.dll",
            Title = "Select assembly to inject",
        };
        if (ofd.ShowDialog().Value) AssemblyPath = ofd.FileName;
    }

    bool CanExecuteInjectCommand(object param) => SelectedProcess != null && File.Exists(AssemblyPath) &&
        !string.IsNullOrEmpty(InjectClassName) && !string.IsNullOrEmpty(InjectMethodName) &&
        !IsExecuting;

    void ExecuteInjectCommand(object param)
    {
        nint handle;
        try
        {
            if ((handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, SelectedProcess.Id)) == 0)
            {
                Status = "Failed to open process";
                return;
            }
        }
        catch (Exception ex)
        {
            Status = "Error: " + ex.Message;
            return;
        }

        byte[] file;
        try
        {
            file = File.ReadAllBytes(AssemblyPath);
        }
        catch (IOException)
        {
            Status = "Failed to read the file " + AssemblyPath;
            return;
        }

        IsExecuting = true;
        Status = "Injecting " + Path.GetFileName(AssemblyPath);

        using (Injector injector = new(handle, SelectedProcess.MonoModule))
        {
            try
            {
                var asm = injector.Inject(file, InjectNamespace, InjectClassName, InjectMethodName);
                InjectedAssemblies.Add(new()
                {
                    ProcessId = SelectedProcess.Id,
                    Address = asm,
                    Name = Path.GetFileName(AssemblyPath),
                    Is64Bit = injector.Is64Bit
                });
                Status = "Injection successful";
            }
            catch (InjectorException ie)
            {
                Status = "Injection failed: " + ie.Message;
            }
            catch (Exception e)
            {
                Status = "Injection failed (unknown error): " + e.Message;
            }
        }
        IsExecuting = false;
    }

    bool CanExecuteEjectCommand(object param) => SelectedAssembly != null &&
        !string.IsNullOrEmpty(EjectClassName) && !string.IsNullOrEmpty(EjectMethodName) &&
        !IsExecuting;

    void ExecuteEjectCommand(object param)
    {
        var handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, SelectedAssembly.ProcessId);
        if (handle == 0)
        {
            Status = "Failed to open process";
            return;
        }

        IsExecuting = true;
        Status = "Ejecting " + SelectedAssembly.Name;

        ProcessUtils.GetMonoModule(handle, out var mono);
        using (Injector injector = new(handle, mono))
        {
            try
            {
                injector.Eject(SelectedAssembly.Address, EjectNamespace, EjectClassName, EjectMethodName);
                InjectedAssemblies.Remove(SelectedAssembly);
                Status = "Ejection successful";
            }
            catch (InjectorException ie)
            {
                Status = "Ejection failed: " + ie.Message;
            }
            catch (Exception e)
            {
                Status = "Ejection failed (unknown error): " + e.Message;
            }
        }
        IsExecuting = false;
    }

    #endregion

    #region XML Props

    bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            Set(ref _isRefreshing, value);
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    bool _isExecuting;
    public bool IsExecuting
    {
        get => _isExecuting;
        set
        {
            Set(ref _isExecuting, value);
            InjectCommand.RaiseCanExecuteChanged();
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    List<MonoProcess> _processes;
    public List<MonoProcess> Processes
    {
        get => _processes;
        set => Set(ref _processes, value);
    }

    MonoProcess _selectedProcess;
    public MonoProcess SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            _selectedProcess = value;
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string _status;
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    bool _avalert;
    public bool AVAlert
    {
        get => _avalert;
        set => Set(ref _avalert, value);
    }

    string _avcolor;
    public string AVColor
    {
        get => _avcolor;
        set => Set(ref _avcolor, value);
    }

    string _assemblyPath;
    public string AssemblyPath
    {
        get => _assemblyPath;
        set
        {
            Set(ref _assemblyPath, value);
            if (File.Exists(_assemblyPath)) InjectNamespace = Path.GetFileNameWithoutExtension(_assemblyPath);
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string _injectNamespace;
    public string InjectNamespace
    {
        get => _injectNamespace;
        set
        {
            Set(ref _injectNamespace, value);
            EjectNamespace = value;
        }
    }

    string _injectClassName;
    public string InjectClassName
    {
        get => _injectClassName;
        set
        {
            Set(ref _injectClassName, value);
            EjectClassName = value;
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string _injectMethodName;
    public string InjectMethodName
    {
        get => _injectMethodName;
        set
        {
            Set(ref _injectMethodName, value);
            if (_injectMethodName == "Load") EjectMethodName = "Unload";
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    ObservableCollection<InjectedAssembly> _injectedAssemblies = [];
    public ObservableCollection<InjectedAssembly> InjectedAssemblies
    {
        get => _injectedAssemblies;
        set => Set(ref _injectedAssemblies, value);
    }

    InjectedAssembly _selectedAssembly;
    public InjectedAssembly SelectedAssembly
    {
        get => _selectedAssembly;
        set
        {
            Set(ref _selectedAssembly, value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string _ejectNamespace;
    public string EjectNamespace
    {
        get => _ejectNamespace;
        set => Set(ref _ejectNamespace, value);
    }

    string _ejectClassName;
    public string EjectClassName
    {
        get => _ejectClassName;
        set
        {
            Set(ref _ejectClassName, value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string _ejectMethodName;
    public string EjectMethodName
    {
        get => _ejectMethodName;
        set
        {
            Set(ref _ejectMethodName, value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    #endregion

    #region Process Refresh Fix

    static string GetProcessUser(Process process)
    {
        var result = "";
        nint processHandle = 0;

        try
        {
            Native.OpenProcessToken(process.Handle, 8, out processHandle);
            var user = new WindowsIdentity(processHandle).Name;
            result = user.Contains('\\') ? user[(user.IndexOf('\\') + 1)..] : user;
        }
        catch (Exception e)
        {
            File.AppendAllText(Environment.CurrentDirectory + "\\DebugLog.txt", "    Error Getting User Process: " + process.ProcessName + " - " + e.Message + "\r\n");
            return null;
        }
        finally
        {
            if (processHandle != 0) Native.CloseHandle(processHandle);
        }
        return result;
    }

    #endregion
}