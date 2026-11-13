using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.TaomCompat.Patches;

/// <summary>
/// Clan.PreAfterLoad repaints each kingdom banner from Kingdom.PrimaryBannerColor, which TAOM leaves at
/// vanilla values, and TAOM restores its banners afterwards only on the authority. The server's save
/// already holds TAOM's banners, so a client skips that repaint while it loads the save.
/// </summary>
[HarmonyPatch(typeof(Clan))]
internal class TaomBannerLoadPatches
{
    [ThreadStatic]
    private static int preAfterLoadDepth;

    private static bool Prepare() => TaomTypes.BannerInjectionBehavior != null;

    [HarmonyPatch(nameof(Clan.PreAfterLoad))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void EnterLoad() => preAfterLoadDepth++;

    // The finalizer also covers a throwing prefix that ran before EnterLoad, so never go below zero.
    [HarmonyPatch(nameof(Clan.PreAfterLoad))]
    [HarmonyFinalizer]
    private static Exception LeaveLoad(Exception __exception)
    {
        if (preAfterLoadDepth > 0) preAfterLoadDepth--;
        return __exception;
    }

    [HarmonyPatch(nameof(Clan.UpdateBannerColorsAccordingToKingdom))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool SkipRepaintWhileClientLoads()
    {
        if (preAfterLoadDepth <= 0) return true;
        if (ModInformation.IsServer) return true;

        // Patches stay installed after a session ends, so single player keeps the vanilla repaint.
        return ContainerProvider.TryGetContainer(out _) == false;
    }
}
