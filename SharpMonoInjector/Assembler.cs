using System;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public readonly ref struct Assembler
{
    readonly ManagedNativeList<byte> asm;
    public Assembler() => asm = new();

    public void MovRax(nint arg)
    {
        asm.AddRange(0x48, 0xB8);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovRcx(nint arg)
    {
        asm.AddRange(0x48, 0xB9);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovRdx(nint arg)
    {
        asm.AddRange(0x48, 0xBA);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovR8(nint arg)
    {
        asm.AddRange(0x49, 0xB8);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovR9(nint arg)
    {
        asm.AddRange(0x49, 0xB9);
        asm.AddRange(BitConverter.GetBytes(arg));
    }

    public void SubRsp(byte arg) => asm.AddRange(0x48, 0x83, 0xEC, arg);
    public void CallRax() => asm.AddRange(0xFF, 0xD0);
    public void AddRsp(byte arg) => asm.AddRange(0x48, 0x83, 0xC4, arg);

    public void MovRaxTo(nint dest)
    {
        asm.AddRange(0x48, 0xA3);
        asm.AddRange(BitConverter.GetBytes(dest));
    }
    public void Push(nint arg)
    {
        var intArg = (int)arg;
        asm.Add(intArg < 128 ? (byte)0x6A : (byte)0x68);

        if (intArg > 255) asm.AddRange(BitConverter.GetBytes(intArg));
        else asm.Add((byte)arg);
    }
    public void MovEax(nint arg)
    {
        asm.Add(0xB8);
        asm.AddRange(BitConverter.GetBytes((int)arg));
    }
    public void CallEax() => asm.AddRange(0xFF, 0xD0);

    public void AddEsp(byte arg) => asm.AddRange(0x83, 0xC4, arg);
    public void MovEaxTo(nint dest)
    {
        asm.Add(0xA3);
        asm.AddRange(BitConverter.GetBytes((int)dest));
    }
    public void Return() => asm.Add(0xC3);

    public ReadOnlySpan<byte> AsSpan() => asm.AsSpan();
}
unsafe class ManagedNativeList<T> where T : unmanaged
{
    T* arr;
    int count, capacity;
    readonly uint elementSize;

    public ManagedNativeList() => arr = (T*)NativeMemory.Alloc(32 * (elementSize = (uint)Marshal.SizeOf<T>()));
    ~ManagedNativeList() => NativeMemory.Free(arr);

    public void Add(T item)
    {
        if (count == capacity) EnsureCapacity(count + 1);
        arr[count++] = item;
    }
    public void AddRange(params T[] array)
    {
        var total = count + array.Length;
        if (total > capacity) EnsureCapacity(total);
        for (var i = 0; i < array.Length; ++i, ++count) arr[count] = array[i];
    }
    void EnsureCapacity(int minimum) => arr = (T*)NativeMemory.Realloc(arr, (uint)(capacity = Math.Max(capacity << 1, minimum)) * elementSize);

    public ReadOnlySpan<T> AsSpan() => new(arr, count);
}