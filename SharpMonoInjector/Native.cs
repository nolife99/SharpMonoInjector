using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpMonoInjector;

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
    public static extern int GetModuleFileNameExA(nint hProcess, nint hModule, nint lpBaseName, int nSize);

    [DllImport("psapi.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern bool GetModuleInformation(nint hProcess, nint hModule, out nint lpmodinfo, int cb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern nint CreateRemoteThread(nint hProcess, nint lpThreadAttributes, int dwStackSize, nint lpStartAddress, nint lpParameter, int dwCreationFlags, out int lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static extern uint WaitForSingleObject(nint hHandle, int dwMilliseconds);
}