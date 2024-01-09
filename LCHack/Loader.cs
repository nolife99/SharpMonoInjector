using System;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;

namespace LCHack;

sealed class Loader
{
    static GameObject Load;

    static void Init()
    {
        Load = new();
        Load.AddComponent<Hacks>();

        Object.DontDestroyOnLoad(Load);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }
    static void Unload()
    {
        Object.Destroy(Load);
        AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
    }
    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        using var resource = typeof(Loader).Assembly.GetManifestResourceStream("LCHack.0Harmony.dll");
        var streamLen = (int)resource.Length;

        var numArray = new byte[streamLen];
        resource.Read(numArray, 0, streamLen);
        return Assembly.Load(numArray);
    }
}