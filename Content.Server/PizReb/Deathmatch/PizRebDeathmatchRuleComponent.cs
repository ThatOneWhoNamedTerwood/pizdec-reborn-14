using System.Numerics;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.PizReb.Deathmatch;

[RegisterComponent, Access(typeof(PizRebDeathmatchRuleSystem))]
public sealed partial class PizRebDeathmatchRuleComponent : Component
{
    [DataField]
    public ProtoId<JobPrototype> FighterJob = "PizRebFighter";

    [DataField]
    public ProtoId<TagPrototype> WeaponTag = "DeathMatchWeapons";

    [DataField]
    public Dictionary<EntProtoId, List<EntProtoId>> WeaponExtras = new();

    [DataField]
    public SoundSpecifier RespawnSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public bool KillfeedEnabled = true;

    [DataField]
    public Vector2 KillfeedAnchor = new(0.015f, 0.62f);

    [DataField]
    public TimeSpan KillfeedLifetime = TimeSpan.FromSeconds(4);

    [DataField]
    public int KillfeedMaxEntries = 5;

    [DataField]
    public bool HealthPercentEnabled = true;

    [DataField]
    public TimeSpan RespawnDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float HealthTextOffset = 0.3f;

    [DataField]
    public string KillCalloutPrefix = "death-match-kill-callout-";

    [DataField]
    public int KillCalloutAmount = 61;

    [DataField]
    public string SelfKillCalloutPrefix = "death-match-kill-callout-env-";

    [DataField]
    public int SelfKillCalloutAmount = 11;

    [DataField]
    public List<KillStreakAnnouncement> KillStreaks = new();
}

[DataDefinition]
public sealed partial class KillStreakAnnouncement
{
    [DataField(required: true)]
    public int Kills;

    [DataField]
    public List<string> Messages = new();
}
