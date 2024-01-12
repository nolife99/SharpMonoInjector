using System;
using System.Collections;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace LCHack.Scripting;

internal partial class Hacks
{
    static void CacheObjects(Type type) => cache[type] = FindObjectsByType(type, FindObjectsSortMode.None);
    static void ProcessObjects<T>(Func<T, Vector3, string> labelBuilder, Color labelColor) where T : Component => CastAndIterate<T>(obj =>
    {
        if (obj is GrabbableObject g && (g.isPocketed || g.isHeld || g.itemProperties.itemName is "clipboard" or "Sticky note") ||
            obj is SteamValveHazard v && !v.triggerScript.interactable) return;

        if (obj is Terminal t && addMoneySignal)
        {
            t.groupCredits += addMoney;
            if (!client.IsServer) t.SyncGroupCreditsServerRpc(t.groupCredits, t.numberOfItemsInDropship);
            addMoneySignal = false;
        }
        if (WorldToScreen(obj.transform.position, out var screen)) DrawLabel(in screen, labelBuilder(obj, screen), in labelColor, obj.transform.position);
    });
    static void ProcessPlayers() => CastAndIterate<PlayerControllerB>(pl =>
    {
        if (!pl.isPlayerDead && !pl.IsLocalPlayer && pl.isPlayerControlled && WorldToScreen(pl.transform.position, out var screen))
            DrawLabel(in screen, pl.playerUsername + " ", Color.green, pl.transform.position);
    });
    static void ProcessEnemies() => enemyCount = CastAndIterate<EnemyAI>(e =>
    {
        if (!e.isEnemyDead && WorldToScreen(e.transform.position, out var screen))
            DrawLabel(in screen, !string.IsNullOrWhiteSpace(e.enemyType.enemyName) ? e.enemyType.enemyName + " " : "Unknown Enemy ", Color.red, e.transform.position);
    });
    static IEnumerator CacheRefreshRoutine()
    {
        while (true)
        {
            CacheObjects(typeof(Terminal));
            CacheObjects(typeof(EntranceTeleport));
            CacheObjects(typeof(PlayerControllerB));
            CacheObjects(typeof(SteamValveHazard));
            CacheObjects(typeof(Landmine));
            CacheObjects(typeof(Turret));
            CacheObjects(typeof(EnemyAI));
            CacheObjects(typeof(GrabbableObject));

            yield return new WaitForSeconds(4);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int CastAndIterate<T>(Action<T> action)
    {
        if (!cache.TryGetValue(typeof(T), out var array)) return 0;
        ref readonly var actual = ref UnsafeUtility.As<Array, T[]>(ref array);

        var length = actual.Length;
        for (var i = 0; i < length; ++i) action(actual[i]);
        return length;
    }
}