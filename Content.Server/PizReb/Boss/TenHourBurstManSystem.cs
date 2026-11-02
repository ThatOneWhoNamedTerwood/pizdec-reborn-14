using System.Linq;
using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared.Damage;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.PizReb.Boss;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Boss;

public sealed partial class TenHourBurstManSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private static readonly Color[] RgbCycle =
    {
        Color.Red,
        Color.Orange,
        Color.Yellow,
        Color.Lime,
        Color.Cyan,
        Color.Blue,
        Color.Purple,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TenHourBurstManComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TenHourBurstManComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TenHourBurstManComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TenHourBurstManComponent, BossPhaseTransitionStartedEvent>(OnTransitionStarted);
        SubscribeLocalEvent<TenHourBurstManComponent, BossPhaseTransitionFinishedEvent>(OnTransitionFinished);
    }

    private void OnStartup(Entity<TenHourBurstManComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextDecision = _timing.CurTime;
        RemComp<SlowOnDamageComponent>(ent.Owner);
        RemComp<BloodstreamComponent>(ent.Owner);
    }

    private void OnShutdown(Entity<TenHourBurstManComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Parasite != null && !Deleted(ent.Comp.Parasite.Value))
            Del(ent.Comp.Parasite.Value);
        ent.Comp.Parasite = null;
    }

    private void OnMobStateChanged(Entity<TenHourBurstManComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        CancelAbility(ent);
        if (ent.Comp.DeathSound != null)
            PlayBossSound(ent.Owner, ent.Comp.DeathSound);
    }

    private void OnTransitionStarted(Entity<TenHourBurstManComponent> ent, ref BossPhaseTransitionStartedEvent args)
    {
        CancelAbility(ent);
        _appearance.SetData(ent.Owner, TenHourBurstManVisuals.State, TenHourBurstManVisualState.Drink);
    }

    private void OnTransitionFinished(Entity<TenHourBurstManComponent> ent, ref BossPhaseTransitionFinishedEvent args)
    {
        _appearance.SetData(ent.Owner, TenHourBurstManVisuals.State, TenHourBurstManVisualState.Default);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TenHourBurstManComponent, BossComponent>();
        while (query.MoveNext(out var uid, out var comp, out var boss))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            if (!boss.Active || boss.Transitioning)
            {
                if (comp.Ability != HbmAbility.None)
                    CancelAbility((uid, comp));
                SleepNpc(uid);
                continue;
            }
            UpdateTarget(uid, comp, boss, now);
            if (comp.Target == null)
                continue;
            var target = comp.Target.Value;
            if (comp.Ability == HbmAbility.None)
            {
                if (now >= comp.NextDecision)
                {
                    var distance = GetDistance(uid, target);
                    var ability = ChooseAbility(comp, boss, distance);
                    if (ability != HbmAbility.None)
                    {
                        StartAbility(uid, comp, ability, target, now);
                        continue;
                    }
                }
                continue;
            }
            UpdateAbility(uid, comp, target, now);
        }
    }

    private void UpdateTarget(EntityUid uid, TenHourBurstManComponent comp, BossComponent boss, TimeSpan now)
    {
        if (comp.Target != null)
        {
            var current = comp.Target.Value;
            if (!Exists(current) || !IsPlayerAlive(current) || GetDistance(uid, current) > comp.AggroRange)
                comp.Target = null;
        }
        if (comp.Target == null)
        {
            EntityUid? best = null;
            var bestDistance = float.MaxValue;
            foreach (var other in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, comp.AggroRange, LookupFlags.Dynamic))
            {
                if (!IsPlayerAlive(other))
                    continue;
                var distance = GetDistance(uid, other);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = other;
            }
            comp.Target = best;
        }
        if (comp.Target == null)
            return;
        var position = _transform.GetWorldPosition(comp.Target.Value);
        comp.History.Add((now, position));
        while (comp.History.Count > 0 && now - comp.History[0].Time > TimeSpan.FromSeconds(1))
            comp.History.RemoveAt(0);
    }

    private bool IsPlayerAlive(EntityUid uid)
    {
        if (!HasComp<ActorComponent>(uid))
            return false;
        if (!TryComp<MobStateComponent>(uid, out var mobState))
            return false;
        return _mobState.IsAlive(uid, mobState);
    }


    private float GetDistance(EntityUid a, EntityUid b)
    {
        var aPos = _transform.GetWorldPosition(a);
        var bPos = _transform.GetWorldPosition(b);
        return (aPos - bPos).Length();
    }

    private bool Ready(TenHourBurstManComponent comp, HbmAbility ability, TimeSpan now)
    {
        return !comp.Cooldowns.TryGetValue(ability, out var ready) || now >= ready;
    }

    private HbmAbility ChooseAbility(TenHourBurstManComponent comp, BossComponent boss, float distance)
    {
        var now = _timing.CurTime;
        var options = new List<HbmAbility>();
        if (distance <= comp.DarkheartRange && Ready(comp, HbmAbility.Darkheart, now))
            options.Add(HbmAbility.Darkheart);
        if (comp.VampirismEnabled && distance <= comp.VampirismRange && Ready(comp, HbmAbility.Vampirism, now))
            options.Add(HbmAbility.Vampirism);
        if (boss.Phase >= 1 && distance <= comp.StrongLeftRange && Ready(comp, HbmAbility.StrongLeft, now))
            options.Add(HbmAbility.StrongLeft);
        if (distance <= comp.TwilightRange && Ready(comp, HbmAbility.Twilight, now))
            options.Add(HbmAbility.Twilight);
        if (distance <= comp.HoloparasiteRange && Ready(comp, HbmAbility.Holoparasite, now))
            options.Add(HbmAbility.Holoparasite);
        if (distance >= comp.ShotgunMinRange && distance <= comp.ShotgunMaxRange && Ready(comp, HbmAbility.Shotgun, now))
            options.Add(HbmAbility.Shotgun);
        if (boss.Phase >= 1 && distance >= comp.GrandValleyMinRange && distance <= comp.GrandValleyMaxRange &&
            Ready(comp, HbmAbility.GrandValley, now))
            options.Add(HbmAbility.GrandValley);
        if (options.Count == 0)
            return HbmAbility.None;
        return _random.Pick(options);
    }

    private void StartAbility(EntityUid uid, TenHourBurstManComponent comp, HbmAbility ability, EntityUid target, TimeSpan now)
    {
        comp.Ability = ability;
        comp.Target = target;
        comp.Step = 0;
        comp.DashesLeft = 0;
        comp.ShotsLeft = 0;
        comp.DashHit = false;
        comp.AbilityHit = false;
        if (ability != HbmAbility.StrongLeft)
            SleepNpc(uid);
        switch (ability)
        {
            case HbmAbility.Darkheart:
                SetState(uid, TenHourBurstManVisualState.Darkheart);
                comp.DashesLeft = comp.DarkheartDashes;
                comp.StepTime = now;
                if (comp.DarkheartSound != null)
                    PlayBossSound(uid, comp.DarkheartSound);
                break;
            case HbmAbility.Twilight:
                SetState(uid, TenHourBurstManVisualState.PeaceForAll);
                ApplySpeed(uid, comp, comp.TwilightSlow, now);
                comp.StepTime = now + comp.TwilightDelay;
                break;
            case HbmAbility.Holoparasite:
                SetState(uid, TenHourBurstManVisualState.Default);
                ApplySpeed(uid, comp, comp.HoloparasiteSlow, now);
                comp.StepTime = now + comp.HoloparasiteDuration;
                if (SpawnParasite(uid, comp) && comp.HoloparasiteSound != null)
                    PlayBossSound(uid, comp.HoloparasiteSound);
                break;
            case HbmAbility.Vampirism:
                SetState(uid, TenHourBurstManVisualState.Default);
                SetGlow(uid, Color.Red, 0.6f);
                comp.DashesLeft = comp.VampirismDashes;
                comp.StepTime = now + comp.VampirismDelay;
                if (comp.VampirismSound != null)
                    PlayBossSound(uid, comp.VampirismSound);
                break;
            case HbmAbility.Shotgun:
                SetState(uid, TenHourBurstManVisualState.Shotgun);
                ApplySpeed(uid, comp, comp.ShotgunSlow, now);
                comp.ShotsLeft = comp.ShotgunShots;
                comp.StepTime = now;
                if (comp.ShotgunSound != null)
                    PlayBossSound(uid, comp.ShotgunSound);
                break;
            case HbmAbility.GrandValley:
                SetState(uid, TenHourBurstManVisualState.Shotgun);
                SetGlow(uid, Color.Red, 0.8f);
                comp.ShotsLeft = comp.GrandValleyShots;
                comp.StepTime = now;
                if (comp.GrandValleySound != null)
                    PlayBossSound(uid, comp.GrandValleySound);
                break;
            case HbmAbility.StrongLeft:
                SetState(uid, TenHourBurstManVisualState.Default);
                comp.StepTime = now + comp.StrongLeftChase;
                break;
        }
    }

    private void UpdateAbility(EntityUid uid, TenHourBurstManComponent comp, EntityUid target, TimeSpan now)
    {
        var distance = GetDistance(uid, target);
        switch (comp.Ability)
        {
            case HbmAbility.Darkheart:
                if (!comp.DashHit && distance <= comp.DashHitRange)
                {
                    comp.DashHit = true;
                    ApplyDamage(target, "Slash", comp.DarkheartDamage, uid);
                    Heal(uid, "Slash", comp.DarkheartHeal);
                    if (comp.DarkheartSound != null)
                        PlayBossSound(uid, comp.DarkheartSound);
                }
                if (now < comp.StepTime)
                    break;
                if (comp.DashesLeft > 0)
                {
                    Dash(uid, comp, target, comp.DarkheartSpeed);
                    comp.DashesLeft--;
                    comp.DashHit = false;
                    comp.StepTime = now + comp.DarkheartGap;
                }
                else
                {
                    Finish(uid, comp, HbmAbility.Darkheart, comp.DarkheartCooldown, now);
                }
                break;
            case HbmAbility.Twilight:
                if (now < comp.StepTime)
                    break;
                Spawn(comp.TwilightEffect, Transform(uid).Coordinates);
                if (comp.TwilightSound != null)
                    PlayBossSound(uid, comp.TwilightSound);
                foreach (var other in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, comp.TwilightRadius, LookupFlags.Dynamic))
                {
                    if (!IsPlayerAlive(other))
                        continue;
                    ApplyDamage(other, "Blunt", comp.TwilightDamage, uid);
                    _stun.TryKnockdown(other, comp.TwilightStun, true);
                }
                Finish(uid, comp, HbmAbility.Twilight, comp.TwilightCooldown, now);
                break;
            case HbmAbility.Holoparasite:
                if (now < comp.StepTime)
                    break;
                Finish(uid, comp, HbmAbility.Holoparasite, comp.HoloparasiteCooldown, now);
                break;
            case HbmAbility.Vampirism:
                if (!comp.DashHit && distance <= comp.DashHitRange)
                {
                    comp.DashHit = true;
                    comp.AbilityHit = true;
                    GrabIntoDarkness(uid, target);
                    Finish(uid, comp, HbmAbility.Vampirism, comp.VampirismCooldown, now);
                    break;
                }
                if (now < comp.StepTime)
                    break;
                if (comp.DashesLeft > 0)
                {
                    Dash(uid, comp, target, comp.VampirismSpeed);
                    comp.DashesLeft--;
                    comp.DashHit = false;
                    comp.StepTime = now + comp.VampirismGap;
                }
                else if (comp.Step == 0)
                {
                    comp.Step = 1;
                    comp.StepTime = now + comp.VampirismMissTime;
                }
                else
                {
                    Finish(uid, comp, HbmAbility.Vampirism, comp.VampirismCooldown, now);
                }
                break;
            case HbmAbility.Shotgun:
                if (now < comp.StepTime)
                    break;
                if (comp.ShotsLeft > 0)
                {
                    Shoot(uid, target, comp.ShotgunProjectile, comp.ShotgunProjectileSpeed, comp.ShotgunPellets, comp.ShotgunSpread);
                    comp.ShotsLeft--;
                    comp.StepTime = now + comp.ShotgunGap;
                }
                else
                {
                    Finish(uid, comp, HbmAbility.Shotgun, comp.ShotgunCooldown, now);
                }
                break;
            case HbmAbility.GrandValley:
                var color = RgbCycle[(int)((now.TotalSeconds * 12) % RgbCycle.Length)];
                SetGlow(uid, color, 0.9f);
                if (now < comp.StepTime)
                    break;
                if (comp.ShotsLeft > 0)
                {
                    Shoot(uid, target, comp.GrandValleyProjectile, comp.GrandValleyProjectileSpeed);
                    comp.ShotsLeft--;
                    comp.StepTime = now + comp.GrandValleyGap;
                }
                else
                {
                    Finish(uid, comp, HbmAbility.GrandValley, comp.GrandValleyCooldown, now);
                }
                break;
            case HbmAbility.StrongLeft:
                if (distance <= comp.StrongLeftRange)
                {
                    ApplyDamage(target, "Blunt", comp.StrongLeftDamage, uid);
                    _stun.TryKnockdown(target, comp.StrongLeftStun, true);
                    var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
                    if (direction.LengthSquared() > 0.001f)
                        _throwing.TryThrow(target, direction.Normalized(), comp.StrongLeftPush, uid, pushbackRatio: 0f);
                    if (comp.StrongLeftSound != null)
                        PlayBossSound(uid, comp.StrongLeftSound);
                    Finish(uid, comp, HbmAbility.StrongLeft, comp.StrongLeftCooldown, now);
                    break;
                }
                if (now >= comp.StepTime)
                {
                    Finish(uid, comp, HbmAbility.StrongLeft, comp.StrongLeftCooldown, now);
                    break;
                }
                break;
        }
    }

    private void Finish(EntityUid uid, TenHourBurstManComponent comp, HbmAbility ability, TimeSpan cooldown, TimeSpan now)
    {
        comp.Cooldowns[ability] = now + cooldown;
        comp.Ability = HbmAbility.None;
        comp.NextDecision = now + comp.AbilityGap;
        WakeNpc(uid);
        SetState(uid, TenHourBurstManVisualState.Default);
        SetGlow(uid, Color.White, 0f);
        RestoreSpeed(uid, comp);
        if (ability == HbmAbility.Holoparasite)
            RemoveParasite(comp);
    }

    private void CancelAbility(Entity<TenHourBurstManComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Parasite != null && !Deleted(comp.Parasite.Value))
            Del(comp.Parasite.Value);
        comp.Parasite = null;
        comp.Ability = HbmAbility.None;
        comp.Step = 0;
        WakeNpc(ent.Owner);
        SetState(ent.Owner, TenHourBurstManVisualState.Default);
        SetGlow(ent.Owner, Color.White, 0f);
        RestoreSpeed(ent.Owner, comp);
    }

    private void PlayBossSound(EntityUid uid, SoundSpecifier? sound)
    {
        if (sound == null)
            return;
        _audio.PlayGlobal(sound, Filter.Pvs(uid), recordReplay: false, new AudioParams().WithVolume(-3f));
    }
    private void SleepNpc(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;
        if (HasComp<ActiveNPCComponent>(uid))
            _npc.SleepNPC(uid);
    }

    private void WakeNpc(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;
        if (TryComp<MobStateComponent>(uid, out var mobState) && !_mobState.IsAlive(uid, mobState))
            return;
        if (!HasComp<ActiveNPCComponent>(uid))
            _npc.WakeNPC(uid);
    }
    private void SetState(EntityUid uid, TenHourBurstManVisualState state)
    {
        _appearance.SetData(uid, TenHourBurstManVisuals.State, state);
    }

    private void ApplySpeed(EntityUid uid, TenHourBurstManComponent comp, float factor, TimeSpan now)
    {
        if (!TryComp<MovementSpeedModifierComponent>(uid, out var movement))
            return;
        comp.StoredWalk = movement.BaseWalkSpeed;
        comp.StoredSprint = movement.BaseSprintSpeed;
        _speed.ChangeBaseSpeed(uid, comp.StoredWalk * factor, comp.StoredSprint * factor, movement.BaseAcceleration);
    }

    private void RestoreSpeed(EntityUid uid, TenHourBurstManComponent comp)
    {
        if (comp.StoredWalk <= 0f || !TryComp<MovementSpeedModifierComponent>(uid, out var movement))
            return;
        _speed.ChangeBaseSpeed(uid, comp.StoredWalk, comp.StoredSprint, movement.BaseAcceleration);
        comp.StoredWalk = 0f;
        comp.StoredSprint = 0f;
    }

    private void SetGlow(EntityUid uid, Color color, float energy)
    {
        var light = EnsureComp<PointLightComponent>(uid);
        _light.SetColor(uid, color, light);
        _light.SetEnergy(uid, energy, light);
        _light.SetEnabled(uid, energy > 0f, light);
    }

    private void Dash(EntityUid uid, TenHourBurstManComponent comp, EntityUid target, float speed)
    {
        var position = GetLaggedPosition(comp, target);
        var bossPosition = _transform.GetWorldPosition(uid);
        var direction = position - bossPosition;
        if (direction.LengthSquared() < 0.0001f)
            return;
        _throwing.TryThrow(uid, direction, speed, pushbackRatio: 0f, recoil: false, animated: false,
            playSound: false, doSpin: false);
    }

    private Vector2 GetLaggedPosition(TenHourBurstManComponent comp, EntityUid target)
    {
        var wanted = _timing.CurTime - comp.TargetLag;
        var result = _transform.GetWorldPosition(target);
        foreach (var (time, position) in comp.History)
        {
            if (time > wanted)
                break;
            result = position;
        }
        return result;
    }

    private void Shoot(EntityUid uid, EntityUid target, EntProtoId projectile, float speed, int pellets = 1, float spread = 0f)
    {
        var bossPosition = _transform.GetWorldPosition(uid);
        var targetPosition = _transform.GetWorldPosition(target);
        var direction = targetPosition - bossPosition;
        if (direction.LengthSquared() < 0.0001f)
            return;
        var coords = Transform(uid).Coordinates;
        var baseAngle = Angle.FromWorldVec(direction);
        var count = Math.Max(pellets, 1);
        for (var i = 0; i < count; i++)
        {
            var offset = spread <= 0f ? Angle.Zero : Angle.FromDegrees(_random.NextFloat(-spread, spread));
            var pelletDirection = (baseAngle + offset).ToWorldVec();
            var projectileUid = Spawn(projectile, coords);
            _gun.ShootProjectile(projectileUid, pelletDirection, Vector2.Zero, null, uid, speed);
        }
    }

    private bool SpawnParasite(EntityUid uid, TenHourBurstManComponent comp)
    {
        var parasite = Spawn(comp.HoloparasiteProto, Transform(uid).Coordinates);
        comp.Parasite = parasite;
        return true;
    }

    private void RemoveParasite(TenHourBurstManComponent comp)
    {
        if (comp.Parasite != null && !Deleted(comp.Parasite.Value))
            Del(comp.Parasite.Value);
        comp.Parasite = null;
    }

    private void GrabIntoDarkness(EntityUid boss, EntityUid victim)
    {
        var bossXform = Transform(boss);
        var query = EntityQueryEnumerator<BossDarknessComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != bossXform.MapID)
                continue;
            _transform.SetCoordinates(victim, xform.Coordinates);
            return;
        }
        QueueDel(victim);
    }

    private void ApplyDamage(EntityUid target, string type, float amount, EntityUid? origin)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = FixedPoint2.New(amount);
        _damageable.TryChangeDamage(target, damage, origin: origin);
    }

    private void Heal(EntityUid target, string type, float amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = -FixedPoint2.New(amount);
        _damageable.TryChangeDamage(target, damage, ignoreResistances: true);
    }
}















