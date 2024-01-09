using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace LCHack;

[HarmonyPatch(typeof(HUDManager))]
static class HUDManagerPatch
{
    [HarmonyTranspiler] [HarmonyPatch("AssignNewNodes")]
    static IEnumerable<CodeInstruction> _(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var ins in instructions) if (ins.opcode == OpCodes.Ldc_R4 && (float)ins.operand == 80f) ins.operand = Hacks.farScan ? float.MaxValue : 80f;
        return instructions;
    }

    [HarmonyPrefix] [HarmonyPatch("MeetsScanNodeRequirements")] [HarmonyPatch(new Type[] { typeof(ScanNodeProperties), typeof(PlayerControllerB) })]
    static bool _(ref bool __result)
    {
        if (!Hacks.farScan) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
static class PlayerPatch
{
    [HarmonyPrefix] [HarmonyPatch("DamagePlayer")]
    static bool a(PlayerControllerB __instance) => __instance.actualClientId != GameNetworkManager.Instance.localPlayerController.actualClientId || !Hacks.godMode;

    [HarmonyPostfix] [HarmonyPatch("LateUpdate")]
    static void _(PlayerControllerB __instance)
    {
        if (!Hacks.infSprint || GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId) return;
        __instance.sprintMeter = 1;
        if (__instance.sprintMeterUI is not null) __instance.sprintMeterUI.fillAmount = 1;
    }

    [HarmonyPrefix] [HarmonyPatch("KillPlayerClientRpc")]
    static bool a(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance) => Display(playerId, causeOfDeath, __instance);

    [HarmonyPrefix] [HarmonyPatch("KillPlayerServerRpc")]
    static bool _(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance) => Display(playerId, causeOfDeath, __instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Display(int id, int causeOfDeath, PlayerControllerB player)
    {
        HUDManager.Instance.DisplayTip("Player dead", $"{player.playersManager.allPlayerObjects[id].GetComponent<PlayerControllerB>().playerUsername} has died. Cause of death: {(CauseOfDeath)causeOfDeath}");
        return true;
    }
}
[HarmonyPatch(typeof(TimeOfDay))]
static class TimePatch
{
    [HarmonyPostfix] [HarmonyPatch("SetInsideLightingDimness")]
    static void _()
    {
        if (Hacks.clock) HUDManager.Instance.SetClockVisible(true);
    }
}

[HarmonyPatch(typeof(GrabbableObject))]
static class ItemsPatch
{
    [HarmonyPrefix] [HarmonyPatch("SyncBatteryServerRpc")]
    static bool _(GrabbableObject __instance, ref int charge)
    {
        if (Hacks.infCharge && __instance.itemProperties.requiresBattery)
        {
            __instance.insertedBattery.empty = false;
            __instance.insertedBattery.charge = 1;
            charge = 100;
        }
        return true;
    }
}

[HarmonyPatch(typeof(RoundManager))]
static class RoundManagerPatch
{
    [HarmonyPrefix] [HarmonyPatch("SyncScrapValuesClientRpc")]
    static bool _(NetworkObjectReference[] spawnedScrap, ref int[] allScrapValue)
    {
        if (Hacks.excScrap <= 0) return true;
        if (spawnedScrap is not null) for (var i = 0; i < spawnedScrap.Length; ++i) if (spawnedScrap[i].TryGet(out var net, null)) if (net.GetComponent<GrabbableObject>() is not null) allScrapValue[i] += Hacks.excScrap / 2;
        return true;
    }
}