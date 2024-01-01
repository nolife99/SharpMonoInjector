using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpMonoInjector;

public sealed class Injector : IDisposable
{
    const string getRootDomain = "mono_get_root_domain",
        threadAttach = "mono_thread_attach",
        openDataImage = "mono_image_open_from_data",
        openImageAsm = "mono_assembly_load_from_full",
        asmImage = "mono_assembly_get_image",
        matchClass = "mono_class_from_name",
        matchMethod = "mono_class_get_method_from_name",
        rtInvoke = "mono_runtime_invoke",
        asmClose = "mono_assembly_close",
        strErr = "mono_image_strerror",
        getClass = "mono_object_get_class",
        getName = "mono_class_get_name";

    readonly Dictionary<string, nint> exports = new()
    {
        { getRootDomain, 0 },
        { threadAttach, 0 },
        { openDataImage, 0 },
        { openImageAsm, 0 },
        { asmImage, 0 },
        { matchClass, 0 },
        { matchMethod, 0 },
        { rtInvoke, 0 },
        { asmClose, 0 },
        { strErr, 0 },
        { getClass, 0 },
        { getName, 0 }
    };

    readonly ProcessMemory memory;
    readonly nint handle, mono;

    bool attach;
    nint root;

    public bool Is64Bit { get; private set; }

    public Injector(string processName)
    {
        if (processName.EndsWith(".exe")) processName = processName.Replace(".exe", "");
        using var process = Process.GetProcesses().AsParallel().FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) ?? 
            throw new InjectorException($"Couldn't find process with name '{processName}'");

        if ((handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, process.Id)) == 0)
            throw new InjectorException("Failed to open process", new Win32Exception(Marshal.GetLastWin32Error()));

        Is64Bit = ProcessUtils.Is64BitProcess(handle);
        if (!ProcessUtils.GetMonoModule(handle, out mono)) throw new InjectorException("Failed to find mono.dll in the target process");

        memory = new(handle);
    }
    public Injector(int processId)
    {
        using var process = Process.GetProcesses().AsParallel().FirstOrDefault(p => p.Id == processId) ?? 
            throw new InjectorException($"Couldn't find process with ID {processId}");

        if ((handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, process.Id)) == 0)
            throw new InjectorException("Failed to open process", new Win32Exception(Marshal.GetLastWin32Error()));

        Is64Bit = ProcessUtils.Is64BitProcess(handle);
        if (!ProcessUtils.GetMonoModule(handle, out mono)) throw new InjectorException("Failed to find mono.dll in the target process");

        memory = new(handle);
    }
    public Injector(nint processHandle, nint monoModule)
    {
        if ((handle = processHandle) == 0) throw new ArgumentNullException(nameof(processHandle), "Handle cannot be zero");
        if ((mono = monoModule) == 0) throw new ArgumentNullException(nameof(monoModule), "Handle cannot be zero");

        Is64Bit = ProcessUtils.Is64BitProcess(handle);
        memory = new(handle);
    }

    ~Injector() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        Native.CloseHandle(handle);
        if (disposing)
        {
            memory.Dispose();
            exports.Clear();
        }
    }

    void ObtainMonoExports()
    {
        ProcessUtils.GetExportedFunctions(handle, mono).AsParallel().ForAll(ef =>
        {
            if (exports.ContainsKey(ef.Name)) exports[ef.Name] = ef.Address;
        });
        exports.AsParallel().ForAll(kvp =>
        {
            if (kvp.Value == 0) throw new InjectorException($"Failed to obtain the addr of {kvp.Key}()");
        });
    }
    public nint Inject(ReadOnlySpan<byte> rawAssembly, string @namespace, string className, string methodName)
    {
        if (rawAssembly.Length == 0) throw new ArgumentException($"{nameof(rawAssembly)} cannot be empty", nameof(rawAssembly));
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(methodName);

        ObtainMonoExports();
        root = GetRootDomain();
        attach = true;

        var assembly = OpenAssemblyFromImage(OpenImageFromData(rawAssembly));
        RuntimeInvoke(GetMethodFromName(GetClassFromName(GetImageFromAssembly(assembly), @namespace, className), methodName));

        attach = false;
        return assembly;
    }
    public void Eject(nint assembly, string @namespace, string className, string methodName)
    {
        if (assembly == 0) throw new ArgumentException($"{nameof(assembly)} cannot be zero", nameof(assembly));
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(methodName);

        ObtainMonoExports();
        root = GetRootDomain();
        attach = true;

        RuntimeInvoke(GetMethodFromName(GetClassFromName(GetImageFromAssembly(assembly), @namespace, className), methodName));
        CloseAssembly(assembly);

        attach = false;
    }
    static void ThrowIfNull(nint ptr, string methodName)
    {
        if (ptr == 0) throw new InjectorException($"{methodName}() returned NULL");
    }

    nint GetRootDomain()
    {
        var rootDomain = Execute(exports[getRootDomain]);
        ThrowIfNull(rootDomain, getRootDomain);
        return rootDomain;
    }
    nint OpenImageFromData(ReadOnlySpan<byte> assembly)
    {
        var statusPtr = memory.Allocate(4);
        var rawImage = Execute(exports[openDataImage], memory.AllocateAndWrite(assembly), assembly.Length, 1, statusPtr);

        var status = memory.Read<int>(statusPtr);
        if (status != 0) throw new InjectorException($"{openDataImage}() failed: {memory.ReadString(Execute(exports[strErr], status), 256, Encoding.UTF8)}");
        return rawImage;
    }
    nint OpenAssemblyFromImage(nint image)
    {
        var statusPtr = memory.Allocate(4);
        var assembly = Execute(exports[openImageAsm], image, memory.AllocateAndWrite(new byte[1]), statusPtr, 0);

        var status = memory.Read<int>(statusPtr);
        if (status != 0) throw new InjectorException($"{openImageAsm}() failed: {memory.ReadString(Execute(exports[strErr], status), 256, Encoding.UTF8)}");
        return assembly;
    }
    nint GetImageFromAssembly(nint assembly)
    {
        var image = Execute(exports[asmImage], assembly);
        ThrowIfNull(image, asmImage);
        return image;
    }
    nint GetClassFromName(nint image, string @namespace, string className)
    {
        var @class = Execute(exports[matchClass], image, memory.AllocateAndWrite(@namespace), memory.AllocateAndWrite(className));
        ThrowIfNull(@class, matchClass);
        return @class;
    }
    nint GetMethodFromName(nint @class, string methodName)
    {
        var method = Execute(exports[matchMethod], @class, memory.AllocateAndWrite(methodName), 0);
        ThrowIfNull(method, matchMethod);
        return method;
    }
    string GetClassName(nint monoObject)
    {
        var @class = Execute(exports[getClass], monoObject);
        ThrowIfNull(@class, getClass);

        var className = Execute(exports[getName], @class);
        ThrowIfNull(className, getName);

        return memory.ReadString(className, 256, Encoding.UTF8);
    }

    string ReadMonoString(nint monoString)
        => memory.ReadString(monoString + (Is64Bit ? 0x14 : 0xC), memory.Read<int>(monoString + (Is64Bit ? 0x10 : 0x8)) * 2, Encoding.Unicode);

    void RuntimeInvoke(nint method)
    {
        var excPtr = Is64Bit ? memory.AllocateAndWrite(0L) : memory.AllocateAndWrite(0);
        Execute(exports[rtInvoke], method, 0, 0, excPtr);

        var exc = (nint)memory.Read<int>(excPtr);
        if (exc != 0)
        {
            var className = GetClassName(exc);
            var message = ReadMonoString(memory.Read<int>(exc + (Is64Bit ? 0x20 : 0x10)));
            throw new InjectorException($"The managed method threw an exception: ({className}) {message}");
        }
    }
    void CloseAssembly(nint assembly) => ThrowIfNull(Execute(exports[asmClose], assembly), asmClose);

    nint Execute(nint addr, params nint[] args)
    {
        var retValPtr = Is64Bit ? memory.AllocateAndWrite(0L) : memory.AllocateAndWrite(0);
        var thread = Native.CreateRemoteThread(handle, 0, 0, memory.AllocateAndWrite(Assemble(addr, retValPtr, args)), 0, 0, out _);
        if (thread == 0) throw new InjectorException("Failed to create a remote thread", new Win32Exception(Marshal.GetLastWin32Error()));

        if (Native.WaitForSingleObject(thread, -1) == 0xFFFFFFFF) throw new InjectorException("Failed to wait for a remote thread", new Win32Exception(Marshal.GetLastWin32Error()));
        var ret = Is64Bit ? (nint)memory.Read<long>(retValPtr) : memory.Read<int>(retValPtr);
        if (ret == 0x00000000C0000005) throw new InjectorException($"An access violation occurred while executing {exports.First(e => e.Value == addr).Key}()");

        return ret;
    }

    ReadOnlySpan<byte> Assemble(nint funcPtr, nint retValPtr, nint[] args)  => Is64Bit ? Assemble64(funcPtr, retValPtr, args) : Assemble86(funcPtr, retValPtr, args);
    ReadOnlySpan<byte> Assemble86(nint funcPtr, nint retValPtr, nint[] args)
    {
        Assembler asm = new();
        if (attach)
        {
            asm.Push(root);
            asm.MovEax(exports[threadAttach]);
            asm.CallEax();
            asm.AddEsp(4);
        }

        for (var i = args.Length - 1; i >= 0; --i) asm.Push(args[i]);
        asm.MovEax(funcPtr);
        asm.CallEax();
        asm.AddEsp((byte)(args.Length * 4));
        asm.MovEaxTo(retValPtr);
        asm.Return();

        return asm.AsSpan();
    }
    ReadOnlySpan<byte> Assemble64(nint funcPtr, nint retValPtr, nint[] args)
    {
        Assembler asm = new();

        asm.SubRsp(40);
        if (attach)
        {
            asm.MovRax(exports[threadAttach]);
            asm.MovRcx(root);
            asm.CallRax();
        }
        asm.MovRax(funcPtr);

        for (var i = 0; i < args.Length; ++i) switch (i)
        {
            case 0: asm.MovRcx(args[i]); break;
            case 1: asm.MovRdx(args[i]); break;
            case 2: asm.MovR8(args[i]); break;
            case 3: asm.MovR9(args[i]); break;
        }

        asm.CallRax();
        asm.AddRsp(40);
        asm.MovRaxTo(retValPtr);
        asm.Return();

        return asm.AsSpan();
    }
}