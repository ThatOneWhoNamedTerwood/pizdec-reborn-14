using Robust.Shared.GameStates;

namespace Content.Shared.PizReb.Sector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PizRebStormRingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 30f;
}
