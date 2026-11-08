using Robust.Shared.Prototypes;

namespace Content.Shared.PizReb.Cyberware;

[RegisterComponent]
public sealed partial class CyberActionGrantComponent : Component
{
    [DataField]
    public List<EntProtoId> Actions = new();

    [DataField]
    public List<EntProtoId> Weapons = new();
}
