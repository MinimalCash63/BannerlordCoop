using Common.Messaging;
using GameInterface.Services.TaomCompat.Interfaces;
using GameInterface.Services.TaomCompat.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.Services.TaomCompat.Patches;

/// <summary>
/// Announces every change TAOM makes to a hero's career on this machine, so it can reach the server
/// and from there the other players and the server's save.
/// </summary>
[HarmonyPatch]
internal class TaomCareerDataChangePatches
{
    private static readonly string[] Mutators = { "SetCareer", "TryAddChoice", "RemoveChoice", "UnlockTier", "SetFlag", "ClearCareer" };

    private static bool Prepare() => TaomTypes.CareerDataService != null && TaomTypes.HasCareerSystem;

    // Only methods whose first parameter is heroStringId, so a changed TAOM signature is skipped instead of failing PatchAll.
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(TaomTypes.CareerDataService)
            .Where(method => Mutators.Contains(method.Name) && method.GetParameters().FirstOrDefault()?.Name == "heroStringId");

    private static void Postfix(object __instance, string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId)) return;
        if (ContainerProvider.TryResolve<ITaomCareerInterface>(out var careers) == false) return;
        if (careers.IsApplyingRemoteCareer) return;

        careers.CaptureDataService(__instance);
        MessageBroker.Instance.Publish(__instance, new TaomCareerDataChanged(heroStringId));
    }
}

/// <summary>
/// TAOM refreshes its career passives at session launch on every machine; the instances it passes are
/// kept so a career received over the network can be applied and its passives refreshed.
/// </summary>
[HarmonyPatch]
internal class TaomCareerPassiveRefreshPatch
{
    private static bool Prepare() => TaomTypes.HasCareerSystem;

    private static MethodBase TargetMethod() => TaomTypes.RefreshCache;

    // Positional arguments (data service, registry) so a renamed TAOM parameter cannot fail patching.
    private static void Postfix(object __instance, object __0, object __1)
    {
        if (ContainerProvider.TryResolve<ITaomCareerInterface>(out var careers) == false) return;

        careers.Capture(__instance, __0, __1);
    }
}
