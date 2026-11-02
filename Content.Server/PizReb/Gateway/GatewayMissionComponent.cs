using Content.Shared.PizReb.Gateway;
using Robust.Shared.Prototypes;

namespace Content.Server.PizReb.Gateway;

[RegisterComponent]
public sealed partial class GatewayMissionComponent : Component
{
    public ProtoId<GatewayMissionPrototype> Mission;
    public EntityUid Gateway;
    public EntityUid? ReturnPortal;
    public EntityUid? Objective;
    public bool ObjectiveDone;
    public bool RewardGiven;
    public bool Entered;
    public TimeSpan IdleSince;
    public List<EntityUid> SpawnPoints = new();
}
