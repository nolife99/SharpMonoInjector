using System;
using System.IO;
using System.Security.Principal;

namespace SharpMonoInjector.Cli;

static class Program
{
    static void Main(string[] args)
    {
        Console.Clear();

        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.WriteLine("\r\nSharpMonoInjector 2.4 wh0am1 Mod\r\n\r\nWARNING: You are running this in an unpriveleged process, try from an Elevated Command Prompt.\r\n");
            Console.WriteLine("\t As an alternative, right-click Game .exe and uncheck the Compatibility\r\n\t setting 'Run this program as Administrator'.\r\n\r\n");
        }
        if (ProcessUtils.AntivirusInstalled()) Console.WriteLine("!!! WARNING ANTIVIRUS DETECTED !!! CHECK DEBUG.LOG FOR RUNNING PROCESS.\r\n\r\n");

        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        CommandLineArguments cla = new(args);
        var inject = cla.IsSwitchPresent("inject");
        var eject = cla.IsSwitchPresent("eject");

        if (!inject && !eject)
        {
            Console.WriteLine("No operation (inject/eject) specified");
            return;
        }
        Injector injector;

        if (cla.GetIntArg("-p", out int pid)) injector = new(in pid);
        else if (cla.GetStringArg("-p", out var pname)) injector = new(pname);
        else
        {
            Console.WriteLine("No process id/name specified");
            return;
        }

        if (inject) Inject(ref injector, cla);
        else Eject(ref injector, cla);
    }

    static void PrintHelp()
    {
        const string help =
            "SharpMonoInjector 2.4 wh0am1 Mod\r\n\r\n" +
            "Usage:\r\n" +
            "smi.exe <inject/eject> <options>\r\n\r\n" +
            "Options:\r\n" +
            "-p - The id or name of the target process\r\n" +
            "-a - When injecting, the path of the assembly to inject. When ejecting, the address of the assembly to eject\r\n" +
            "-n - The namespace in which the loader class resides\r\n" +
            "-c - The name of the loader class\r\n" +
            "-m - The name of the method to invoke in the loader class\r\n\r\n" +
            "Examples:\r\n" +
            "smi.exe inject -p testgame -a ExampleAssembly.dll -n ExampleAssembly -c Loader -m Load\r\n" +
            "smi.exe eject -p testgame -a 0x13D23A98 -n ExampleAssembly -c Loader -m Unload\r\n";

        Console.WriteLine(help);
    }

    static void Inject(ref Injector injector, CommandLineArguments args)
    {
        byte[] assembly;
        if (args.GetStringArg("-a", out var assemblyPath)) try
        {
            assembly = File.ReadAllBytes(assemblyPath.ToString());
        }
        catch
        {
                Console.WriteLine(string.Concat("Could not read the file ", assemblyPath));
            return;
        }
        else
        {
            Console.WriteLine("No assembly specified");
            return;
        }

        args.GetStringArg("-n", out var @namespace);
        if (!args.GetStringArg("-c", out var className))
        {
            Console.WriteLine("No class name specified");
            return;
        }
        if (!args.GetStringArg("-m", out var methodName))
        {
            Console.WriteLine("No method name specified");
            return;
        }

        using (injector)
        {
            nint remoteAssembly = 0;
            try
            {
                remoteAssembly = injector.Inject(assembly, @namespace, className, methodName);
            }
            catch (InjectorException ie)
            {
                Console.WriteLine("Failed to inject assembly: " + ie);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Failed to inject assembly (unknown error): " + exc);
            }

            if (remoteAssembly == 0) return;
            Console.WriteLine($"{Path.GetFileName(assemblyPath)}: " + (injector.Is64Bit ? $"0x{remoteAssembly.ToInt64():X16}" : $"0x{remoteAssembly.ToInt32():X8}"));
        }
    }

    static void Eject(ref Injector injector, CommandLineArguments args)
    {
        nint assembly;

        if (args.GetIntArg("-a", out var nint)) assembly = nint;
        else if (args.GetLongArg("-a", out var longPtr)) assembly = (nint)longPtr;
        else
        {
            Console.WriteLine("No assembly pointer specified");
            return;
        }

        args.GetStringArg("-n", out var @namespace);
        if (!args.GetStringArg("-c", out var className))
        {
            Console.WriteLine("No class name specified");
            return;
        }
        if (!args.GetStringArg("-m", out var methodName))
        {
            Console.WriteLine("No method name specified");
            return;
        }

        using (injector) try
        {
            injector.Eject(in assembly, @namespace, className, methodName);
            Console.WriteLine("Ejection successful");
        }
        catch (InjectorException ie)
        {
            Console.WriteLine("Ejection failed: " + ie);
        }
        catch (Exception exc)
        {
            Console.WriteLine("Ejection failed (unknown error): " + exc);
        }
    }
}