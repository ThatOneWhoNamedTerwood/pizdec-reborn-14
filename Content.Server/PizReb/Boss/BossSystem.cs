using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.PizReb.Boss;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Boss;

public sealed partial class BossSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BossComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BossComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<BossComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnStartup(Entity<BossComponent> ent, ref ComponentStartup args)
    {
        if (!ProtoMan.TryIndex(ent.Comp.Boss, out var boss))
        {
            Log.Error($"Boss prototype {ent.Comp.Boss} not found");
            return;
        }
        if (_thresholds.TryGetThresholdForState(ent.Owner, MobState.Dead, out var maxHealth) && maxHealth != null)
            ent.Comp.MaxHealth = maxHealth.Value.Float();
        ent.Comp.Health = ent.Comp.MaxHealth;
        ent.Comp.Active = true;
        ent.Comp.Phase = 0;
        ApplyPhase(ent, boss, 0, true);
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnMobStateChanged(Entity<BossComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !ent.Comp.Active)
            return;
        ent.Comp.Active = false;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnBeforeDamage(Entity<BossComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Transitioning || !ent.Comp.Active)
            args.Cancelled = true;
    }

    private void OnDamageModify(Entity<BossComponent> ent, ref DamageModifyEvent args)
    {
        if (MathHelper.CloseToPercent(ent.Comp.DamageTakenMultiplier, 1f))
            return;
        args.Damage *= ent.Comp.DamageTakenMultiplier;
    }

    private void ApplyPhase(Entity<BossComponent> ent, BossPrototype boss, int phase, bool initial = false)
    {
        if (phase >= boss.Phases.Count)
            return;
        var definition = boss.Phases[phase];
        ent.Comp.DamageTakenMultiplier = definition.DamageTakenMultiplier;
        if (TryComp<MovementSpeedModifierComponent>(ent.Owner, out var movement))
        {
            if (initial)
            {
                ent.Comp.BaseWalkSpeed = movement.BaseWalkSpeed;
                ent.Comp.BaseSprintSpeed = movement.BaseSprintSpeed;
            }
            _speed.ChangeBaseSpeed(ent.Owner,
                ent.Comp.BaseWalkSpeed * definition.SpeedMultiplier,
                ent.Comp.BaseSprintSpeed * definition.SpeedMultiplier,
                movement.BaseAcceleration);
        }
        _appearance.SetData(ent.Owner, BossVisuals.Phase, phase);
        Dirty(ent.Owner, ent.Comp);
    }

    private void BeginTransition(Entity<BossComponent> ent, BossPrototype boss, int nextPhase)
    {
        var definition = boss.Phases[nextPhase];
        ent.Comp.Transitioning = true;
        ent.Comp.PendingPhase = nextPhase;
        ent.Comp.TransitionEnd = _timing.CurTime + definition.TransitionDuration;
        if (definition.TransitionSound != null)
            _audio.PlayGlobal(definition.TransitionSound, Filter.Pvs(ent.Owner), recordReplay: false, new AudioParams().WithVolume(-3f));
        var markerQuery = EntityQueryEnumerator<BossPhaseMarkerComponent, TransformComponent>();
        var bossXform = Transform(ent.Owner);
        while (markerQuery.MoveNext(out _, out _, out var markerXform))
        {
            if (markerXform.MapID != bossXform.MapID)
                continue;
            _transform.SetCoordinates(ent.Owner, markerXform.Coordinates);
            break;
        }
        RaiseLocalEvent(ent.Owner, new BossPhaseTransitionStartedEvent { Phase = nextPhase });
        Dirty(ent.Owner, ent.Comp);
    }

    private void FinishTransition(Entity<BossComponent> ent, BossPrototype boss)
    {
        var phase = ent.Comp.PendingPhase;
        ent.Comp.Phase = phase;
        ent.Comp.Transitioning = false;
        if (TryComp<DamageableComponent>(ent.Owner, out var damageable))
            _damageable.SetDamage((ent.Owner, damageable), new DamageSpecifier());
        ent.Comp.Health = ent.Comp.MaxHealth;
        ApplyPhase(ent, boss, phase);
        RaiseLocalEvent(ent.Owner, new BossPhaseTransitionFinishedEvent { Phase = phase });
        Dirty(ent.Owner, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (!bossComp.Active)
                continue;
            if (!ProtoMan.TryIndex(bossComp.Boss, out var boss))
                continue;
            if (TryComp<DamageableComponent>(uid, out var damageable))
            {
                var health = bossComp.MaxHealth - _damageable.GetTotalDamage((uid, damageable)).Float();
                health = MathF.Max(health, 0f);
                if (MathF.Abs(health - bossComp.Health) > 0.01f)
                {
                    bossComp.Health = health;
                    Dirty(uid, bossComp);
                }
            }
            if (bossComp.Transitioning)
            {
                if (now >= bossComp.TransitionEnd)
                    FinishTransition((uid, bossComp), boss);
                continue;
            }
            var nextPhase = bossComp.Phase + 1;
            if (nextPhase >= boss.Phases.Count || bossComp.MaxHealth <= 0f)
                continue;
            if (bossComp.Health / bossComp.MaxHealth > boss.Phases[nextPhase].Threshold)
                continue;
            BeginTransition((uid, bossComp), boss, nextPhase);
        }
    }
}






