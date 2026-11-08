using Content.Shared.Damage;
using Content.Shared.PizReb.Cyberware;
using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared.PizReb.Cyberware;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CyberwareComponent : Component
{
    [AutoNetworkedField]
    public int Capacity = 35;

    [AutoNetworkedField]
    public int BrainLoad;

    [AutoNetworkedField]
    public int Heat;

    [AutoNetworkedField]
    public List<CyberwareVisualOverride> Visuals = new();

    [ViewVariables]
    public readonly List<EntityUid> Implants = new();

    [ViewVariables]
    public readonly Dictionary<EntityUid, List<EntityUid>> ActionEntities = new();

    [ViewVariables]
    public readonly Dictionary<CyberSlot, List<EntityUid>> Slots = new();

    [ViewVariables]
    public readonly Dictionary<EntityUid, CyberSlot> AssignedSlots = new();

    [ViewVariables]
    public float MeleeDamageBonus;

    [ViewVariables]
    public float SpeedMultiplier = 1f;

    [ViewVariables]
    public float SpreadMultiplier = 1f;

    [ViewVariables]
    public bool PushOnHit;

    [ViewVariables]
    public float StaminaDamage;

    [ViewVariables]
    public float ArmorPercent;

    [ViewVariables]
    public float PushStrength = 6f;

    [ViewVariables]
    public bool CapacityRolled;

    [ViewVariables]
    public NetUserId? Owner;

    [ViewVariables]
    public TimeSpan NextHeatTick;

    [ViewVariables]
    public float OriginalWalk;

    [ViewVariables]
    public float OriginalSprint;

    [ViewVariables]
    public bool OriginalSpeedsStored;

    [ViewVariables]
    public DamageSpecifier? OriginalMelee;
}




