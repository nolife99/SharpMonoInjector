using System.Reflection;
using System.Threading;
using UnityEngine;

namespace LCHack;

class Loader
{
    static GameObject Load;

    static void Init()
    {
        Load = new();
        Load.AddComponent<Hacks>();
        Object.DontDestroyOnLoad(Load);
        Thread.GetDomain().AssemblyResolve += CurrentDomain_AssemblyResolve;
    }
    static void Unload()
    {
        Object.Destroy(Load);
        Thread.GetDomain().AssemblyResolve -= CurrentDomain_AssemblyResolve;
    }
    static Assembly CurrentDomain_AssemblyResolve(object sender, System.ResolveEventArgs args)
    {
        using var manifestResourceStream = typeof(Loader).Assembly.GetManifestResourceStream("LCHack.0Harmony.dll");
        var numArray = new byte[(int)manifestResourceStream.Length];
        manifestResourceStream.Read(numArray, 0, (int)manifestResourceStream.Length);
        return Assembly.Load(numArray);
    }
}