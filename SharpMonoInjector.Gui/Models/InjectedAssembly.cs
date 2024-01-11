namespace SharpMonoInjector.Gui.Models;

internal readonly struct InjectedAssembly
{
    public int ProcessId { get; init; }
    public nint Address { get; init; }
    public bool Is64Bit { get; init; }
    public string Name { get; init; }
}