using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        if (AVAlert = ProcessUtils.AntivirusInstalled()) AVColor = "#FFA00668";
        else AVColor = "#FF21AC40";

        RefreshCommand = new(async () =>
        {
            IsRefreshing = true;
            Status = "Refreshing processes";

            Processes = [];
            Trace.WriteLine("[MainWindowViewModel] - Checking Processes for Mono");

            var cp = Environment.ProcessId;
            await Parallel.ForEachAsync(Process.GetProcesses(), (p, t) => 
            {
                using (p) try
                {
                    using ProcessHandle handle = new(p.Id, ProcessAccess.QueryInfo | ProcessAccess.ReadVM);
                    if (!handle.IsInvalid && !GetProcessUser(handle, p.ProcessName).IsEmpty && p.Id != cp)
                    {
                        Trace.WriteLine("\t" + p.ProcessName + ".exe");
                        if (ProcessUtils.GetMonoModule(handle, out var mono))
                        {
                            Trace.WriteLine("\t\tMono found in process: " + p.ProcessName + ".exe");
                            Processes = processes.Add(new()
                            {
                                MonoModule = mono,
                                Id = p.Id,
                                Name = p.ProcessName
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine("\tERROR SCANNING: " + p.ProcessName + " - " + e.Message);
                }
                return new();
            });
            IsRefreshing = false;

            if (processes.Count > 0)
            {
                Status = "Processes refreshed";
                SelectedProcess = processes[0];
            }
            else
            {
                Status = "No Mono process found";
                Trace.WriteLine("No Mono process found");
            }
        }, () => !IsRefreshing);

        BrowseCommand = new(() =>
        {
            OpenFileDialog dialog = new()
            {
                Filter = ".NET Assemblies|*.dll",
                Title = "Select assembly to inject"
            };
            if (dialog.ShowDialog().Value) AssemblyPath = dialog.FileName;
        });

        InjectCommand = new(async () =>
        {
            using ProcessHandle handle = new(SelectedProcess.Id);
            if (handle.IsInvalid)
            {
                Status = "Failed to open process";
                return;
            }

            var filename = Path.GetFileName(assemblyPath);
            byte[] file;

            try
            {
                Status = "Loading " + filename;
                file = await File.ReadAllBytesAsync(assemblyPath);
            }
            catch (IOException e)
            {
                Status = $"Failed to load {assemblyPath} {e.Message}";
                return;
            }

            IsExecuting = true;
            Status = "Injecting " + filename;

            using (Injector injector = new(handle, SelectedProcess.MonoModule))
            {
                try
                {
                    var asm = await Task.Run(() => injector.Inject(file, InjectNamespace, InjectClassName, InjectMethodName));
                    InjectedAssemblies = injectedAssemblies.Add(new()
                    {
                        ProcessId = SelectedProcess.Id,
                        Address = asm,
                        Name = filename,
                        Is64Bit = injector.Is64Bit
                    });
                    Status = "Injected " + filename;
                }
                catch (InjectorException e)
                {
                    Status = "Injection failed: " + e.Message;
                }
                catch (Exception e)
                {
                    Status = "Injection failed (unknown error): " + e.Message;
                }
            }
            IsExecuting = false;
        }, () => SelectedProcess.Id != 0 && File.Exists(assemblyPath) && !string.IsNullOrEmpty(InjectClassName) && !string.IsNullOrEmpty(InjectMethodName) && !IsExecuting);

        EjectCommand = new(() =>
        {
            using ProcessHandle handle = new(SelectedAssembly.ProcessId);
            if (handle.IsInvalid)
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
                    InjectedAssemblies = injectedAssemblies.Remove(SelectedAssembly);
                    Status = "Ejected " + SelectedAssembly.Name;
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
        }, () => SelectedAssembly.ProcessId != 0 && !string.IsNullOrEmpty(EjectClassName) && !string.IsNullOrEmpty(EjectMethodName) && !IsExecuting);

        CopyStatusCommand = new(() => Clipboard.SetText(Status));
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand InjectCommand { get; }
    public RelayCommand EjectCommand { get; }
    public RelayCommand CopyStatusCommand { get; }

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

    ImmutableList<MonoProcess> processes = [];
    public ImmutableList<MonoProcess> Processes
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

    ImmutableList<InjectedAssembly> injectedAssemblies = [];
    public ImmutableList<InjectedAssembly> InjectedAssemblies
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

    static ReadOnlySpan<char> GetProcessUser(ProcessHandle process, ReadOnlySpan<char> procName)
    {
        nint token = 0;
        try
        {
            Native.OpenProcessToken(process.DangerousGetHandle(), 8, out token);
            using WindowsIdentity id = new(token);

            var user = id.Name.AsSpan();
            return user.Contains('\\') ? user[(user.IndexOf('\\') + 1)..] : user;
        }
        catch (Exception e)
        {
            Trace.WriteLine(string.Concat("\tError Getting User Process: ", procName, " - ", e.Message));
            return ReadOnlySpan<char>.Empty;
        }
        finally
        {
            if (token != 0) Native.CloseHandle(token);
        }
    }
}