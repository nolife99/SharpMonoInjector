namespace SharpMonoInjector;

public readonly struct ExportedFunction(string name, nint address)
{
    public readonly string Name = name;
    public readonly nint Address = address;
}