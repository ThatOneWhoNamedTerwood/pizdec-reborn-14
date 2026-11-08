using Robust.Shared.GameStates;

namespace Content.Shared.PizReb.Cyberware;

[RegisterComponent]
public sealed partial class CyberAnklesActiveComponent : Component;

[RegisterComponent]
public sealed partial class CyberLegsJumpComponent : Component
{
    [DataField]
    public float Distance = 5f;

    [DataField]
    public bool Knockdown;
}
