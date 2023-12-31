using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpMonoInjector;

public class Injector : IDisposable
{
    const string mono_get_root_domain = "mono_get_root_domain",
        mono_thread_attach = "mono_thread_attach",
        mono_image_open_from_data = "mono_image_open_from_data",
        mono_assembly_load_from_full = "mono_assembly_load_from_full",
        mono_assembly_get_image = "mono_assembly_get_image",
        mono_class_from_name = "mono_class_from_name",
        mono_class_get_method_from_name = "mono_class_get_method_from_name",
        mono_runtime_invoke = "mono_runtime_invoke",
        mono_assembly_close = "mono_assembly_close",
        mono_image_strerror = "mono_image_strerror",
        mono_object_get_class = "mono_object_get_class",
        mono_class_get_name = "mono_class_get_name";

    readonly Dictionary<string, nint> Exports = new()
    {
        { mono_get_root_domain, 0 },
        { mono_thread_attach, 0 },
        { mono_image_open_from_data, 0 },
        { mono_assembly_load_from_full, 0 },
        { mono_assembly_get_image, 0 },
        { mono_class_from_name, 0 },
        { mono_class_get_method_from_name, 0 },
        { mono_runtime_invoke, 0 },
        { mono_assembly_close, 0 },
        { mono_image_strerror, 0 },
        { mono_object_get_class, 0 },
        { mono_class_get_name, 0 }
    };

    readonly Memory _memory;
    bool _attach;

    readonly nint _handle, _mono;
    nint _rootDomain;

    public bool Is64Bit { get; private set; }

    public Injector(string processName)
    {
        if (processName.EndsWith(".exe")) processName = processName.Replace(".exe", "");
        var process = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) ?? 
            throw new InjectorException($"Could not find a process with the name {processName}");

        if ((_handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, process.Id)) == 0)
            throw new InjectorException("Failed to open process", new Win32Exception(Marshal.GetLastWin32Error()));

        Is64Bit = ProcessUtils.Is64BitProcess(_handle);
        if (!ProcessUtils.GetMonoModule(_handle, out _mono)) throw new InjectorException("Failed to find mono.dll in the target process");

        _memory = new(_handle);
    }
    public Injector(int processId)
    {
        var process = Process.GetProcesses().FirstOrDefault(p => p.Id == processId) ?? 
            throw new InjectorException($"Could not find a process with the id {processId}");

        if ((_handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, process.Id)) == 0)
            throw new InjectorException("Failed to open process", new Win32Exception(Marshal.GetLastWin32Error()));

        Is64Bit = ProcessUtils.Is64BitProcess(_handle);
        if (!ProcessUtils.GetMonoModule(_handle, out _mono)) throw new InjectorException("Failed to find mono.dll in the target process");

        _memory = new(_handle);
    }
    public Injector(nint processHandle, nint monoModule)
    {
        if ((_handle = processHandle) == 0) throw new ArgumentNullException(nameof(processHandle), "Handle cannot be zero");
        if ((_mono = monoModule) == 0) throw new ArgumentNullException(nameof(monoModule), "Handle cannot be zero");

        Is64Bit = ProcessUtils.Is64BitProcess(_handle);
        _memory = new(_handle);
    }

    public void Dispose()
    {
        _memory.Dispose();
        Native.CloseHandle(_handle);
    }

    void ObtainMonoExports()
    {
        foreach (var ef in ProcessUtils.GetExportedFunctions(_handle, _mono)) if (Exports.ContainsKey(ef.Name)) Exports[ef.Name] = ef.Address;
        foreach (var kvp in Exports) if (kvp.Value == 0) throw new InjectorException($"Failed to obtain the address of {kvp.Key}()");
    }
    public nint Inject(ReadOnlySpan<byte> rawAssembly, string @namespace, string className, string methodName)
    {
        if (rawAssembly.Length == 0) throw new ArgumentException($"{nameof(rawAssembly)} cannot be empty", nameof(rawAssembly));
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(methodName);

        nint rawImage, assembly, image, @class, method;
        ObtainMonoExports();

        _rootDomain = GetRootDomain();
        rawImage = OpenImageFromData(rawAssembly);
        _attach = true;

        assembly = OpenAssemblyFromImage(rawImage);
        image = GetImageFromAssembly(assembly);

        @class = GetClassFromName(image, @namespace, className);
        method = GetMethodFromName(@class, methodName);
        RuntimeInvoke(method);

        _attach = false;
        return assembly;
    }
    public void Eject(nint assembly, string @namespace, string className, string methodName)
    {
        if (assembly == 0) throw new ArgumentException($"{nameof(assembly)} cannot be zero", nameof(assembly));
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(methodName);

        nint image, @class, method;
        ObtainMonoExports();

        _rootDomain = GetRootDomain();
        _attach = true;
        image = GetImageFromAssembly(assembly);

        @class = GetClassFromName(image, @namespace, className);
        method = GetMethodFromName(@class, methodName);
        RuntimeInvoke(method);

        CloseAssembly(assembly);
        _attach = false;
    }

    static void ThrowIfNull(nint ptr, string methodName)
    {
        if (ptr == 0) throw new InjectorException($"{methodName}() returned NULL");
    }

    nint GetRootDomain()
    {
        var rootDomain = Execute(Exports[mono_get_root_domain]);
        ThrowIfNull(rootDomain, mono_get_root_domain);
        return rootDomain;
    }
    nint OpenImageFromData(ReadOnlySpan<byte> assembly)
    {
        var statusPtr = _memory.Allocate(4);
        var rawImage = Execute(Exports[mono_image_open_from_data], _memory.AllocateAndWrite(assembly), assembly.Length, 1, statusPtr);

        var status = (MonoImageOpenStatus)_memory.Read<int>(statusPtr);
        if (status != MonoImageOpenStatus.MONO_IMAGE_OK) 
        {
            var messagePtr = Execute(Exports[mono_image_strerror], (nint)status);
            var message = _memory.ReadString(messagePtr, 256, Encoding.UTF8);
            throw new InjectorException($"{mono_image_open_from_data}() failed: {message}");
        }
        return rawImage;
    }
    nint OpenAssemblyFromImage(nint image)
    {
        var statusPtr = _memory.Allocate(4);
        var assembly = Execute(Exports[mono_assembly_load_from_full], image, _memory.AllocateAndWrite(new byte[1]), statusPtr, 0);

        var status = (MonoImageOpenStatus)_memory.Read<int>(statusPtr);
        if (status != MonoImageOpenStatus.MONO_IMAGE_OK) 
        {
            var messagePtr = Execute(Exports[mono_image_strerror], (nint)status);
            var message = _memory.ReadString(messagePtr, 256, Encoding.UTF8);
            throw new InjectorException($"{mono_assembly_load_from_full}() failed: {message}");
        }
        return assembly;
    }
    nint GetImageFromAssembly(nint assembly)
    {
        var image = Execute(Exports[mono_assembly_get_image], assembly);
        ThrowIfNull(image, mono_assembly_get_image);
        return image;
    }
    nint GetClassFromName(nint image, string @namespace, string className)
    {
        var @class = Execute(Exports[mono_class_from_name], image, _memory.AllocateAndWrite(@namespace), _memory.AllocateAndWrite(className));
        ThrowIfNull(@class, mono_class_from_name);
        return @class;
    }
    nint GetMethodFromName(nint @class, string methodName)
    {
        var method = Execute(Exports[mono_class_get_method_from_name], @class, _memory.AllocateAndWrite(methodName), 0);
        ThrowIfNull(method, mono_class_get_method_from_name);
        return method;
    }
    string GetClassName(nint monoObject)
    {
        var @class = Execute(Exports[mono_object_get_class], monoObject);
        ThrowIfNull(@class, mono_object_get_class);

        var className = Execute(Exports[mono_class_get_name], @class);
        ThrowIfNull(className, mono_class_get_name);

        return _memory.ReadString(className, 256, Encoding.UTF8);
    }

    string ReadMonoString(nint monoString)
        => _memory.ReadString(monoString + (Is64Bit ? 0x14 : 0xC), _memory.Read<int>(monoString + (Is64Bit ? 0x10 : 0x8)) * 2, Encoding.Unicode);

    void RuntimeInvoke(nint method)
    {
        var excPtr = Is64Bit ? _memory.AllocateAndWrite((long)0) : _memory.AllocateAndWrite(0);
        Execute(Exports[mono_runtime_invoke], method, 0, 0, excPtr);

        var exc = (nint)_memory.Read<int>(excPtr);
        if (exc != 0)
        {
            var className = GetClassName(exc);
            var message = ReadMonoString(_memory.Read<int>(exc + (Is64Bit ? 0x20 : 0x10)));
            throw new InjectorException($"The managed method threw an exception: ({className}) {message}");
        }
    }
    void CloseAssembly(nint assembly)
    {
        var result = Execute(Exports[mono_assembly_close], assembly);
        ThrowIfNull(result, mono_assembly_close);
    }
    nint Execute(nint address, params nint[] args)
    {
        var retValPtr = Is64Bit ? _memory.AllocateAndWrite(0L) : _memory.AllocateAndWrite(0);

        var code = Assemble(address, retValPtr, args);
        var alloc = _memory.AllocateAndWrite(code);

        var thread = Native.CreateRemoteThread(_handle, 0, 0, alloc, 0, 0, out _);
        if (thread == 0) throw new InjectorException("Failed to create a remote thread", new Win32Exception(Marshal.GetLastWin32Error()));

        var result = Native.WaitForSingleObject(thread, -1);
        if (result is WaitResult.WAIT_FAILED) throw new InjectorException("Failed to wait for a remote thread", new Win32Exception(Marshal.GetLastWin32Error()));

        var ret = Is64Bit ? (nint)_memory.Read<long>(retValPtr) : _memory.Read<int>(retValPtr);
        if (ret == 0x00000000C0000005) throw new InjectorException($"An access violation occurred while executing {Exports.First(e => e.Value == address).Key}()");

        return ret;
    }

    ReadOnlySpan<byte> Assemble(nint functionPtr, nint retValPtr, nint[] args)  => Is64Bit ? Assemble64(functionPtr, retValPtr, args) : Assemble86(functionPtr, retValPtr, args);
    ReadOnlySpan<byte> Assemble86(nint functionPtr, nint retValPtr, nint[] args)
    {
        Assembler asm = new();
        if (_attach)
        {
            asm.Push(_rootDomain);
            asm.MovEax(Exports[mono_thread_attach]);
            asm.CallEax();
            asm.AddEsp(4);
        }

        for (var i = args.Length - 1; i >= 0; --i) asm.Push(args[i]);
        asm.MovEax(functionPtr);
        asm.CallEax();
        asm.AddEsp((byte)(args.Length * 4));
        asm.MovEaxTo(retValPtr);
        asm.Return();

        return asm.AsSpan();
    }
    ReadOnlySpan<byte> Assemble64(nint functionPtr, nint retValPtr, nint[] args)
    {
        Assembler asm = new();

        asm.SubRsp(40);
        if (_attach)
        {
            asm.MovRax(Exports[mono_thread_attach]);
            asm.MovRcx(_rootDomain);
            asm.CallRax();
        }
        asm.MovRax(functionPtr);

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