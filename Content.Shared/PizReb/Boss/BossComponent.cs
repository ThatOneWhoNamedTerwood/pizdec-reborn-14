using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BossPrototype> Boss = default!;

    [AutoNetworkedField]
    public int Phase;

    [AutoNetworkedField]
    public bool Active;

    [AutoNetworkedField]
    public float Health;

    [AutoNetworkedField]
    public float MaxHealth;

    [ViewVariables]
    public bool Transitioning;

    [ViewVariables]
    public int PendingPhase;

    [ViewVariables]
    public TimeSpan TransitionEnd;

    [ViewVariables]
    public float BaseWalkSpeed;

    [ViewVariables]
    public float BaseSprintSpeed;

    [ViewVariables]
    public float DamageTakenMultiplier = 1f;

    [ViewVariables]
    public readonly Dictionary<NetUserId, EntityUid> MusicStreams = new();

    [ViewVariables]
    public int MusicPhase = -1;
}

[Serializable, NetSerializable]
public enum BossVisuals : byte
{
    Phase,
}

[Serializable, NetSerializable]
public enum TenHourBurstManVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum TenHourBurstManVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum TenHourBurstManVisualState : byte
{
    Default,
    Darkheart,
    Twilight,
    Shotgun,
    PeaceForAll,
    Drink,
}

public sealed class BossPhaseTransitionStartedEvent : EntityEventArgs
{
    public int Phase;
}

public sealed class BossPhaseTransitionFinishedEvent : EntityEventArgs
{
    public int Phase;
}
