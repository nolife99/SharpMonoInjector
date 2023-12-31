using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpMonoInjector;

public class Memory(nint handle) : IDisposable
{
    readonly List<(nint, int)> allocs = [];

    public string ReadString(nint address, int length, Encoding encoding)
    {
        Span<byte> bytes = stackalloc byte[length];
        for (var i = 0; i < length; ++i)
        {
            var read = Read<byte>(address + i);
            if (read == 0x00)
            {
                length = i;
                break;
            }
            bytes[i] = read;
        }
        return encoding.GetString(bytes[..length]);
    }
    public unsafe T Read<T>(nint address) where T : unmanaged
    {
        T ret;
        if (!Native.ReadProcessMemory(handle, address, (nint)(&ret), Marshal.SizeOf<T>()))
            throw new InjectorException("Failed to read process memory", new Win32Exception(Marshal.GetLastWin32Error()));
        
        return ret;
    }

    public nint AllocateAndWrite(ReadOnlySpan<byte> data)
    {
        var addr = Allocate(data.Length);
        Write(addr, data);
        return addr;
    }
    public nint AllocateAndWrite(string data) => AllocateAndWrite(Encoding.UTF8.GetBytes(data));
    public nint AllocateAndWrite(int data) => AllocateAndWrite(BitConverter.GetBytes(data));
    public nint AllocateAndWrite(long data) => AllocateAndWrite(BitConverter.GetBytes(data));

    public nint Allocate(int size)
    {
        var addr = Native.VirtualAllocEx(handle, 0, size, AllocationType.MEM_COMMIT, MemoryProtection.PAGE_EXECUTE_READWRITE);
        if (addr == 0) throw new InjectorException("Failed to allocate process memory", new Win32Exception(Marshal.GetLastWin32Error()));

        allocs.Add((addr, size));
        return addr;
    }
    public unsafe void Write(nint addr, ReadOnlySpan<byte> data)
    {
        fixed (void* ptr = &MemoryMarshal.GetReference(data)) if (!Native.WriteProcessMemory(handle, addr, (nint)ptr, data.Length))
            throw new InjectorException("Failed to write process memory", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    public void Dispose()
    {
        allocs.ForEach(kvp => Native.VirtualFreeEx(handle, kvp.Item1, kvp.Item2, MemoryFreeType.MEM_DECOMMIT));
        allocs.Clear();
    }
}