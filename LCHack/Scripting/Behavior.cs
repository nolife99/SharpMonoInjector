using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LCHack.Scripting;

internal partial class Hacks : MonoBehaviour
{
    void Start()
    {
        try
        {
            if (!PatchProcessor.GetAllPatchedMethods().Any(f => PatchProcessor.GetPatchInfo(f).Prefixes.Any(t => t.owner == harmonyID)))
            {
                Harmony patcher = new(harmonyID);

                var modules = typeof(Hacks).Assembly.GetModules();
                for (var i = 0; i < modules.Length; ++i)
                {
                    var types = modules[i].GetTypes();
                    for (var j = 0; j < types.Length; ++j) new PatchClassProcessor(patcher, types[j]).Patch();
                }
            }
            setLevel = typeof(HUDManager).GetMethod("SetPlayerLevelSmoothly", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        StartCoroutine(CacheRefreshRoutine());
    }
    void OnGUI()
    {
        GUI.Label(new(10, 5, 200, 30), "Lethal Company Menu v1.3.7");
        if ((client = GameNetworkManager.Instance.localPlayerController) is not null) GUI.Label(new(10, 25, 200, 30), $"{enemyCount:n0} enem{(enemyCount == 1 ? "y" : "ies")}");

        if (isMenuOpen)
        {
            var rect = GUILayout.Window(short.MinValue, windowRect, _ =>
            {
                GUILayout.Label("Toggles");
                if (GUILayout.Button("Toggle invincibility (non insta-kill): " + (godMode ? on : off))) godMode = !godMode;
                if (GUILayout.Button("Toggle infinite sprint: " + (infSprint ? on : off))) infSprint = !infSprint;
                if (GUILayout.Button("Toggle all ESP: " + (esp ? on : off))) esp = !esp;
                if (GUILayout.Button("Toggle item ESP: " + (itemEsp ? on : off))) itemEsp = !itemEsp;
                if (GUILayout.Button("Toggle enemy ESP: " + (enemyEsp ? on : off))) enemyEsp = !enemyEsp;
                if (GUILayout.Button("Toggle distant scan: " + (farScan ? on : off))) farScan = !farScan;
                if (GUILayout.Button("Show clock: " + (clock ? on : off))) clock = !clock;

                GUILayout.Label("When non-host, drop the item and pick it back up for a full charge.");
                if (GUILayout.Button("Toggle infinite battery: " + (infCharge ? on : off))) infCharge = !infCharge;

                if (GUILayout.Button($"Add money: {addMoney:n0}")) addMoneySignal = true;
                moneyS = GUILayout.TextField(moneyS);
                if (float.TryParse(moneyS, out var add)) addMoney = (int)Mathf.Clamp(add, -20000000, 20000000);

                if (GUILayout.Button($"Add XP: {xpCount:n0}") && xpCount != 0) HUDManager.Instance?.StartCoroutine((IEnumerator)setLevel.Invoke(HUDManager.Instance, [xpCount]));
                xp = GUILayout.TextField(xp);
                if (float.TryParse(xp, out add)) xpCount = (int)Mathf.Clamp(add, -100000, 100000);

                GUILayout.Label("Host only features:");

                var t = TimeOfDay.Instance;
                if (GUILayout.Button("Set quota met") && t is not null)
                {
                    t.quotaFulfilled = t.profitQuota;
                    t.UpdateProfitQuotaCurrentTime();
                }

                GUILayout.Label($"Increase scrap value: {excScrap:n0}");
                scrapS = GUILayout.TextField(scrapS);
                if (float.TryParse(scrapS, out add)) excScrap = (int)Mathf.Clamp(add, -1000000, 1000000);

                GUI.DragWindow();
            }, "Lethal Company");
            if (rect.xMax < Screen.width && rect.yMax < Screen.height) windowRect = rect;
        }

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
        if (keyDown && !keyPress) isMenuOpen = !isMenuOpen;

        if (client is not null)
        {
            if (isMenuOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                lockedCursor = false;
            }
            else if (!lockedCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                lockedCursor = true;
            }
        }
        keyPress = keyDown;
    }
}