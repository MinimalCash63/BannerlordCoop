using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace GameInterface.Services.TaomCompat.Patches;

/// <summary>
/// TAOM skips its themed MapBar and field camp overlay whenever any co-op module is enabled, a gate
/// written for BannerlordTogether. Co-op patches the map bar view models, not the prefab, so the
/// themed movies work here; this turns that one CoopPresence read off.
/// </summary>
[HarmonyPatch]
internal class TaomThemedMapBarGatePatch
{
    private static bool Prepare() => TaomTypes.LoadMoviePatchPrefix != null && TaomTypes.CoopPresenceIsActive != null;

    private static MethodBase TargetMethod() => TaomTypes.LoadMoviePatchPrefix;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(typeof(TaomThemedMapBarGatePatch), nameof(CoopGateForThemedMovies));
        foreach (var instruction in instructions)
        {
            // Rewritten in place so labels and exception blocks stay on the instruction.
            if (instruction.Calls(TaomTypes.CoopPresenceIsActive)) instruction.operand = replacement;
            yield return instruction;
        }
    }

    public static bool CoopGateForThemedMovies() => false;
}

/// <summary>
/// The themed time buttons call ExecuteFastForward and ExecuteExtraFastForward, which only TAOM's
/// TimeAccelerationMixin provides, and TAOM does not register that mixin under co-op. Forward both to
/// the vanilla time change, which Co-op already routes through the server.
/// </summary>
[HarmonyPatch(typeof(ViewModel), nameof(ViewModel.ExecuteCommand))]
internal class TaomThemedTimeButtonsPatch
{
    private static bool Prepare() => TaomTypes.LoadMoviePatchPrefix != null;

    private static bool Prefix(ViewModel __instance, string commandName)
    {
        if (commandName != "ExecuteFastForward" && commandName != "ExecuteExtraFastForward") return true;
        if (__instance is not MapTimeControlVM timeControl) return true;
        if (AccessTools.Method(__instance.GetType(), commandName) != null) return true;

        timeControl.ExecuteTimeControlChange(2);
        return false;
    }
}
