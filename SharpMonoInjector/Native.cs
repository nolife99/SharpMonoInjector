using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

[Flags] public enum AllocationType
{
    MEM_COMMIT = 0x00001000,
    MEM_RESERVE = 0x00002000,
    MEM_RESET = 0x00080000,
    MEM_RESET_UNDO = 0x1000000,
    MEM_LARGE_PAGES = 0x20000000,
    MEM_PHYSICAL = 0x00400000,
    MEM_TOP_DOWN = 0x00100000
}
[Flags] public enum MemoryProtection
{
    PAGE_EXECUTE = 0x10,
    PAGE_EXECUTE_READ = 0x20,
    PAGE_EXECUTE_READWRITE = 0x40,
    PAGE_EXECUTE_WRITECOPY = 0x80,
    PAGE_NOACCESS = 0x01,
    PAGE_READONLY = 0x02,
    PAGE_READWRITE = 0x4,
    PAGE_WRITECOPY = 0x8,
    PAGE_TARGETS_INVALID = 0x40000000,
    PAGE_TARGETS_NO_UPDATE = 0x40000000,
    PAGE_GUARD = 0x100,
    PAGE_NOCACHE = 0x200,
    PAGE_WRITECOMBINE = 0x400
}
[Flags] public enum MemoryFreeType
{
    MEM_DECOMMIT = 0x4000,
    MEM_RELEASE = 0x8000
}

public static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool CloseHandle(nint handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool OpenProcessToken(nint ProcessHandle, int DesiredAccess, out nint TokenHandle);

    [DllImport("psapi.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool EnumProcessModulesEx(nint hProcess, nint lphModule, int cb, out int lpcbNeeded, int dwFilterFlag = 0x03);

    [DllImport("psapi.dll")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern int GetModuleFileNameEx(nint hProcess, nint hModule, nint lpBaseName, int nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool GetModuleInformation(nint hProcess, nint hModule, out nint lpmodinfo, int cb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, nint lpBuffer, int nSize, int lpNumberOfBytesWritten = 0);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, nint lpBuffer, int nSize, int lpNumberOfBytesRead = 0);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern nint VirtualAllocEx(nint hProcess, nint lpAddress, int dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool VirtualFreeEx(nint hProcess, nint lpAddress, int dwSize, MemoryFreeType dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern nint CreateRemoteThread(nint hProcess, nint lpThreadAttributes, int dwStackSize, nint lpStartAddress, nint lpParameter, int dwCreationFlags, out int lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern uint WaitForSingleObject(nint hHandle, int dwMilliseconds);
}