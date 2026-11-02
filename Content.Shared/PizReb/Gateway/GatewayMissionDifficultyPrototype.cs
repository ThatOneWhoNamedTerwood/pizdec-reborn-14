using Robust.Shared.Prototypes;

namespace Content.Shared.PizReb.Gateway;

[Prototype]
public sealed partial class GatewayMissionDifficultyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Color = Color.White;
}
