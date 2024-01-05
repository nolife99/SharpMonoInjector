﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public readonly ref struct Assembler
{
    readonly List<byte> asm;
    public Assembler() => asm = [];

    public void CallRax() => asm.AddRange(stackalloc byte[] { 0xFF, 0xD0 });
    public void MovRax(nint arg)
    {
        asm.AddRange(stackalloc byte[] { 0x48, 0xB8 });
        asm.AddRange(MemoryMarshal.AsBytes([arg]));
    }
    public void MovRaxTo(nint dest)
    {
        asm.AddRange([0x48, 0xA3]);
        asm.AddRange(BitConverter.GetBytes(dest));
    }
    public void MovRcx(nint arg)
    {
        asm.AddRange(stackalloc byte[] { 0x48, 0xB9 });
        asm.AddRange(MemoryMarshal.AsBytes([arg]));
    }
    public void MovRdx(nint arg)
    {
        asm.AddRange(stackalloc byte[] { 0x48, 0xBA });
        asm.AddRange(MemoryMarshal.AsBytes([arg]));
    }

    public void CallEax() => asm.AddRange(stackalloc byte[] { 0xFF, 0xD0 });
    public void MovEax(nint arg)
    {
        asm.Add(0xB8);
        asm.AddRange(MemoryMarshal.AsBytes([(int)arg]));
    }
    public void MovEaxTo(nint dest)
    {
        asm.Add(0xA3);
        asm.AddRange(MemoryMarshal.AsBytes([(int)dest]));
    }
    public void MovR8(nint arg)
    {
        asm.AddRange(stackalloc byte[] { 0x49, 0xB8 });
        asm.AddRange(MemoryMarshal.AsBytes([arg]));
    }
    public void MovR9(nint arg)
    {
        asm.AddRange(stackalloc byte[] { 0x49, 0xB9 });
        asm.AddRange(MemoryMarshal.AsBytes([arg]));
    }

    public void SubRsp(byte arg) => asm.AddRange(stackalloc byte[] { 0x48, 0x83, 0xEC, arg });
    public void AddRsp(byte arg) => asm.AddRange(stackalloc byte[] { 0x48, 0x83, 0xC4, arg });
    public void AddEsp(byte arg) => asm.AddRange(stackalloc byte[] { 0x83, 0xC4, arg });

    public void Push(nint arg)
    {
        var intArg = (int)arg;
        asm.Add(intArg < 128 ? (byte)0x6A : (byte)0x68);

        if (intArg > 255) asm.AddRange(MemoryMarshal.AsBytes([intArg]));
        else asm.Add((byte)arg);
    }
    public void Return() => asm.Add(0xC3);

    public ReadOnlySpan<byte> Compile() => CollectionsMarshal.AsSpan(asm);
}

/* 
using System.Runtime.CompilerServices;
using System.Collections;

unsafe class PrimitiveCollection<T> : ICollection<T> where T : unmanaged, IEquatable<T>
{
    void* buf;
    int count, capacity;
    readonly uint elementSize;

    public int Count => count;
    public bool IsReadOnly => false;

    public PrimitiveCollection() => buf = NativeMemory.Alloc((uint)(capacity = 16) * (elementSize = (uint)sizeof(T)));
    ~PrimitiveCollection() => NativeMemory.Free(buf);

    public void CopyTo(T[] arr, int index)
    {
        var realLen = Math.Min(count, arr.Length - index);
        new ReadOnlySpan<T>(buf, realLen).CopyTo(new(arr, index, realLen));
    }
    public ReadOnlySpan<T> AsSpan() => new(buf, count);

    public void Clear()
    {
        buf = NativeMemory.Realloc(buf, 0);
        count = 0;
        capacity = 0;
    }

    public void Add(T item)
    {
        if (count == capacity) EnsureCapacity(count + 1);
        Unsafe.WriteUnaligned(Unsafe.Add<T>(buf, count++), item);
    }
    public void AddRange(ReadOnlySpan<T> values)
    {
        var len = values.Length;
        var total = count + len;
        if (total > capacity) EnsureCapacity(total);

        values.CopyTo(new(Unsafe.Add<T>(buf, count), len));
        count = total;
    }
    public bool Remove(T item)
    {
        try
        {
            RemoveAt(IndexOf(item));
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
        return true;
    }
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)count) throw new IndexOutOfRangeException($"Index: {index} | Count: {count}");
        --count;
        if (index < count) NativeMemory.Copy(Unsafe.Add<T>(buf, index + 1), Unsafe.Add<T>(buf, index), (uint)(count - index) * elementSize);
    }

    public bool Contains(T item) => AsSpan().Contains(item);
    public int IndexOf(T item) => AsSpan().IndexOf(item);

    void EnsureCapacity(int minimum) => buf = NativeMemory.Realloc(buf, (uint)Math.Max(capacity += capacity / 2, minimum) * elementSize);

    public IEnumerator<T> GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    struct Enumerator(PrimitiveCollection<T> data) : IEnumerator<T>, IEnumerator
    {
        int _index = -1;

        bool IEnumerator.MoveNext()
        {
            var index = _index + 1;
            if (index < data.Count)
            {
                _index = index;
                return true;
            }
            return false;
        }

        void IEnumerator.Reset() => _index = -1;
        void IDisposable.Dispose() => _index = int.MaxValue;

        readonly T IEnumerator<T>.Current => Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(data.buf, _index < data.Count ? _index : data.Count));
        readonly object IEnumerator.Current => Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(data.buf, _index < data.Count ? _index : data.Count));
    }
} */