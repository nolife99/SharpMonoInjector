using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using Object = UnityEngine.Object;

namespace LCHack;

internal sealed class Hacks : MonoBehaviour
{
    static PlayerControllerB server;
    static bool cursorIsLocked = true, insertKeyWasPressed, isMenuOpen, esp = true, itemEsp = true, enemyEsp = true, addMoney;

    internal static bool godMode, farScan = true, infCharge = true, infSprint = true, clock = true;
    internal static int excScrap;

    void Start()
    {
        try
        {
            new Harmony("com.p1st.LCHack").PatchAll();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error during Harmony patching: {e.Message}\n");
        }
        StartCoroutine(CacheRefreshRoutine());
    }

    static IEnumerator CacheRefreshRoutine()
    {
        while (true)
        {
            cache.Clear();
            CacheObjects<EntranceTeleport>();
            CacheObjects<GrabbableObject>();
            CacheObjects<Landmine>();
            CacheObjects<Turret>();
            CacheObjects<Terminal>();
            CacheObjects<PlayerControllerB>();
            CacheObjects<SteamValveHazard>();
            CacheObjects<EnemyAI>();

            yield return new WaitForSecondsRealtime(1.5f);
        }
    }

    static readonly Dictionary<Type, Object[]> cache = [];

    static void CacheObjects<T>() where T : Component => cache[typeof(T)] = FindObjectsOfType(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool WorldToScreen(Vector3 world, out Vector3 screen)
    {
        screen = server.gameplayCamera.WorldToViewportPoint(world);
        var visible = screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1 && screen.z > 0;

        screen.x *= Screen.width;
        screen.y *= Screen.height;
        screen.y = Screen.height - screen.y;

        return visible;
    }

    static void ProcessObjects<T>(Func<T, Vector3, string> labelBuilder, Color labelColor) where T : Component
    {
        if (cache.TryGetValue(typeof(T), out var source)) foreach (T obj in source)
        {
            if ((obj is GrabbableObject g && (g.isPocketed || g.isHeld)) || 
                (obj is GrabbableObject g2 && g2.itemProperties.itemName is "clipboard" or "Sticky note") ||
                (obj is SteamValveHazard v && !v.triggerScript.interactable)) continue;

            if (obj is Terminal t && addMoney)
            {
                t.groupCredits += 100;
                if (!server.IsServer) t.SyncGroupCreditsServerRpc(t.groupCredits, t.numberOfItemsInDropship);
                addMoney = false;
            }
            if (WorldToScreen(obj.transform.position, out var screen)) DrawLabel(screen, labelBuilder(obj, screen), labelColor, obj.transform.position);
        }
    }
    static void ProcessPlayers()
    {
        if (cache.TryGetValue(typeof(PlayerControllerB), out var source)) foreach (PlayerControllerB pl in source) if (!pl.IsLocalPlayer && pl.isPlayerControlled && WorldToScreen(pl.transform.position, out var screen))
            DrawLabel(screen, pl.playerUsername + (pl.isPlayerDead ? " (Dead)" : " "), Color.green, pl.transform.position);
    }

    static int enemyCount;
    static void ProcessEnemies()
    {
        if (cache.TryGetValue(typeof(EnemyAI), out var source))
        {
            foreach (EnemyAI e in source) if (WorldToScreen(e.transform.position, out var screen))
                DrawLabel(screen, !string.IsNullOrWhiteSpace(e.enemyType.enemyName) ? e.enemyType.enemyName + " " : "Unknown Enemy ", Color.red, e.transform.position);

            enemyCount = source.Length;
        }
        else enemyCount = 0;
    }

    static void DrawLabel(Vector3 screen, string text, Color color, Vector3 distObj)
    {
        GUI.contentColor = color;
        GUI.Label(new(screen, new(75, 50)), $"{text}{Vector3.Distance(server.transform.position, distObj):f0}m");
    }

    static Rect windowRect = new(100, 100, 300, 500);
    const string on = "on", off = "off";

    void OnGUI()
    {
        GUI.Label(new(10, 5, 200, 30), "Lethal Company Menu v1.3.7");

        server = GameNetworkManager.Instance.localPlayerController;
        if (server is not null) GUI.Label(new(10, 25, 200, 30), enemyCount == 1 ? $"{enemyCount} enemy" : $"{enemyCount} enemies");

        if (isMenuOpen) windowRect = GUILayout.Window(short.MinValue, windowRect, _ =>
        {
            GUILayout.Label("Master ESP: " + (esp ? on : off));
            GUILayout.Label("Item ESP: " + (itemEsp ? on : off));
            GUILayout.Label("Enemy ESP: " + (enemyEsp ? on : off));
            GUILayout.Label("Invincible (non insta-kill): " + (godMode ? on : off));
            GUILayout.Label("Infinite Sprint: " + (infSprint ? on : off));
            GUILayout.Label("Unlimited Scan Range: " + (farScan ? on : off));
            GUILayout.Label("Unlimited Item Power: " + (infCharge ? on : off));
            GUILayout.Label($"Scrap Value: {excScrap:n0}");
            GUILayout.Label("Show Clock: " + (clock ? on : off));

            if (GUILayout.Button("Toggle Invincibility")) godMode = !godMode;
            if (GUILayout.Button("Toggle Infinite Sprint")) infSprint = !infSprint;
            if (GUILayout.Button("Toggle All ESP")) esp = !esp;
            if (GUILayout.Button("Toggle Item ESP")) itemEsp = !itemEsp;
            if (GUILayout.Button("Toggle Enemy ESP")) enemyEsp = !enemyEsp;
            if (GUILayout.Button("Unlimited Scan Range")) farScan = !farScan;
            if (GUILayout.Button("Add 100 Cash")) addMoney = true;
            if (GUILayout.Button("Show Clock")) clock = !clock;

            GUILayout.Label("When non-host, drop the item on the ground and pick it back up for a full charge.");
            if (GUILayout.Button("Toggle Unlimited Item Power")) infCharge = !infCharge;

            GUILayout.Label("Host only features:");

            var t = TimeOfDay.Instance;
            if (GUILayout.Button("Set Quota Reached") && t is not null)
            {
                t.quotaFulfilled = t.profitQuota;
                t.UpdateProfitQuotaCurrentTime();
            }

            GUILayout.Label("Set Scrap Value");
            var text = GUILayout.TextField($"{excScrap:f0}");
            if (string.IsNullOrWhiteSpace(text)) excScrap = 0;
            else if (ulong.TryParse(text, out var exc)) excScrap = (int)Mathf.Clamp(exc, 0, 1000000);

            GUI.DragWindow();
        }, "Lethal Company");

        if (esp)
        {
            ProcessObjects<EntranceTeleport>((entrance, _) => entrance.isEntranceToBuilding ? " Entrance " : " Exit ", Color.cyan);
            ProcessObjects<Landmine>((_, _) => "Landmine ", Color.red);
            ProcessObjects<Turret>((_, _) => "Turret ", Color.red);
            ProcessObjects<Terminal>((_, _) => "Ship Terminal ", Color.magenta);
            ProcessObjects<SteamValveHazard>((_, _) => "Steam Valve ", Color.yellow);
            ProcessPlayers();

            if (itemEsp) ProcessObjects<GrabbableObject>((grabbableObject, _) => grabbableObject.itemProperties.itemName + " ", Color.blue);
            if (enemyEsp) ProcessEnemies();
        }
        if (infCharge && server.currentlyHeldObjectServer is not null && server.IsServer) server.currentlyHeldObjectServer.insertedBattery.charge = 1;
    }

    [DllImport("user32")] [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern short GetAsyncKeyState(int vKey);

    void Update()
    {
        var keyDown = (GetAsyncKeyState(45) & 0x8000) > 0;
        if (keyDown && !insertKeyWasPressed) isMenuOpen = !isMenuOpen;

        if (StartOfRound.Instance is not null)
        {
            if (isMenuOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                cursorIsLocked = false;
            }
            else if (!cursorIsLocked)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                cursorIsLocked = true;
            }
        }
        insertKeyWasPressed = keyDown;
    }
}