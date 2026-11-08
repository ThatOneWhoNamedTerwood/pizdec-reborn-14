using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.PizReb.Cyberware;

[RegisterComponent]
public sealed partial class CyberImplantComponent : Component
{
    [DataField]
    public CyberSlot Slot = CyberSlot.ArmLeft;

    [DataField]
    public int BrainLoad = 4;

    [DataField]
    public TimeSpan InstallTime = TimeSpan.FromSeconds(30);

    [DataField]
    public bool ReplacesLimb;

    [DataField]
    public string? LayerState;

    [DataField]
    public Color? LayerColor;

    [DataField]
    public int Tier = 1;

    [DataField]
    public float HeatPerUse;

    [DataField]
    public float BodyHeatPerUse;

    [DataField]
    public float MeleeDamageBonus;

    [DataField]
    public float SpeedMultiplier = 1f;

    [DataField]
    public bool PushOnHit;

    [DataField]
    public float PushStrength = 6f;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public float ArmorPercent;

    [DataField]
    public float SpreadMultiplier = 1f;
}


