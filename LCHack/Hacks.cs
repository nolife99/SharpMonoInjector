using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LCHack;

internal sealed class Hacks : MonoBehaviour
{
    bool cursorIsLocked = true, insertKeyWasPressed, isMenuOpen, esp = true, itemEsp = true, enemyEsp = true, addMoney;
    internal bool godMode, farScan = true, infCharge = true, infSprint = true, highItemVal, clock = true;
    static int windowId;

    static Hacks instance;
    internal static Hacks Instance => instance ??= FindObjectOfType<Hacks>() ?? new GameObject("HacksSingleton").AddComponent<Hacks>();

    void Awake()
    {
        if (instance is null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }
        Destroy(gameObject);
    }
    void Start()
    {
        using (var rg = RandomNumberGenerator.Create())
        {
            var bytes = new byte[5];
            rg.GetBytes(bytes);
            windowId = BitConverter.ToInt32(bytes, 0);
        }
        try
        {
            new Harmony("com.p1st.LCHack").PatchAll();
        }
        catch (Exception ex)
        {
            Logger.Log("Error during Harmony patching: " + ex.Message);
        }
        StartCoroutine(CacheRefreshRoutine());
    }

    IEnumerator CacheRefreshRoutine()
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
            UpdateEnemyCount();

            yield return new WaitForSeconds(2.5f);
        }
    }

    int enemyCount;
    void UpdateEnemyCount()
    {
        if (cache.TryGetValue(typeof(EnemyAI), out var list)) enemyCount = list.Length;
        else enemyCount = 0;
    }

    readonly Dictionary<Type, Component[]> cache = [];
    void CacheObjects<T>() where T : Component => cache[typeof(T)] = FindObjectsOfType<T>();

    static bool WorldToScreen(Camera camera, Vector3 world, out Vector3 screen)
    {
        screen = camera.WorldToViewportPoint(world);
        screen.x *= Screen.width;
        screen.y *= Screen.height;
        screen.y = Screen.height - screen.y;
        return screen.z > 0;
    }

    void ProcessObjects<T>(Func<T, Vector3, string> labelBuilder, Color labelColor) where T : Component
    {
        if (cache.TryGetValue(typeof(T), out var source)) for (var i = 0; i < source.Length; ++i) if (source[i] is T obj)
        {
            if (obj is GrabbableObject GO && (GO.isPocketed || GO.isHeld || GO.itemProperties.itemName is "clipboard" or "Sticky note")) continue;
            if (obj is SteamValveHazard valve && !valve.triggerScript.interactable) continue;
            if (obj is Terminal terminal && addMoney)
            {
                if (GameNetworkManager.Instance.localPlayerController.IsServer)
                {
                    terminal.groupCredits += 200;
                    addMoney = false;
                }
                else
                {
                    terminal.groupCredits += 200;
                    terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                    addMoney = false;
                }
            }
            if (WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera, obj.transform.position, out var screen))
                DrawLabel(screen, labelBuilder(obj, screen), labelColor, Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, obj.transform.position));
        }
    }
    void ProcessPlayers()
    {
        if (cache.TryGetValue(typeof(PlayerControllerB), out var source)) for (var i = 0; i < source.Length; ++i) if (source[i] is PlayerControllerB player && !player.isPlayerDead && !player.IsLocalPlayer && player.isPlayerControlled && WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera, player.transform.position, out var screen))
            DrawLabel(screen, player.playerUsername + " ", Color.green, Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, player.transform.position));
    }
    void ProcessEnemies()
    {
        if (cache.TryGetValue(typeof(EnemyAI), out var source)) for (var i = 0; i < source.Length; ++i) if (source[i] is EnemyAI enemy && WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera, enemy.transform.position, out var screen))
            DrawLabel(screen, !string.IsNullOrWhiteSpace(enemy.enemyType.enemyName) ? enemy.enemyType.enemyName + " " : "Unknown Enemy ", Color.red, Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, enemy.transform.position));
    }

    static void DrawLabel(Vector3 screenPosition, string text, Color color, float distance)
    {
        GUI.contentColor = color;
        GUI.Label(new(screenPosition.x, screenPosition.y, 75, 50), $"{text}{Mathf.RoundToInt(distance * 3.28084f)} ft");
    }

    Rect windowRect = new(100, 100, 300, 500);
    void OnGUI()
    {
        GUI.Label(new(10, 5, 200, 30), "Lethal Company Menu v1.3.7");
        if (StartOfRound.Instance is not null) GUI.Label(new(10, 25, 200, 30), enemyCount == 1 ? $"{enemyCount} enemy" : $"{enemyCount} enemies");

        const string on = "on", off = "off";
        if (isMenuOpen) windowRect = GUILayout.Window(windowId, windowRect, _ =>
        {
            GUILayout.Label("Master ESP: " + (esp ? on : off));
            GUILayout.Label("Item ESP: " + (itemEsp ? on : off));
            GUILayout.Label("Enemy ESP: " + (enemyEsp ? on : off));
            GUILayout.Label("Godmode: " + (godMode ? on : off));
            GUILayout.Label("Infinite sprint: " + (infSprint ? on : off));
            GUILayout.Label("Unlimited Scan Range: " + (farScan ? on : off));
            GUILayout.Label("Unlimited Item Power: " + (infCharge ? on : off));
            GUILayout.Label("High Scrap Value: " + (highItemVal ? on : off));
            GUILayout.Label("Show Clock: " + (clock ? on : off));

            if (GUILayout.Button("Toggle Godmode")) godMode = !godMode;
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
            if (GUILayout.Button("Set Quota Reached") && TimeOfDay.Instance is not null)
            {
                TimeOfDay.Instance.quotaFulfilled = TimeOfDay.Instance.profitQuota;
                TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
            }
            if (GUILayout.Button("High scrap value")) highItemVal = !highItemVal;

            GUI.DragWindow();
        }, "Lethal Company");

        if (esp)
        {
            ProcessObjects<EntranceTeleport>((entrance, _) => !entrance.isEntranceToBuilding ? " Exit " : " Entrance ", Color.cyan);
            ProcessObjects<Landmine>((_, _) => "Landmine ", Color.red);
            ProcessObjects<Turret>((_, _) => "Turret ", Color.red);
            ProcessObjects<Terminal>((_, _) => "Ship Terminal ", Color.magenta);
            ProcessObjects<SteamValveHazard>((_, _) => "Steam Valve ", Color.yellow);
            ProcessPlayers();

            if (itemEsp) ProcessObjects<GrabbableObject>((grabbableObject, _) => grabbableObject.itemProperties.itemName + " ", Color.blue);
            if (enemyEsp) ProcessEnemies();
        }
        if (StartOfRound.Instance is not null && infCharge && GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer is not null && GameNetworkManager.Instance.localPlayerController.IsServer)
            GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer.insertedBattery.charge = 1;
    }

    [DllImport("user32")] [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static extern short GetAsyncKeyState(int vKey);

    float lastToggleTime;
    void Update()
    {
        var keyDown = (GetAsyncKeyState(45) & 0x8000) > 0;
        if (keyDown && !insertKeyWasPressed && Time.time - lastToggleTime > .5f)
        {
            isMenuOpen = !isMenuOpen;
            lastToggleTime = Time.time;
        }
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