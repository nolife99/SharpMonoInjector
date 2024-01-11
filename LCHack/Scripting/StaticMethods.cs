using System;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

namespace LCHack.Scripting;

internal partial class Hacks
{
    static void CacheObjects<T>() => cache[typeof(T)] = FindObjectsByType(typeof(T), FindObjectsSortMode.None);
    static void ProcessObjects<T>(Func<T, Vector3, string> labelBuilder, Color labelColor) where T : Component
    {
        if (cache.TryGetValue(typeof(T), out var source)) foreach (T obj in source)
        {
            if (obj is GrabbableObject g && (g.isPocketed || g.isHeld || g.itemProperties.itemName is "clipboard" or "Sticky note") ||
                obj is SteamValveHazard v && !v.triggerScript.interactable) continue;

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
    static void ProcessEnemies()
    {
        if (cache.TryGetValue(typeof(EnemyAI), out var source))
        {
            enemyCount = source.Length;
            foreach (EnemyAI e in source) if (WorldToScreen(e.transform.position, out var screen))
                DrawLabel(screen, !string.IsNullOrWhiteSpace(e.enemyType.enemyName) ? e.enemyType.enemyName + " " : "Unknown Enemy ", Color.red, e.transform.position);
        }
        else enemyCount = 0;
    }
    static IEnumerator CacheRefreshRoutine()
    {
        while (true)
        {
            cache.Clear();
            CacheObjects<Terminal>();
            CacheObjects<EntranceTeleport>();
            CacheObjects<PlayerControllerB>();
            CacheObjects<SteamValveHazard>();
            CacheObjects<Landmine>();
            CacheObjects<Turret>();
            CacheObjects<EnemyAI>();
            CacheObjects<GrabbableObject>();

            yield return new WaitForSeconds(4);
        }
    }
}