using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpMonoInjector;

public sealed class ProcessMemory(ProcessHandle handle) : IDisposable
{
    readonly List<(nint, int)> allocs = [];

    public string ReadString(nint addr, int length, Encoding encoding)
    {
        Span<byte> bytes = stackalloc byte[length];
        for (var i = 0; i < length; ++i)
        {
            var read = Read<byte>(addr + i);
            if (read == 0)
            {
                length = i;
                break;
            }
            bytes[i] = read;
        }
        return encoding.GetString(bytes[..length]);
    }
    public unsafe T Read<T>(nint addr) where T : unmanaged
    {
        T ret;
        if (!ReadProcessMemory(handle.DangerousGetHandle(), addr, (nint)(&ret), Marshal.SizeOf<T>()))
            throw new InjectorException("Failed to read process memory", new Win32Exception(Marshal.GetLastWin32Error()));
        
        return ret;
    }

    public nint AllocateAndWrite(ReadOnlySpan<byte> data)
    {
        var addr = Allocate(data.Length);
        Write(addr, data);
        return addr;
    }
    public nint AllocateAndWrite(ReadOnlySpan<char> data) => AllocateAndWrite(Encoding.ASCII.GetBytes(data.ToArray()));
    public nint AllocateAndWrite<T>(T data) where T : unmanaged => AllocateAndWrite(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref data), Marshal.SizeOf<T>()));

    public nint Allocate(int size)
    {
        var addr = VirtualAllocEx(handle.DangerousGetHandle(), 0, size, 0x00001000, 0x40);
        if (addr == 0) throw new InjectorException("Failed to allocate process memory", new Win32Exception(Marshal.GetLastWin32Error()));

        allocs.Add((addr, size));
        return addr;
    }
    public unsafe void Write(nint addr, ReadOnlySpan<byte> data)
    {
        fixed (void* ptr = data) if (!WriteProcessMemory(handle.DangerousGetHandle(), addr, (nint)ptr, data.Length))
            throw new InjectorException("Failed to write process memory", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    ~ProcessMemory() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        allocs.AsParallel().ForAll(kvp => VirtualFreeEx(handle.DangerousGetHandle(), kvp.Item1, kvp.Item2, 0x4000));
        if (disposing) allocs.Clear();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, nint lpBuffer, int nSize, int lpNumberOfBytesWritten = 0);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, nint lpBuffer, int nSize, int lpNumberOfBytesRead = 0);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern nint VirtualAllocEx(nint hProcess, nint lpAddress, int dwSize, int flAllocationType, int flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern bool VirtualFreeEx(nint hProcess, nint lpAddress, int dwSize, int dwFreeType);
}
public class ProcessHandle : SafeHandle
{
    public override bool IsInvalid => handle == 0;
    public ProcessHandle(int processId, ProcessAccess rights = ProcessAccess.All) : base(0, true) => handle = OpenProcess(rights, false, processId);

    protected override bool ReleaseHandle() => Native.CloseHandle(handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern nint OpenProcess(ProcessAccess dwDesiredAccess, bool bInheritHandle, int processId);
}
[Flags] public enum ProcessAccess
{
    All = 0x1FFFFF,
    CreateProcess = 0x0080,
    CreateThread = 0x0002,
    Duplicate = 0x0040,
    QueryInfo = 0x0400,
    QueryLimitedInfo = 0x1000,
    SetInfo = 0x0200,
    SetQuota = 0x0100,
    SuspendResume = 0x0800,
    Terminate = 0x0001,
    OperationVM = 0x0008,
    ReadVM = 0x0010,
    WriteVM = 0x0020,
    Sync = 0x00100000
}