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
    readonly ProcessHandle handle;
    readonly nint mono;

    bool attach;
    nint root;

    public bool Is64Bit { get; private set; }

    public Injector(ReadOnlySpan<char> processName)
    {
        processName = processName[..processName.IndexOf(".exe")];
        using var process = Process.GetProcessesByName(processName.ToString()).FirstOrDefault() ?? throw new InjectorException($"Couldn't find process with name '{processName}'");

        if ((handle = new(process.Id)).IsInvalid) throw new InjectorException($"Failed to open process with ID {process.Id}", new Win32Exception(Marshal.GetLastWin32Error()));
        Is64Bit = ProcessUtils.Is64BitProcess(handle);
        if (!ProcessUtils.GetMonoModule(handle, out mono)) throw new InjectorException("Failed to find mono.dll in target process");

        memory = new(handle);
    }
    public Injector(int processId)
    {
        if ((handle = new(processId)).IsInvalid) throw new InjectorException($"Failed to open process with ID {processId}", new Win32Exception(Marshal.GetLastWin32Error()));
        Is64Bit = ProcessUtils.Is64BitProcess(handle);
        if (!ProcessUtils.GetMonoModule(handle, out mono)) throw new InjectorException("Failed to find mono.dll in target process");

        memory = new(handle);
    }
    public Injector(ProcessHandle processHandle, nint monoModule)
    {
        mono = monoModule;
        Is64Bit = ProcessUtils.Is64BitProcess(handle = processHandle);
        memory = new(handle);
    }

    public void Dispose()
    {
        memory.Dispose();
        handle.Dispose();
        exports.Clear();

        GC.SuppressFinalize(this);
    }

    void ObtainMonoExports()
    {
        ProcessUtils.GetExportedFunctions(handle, mono).AsParallel().ForAll(ef =>
        {
            if (exports.ContainsKey(ef.Name)) exports[ef.Name] = ef.Address;
        });
        exports.AsParallel().ForAll(kvp =>
        {
            if (kvp.Value == 0) throw new InjectorException($"Failed to get address of {kvp.Key}()");
        });
    }
    public nint Inject(ReadOnlySpan<byte> rawAssembly, ReadOnlySpan<char> @namespace, ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
    {
        if (rawAssembly.IsEmpty) throw new ArgumentException($"{nameof(rawAssembly)} can't be empty", nameof(rawAssembly));
        if (className.IsEmpty) throw new ArgumentException($"{nameof(className)} can't be empty", nameof(className));
        if (methodName.IsEmpty) throw new ArgumentException($"{nameof(methodName)} can't be empty", nameof(methodName));

        ObtainMonoExports();
        root = GetRootDomain();
        attach = true;

        var assembly = OpenAssemblyFromImage(OpenImageFromData(rawAssembly));
        RuntimeInvoke(GetMethodFromName(GetClassFromName(GetImageFromAssembly(assembly), @namespace, className), methodName));

        attach = false;
        return assembly;
    }
    public void Eject(nint assembly, ReadOnlySpan<char> @namespace, ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
    {
        if (assembly == 0) throw new ArgumentException($"{nameof(assembly)} can't be zero", nameof(assembly));
        if (className.IsEmpty) throw new ArgumentException($"{nameof(className)} can't be empty", nameof(className));
        if (methodName.IsEmpty) throw new ArgumentException($"{nameof(methodName)} can't be empty", nameof(methodName));

        ObtainMonoExports();
        root = GetRootDomain();
        attach = true;

        RuntimeInvoke(GetMethodFromName(GetClassFromName(GetImageFromAssembly(assembly), @namespace, className), methodName));
        CloseAssembly(assembly);

        attach = false;
    }
    static void ThrowIfNull(nint ptr, ReadOnlySpan<char> methodName)
    {
        if (ptr == 0) throw new InjectorException(string.Concat(methodName, "() returned null"));
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
        if (status != 0) throw new InjectorException($"{openDataImage}() failed: {memory.ReadString(Execute(exports[strErr], status), 256, Encoding.ASCII)}");
        return rawImage;
    }
    nint OpenAssemblyFromImage(nint image)
    {
        var statusPtr = memory.Allocate(4);
        var assembly = Execute(exports[openImageAsm], image, memory.Allocate(1), statusPtr, 0);

        var status = memory.Read<int>(statusPtr);
        if (status != 0) throw new InjectorException($"{openImageAsm}() failed: {memory.ReadString(Execute(exports[strErr], status), 256, Encoding.ASCII)}");
        return assembly;
    }
    nint GetImageFromAssembly(nint assembly)
    {
        var image = Execute(exports[asmImage], assembly);
        ThrowIfNull(image, asmImage);
        return image;
    }
    nint GetClassFromName(nint image, ReadOnlySpan<char> @namespace, ReadOnlySpan<char> className)
    {
        var @class = Execute(exports[matchClass], image, memory.AllocateAndWrite(@namespace), memory.AllocateAndWrite(className));
        ThrowIfNull(@class, matchClass);
        return @class;
    }
    nint GetMethodFromName(nint @class, ReadOnlySpan<char> methodName)
    {
        var method = Execute(exports[matchMethod], @class, memory.AllocateAndWrite(methodName), 0);
        ThrowIfNull(method, matchMethod);
        return method;
    }
    ReadOnlySpan<char> GetClassName(nint monoObject)
    {
        var @class = Execute(exports[getClass], monoObject);
        ThrowIfNull(@class, getClass);

        var className = Execute(exports[getName], @class);
        ThrowIfNull(className, getName);

        return memory.ReadString(className, 256, Encoding.ASCII);
    }
    ReadOnlySpan<char> ReadMonoString(nint monoString)
        => memory.ReadString(monoString + (Is64Bit ? 0x14 : 0xC), memory.Read<int>(monoString + (Is64Bit ? 0x10 : 0x8)) * 2, Encoding.Unicode);

    void RuntimeInvoke(nint method)
    {
        var excPtr = Is64Bit ? memory.Allocate(8) : memory.Allocate(4);
        Execute(exports[rtInvoke], method, 0, 0, excPtr);

        var exc = (nint)memory.Read<int>(excPtr);
        if (exc != 0) throw new InjectorException($"Managed method threw exception: ({GetClassName(exc)}) {ReadMonoString(memory.Read<int>(exc + (Is64Bit ? 0x20 : 0x10)))}");
    }
    void CloseAssembly(nint assembly) => ThrowIfNull(Execute(exports[asmClose], assembly), asmClose);

    nint Execute(nint addr, params nint[] args)
    {
        var retValPtr = Is64Bit ? memory.Allocate(8) : memory.Allocate(4);

        var thread = Native.CreateRemoteThread(handle.DangerousGetHandle(), 0, 0, memory.AllocateAndWrite(Assemble(addr, retValPtr, args)), 0, 0, out _);
        if (thread == 0) throw new InjectorException("Failed to create remote thread", new Win32Exception(Marshal.GetLastWin32Error()));

        if (Native.WaitForSingleObject(thread, -1) == 0xFFFFFFFF) throw new InjectorException("Failed to wait for remote thread", new Win32Exception(Marshal.GetLastWin32Error()));
        var ret = Is64Bit ? (nint)memory.Read<long>(retValPtr) : memory.Read<int>(retValPtr);
        if (ret == 0x00000000C0000005) throw new InjectorException($"Access violation while executing {exports.First(e => e.Value == addr).Key}()");

        return ret;
    }

    ReadOnlySpan<byte> Assemble(nint funcPtr, nint retValPtr, Span<nint> args) => Is64Bit ? Assemble64(funcPtr, retValPtr, args) : Assemble86(funcPtr, retValPtr, args);
    ReadOnlySpan<byte> Assemble86(nint funcPtr, nint retValPtr, Span<nint> args)
    {
        Assembler asm = new();
        if (attach)
        {
            asm.Push(root);
            asm.MovEax(exports[threadAttach]);
            asm.CallEax();
            asm.AddEsp(4);
        }

        var size = args.Length;
        for (var i = size - 1; i >= 0; --i) asm.Push(args[i]);

        asm.MovEax(funcPtr);
        asm.CallEax();
        asm.AddEsp((byte)(size << 2));
        asm.MovEaxTo(retValPtr);
        asm.Return();

        return asm.Compile();
    }
    ReadOnlySpan<byte> Assemble64(nint funcPtr, nint retValPtr, Span<nint> args)
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

        return asm.Compile();
    }
}