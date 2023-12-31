namespace SharpMonoInjector.Gui.Models;

public class InjectedAssembly
{
    public int ProcessId { get; init; }
    public nint Address { get; init; }
    public bool Is64Bit { get; init; }
    public string Name { get; init; }
}