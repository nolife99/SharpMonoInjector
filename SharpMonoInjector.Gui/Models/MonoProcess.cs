namespace SharpMonoInjector.Gui.Models;

public readonly struct MonoProcess
{
    public nint MonoModule { get; init; }
    public string Name { get; init; }
    public int Id { get; init; }
}