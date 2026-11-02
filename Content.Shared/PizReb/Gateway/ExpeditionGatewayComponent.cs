using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Gateway;

[RegisterComponent]
public sealed partial class ExpeditionGatewayComponent : Component
{
    [DataField]
    public TimeSpan OfferInterval = TimeSpan.FromMinutes(5);

    [DataField]
    public int OfferCount = 4;

    [DataField]
    public float IdlePowerLoad = 1f;

    [DataField]
    public float ActivePowerLoad = 10000f;

    [DataField]
    public TimeSpan EntryTimeout = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/gateway_off.ogg");

    public TimeSpan NextOffer;
    public TimeSpan NextReady;
    public List<ProtoId<GatewayMissionPrototype>> Offered = new();
    public EntityUid? Mission;
}

[Serializable, NetSerializable]
public enum ExpeditionGatewayVisuals : byte
{
    Active,
}

[Serializable, NetSerializable]
public enum ExpeditionGatewayLayers : byte
{
    Portal,
    Power,
}
