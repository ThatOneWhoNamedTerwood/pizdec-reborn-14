using Content.Shared.PizReb.Cyberware;

namespace Content.Server.PizReb.Cyberware;

[RegisterComponent]
public sealed partial class AutoSurgeonComponent : Component
{
    [DataField]
    public float Durability = 100f;

    [DataField]
    public float WearMin = 3f;

    [DataField]
    public float WearMax = 6f;

    [DataField]
    public float DamageAbove = 50f;

    [DataField]
    public float FailAbove = 80f;

    [DataField]
    public TimeSpan SleepDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan PrepareTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan CloseTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan RevertTime = TimeSpan.FromSeconds(25);

    [DataField]
    public int MaxImplants = 15;

    [DataField]
    public int MaxJugs = 10;

    [ViewVariables]
    public EntityUid? Patient;

    [ViewVariables]
    public bool Operating;

    [ViewVariables]
    public AutoSurgeonStage Stage;

    [ViewVariables]
    public AutoSurgeonMode Mode;

    [ViewVariables]
    public NetEntity? SelectedImplant;

    [ViewVariables]
    public EntityUid? ReplaceTarget;

    [ViewVariables]
    public EntityUid? PreviousImplant;

    [ViewVariables]
    public CyberSlot PreviousSlot;

    [ViewVariables]
    public TimeSpan NextDamageCheck;

    [ViewVariables]
    public EntityUid? EquippedMask;

    [ViewVariables]
    public EntityUid? EquippedTank;

    [ViewVariables]
    public List<EntityUid> Stripped = new();

    [ViewVariables]
    public List<string> StrippedSlots = new();

    [ViewVariables]
    public bool Pending;

    [ViewVariables]
    public AutoSurgeonStage PendingStage;

    [ViewVariables]
    public TimeSpan PendingStart;

    [ViewVariables]
    public TimeSpan PendingDuration;

    [ViewVariables]
    public bool DoAfterRunning;

    [ViewVariables]
    public HashSet<EntityUid> Contents = new();
}




