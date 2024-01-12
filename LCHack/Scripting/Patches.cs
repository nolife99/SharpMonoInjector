using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LCHack.Scripting;

[HarmonyPatch(typeof(HUDManager))]
static class HUDManagerPatch
{
    [HarmonyPatch("AssignNewNodes")]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var ins in instructions) if (ins.opcode == OpCodes.Ldc_R4 && (float)ins.operand == 80) ins.operand = Hacks.farScan ? float.MaxValue : 80;
        return instructions;
    }

    [HarmonyPatch("MeetsScanNodeRequirements", [typeof(ScanNodeProperties), typeof(PlayerControllerB)])]
    static bool Prefix(ref bool __result)
    {
        if (!Hacks.farScan) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
static class PlayerPatch
{
    [HarmonyPatch("DamagePlayer")]
    static bool Prefix(PlayerControllerB __instance) => __instance.actualClientId != GameNetworkManager.Instance.localPlayerController.actualClientId || !Hacks.godMode;

    [HarmonyPatch("LateUpdate")]
    static void Postfix(PlayerControllerB __instance)
    {
        if (!Hacks.infSprint || GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId) return;
        __instance.sprintMeter = 1;
        if (__instance.sprintMeterUI is not null) __instance.sprintMeterUI.fillAmount = 1;
    }

    [HarmonyPatch("KillPlayerClientRpc")]
    static bool Prefix(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance) => Display(ref playerId, ref causeOfDeath, __instance);

    [HarmonyPrefix] [HarmonyPatch("KillPlayerServerRpc")]
    static bool _(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance) => Display(ref playerId, ref causeOfDeath, __instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Display(ref readonly int id, ref readonly int causeOfDeath, PlayerControllerB p)
    {
        HUDManager.Instance.DisplayTip("Player dead", $"{p.playersManager.allPlayerObjects[id].GetComponent<PlayerControllerB>().playerUsername} has died. Cause of death: {(CauseOfDeath)causeOfDeath}");
        return true;
    }
}
[HarmonyPatch(typeof(TimeOfDay))]
static class TimePatch
{
    [HarmonyPatch("SetInsideLightingDimness")]
    static void Postfix()
    {
        if (Hacks.clock) HUDManager.Instance.SetClockVisible(true);
    }
}

[HarmonyPatch(typeof(GrabbableObject))]
static class ItemsPatch
{
    [HarmonyPatch("SyncBatteryServerRpc")]
    static bool Prefix(GrabbableObject __instance, ref int charge)
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
    [HarmonyPatch("SyncScrapValuesClientRpc")]
    static bool Prefix(NetworkObjectReference[] spawnedScrap, ref int[] allScrapValue)
    {
        if (Hacks.excScrap > 0 && spawnedScrap is not null) for (var i = 0; i < spawnedScrap.Length; ++i) if (spawnedScrap[i].TryGet(out var net, null)) if (net.GetComponent<GrabbableObject>() is not null) allScrapValue[i] += (int)Math.Round(Hacks.excScrap * .5f);
        return true;
    }
}