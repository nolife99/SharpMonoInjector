using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public sealed class Assembler
{
    readonly List<byte> asm = [];

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
    public void SubRsp(byte arg)
    {
        asm.AddRange([0x48, 0x83, 0xEC]);
        asm.Add(arg);
    }

    public void CallRax() => asm.AddRange([0xFF, 0xD0]);
    public void AddRsp(byte arg)
    {
        asm.AddRange([0x48, 0x83, 0xC4]);
        asm.Add(arg);
    }
    public void MovRaxTo(nint dest)
    {
        asm.AddRange([0x48, 0xA3]);
        asm.AddRange(BitConverter.GetBytes(dest));
    }
    public void Push(nint arg)
    {
        var intArg = (int)arg;
        asm.Add(intArg < 128 ? (byte)0x6A : (byte)0x68);
        asm.AddRange(intArg <= 255 ? [(byte)arg] : BitConverter.GetBytes(intArg));
    }
    public void MovEax(nint arg)
    {
        asm.Add(0xB8);
        asm.AddRange(BitConverter.GetBytes((int)arg));
    }
    public void CallEax() => asm.AddRange([0xFF, 0xD0]);

    public void AddEsp(byte arg)
    {
        asm.AddRange([0x83, 0xC4]);
        asm.Add(arg);
    }
    public void MovEaxTo(nint dest)
    {
        asm.Add(0xA3);
        asm.AddRange(BitConverter.GetBytes((int)dest));
    }
    public void Return() => asm.Add(0xC3);

    public ReadOnlySpan<byte> AsSpan() => CollectionsMarshal.AsSpan(asm);
}