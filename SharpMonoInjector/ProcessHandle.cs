using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

public class ProcessHandle : SafeHandle
{
    public override bool IsInvalid => handle == 0;
    public ProcessHandle(int processId, ProcessAccess rights) : base(0, true) => handle = OpenProcess(rights, false, processId);

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