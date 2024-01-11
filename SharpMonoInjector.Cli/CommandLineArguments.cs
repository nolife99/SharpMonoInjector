using System;
using System.Globalization;
using System.Linq;

namespace SharpMonoInjector.Cli;

public ref struct CommandLineArguments(string[] args)
{
    public readonly bool IsSwitchPresent(string name) => args.Any(arg => arg == name);

    public readonly bool GetLongArg(ReadOnlySpan<char> name, out long value)
    {
        if (GetStringArg(name, out var str)) return long.TryParse(str.StartsWith("0x") ? str[2..] : str, NumberStyles.AllowHexSpecifier, null, out value);
        value = 0;
        return false;
    }
    public readonly bool GetIntArg(ReadOnlySpan<char> name, out int value)
    {
        if (GetStringArg(name, out var str)) return int.TryParse(str.StartsWith("0x") ? str[2..] : str, NumberStyles.AllowHexSpecifier, null, out value);
        value = 0;
        return false;
    }
    public readonly bool GetStringArg(ReadOnlySpan<char> name, out ReadOnlySpan<char> value)
    {
        for (var i = 0; i < args.Length; ++i) if (args[i] == name) 
        {
            if (i == args.Length - 1) break;
            value = args[i + 1];
            return true;
        }

        value = null;
        return false;
    }
}