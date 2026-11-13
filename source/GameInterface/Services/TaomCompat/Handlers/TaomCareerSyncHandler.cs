using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Players;
using GameInterface.Services.TaomCompat.Interfaces;
using GameInterface.Services.TaomCompat.Messages;
using GameInterface.Services.UI.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.TaomCompat.Handlers;

/// <summary>
/// Keeps TAOM careers the same on every machine. TAOM stores careers only in the campaign save, which
/// only the server writes, so a career picked or changed on a client is sent to the server, stored there
/// (and so saved), and passed on to the other players.
/// </summary>
public class TaomCareerSyncHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TaomCareerSyncHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITaomCareerInterface careers;
    private readonly IPlayerManager playerManager;

    // A client only sends once it plays its own hero; before that it is in the join's throwaway campaign.
    private bool playingOwnHero;

    public TaomCareerSyncHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ITaomCareerInterface careers,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.careers = careers;
        this.playerManager = playerManager;

        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
        messageBroker.Subscribe<TaomCareerDataChanged>(Handle_CareerDataChanged);
        messageBroker.Subscribe<NetworkTaomCareerChanged>(Handle_NetworkCareerChanged);
        messageBroker.Subscribe<NetworkTaomCareerUpdated>(Handle_NetworkCareerUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
        messageBroker.Unsubscribe<TaomCareerDataChanged>(Handle_CareerDataChanged);
        messageBroker.Unsubscribe<NetworkTaomCareerChanged>(Handle_NetworkCareerChanged);
        messageBroker.Unsubscribe<NetworkTaomCareerUpdated>(Handle_NetworkCareerUpdated);
    }

    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> payload)
    {
        if (ModInformation.IsServer || TaomTypes.HasCareerSystem == false) return;

        GameThread.RunSafe(() =>
        {
            string heroId = Hero.MainHero?.StringId;
            careers.TryGetSnapshot(heroId, out var before);

            // TAOM only runs this on the hourly tick, and a client's clock can stay stopped for minutes.
            try
            {
                careers.RunPossessionCheck();
            }
            catch (Exception e)
            {
                Logger.Error(e, "[TaomCompat] Possession check failed for {HeroId}", heroId);
            }

            // A rejoin skips character creation, so possession has nothing to apply; TAOM's own load-time
            // fallback then gives a hero without a career the starting career of its culture.
            try
            {
                careers.RunLoadTimeCareerSteps(Hero.MainHero);
            }
            catch (Exception e)
            {
                Logger.Error(e, "[TaomCompat] Load-time career steps failed for {HeroId}", heroId);
            }
            playingOwnHero = true;

            Logger.Information("[TaomCompat] Playing hero {HeroId}, career present: {HasCareer}", heroId, careers.HasCareer(heroId));

            // Sent only when the check changed it, so a rejoin never echoes the save copy back over newer server data.
            if (careers.TryGetSnapshot(heroId, out var after) && before.Equals(after) == false) SendOwnCareer(heroId);
        }, context: "TAOM career on player switch");
    }

    private void Handle_CareerDataChanged(MessagePayload<TaomCareerDataChanged> payload)
    {
        string heroId = payload.What.HeroStringId;

        if (ModInformation.IsClient)
        {
            if (playingOwnHero == false) return;
            if (heroId != Hero.MainHero?.StringId) return;

            SendOwnCareer(heroId);
            return;
        }

        if (Campaign.Current == null) return;
        if (careers.TryGetSnapshot(heroId, out var snapshot) == false) return;

        network.SendAll(new NetworkTaomCareerUpdated(snapshot));
    }

    private void Handle_NetworkCareerChanged(MessagePayload<NetworkTaomCareerChanged> payload)
    {
        if (ModInformation.IsClient) return;
        if (payload.Who is not NetPeer peer) return;

        var career = payload.What.Career;

        GameThread.RunSafe(() =>
        {
            if (playerManager.TryGetSenderHero(peer, out var hero) == false || hero.StringId != career.HeroStringId)
            {
                Logger.Warning("[TaomCompat] Ignoring career for {HeroId} from a peer that does not play that hero", career.HeroStringId);
                return;
            }

            if (careers.TryApply(career) == false) return;

            Logger.Information("[TaomCompat] Stored career {CareerId} for {HeroId} ({Choices})", career.CareerId, career.HeroStringId, career.Choices);
            network.SendAllBut(peer, new NetworkTaomCareerUpdated(career));
        }, context: "TAOM career from client");
    }

    private void Handle_NetworkCareerUpdated(MessagePayload<NetworkTaomCareerUpdated> payload)
    {
        if (ModInformation.IsServer) return;

        var career = payload.What.Career;
        GameThread.RunSafe(() => careers.TryApply(career), context: "TAOM career from server");
    }

    private void SendOwnCareer(string heroId)
    {
        if (careers.TryGetSnapshot(heroId, out var snapshot) == false) return;

        network.SendAll(new NetworkTaomCareerChanged(snapshot));
    }
}
