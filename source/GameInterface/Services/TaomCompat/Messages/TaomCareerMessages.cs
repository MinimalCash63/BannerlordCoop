using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TaomCompat.Messages;

/// <summary>
/// One hero's TAOM career, in the same comma-joined form TAOM writes to the save.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct TaomCareerSnapshot
{
    [ProtoMember(1)] public readonly string HeroStringId;
    [ProtoMember(2)] public readonly string CareerId;
    [ProtoMember(3)] public readonly string Choices;
    [ProtoMember(4)] public readonly string Tiers;
    [ProtoMember(5)] public readonly string Flags;

    public TaomCareerSnapshot(string heroStringId, string careerId, string choices, string tiers, string flags)
    {
        HeroStringId = heroStringId;
        CareerId = careerId;
        Choices = choices;
        Tiers = tiers;
        Flags = flags;
    }
}

/// <summary>Client to server: the career of the hero this client plays.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTaomCareerChanged : ICommand
{
    [ProtoMember(1)] public readonly TaomCareerSnapshot Career;

    public NetworkTaomCareerChanged(TaomCareerSnapshot career)
    {
        Career = career;
    }
}

/// <summary>Server to clients: a hero's career as the server holds it.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTaomCareerUpdated : IEvent
{
    [ProtoMember(1)] public readonly TaomCareerSnapshot Career;

    public NetworkTaomCareerUpdated(TaomCareerSnapshot career)
    {
        Career = career;
    }
}

/// <summary>Local: TAOM changed a hero's career on this machine.</summary>
public readonly struct TaomCareerDataChanged : IEvent
{
    public readonly string HeroStringId;

    public TaomCareerDataChanged(string heroStringId)
    {
        HeroStringId = heroStringId;
    }
}
