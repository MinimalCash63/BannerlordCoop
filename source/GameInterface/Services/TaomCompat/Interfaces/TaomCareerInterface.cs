using Common.Logging;
using GameInterface.Services.TaomCompat.Messages;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.TaomCompat.Interfaces;

/// <summary>
/// Reads and writes TAOM career data through the TAOM instances captured at session launch.
/// </summary>
public interface ITaomCareerInterface : IGameAbstraction
{
    /// <summary>True while a career received over the network is written, so it is not sent back.</summary>
    bool IsApplyingRemoteCareer { get; }

    void Capture(object passiveService, object dataService, object registry);

    void CaptureDataService(object dataService);

    bool HasCareer(string heroStringId);

    bool TryGetSnapshot(string heroStringId, out TaomCareerSnapshot snapshot);

    bool TryApply(TaomCareerSnapshot snapshot);

    /// <summary>Runs TAOM's own check that re-applies a joining player's character-creation career.</summary>
    void RunPossessionCheck();

    /// <summary>
    /// Runs TAOM's load-time career steps for <paramref name="hero"/>: the culture fallback career when it
    /// has none, then the foreign-choice repair and the tree-version migration. TAOM runs these for the
    /// main hero at load, which on a co-op client is the save's hero, not the player's own.
    /// </summary>
    void RunLoadTimeCareerSteps(Hero hero);
}

/// <inheritdoc cref="ITaomCareerInterface"/>
internal class TaomCareerInterface : ITaomCareerInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TaomCareerInterface>();

    private object passiveService;
    private object dataService;
    private object registry;

    public bool IsApplyingRemoteCareer { get; private set; }

    public void Capture(object passiveService, object dataService, object registry)
    {
        this.passiveService = passiveService;
        this.dataService = dataService;
        this.registry = registry;
    }

    public void CaptureDataService(object dataService)
    {
        this.dataService ??= dataService;
    }

    public bool HasCareer(string heroStringId)
    {
        if (dataService == null || string.IsNullOrEmpty(heroStringId)) return false;

        return (bool)TaomTypes.HasCareer.Invoke(dataService, new object[] { heroStringId });
    }

    public bool TryGetSnapshot(string heroStringId, out TaomCareerSnapshot snapshot)
    {
        snapshot = default;
        if (dataService == null || string.IsNullOrEmpty(heroStringId)) return false;

        var data = TaomTypes.GetOrCreateData.Invoke(dataService, new object[] { heroStringId });
        if (data == null) return false;

        snapshot = new TaomCareerSnapshot(
            heroStringId,
            (string)TaomTypes.CareerStringId.GetValue(data),
            Join(TaomTypes.ChoiceIds.GetValue(data)),
            Join(TaomTypes.TierUnlocks.GetValue(data)),
            Join(TaomTypes.Flags.GetValue(data)));
        return true;
    }

    public bool TryApply(TaomCareerSnapshot snapshot)
    {
        if (dataService == null)
        {
            Logger.Warning("[TaomCompat] Career for {HeroId} received before TAOM's career service was seen", snapshot.HeroStringId);
            return false;
        }
        if (string.IsNullOrEmpty(snapshot.HeroStringId)) return false;

        IsApplyingRemoteCareer = true;
        try
        {
            var data = TaomTypes.GetOrCreateData.Invoke(dataService, new object[] { snapshot.HeroStringId });
            TaomTypes.CareerStringId.SetValue(data, string.IsNullOrEmpty(snapshot.CareerId) ? null : snapshot.CareerId);
            Refill((IList)TaomTypes.ChoiceIds.GetValue(data), Split(snapshot.Choices));
            Refill((IList)TaomTypes.TierUnlocks.GetValue(data), Split(snapshot.Tiers)
                .Select(tier => int.TryParse(tier, out int value) ? (object)value : null)
                .Where(tier => tier != null)
                .ToArray());
            Refill((IList)TaomTypes.Flags.GetValue(data), Split(snapshot.Flags));

            if (passiveService != null && registry != null)
            {
                TaomTypes.RefreshCache.Invoke(passiveService, new[] { dataService, registry });
            }
        }
        finally
        {
            IsApplyingRemoteCareer = false;
        }

        return true;
    }

    public void RunPossessionCheck()
    {
        if (TaomTypes.CheckForPossession == null) return;

        var campaign = Campaign.Current;
        if (campaign == null) return;

        var behavior = campaign.GetCampaignBehaviors<CampaignBehaviorBase>()
            .FirstOrDefault(b => TaomTypes.PlayerPossessionBehavior.IsInstanceOfType(b));
        if (behavior == null) return;

        TaomTypes.CheckForPossession.Invoke(behavior, null);
    }

    public void RunLoadTimeCareerSteps(Hero hero)
    {
        if (hero == null || TaomTypes.CareerLifecycleField == null) return;

        var behavior = Campaign.Current?.GetCampaignBehaviors<CampaignBehaviorBase>()
            .FirstOrDefault(b => TaomTypes.CareerCampaignBehavior.IsInstanceOfType(b));
        var lifecycle = behavior == null ? null : TaomTypes.CareerLifecycleField.GetValue(behavior);
        if (lifecycle == null) return;

        string heroId = hero.StringId;
        if (TaomTypes.AssignFallbackCareerIfMissing != null &&
            (bool)TaomTypes.AssignFallbackCareerIfMissing.Invoke(lifecycle, new object[] { heroId, hero.Culture?.StringId }))
        {
            Logger.Information("[TaomCompat] {HeroId} had no career; TAOM gave it the starting career for culture {Culture}", heroId, hero.Culture?.StringId);
        }

        TaomTypes.RepairForeignChoices?.Invoke(lifecycle, new object[] { heroId });
        TaomTypes.MigrateAllTreeVersions?.Invoke(lifecycle, new object[] { heroId });

        if (passiveService != null && dataService != null && registry != null)
        {
            TaomTypes.RefreshCache.Invoke(passiveService, new[] { dataService, registry });
        }
    }

    private static string Join(object list)
    {
        if (list is not IEnumerable items) return string.Empty;

        return string.Join(",", items.Cast<object>().Select(item => item?.ToString()).Where(item => !string.IsNullOrEmpty(item)));
    }

    private static string[] Split(string joined)
    {
        if (string.IsNullOrEmpty(joined)) return Array.Empty<string>();

        return joined.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static void Refill(IList target, IEnumerable values)
    {
        if (target == null) return;

        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}
