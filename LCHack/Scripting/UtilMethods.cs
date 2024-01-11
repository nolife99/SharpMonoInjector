using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameNetcodeStuff;
using UnityEngine;

namespace LCHack.Scripting;

internal partial class Hacks
{
    public static bool godMode, farScan = true, infCharge = true, infSprint = true, clock = true;
    public static int excScrap;

    static int addMoney, enemyCount, xpCount;
    static bool lockedCursor = true, keyPress, isMenuOpen, esp = true, itemEsp = true, enemyEsp = true, addMoneySignal;

    static string moneyS, scrapS, xp;
    const string on = "on", off = "off", harmonyID = "com.p1st.LCHack";
    static Rect windowRect = new(Screen.width * .6f, Screen.height / 6f, 300, 0);

    static readonly Dictionary<Type, Array> cache = new(8);
    static PlayerControllerB client;
    static MethodBase setLevel;

    static bool WorldToScreen(Vector3 world, out Vector3 screen)
    {
        screen = client.gameplayCamera.WorldToViewportPoint(world);
        var visible = screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1 && screen.z > 0;

        screen.x *= Screen.width;
        screen.y *= Screen.height;
        screen.y = Screen.height - screen.y;

        return visible;
    }
    static void DrawLabel(Vector3 screen, string text, Color color, Vector3 distObj)
    {
        GUI.contentColor = color;
        GUI.Label(new(screen, new(75, 50)), $"{text}{Vector3.Distance(client.transform.position, distObj):n0} ft");
    }

    [DllImport("user32")] [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern short GetAsyncKeyState(int vKey);
}