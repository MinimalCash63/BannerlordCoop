using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace GameInterface.Services.TaomCompat;

/// <summary>
/// TAOM types and members, looked up by name because Co-op does not reference TAOM. Every member is
/// null when TAOM is not loaded, and the TAOM patches turn themselves off through Prepare.
/// </summary>
internal static class TaomTypes
{
    public static readonly Type LoadMoviePatch = AccessTools.TypeByName("TAOM.Features.FactionUI.Hooks.GauntletLayerLoadMoviePatch");
    public static readonly MethodInfo LoadMoviePatchPrefix = LoadMoviePatch == null ? null : AccessTools.Method(LoadMoviePatch, "Prefix");

    public static readonly Type CoopPresence = AccessTools.TypeByName("TAOM.Dependencies.Foundation.CoopPresence");
    public static readonly MethodInfo CoopPresenceIsActive = CoopPresence == null ? null : AccessTools.PropertyGetter(CoopPresence, "IsActive");

    public static readonly Type BannerInjectionBehavior = AccessTools.TypeByName("TAOM.Features.BannerInjection.BannerInjectionBehavior");

    public static readonly Type PlayerPossessionBehavior = AccessTools.TypeByName("TAOM.Features.PlayerPossession.PlayerPossessionBehavior");
    public static readonly MethodInfo CheckForPossession = PlayerPossessionBehavior == null ? null : AccessTools.Method(PlayerPossessionBehavior, "CheckForPossession", Type.EmptyTypes);

    // Parameter types are given so a later TAOM overload cannot throw here, which would fail Co-op's PatchAll.
    public static readonly Type CareerDataService = AccessTools.TypeByName("TAOM.Features.CareerSystem.CareerDataService");
    public static readonly MethodInfo GetOrCreateData = CareerDataService == null ? null : AccessTools.Method(CareerDataService, "GetOrCreateData", new[] { typeof(string) });
    public static readonly MethodInfo HasCareer = CareerDataService == null ? null : AccessTools.Method(CareerDataService, "HasCareer", new[] { typeof(string) });

    public static readonly Type CareerCampaignBehavior = AccessTools.TypeByName("TAOM.Features.CareerSystem.CareerCampaignBehavior");
    public static readonly FieldInfo CareerLifecycleField = CareerCampaignBehavior == null ? null : AccessTools.Field(CareerCampaignBehavior, "_lifecycle");

    public static readonly Type CareerLifecycleService = AccessTools.TypeByName("TAOM.Features.CareerSystem.CareerLifecycleService");
    public static readonly MethodInfo AssignFallbackCareerIfMissing = CareerLifecycleService == null ? null : AccessTools.Method(CareerLifecycleService, "AssignFallbackCareerIfMissing", new[] { typeof(string), typeof(string) });
    public static readonly MethodInfo RepairForeignChoices = CareerLifecycleService == null ? null : AccessTools.Method(CareerLifecycleService, "RepairForeignChoices", new[] { typeof(string) });
    public static readonly MethodInfo MigrateAllTreeVersions = CareerLifecycleService == null ? null : AccessTools.Method(CareerLifecycleService, "MigrateAllTreeVersions", new[] { typeof(string) });

    public static readonly Type CareerPassiveService = AccessTools.TypeByName("TAOM.Features.CareerSystem.CareerPassiveService");
    public static readonly MethodInfo RefreshCache = CareerPassiveService == null ? null : AccessTools.GetDeclaredMethods(CareerPassiveService)
        .FirstOrDefault(method => method.Name == "RefreshCache" && method.GetParameters().Length == 2);

    public static readonly Type HeroCareerData = AccessTools.TypeByName("TAOM.Features.CareerSystem.Domain.HeroCareerData");
    public static readonly PropertyInfo CareerStringId = HeroCareerData == null ? null : AccessTools.Property(HeroCareerData, "CareerStringId");
    public static readonly PropertyInfo ChoiceIds = HeroCareerData == null ? null : AccessTools.Property(HeroCareerData, "ChoiceIds");
    public static readonly PropertyInfo TierUnlocks = HeroCareerData == null ? null : AccessTools.Property(HeroCareerData, "TierUnlocks");
    public static readonly PropertyInfo Flags = HeroCareerData == null ? null : AccessTools.Property(HeroCareerData, "Flags");

    public static bool HasCareerSystem =>
        GetOrCreateData != null && HasCareer != null && RefreshCache != null &&
        CareerStringId != null && ChoiceIds != null && TierUnlocks != null && Flags != null;
}
