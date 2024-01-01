using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SharpMonoInjector.Gui.Models;

namespace SharpMonoInjector.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModel
{
    public MainWindowViewModel()
    {
        AVAlert = ProcessUtils.AntivirusInstalled();
        if (AVAlert) AVColor = "#FFA00668";
        else AVColor = "#FF21AC40";

        RefreshCommand = new(async () =>
        {
            Trace.WriteLine("[MainWindowViewModel] - ExecuteRefresh Entered");
            IsRefreshing = true;
            Status = "Refreshing processes";

            List<MonoProcess> processes = [];
            Trace.WriteLine("[MainWindowViewModel] - Setting Process Access Rights:\r\n\tPROCESS_QUERY_INFORMATION\r\n\tPROCESS_VM_READ");
            Trace.WriteLine("[MainWindowViewModel] - Checking Processes for Mono");

            await Task.Run(() =>
            {
                var cp = Environment.ProcessId;
                Parallel.ForEach(Process.GetProcesses(), (p, l) =>
                {
                    try
                    {
                        if (GetProcessUser(p) is not null && p.Id != cp)
                        {
                            nint handle;
                            if ((handle = Native.OpenProcess(ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ, false, p.Id)) != 0)
                            {
                                Trace.WriteLine("\t" + p.ProcessName + ".exe");
                                if (ProcessUtils.GetMonoModule(handle, out var mono))
                                {
                                    Trace.WriteLine("\t\tMono found in process: " + p.ProcessName + ".exe");
                                    processes.Add(new()
                                    {
                                        MonoModule = mono,
                                        Id = p.Id,
                                        Name = p.ProcessName
                                    });
                                    l.Stop();
                                }
                                Native.CloseHandle(handle);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine("    ERROR SCANNING: " + p.ProcessName + " - " + e.Message);
                    }
                });
                Trace.WriteLine("FINISHED SCANNING PROCESSES...");
            });

            Processes = processes;
            if (processes.Count > 0)
            {
                Status = "Processes refreshed";
                SelectedProcess = processes[0];
            }
            else
            {
                Status = "No Mono process found";
                Trace.WriteLine("No Mono process found:");
            }
            IsRefreshing = false;
        }, () => !IsRefreshing);

        BrowseCommand = new(() =>
        {
            OpenFileDialog ofd = new()
            {
                Filter = "Dynamic Link Library|*.dll",
                Title = "Select assembly to inject",
            };
            if (ofd.ShowDialog().Value) AssemblyPath = ofd.FileName;
        });

        InjectCommand = new(() =>
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
            catch (Exception e)
            {
                Status = "Error: " + e.Message;
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
        }, () => SelectedProcess != null && File.Exists(AssemblyPath) && !string.IsNullOrEmpty(InjectClassName) && !string.IsNullOrEmpty(InjectMethodName) && !IsExecuting);

        EjectCommand = new(() => 
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
        }, () => SelectedAssembly != null && !string.IsNullOrEmpty(EjectClassName) && !string.IsNullOrEmpty(EjectMethodName) && !IsExecuting);

        CopyStatusCommand = new(() => Clipboard.SetText(Status));
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand InjectCommand { get; }
    public RelayCommand EjectCommand { get; }
    public RelayCommand CopyStatusCommand { get; }

    #region XML Props

    bool isRefreshing;
    public bool IsRefreshing
    {
        get => isRefreshing;
        set
        {
            Set(ref isRefreshing, value);
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    bool isExecuting;
    public bool IsExecuting
    {
        get => isExecuting;
        set
        {
            Set(ref isExecuting, value);
            InjectCommand.RaiseCanExecuteChanged();
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    List<MonoProcess> processes;
    public List<MonoProcess> Processes
    {
        get => processes;
        set => Set(ref processes, value);
    }

    MonoProcess selectedProcess;
    public MonoProcess SelectedProcess
    {
        get => selectedProcess;
        set
        {
            selectedProcess = value;
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string status;
    public string Status
    {
        get => status;
        set => Set(ref status, value);
    }

    bool avalert;
    public bool AVAlert
    {
        get => avalert;
        set => Set(ref avalert, value);
    }

    string avcolor;
    public string AVColor
    {
        get => avcolor;
        set => Set(ref avcolor, value);
    }

    string assemblyPath;
    public string AssemblyPath
    {
        get => assemblyPath;
        set
        {
            Set(ref assemblyPath, value);
            if (File.Exists(assemblyPath)) InjectNamespace = Path.GetFileNameWithoutExtension(assemblyPath);
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string injectNamespace;
    public string InjectNamespace
    {
        get => injectNamespace;
        set
        {
            Set(ref injectNamespace, value);
            EjectNamespace = value;
        }
    }

    string injectClassName;
    public string InjectClassName
    {
        get => injectClassName;
        set
        {
            Set(ref injectClassName, value);
            EjectClassName = value;
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    string injectMethodName;
    public string InjectMethodName
    {
        get => injectMethodName;
        set
        {
            Set(ref injectMethodName, value);
            if (injectMethodName == "Load") EjectMethodName = "Unload";
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    ObservableCollection<InjectedAssembly> injectedAssemblies = [];
    public ObservableCollection<InjectedAssembly> InjectedAssemblies
    {
        get => injectedAssemblies;
        set => Set(ref injectedAssemblies, value);
    }

    InjectedAssembly selectedAssembly;
    public InjectedAssembly SelectedAssembly
    {
        get => selectedAssembly;
        set
        {
            Set(ref selectedAssembly, value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string ejectNamespace;
    public string EjectNamespace
    {
        get => ejectNamespace;
        set => Set(ref ejectNamespace, value);
    }

    string ejectClassName;
    public string EjectClassName
    {
        get => ejectClassName;
        set
        {
            Set(ref ejectClassName, value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string ejectMethodName;
    public string EjectMethodName
    {
        get => ejectMethodName;
        set
        {
            Set(ref ejectMethodName, value);
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
            Trace.WriteLine("    Error Getting User Process: " + process.ProcessName + " - " + e.Message);
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