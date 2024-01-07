using System;
using System.Collections.Immutable;
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

            await Parallel.ForEachAsync(Process.GetProcesses(), (p, t) => 
            {
                using (p) try
                {
                    if (!GetProcessUser(p).IsEmpty && p.Id != Environment.ProcessId)
                    {
                        Trace.WriteLine("\t" + p.ProcessName + ".exe");
                        if (ProcessUtils.GetMonoModule(p, out var mono))
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
                Title = "Pick assembly to inject"
            };
            if (dialog.ShowDialog().Value) AssemblyPath = dialog.FileName;
        });

        InjectCommand = new(async () =>
        {
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

            try
            {
                using Injector injector = new(Process.GetProcessById(selectedProcess.Id), selectedProcess.MonoModule);
                await Task.Run(() =>
                {
                    var asm = injector.Inject(file, injectNamespace, injectClassName, injectMethodName);
                    InjectedAssemblies = injectedAssemblies.Add(new()
                    {
                        ProcessId = selectedProcess.Id,
                        Address = asm,
                        Name = filename,
                        Is64Bit = injector.Is64Bit
                    });
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
            IsExecuting = false;
        }, () => selectedProcess.Id != 0 && File.Exists(assemblyPath) && !string.IsNullOrWhiteSpace(injectClassName) && !string.IsNullOrWhiteSpace(injectMethodName) && !IsExecuting);

        EjectCommand = new(async () =>
        {
            IsExecuting = true;
            Status = "Ejecting " + selectedAssembly.Name;

            try
            {
                using Injector injector = new(selectedAssembly.ProcessId);
                await Task.Run(() =>
                {
                    injector.Eject(selectedAssembly.Address, ejectNamespace, ejectClassName, ejectMethodName);
                    InjectedAssemblies = injectedAssemblies.Remove(selectedAssembly);
                });
                Status = "Ejected " + selectedAssembly.Name;
            }
            catch (InjectorException ie)
            {
                Status = "Ejection failed: " + ie.Message;
            }
            catch (Exception e)
            {
                Status = "Ejection failed (unknown error): " + e.Message;
            }
            IsExecuting = false;
        }, () => selectedAssembly.ProcessId != 0 && !string.IsNullOrWhiteSpace(ejectClassName) && !string.IsNullOrWhiteSpace(ejectMethodName) && !IsExecuting);

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

    static ReadOnlySpan<char> GetProcessUser(Process process)
    {
        try
        {
            Native.OpenProcessToken(process.SafeHandle, 8, out var token);
            using (token)
            {
                using WindowsIdentity id = new(token.DangerousGetHandle());
                var user = id.Name.AsSpan();
                return user.Contains('\\') ? user[(user.IndexOf('\\') + 1)..] : user;
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(string.Concat("\tError getting user process: ", process.ProcessName, " - ", e.Message));
            return [];
        }
    }
}