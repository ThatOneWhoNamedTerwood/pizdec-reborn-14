using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.PizReb.Boss;

[Prototype]
public sealed partial class BossPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public SpriteSpecifier Icon;

    [DataField]
    public float DisplayRange = 35f;

    [DataField]
    public List<BossPhaseDefinition> Phases = new();
}

[DataDefinition]
public sealed partial class BossPhaseDefinition
{
    [DataField]
    public float Threshold = 1f;

    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public string? SpriteState;

    [DataField]
    public float SpeedMultiplier = 1f;

    [DataField]
    public float DamageTakenMultiplier = 1f;

    [DataField]
    public TimeSpan TransitionDuration;

    [DataField]
    public string? TransitionSpriteState;

    [DataField]
    public SoundSpecifier? TransitionSound;
}
