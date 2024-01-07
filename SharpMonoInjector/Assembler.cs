using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public readonly ref struct Assembler
{
    readonly List<byte> asm;
    public Assembler() => asm = [];

    public void CallRax() => AddStack([0xFF, 0xD0]);
    public void MovRax(nint arg)
    {
        AddStack([0x48, 0xB8]);
        AddArgAsBytes(ref arg);
    }
    public void MovRaxTo(nint arg)
    {
        AddStack([0x48, 0xA3]);
        AddArgAsBytes(ref arg);
    }
    public void MovRcx(nint arg)
    {
        AddStack([0x48, 0xB9]);
        AddArgAsBytes(ref arg);
    }
    public void MovRdx(nint arg)
    {
        AddStack([0x48, 0xBA]);
        AddArgAsBytes(ref arg);
    }

    public void CallEax() => AddStack([0xFF, 0xD0]);
    public void MovEax(nint arg)
    {
        asm.Add(0xB8);
        AddArgAsBytes(ref Unsafe.As<nint, int>(ref arg));
    }
    public void MovEaxTo(nint arg)
    {
        asm.Add(0xA3);
        AddArgAsBytes(ref arg);
    }
    public void MovR8(nint arg)
    {
        AddStack([0x49, 0xB8]);
        AddArgAsBytes(ref arg);
    }
    public void MovR9(nint arg)
    {
        AddStack([0x49, 0xB9]);
        AddArgAsBytes(ref arg);
    }

    public void SubRsp(byte arg) => AddStack([0x48, 0x83, 0xEC, arg]);
    public void AddRsp(byte arg) => AddStack([0x48, 0x83, 0xC4, arg]);
    public void AddEsp(byte arg) => AddStack([0x83, 0xC4, arg]);

    public void Push(nint arg)
    {
        ref var intArg = ref Unsafe.As<nint, int>(ref arg);
        asm.Add(intArg < 128 ? (byte)0x6A : (byte)0x68);

        if (intArg > 255) AddArgAsBytes(ref intArg);
        else asm.Add(Unsafe.As<nint, byte>(ref arg));
    }

    public void Return() => asm.Add(0xC3);
    public ReadOnlySpan<byte> Compile() => CollectionsMarshal.AsSpan(asm);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddArgAsBytes<T>(ref T arg) => AddStack(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref arg), Unsafe.SizeOf<T>()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddStack(ReadOnlySpan<byte> arg) => asm.AddRange(arg);
}