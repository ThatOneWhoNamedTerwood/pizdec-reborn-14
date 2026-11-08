using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.PizReb.Boss;

public enum HbmAbility : byte
{
    None,
    Darkheart,
    Twilight,
    Holoparasite,
    Vampirism,
    Shotgun,
    GrandValley,
    StrongLeft,
}

[RegisterComponent]
public sealed partial class TenHourBurstManComponent : Component
{
    [DataField]
    public float AggroRange = 40f;

    [DataField]
    public TimeSpan AbilityGap = TimeSpan.FromSeconds(1.5f);

    [DataField]
    public float DashHitRange = 1.4f;

    [DataField]
    public TimeSpan TargetLag = TimeSpan.FromSeconds(0.15);

    [DataField]
    public TimeSpan DarkheartCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public float DarkheartRange = 3f;

    [DataField]
    public int DarkheartDashes = 3;

    [DataField]
    public TimeSpan DarkheartGap = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float DarkheartDamage = 25f;

    [DataField]
    public float DarkheartHeal = 75f;

    [DataField]
    public float DarkheartSpeed = 16f;

    [DataField]
    public SoundSpecifier? DarkheartSound;

    [DataField]
    public TimeSpan TwilightCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public float TwilightRange = 4.5f;

    [DataField]
    public TimeSpan TwilightDelay = TimeSpan.FromSeconds(1.1);

    [DataField]
    public float TwilightRadius = 3.5f;

    [DataField]
    public float TwilightDamage = 45f;

    [DataField]
    public TimeSpan TwilightStun = TimeSpan.FromSeconds(2);

    [DataField]
    public float TwilightSlow = 0.85f;

    [DataField]
    public EntProtoId TwilightEffect = "BossTwilightWave";

    [DataField]
    public SoundSpecifier? TwilightSound;

    [DataField]
    public TimeSpan HoloparasiteCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public float HoloparasiteRange = 5f;

    [DataField]
    public TimeSpan HoloparasiteDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public float HoloparasiteSlow = 0.9f;

    [DataField]
    public EntProtoId HoloparasiteProto = "MobBossHoloparasite";

    [DataField]
    public SoundSpecifier? HoloparasiteSound;

    [DataField]
    public bool VampirismEnabled;

    [DataField]
    public TimeSpan VampirismCooldown = TimeSpan.FromSeconds(14);

    [DataField]
    public float VampirismRange = 3f;

    [DataField]
    public TimeSpan VampirismDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public int VampirismDashes = 2;

    [DataField]
    public TimeSpan VampirismGap = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan VampirismMissTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public float VampirismSpeed = 16f;

    [DataField]
    public SoundSpecifier? VampirismSound;

    [DataField]
    public TimeSpan ShotgunCooldown = TimeSpan.FromSeconds(9);

    [DataField]
    public float ShotgunMaxRange = 22f;

    [DataField]
    public float ShotgunMinRange = 3.5f;

    [DataField]
    public int ShotgunShots = 4;

    [DataField]
    public TimeSpan ShotgunGap = TimeSpan.FromSeconds(0.5);

    [DataField]
    public int ShotgunPellets = 6;

    [DataField]
    public float ShotgunSpread = 7f;

    [DataField]
    public float ShotgunSlow = 0.25f;

    [DataField]
    public EntProtoId ShotgunProjectile = "PelletShotgun";

    [DataField]
    public float ShotgunProjectileSpeed = 25f;

    [DataField]
    public SoundSpecifier? ShotgunSound;

    [DataField]
    public SoundSpecifier? ShotgunEquipSound;

    public DoAfterId? TwilightDoAfter;

    [DataField]
    public TimeSpan GrandValleyCooldown = TimeSpan.FromSeconds(9);

    [DataField]
    public float GrandValleyMaxRange = 22f;

    [DataField]
    public float GrandValleyMinRange = 3.5f;

    [DataField]
    public int GrandValleyShots = 4;

    [DataField]
    public float GrandValleySlow = 0.5f;

    [DataField]
    public TimeSpan GrandValleyGap = TimeSpan.FromSeconds(0.25);




    [DataField]
    public EntProtoId GrandValleyProjectile = "BossArcaneBarrage";

    [DataField]
    public float GrandValleyProjectileSpeed = 22f;

    [DataField]
    public SoundSpecifier? GrandValleySound;

    [DataField]
    public TimeSpan StrongLeftCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public float StrongLeftRange = 1.6f;

    [DataField]
    public float StrongLeftDamage = 45f;

    [DataField]
    public TimeSpan StrongLeftStun = TimeSpan.FromSeconds(1.2);

    [DataField]
    public float StrongLeftPush = 9f;

    [DataField]
    public TimeSpan StrongLeftChase = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? StrongLeftSound;

    [DataField]
    public SoundSpecifier? DeathSound;

    public HbmAbility Ability = HbmAbility.None;
    public int Step;
    public TimeSpan StepTime;
    public TimeSpan NextDecision;
    public EntityUid? Target;
    public int DashesLeft;
    public int ShotsLeft;
    public bool DashHit;
    public bool AbilityHit;
    public EntityUid? Parasite;
    public float StoredWalk = 1f;
    public float StoredSprint = 1f;
    public readonly Dictionary<HbmAbility, TimeSpan> Cooldowns = new();
    public readonly List<(TimeSpan Time, Vector2 Position)> History = new();
}





