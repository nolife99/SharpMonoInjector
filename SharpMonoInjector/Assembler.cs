using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public readonly ref struct Assembler
{
    readonly List<byte> asm;
    public Assembler() => asm = new(16);

    public void MovRax(nint arg)
    {
        asm.AddRange([0x48, 0xB8]);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovRcx(nint arg)
    {
        asm.AddRange([0x48, 0xB9]);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovRdx(nint arg)
    {
        asm.AddRange([0x48, 0xBA]);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovR8(nint arg)
    {
        asm.AddRange([0x49, 0xB8]);
        asm.AddRange(BitConverter.GetBytes(arg));
    }
    public void MovR9(nint arg)
    {
        asm.AddRange([0x49, 0xB9]);
        asm.AddRange(BitConverter.GetBytes(arg));
    }

    public void SubRsp(byte arg) => asm.AddRange([0x48, 0x83, 0xEC, arg]);
    public void CallRax() => asm.AddRange([0xFF, 0xD0]);
    public void AddRsp(byte arg) => asm.AddRange([0x48, 0x83, 0xC4, arg]);

    public void MovRaxTo(nint dest)
    {
        asm.AddRange([0x48, 0xA3]);
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
    public void CallEax() => asm.AddRange([0xFF, 0xD0]);

    public void AddEsp(byte arg) => asm.AddRange([0x83, 0xC4, arg]);
    public void MovEaxTo(nint dest)
    {
        asm.Add(0xA3);
        asm.AddRange(BitConverter.GetBytes((int)dest));
    }
    public void Return() => asm.Add(0xC3);

    public ReadOnlySpan<byte> Compile() => CollectionsMarshal.AsSpan(asm);
}

// Unused
unsafe class PrimitiveCollection<T> : IList<T> where T : unmanaged
{
    void* buf;
    int count, capacity;
    readonly uint elementSize;

    public int Count => count;
    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => ThrowIfIndexOverflow(index) ? Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(buf, index)) : default;
        set
        {
            if (ThrowIfIndexOverflow(index)) Unsafe.WriteUnaligned(Unsafe.Add<T>(buf, index), value);
        }
    }

    public PrimitiveCollection() => buf = NativeMemory.Alloc(16, elementSize = (uint)sizeof(T));
    ~PrimitiveCollection() => NativeMemory.Free(buf); 
    
    public void Clear()
    {
        NativeMemory.Free(buf);
        buf = NativeMemory.Alloc(0);

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

    public ReadOnlySpan<byte> AsBytes() => new(buf, count * (int)elementSize);
    void EnsureCapacity(int minimum) => buf = NativeMemory.Realloc(buf, (uint)Math.Max(capacity += capacity / 2, minimum) * elementSize);

    public bool Contains(T item)
    {
        for (var i = 0; i < count; ++i) if (item.Equals(Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(buf, i)))) return true;
        return false;
    }
    public void CopyTo(T[] arr, int index)
    {
        var realLen = Math.Min(count, arr.Length - index);
        new ReadOnlySpan<T>(buf, realLen).CopyTo(new(arr, index, realLen));
    }

    public void Insert(int index, T item)
    {
        ThrowIfIndexOverflow(index);
        if (count == capacity) EnsureCapacityInsert(index);
        else if (index < count) NativeMemory.Copy(Unsafe.Add<T>(buf, index), Unsafe.Add<T>(buf, index + 1), (uint)(count - index) * elementSize);

        Unsafe.WriteUnaligned(Unsafe.Add<T>(buf, index), item);
        ++count;
    }
    public void InsertRange(int index, ReadOnlySpan<T> items)
    {
        var size = items.Length;
        if (count > 0)
        {
            if (capacity - count < size) EnsureCapacityInsert(index, size);
            else if (index < count) NativeMemory.Copy(Unsafe.Add<T>(buf, index), Unsafe.Add<T>(buf, index + size), (uint)(count - index) * elementSize);

            if (Unsafe.AreSame(ref Unsafe.AsRef<T>(buf), ref MemoryMarshal.GetReference(items)))
            {
                NativeMemory.Copy(buf, Unsafe.Add<T>(buf, index), (uint)index * elementSize);
                NativeMemory.Copy(Unsafe.Add<T>(buf, index + size), Unsafe.Add<T>(buf, index * 2), (uint)(count - index) * elementSize);
            }
            else items.CopyTo(new(Unsafe.Add<T>(buf, index), size));

            count += size;
        }
    }
    void EnsureCapacityInsert(int indexToInsert, int insertionCount = 1)
    {
        var newBuf = NativeMemory.Alloc((uint)Math.Max(count + insertionCount, capacity * 1.5f) * elementSize);
        if (indexToInsert != 0) NativeMemory.Copy(buf, newBuf, (uint)indexToInsert * elementSize);
        if (count != indexToInsert) NativeMemory.Copy(Unsafe.Add<T>(buf, indexToInsert), Unsafe.Add<T>(newBuf, indexToInsert + insertionCount), (uint)(count - indexToInsert) * elementSize);

        NativeMemory.Free(buf);
        buf = newBuf;
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
        ThrowIfIndexOverflow(index);
        --count;
        if (index < count) NativeMemory.Copy(Unsafe.Add<T>(buf, index + 1), Unsafe.Add<T>(buf, index), (uint)(count - index) * elementSize);
    }
    public int IndexOf(T item) => new ReadOnlySpan<byte>(buf, count * sizeof(T)).IndexOf(new ReadOnlySpan<byte>(&item, sizeof(T)));

    bool ThrowIfIndexOverflow(int index)
    {
        if ((uint)index >= (uint)count) throw new IndexOutOfRangeException($"Index: {index} | Count: {count}");
        return true;
    }

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

        readonly T IEnumerator<T>.Current => Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(data.buf, _index));
        readonly object IEnumerator.Current => Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(data.buf, _index));
    }
}