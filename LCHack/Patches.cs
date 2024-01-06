using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace LCHack;

[HarmonyPatch]
static class AssignNewNodesPatch
{
    [HarmonyPatch(typeof(HUDManager), "AssignNewNodes")]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var ins in instructions) if (ins.opcode == OpCodes.Ldc_R4 && (float)ins.operand == 20) ins.operand = !Hacks.Instance.farScan ? 20f : 500f;
        return instructions;
    }
}

[HarmonyPatch]
static class DamagePlayerPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
    static bool Prefix(PlayerControllerB __instance) => __instance.actualClientId != GameNetworkManager.Instance.localPlayerController.actualClientId || !Hacks.Instance.godMode;
}

[HarmonyPatch]
static class KillPlayerClientRpcPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), "KillPlayerClientRpc")]
    static bool Prefix(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance)
    {
        HUDManager.Instance.DisplayTip("Player dead", __instance.playersManager.allPlayerObjects[playerId].GetComponent<PlayerControllerB>().playerUsername + " has died. Cause of death: " + ((CauseOfDeath)causeOfDeath).ToString(), false, false, "LC_Tip1");
        return true;
    }
}

[HarmonyPatch]
static class KillPlayerServerRpcPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), "KillPlayerServerRpc")]
    static bool Prefix(int playerId, bool spawnBody, Vector3 bodyVelocity, int causeOfDeath, int deathAnimation, PlayerControllerB __instance)
    {
        HUDManager.Instance.DisplayTip("Player dead", __instance.playersManager.allPlayerObjects[playerId].GetComponent<PlayerControllerB>().playerUsername + " has died. Cause of death: " + ((CauseOfDeath)causeOfDeath).ToString(), false, false, "LC_Tip1");
        return true;
    }
}

[HarmonyPatch]
static class LateUpdatePostfixPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
    static void Postfix(PlayerControllerB __instance)
    {
        if (!Hacks.Instance.infSprint || GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId) return;
        __instance.sprintMeter = 1;
        if (__instance.sprintMeterUI is not null) __instance.sprintMeterUI.fillAmount = 1;
    }
}

[HarmonyPatch]
static class MeetsScanNodeRequirementsPatch
{
    [HarmonyPatch(typeof(HUDManager), "MeetsScanNodeRequirements")]
    [HarmonyPatch(new Type[] { typeof(ScanNodeProperties), typeof(PlayerControllerB) })]
    static bool Prefix(ref bool __result)
    {
        if (!Hacks.Instance.farScan) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch]
static class SetInsideLightingDimnessPatch
{
    [HarmonyPatch(typeof(TimeOfDay), "SetInsideLightingDimness")]
    static void Postfix()
    {
        if (Hacks.Instance.clock) HUDManager.Instance.SetClockVisible(true);
    }
}

[HarmonyPatch]
static class SyncBatteryServerRpcPatch
{
    [HarmonyPatch(typeof(GrabbableObject), "SyncBatteryServerRpc")]
    static bool Prefix(GrabbableObject __instance, ref int charge)
    {
        if (Hacks.Instance.infCharge && __instance.itemProperties.requiresBattery)
        {
            __instance.insertedBattery.empty = false;
            __instance.insertedBattery.charge = 1;
            charge = 100;
        }
        return true;
    }
}

[HarmonyPatch]
static class SyncScrapValuesClientRpcPatch
{
    [HarmonyPatch(typeof(RoundManager), "SyncScrapValuesClientRpc")]
    static bool Prefix(NetworkObjectReference[] spawnedScrap, ref int[] allScrapValue)
    {
        if (!Hacks.Instance.highItemVal) return true;
        if (spawnedScrap is not null) for (var i = 0; i < spawnedScrap.Length; ++i) if (spawnedScrap[i].TryGet(out var net, null)) if (net.GetComponent<GrabbableObject>() is not null) allScrapValue[i] = 420;
        return true;
    }
}