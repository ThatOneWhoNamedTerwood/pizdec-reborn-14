using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.PizReb.Gateway;

public enum GatewayMissionObjective : byte
{
    Kill,
    Retrieve,
    Activate,
}

[Prototype]
public sealed partial class GatewayMissionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Goal;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public ResPath MapPath;

    [DataField]
    public SpriteSpecifier? Preview;

    [DataField]
    public GatewayMissionObjective Objective = GatewayMissionObjective.Kill;

    [DataField(required: true)]
    public EntProtoId ObjectiveEntity;

    [DataField]
    public ProtoId<GatewayMissionDifficultyPrototype> Difficulty = "Moderate";

    [DataField]
    public List<EntProtoId> Rewards = new();
}
