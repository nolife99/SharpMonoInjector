using System.Reflection;
using System.Threading;
using LCHack.Scripting;
using UnityEngine;

namespace LCHack;

static class Loader
{
    static GameObject Load;

    static void Init()
    {
        Object.DontDestroyOnLoad(Load = new(null, typeof(Hacks)));
        Thread.GetDomain().AssemblyResolve += Resolve;
    }
    static void Unload()
    {
        Object.Destroy(Load);
        Thread.GetDomain().AssemblyResolve -= Resolve;
    }
    static Assembly Resolve(object sender, System.ResolveEventArgs args)
    {
        using var patch = typeof(Loader).Assembly.GetManifestResourceStream("LCHack.0Harmony.dll");
        var len = (int)patch.Length;

        var bytes = new byte[len];
        patch.Read(bytes, 0, len);
        return Assembly.Load(bytes);
    }
}