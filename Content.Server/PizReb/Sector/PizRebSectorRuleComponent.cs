using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.PizReb.Sector;

[RegisterComponent, Access(typeof(PizRebSectorRuleSystem))]
public sealed partial class PizRebSectorRuleComponent : Component
{
    [DataField]
    public EntProtoId EpicenterPrototype = "PizRebEpicenter";

    [DataField]
    public List<EntProtoId> StormRingPrototypes = new()
    {
        "PizRebStormRing1",
        "PizRebStormRing2",
        "PizRebStormRing3",
        "PizRebStormRing4",
        "PizRebStormRing5",
    };

    [DataField]
    public EntProtoId StormMotePrototype = "PizRebStormMote";

    [DataField]
    public EntProtoId StormStrikePrototype = "PizRebStormStrike";

    [DataField]
    public List<EntProtoId> MeteorPrototypes = new()
    {
        "MeteorSmall",
        "MeteorMedium",
        "MeteorLarge",
    };

    [DataField]
    public EntProtoId RodPrototype = "ImmovableRod";

    [DataField]
    public List<EntProtoId> PodBeacons = new()
    {
        "PizRebBeaconNanotrasen",
        "PizRebBeaconSyndicate",
        "PizRebBeaconGorlex",
        "PizRebBeaconPirate",
        "PizRebBeaconIndependent",
    };

    [DataField]
    public float PodDistance = 700f;

    [DataField]
    public int PodCapacity = 1;

    [DataField]
    public TimeSpan EpicenterOpenTime = TimeSpan.FromMinutes(90);

    [DataField]
    public Vector2 EpicenterPosition = new(500f, 0f);

    [DataField]
    public List<float> ZoneRadii = new() { 400f, 300f, 200f, 110f, 40f };

    [DataField]
    public float BandWidth = 14f;

    [DataField]
    public float RingRotationSpeed = 0.04f;

    [DataField]
    public float MeteorVelocity = 20f;

    [DataField]
    public float MoteInterval = 0.08f;

    [DataField]
    public List<float> LightningChancePerLayer = new() { 0.1f, 0.5f, 2f, 5f, 10f };

    [DataField]
    public List<float> MeteorChancePerLayer = new() { 0f, 0.3f, 1f, 2.5f, 5f };

    [DataField]
    public List<float> RodChancePerLayer = new() { 0f, 0f, 0.05f, 0.2f, 0.5f };

    [DataField]
    public List<SoundSpecifier> PhaseAmbience = new()
    {
        new SoundPathSpecifier("/Audio/Ambience/ambispace2.ogg"),
        new SoundPathSpecifier("/Audio/Ambience/spookyspace1.ogg"),
        new SoundPathSpecifier("/Audio/Ambience/spookyspace2.ogg"),
        new SoundPathSpecifier("/Audio/Ambience/spookyspace1.ogg"),
        new SoundPathSpecifier("/Audio/Ambience/engine_hum.ogg"),
    };

    [DataField]
    public float AmbienceVolume = -25f;

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/tesla_collapse.ogg");

    [DataField]
    public SoundSpecifier RingCrossSound = new SoundPathSpecifier("/Audio/Effects/tesla_collapse.ogg");
}
