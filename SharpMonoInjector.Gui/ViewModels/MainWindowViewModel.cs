using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SharpMonoInjector.Gui.Models;

namespace SharpMonoInjector.Gui.ViewModels;

internal sealed class MainWindowViewModel : ViewModel
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
            Trace.WriteLine("[MainWindowViewModel] - Checking processes for Mono");

            await Parallel.ForEachAsync(Process.GetProcesses(), (p, t) => 
            {
                using (p) try
                {
                    if (GetProcessUser(p) && p.Id != Environment.ProcessId)
                    {
                        Trace.WriteLine($"\t{p.ProcessName}.exe");
                        if (ProcessUtils.GetMonoModule(p, out var mono))
                        {
                            Trace.WriteLine($"\t\tMono found in process: {p.ProcessName}.exe");
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
                    Trace.WriteLine($"\tERROR SCANNING: {p.ProcessName} - {e}");
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
        }, () => !isRefreshing);

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
                Status = $"Failed to load {filename}: {e.Message}";
                Trace.WriteLine($"Failed to load {filename}: {e}");
                return;
            }

            IsExecuting = true;
            Status = "Injecting " + filename;

            try
            {
                await Task.Run(() =>
                {
                    using Injector injector = new(Process.GetProcessById(selectedProcess.Id), selectedProcess.MonoModule);
                    InjectedAssemblies = assemblies.Add(new()
                    {
                        ProcessId = selectedProcess.Id,
                        Address = injector.Inject(file, injectNamespace, injectClassName, injectMethodName),
                        Name = filename,
                        Is64Bit = injector.Is64Bit
                    });
                });
                Status = "Injected " + filename;
            }
            catch (ArgumentException)
            {
                Status = "Injection failed: The selected process isn't running";
                InjectedAssemblies = assemblies.RemoveAll(s => selectedProcess.Id == s.ProcessId);
                Processes = processes.RemoveAll(p => selectedProcess.Id == p.Id);

                SelectedAssembly = assemblies.FirstOrDefault();
                SelectedProcess = processes.FirstOrDefault();
            }
            catch (InjectorException e)
            {
                Status = "Injection failed: " + e.Message;
                Trace.WriteLine($"Injection failed: {e}");
            }
            catch (Exception e)
            {
                Status = "Injection failed (unknown error): " + e.Message;
                Trace.WriteLine($"Injection failed: {e}");
            }
            IsExecuting = false;
        }, () => selectedProcess.MonoModule != 0 && File.Exists(assemblyPath) && !string.IsNullOrWhiteSpace(injectClassName) && !string.IsNullOrWhiteSpace(injectMethodName) && !isExecuting);

        EjectCommand = new(async () =>
        {
            IsExecuting = true;
            Status = "Ejecting " + selectedAssembly.Name;

            try
            {
                await Task.Run(() =>
                {
                    using (Injector injector = new(selectedAssembly.ProcessId)) injector.Eject(selectedAssembly.Address, ejectNamespace, ejectClassName, ejectMethodName);
                    InjectedAssemblies = assemblies.Remove(selectedAssembly);
                });
                Status = "Ejected " + selectedAssembly.Name;
            }
            catch (ArgumentException)
            {
                Status = "Ejection failed: The selected assembly's process isn't running";
                InjectedAssemblies = assemblies.RemoveAll(s => selectedAssembly.ProcessId == s.ProcessId);
                Processes = processes.RemoveAll(p => selectedAssembly.ProcessId == p.Id);

                SelectedAssembly = assemblies.FirstOrDefault();
                SelectedProcess = processes.FirstOrDefault();
            }
            catch (InjectorException e)
            {
                Status = "Ejection failed: " + e.Message;
                Trace.WriteLine($"Ejection failed: {e}");
            }
            catch (Exception e)
            {
                Status = "Ejection failed (unknown error): " + e.Message;
                Trace.WriteLine($"Ejection failed: {e}");
            }
            IsExecuting = false;
        }, () => selectedAssembly.Address != 0 && !string.IsNullOrWhiteSpace(ejectClassName) && !string.IsNullOrWhiteSpace(ejectMethodName) && !isExecuting);

        CopyStatusCommand = new(() => Clipboard.SetText(status));
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
            Set(ref isRefreshing, in value);
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    bool isExecuting;
    public bool IsExecuting
    {
        get => isExecuting;
        set
        {
            Set(ref isExecuting, in value);
            InjectCommand.RaiseCanExecuteChanged();
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    ImmutableList<MonoProcess> processes = [];
    public ImmutableList<MonoProcess> Processes
    {
        get => processes;
        set => Set(ref processes, in value);
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
        set => Set(ref status, in value);
    }

    bool avalert;
    public bool AVAlert
    {
        get => avalert;
        set => Set(ref avalert, in value);
    }

    string avcolor;
    public string AVColor
    {
        get => avcolor;
        set => Set(ref avcolor, in value);
    }

    string assemblyPath;
    public string AssemblyPath
    {
        get => assemblyPath;
        set
        {
            Set(ref assemblyPath, in value);
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
            Set(ref injectNamespace, in value);
            EjectNamespace = value;
        }
    }

    string injectClassName;
    public string InjectClassName
    {
        get => injectClassName;
        set
        {
            Set(ref injectClassName, in value);
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
            Set(ref injectMethodName, in value);
            if (injectMethodName == "Load") EjectMethodName = "Unload";
            InjectCommand.RaiseCanExecuteChanged();
        }
    }

    ImmutableList<InjectedAssembly> assemblies = [];
    public ImmutableList<InjectedAssembly> InjectedAssemblies
    {
        get => assemblies;
        set => Set(ref assemblies, in value);
    }

    InjectedAssembly selectedAssembly;
    public InjectedAssembly SelectedAssembly
    {
        get => selectedAssembly;
        set
        {
            Set(ref selectedAssembly, in value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string ejectNamespace;
    public string EjectNamespace
    {
        get => ejectNamespace;
        set => Set(ref ejectNamespace, in value);
    }

    string ejectClassName;
    public string EjectClassName
    {
        get => ejectClassName;
        set
        {
            Set(ref ejectClassName, in value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    string ejectMethodName;
    public string EjectMethodName
    {
        get => ejectMethodName;
        set
        {
            Set(ref ejectMethodName, in value);
            EjectCommand.RaiseCanExecuteChanged();
        }
    }

    static bool GetProcessUser(Process process)
    {
        try
        {
            Native.OpenProcessToken(process.SafeHandle, 8, out var token);
            using (token)
            {
                using WindowsIdentity id = new(token.DangerousGetHandle());
                return !string.IsNullOrWhiteSpace(id.Name);
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"\tError getting user process: {process.ProcessName} - {e.Message}");
            return false;
        }
    }
}