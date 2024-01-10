using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LCHack;

internal sealed class Hacks : MonoBehaviour
{
    public static bool godMode, farScan = true, infCharge = true, infSprint = true, clock = true, cursorIsLocked = true, insertKeyWasPressed, isMenuOpen, esp = true, itemEsp = true, enemyEsp = true, addMoneySignal;
    public static int excScrap, addMoney;
    static string moneyS, scrapS;

    static PlayerControllerB client;
    static Rect windowRect = new(100, 100, 300, 300);
    const string on = "on", off = "off";

    void Start()
    {
        try
        {
            if (!Harmony.HasAnyPatches("com.p1st.LCHack")) new Harmony("com.p1st.LCHack").PatchAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
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

    static readonly Dictionary<Type, Array> cache = [];
    static void CacheObjects<T>() => cache[typeof(T)] = FindObjectsOfType(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool WorldToScreen(Vector3 world, out Vector3 screen)
    {
        screen = client.gameplayCamera.WorldToViewportPoint(world);
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

            if (obj is Terminal t && addMoneySignal)
            {
                t.groupCredits += addMoney;
                if (!client.IsServer) t.SyncGroupCreditsServerRpc(t.groupCredits, t.numberOfItemsInDropship);
                addMoneySignal = false;
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
        GUI.Label(new(screen, new(75, 50)), $"{text}{Vector3.Distance(client.transform.position, distObj):n0} ft");
    }
    void OnGUI()
    {
        GUI.Label(new(10, 5, 200, 30), "Lethal Company Menu v1.3.7");
        if ((client = GameNetworkManager.Instance.localPlayerController) is not null) GUI.Label(new(10, 25, 200, 30), $"{enemyCount:n0} enem{(enemyCount == 1 ? "y" : "ies")}");

        if (isMenuOpen) windowRect = GUILayout.Window(short.MinValue, windowRect, _ =>
        {
            if (GUILayout.Button("Toggle invincibility (non insta-kill): " + (godMode ? on : off))) godMode = !godMode;
            if (GUILayout.Button("Toggle infinite sprint: " + (infSprint ? on : off))) infSprint = !infSprint;
            if (GUILayout.Button("Toggle all ESP: " + (esp ? on : off))) esp = !esp;
            if (GUILayout.Button("Toggle item ESP: " + (itemEsp ? on : off))) itemEsp = !itemEsp;
            if (GUILayout.Button("Toggle enemy ESP: " + (enemyEsp ? on : off))) enemyEsp = !enemyEsp;
            if (GUILayout.Button("Toggle distant scan: " + (farScan ? on : off))) farScan = !farScan;
            if (GUILayout.Button("Show clock: " + (clock ? on : off))) clock = !clock;

            if (GUILayout.Button($"Add money: {addMoney:n0}")) addMoneySignal = true;
            moneyS = GUILayout.TextField(moneyS);
            if (float.TryParse(moneyS, NumberStyles.Number, CultureInfo.InvariantCulture, out var add)) addMoney = (int)Mathf.Clamp(add, -20000000, 20000000);

            GUILayout.Label("When non-host, drop the item and pick it back up for a full charge.");
            if (GUILayout.Button("Toggle infinite battery: " + (infCharge ? on : off))) infCharge = !infCharge;

            GUILayout.Label("Host only features:");

            var t = TimeOfDay.Instance;
            if (GUILayout.Button("Set quota reached") && t is not null)
            {
                t.quotaFulfilled = t.profitQuota;
                t.UpdateProfitQuotaCurrentTime();
            }

            GUILayout.Label($"Add scrap value: {excScrap:n0}");
            scrapS = GUILayout.TextField(scrapS);
            if (float.TryParse(scrapS, NumberStyles.Number, CultureInfo.InvariantCulture, out add)) excScrap = (int)Mathf.Clamp(add, -1000000, 1000000);

            GUI.DragWindow();
        }, "Lethal Company");

        if (esp)
        {
            ProcessObjects<EntranceTeleport>((entry, _) => entry.isEntranceToBuilding ? " Entrance " : " Exit ", Color.cyan);
            ProcessObjects<Landmine>((_, _) => "Landmine ", Color.red);
            ProcessObjects<Turret>((_, _) => "Turret ", Color.red);
            ProcessObjects<Terminal>((_, _) => "Terminal ", Color.magenta);
            ProcessObjects<SteamValveHazard>((_, _) => "Steam Valve ", Color.yellow);
            ProcessPlayers();

            if (itemEsp) ProcessObjects<GrabbableObject>((obj, _) => obj.itemProperties.itemName + " ", Color.blue);
            if (enemyEsp) ProcessEnemies();
        }
        if (infCharge && client.currentlyHeldObjectServer is not null && client.IsServer) client.currentlyHeldObjectServer.insertedBattery.charge = 1;
    }
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

    [DllImport("user32")] [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern short GetAsyncKeyState(int vKey);
}