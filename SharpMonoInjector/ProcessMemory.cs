using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpMonoInjector;

public sealed class ProcessMemory(Process process) : IDisposable
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
        if (!Native.ReadProcessMemory(process.SafeHandle, addr, (nint)(&ret), sizeof(T)))
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
    public nint AllocateAndWrite<T>(T data) where T : unmanaged => AllocateAndWrite(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref data), Unsafe.SizeOf<T>()));

    public nint Allocate(int size)
    {
        var addr = Native.VirtualAllocEx(process.SafeHandle, 0, size, 0x00001000, 0x40);
        if (addr == 0) throw new InjectorException("Failed to allocate process memory", new Win32Exception(Marshal.GetLastWin32Error()));

        allocs.Add((addr, size));
        return addr;
    }
    public unsafe void Write(nint addr, ReadOnlySpan<byte> data)
    {
        fixed (void* ptr = data) if (!Native.WriteProcessMemory(process.SafeHandle, addr, (nint)ptr, data.Length))
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
        allocs.AsParallel().ForAll(pair => Native.VirtualFreeEx(process.SafeHandle, pair.Item1, pair.Item2, 0x00008000));
        if (disposing) allocs.Clear();
    }
}