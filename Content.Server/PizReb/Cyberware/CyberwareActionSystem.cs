using Content.Server.Explosion.EntitySystems;
using Content.Shared.Actions;
using Robust.Shared.Audio.Systems;
using Content.Shared.Actions.Events;
using Content.Shared.Charges.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Charges.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Random;

namespace Content.Server.PizReb.Cyberware;

public sealed partial class CyberwareActionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberDashLongEvent>(OnDashLong);
        SubscribeLocalEvent<CyberDashShortEvent>(OnDashShort);
        SubscribeLocalEvent<CyberMantisToggleEvent>(OnMantis);
        SubscribeLocalEvent<CyberExplosiveShotEvent>(OnExplosiveShot);
    }

    private void OnDashLong(CyberDashLongEvent args)
    {
        if (!_charges.TryUseCharge(args.Action.Owner))
        {
            args.Handled = true;
            return;
        }
        var direction = GetMoveDirection(args.Performer);
        KnockdownAlongPath(args.Performer, direction, 6f);
        Dash(args.Performer, direction, 6f);
    }

    private void OnDashShort(CyberDashShortEvent args)
    {
        if (!_charges.TryUseCharge(args.Action.Owner))
        {
            args.Handled = true;
            return;
        }
        Dash(args.Performer, GetMoveDirection(args.Performer), 4f);
    }

    private void KnockdownAlongPath(EntityUid uid, System.Numerics.Vector2 direction, float distance)
    {
        var origin = Transform(uid).Coordinates;
        var dir = direction.Normalized();
        for (var step = 1; step <= 4; step++)
        {
            var point = origin.Offset(dir * (distance / 4f * step));
            foreach (var other in _lookup.GetEntitiesInRange(point, 0.7f, LookupFlags.Dynamic))
            {
                if (other == uid || !HasComp<MobStateComponent>(other))
                    continue;
                _stun.TryKnockdown(other, TimeSpan.FromSeconds(_random.NextFloat(1f, 2f)), true);
            }
        }
    }

    private System.Numerics.Vector2 GetMoveDirection(EntityUid uid)
    {
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            var dir = mover.CurTickSprintMovement + mover.CurTickWalkMovement;
            if (dir.LengthSquared() > 0.01f)
                return dir.Normalized();
        }
        return _transform.GetWorldRotation(uid).ToVec();
    }

    private void Dash(EntityUid uid, System.Numerics.Vector2 direction, float distance)
    {
        if (direction.LengthSquared() < 0.01f)
            return;
        var leaper = EnsureComp<ActiveLeaperComponent>(uid);
        leaper.KnockdownDuration = TimeSpan.Zero;
        _throwing.TryThrow(uid, direction.Normalized() * distance, 12f, uid, pushbackRatio: 0f);
    }

    private void OnExplosiveShot(CyberExplosiveShotEvent args)
    {
        if (!_charges.TryUseCharge(args.Action.Owner))
        {
            args.Handled = true;
            return;
        }
        var coords = Transform(args.Performer).Coordinates;
        var direction = _transform.ToMapCoordinates(args.Target).Position - _transform.GetWorldPosition(args.Performer);
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(args.Performer).ToVec();
        var projectile = Spawn("CyberExplosiveProjectile", coords);
        _gun.ShootProjectile(projectile, direction, System.Numerics.Vector2.Zero, null, args.Performer, 20f);
    }

    private void OnMantis(CyberMantisToggleEvent args)
    {
        var uid = args.Performer;
        if (TryComp<CyberMantisActiveComponent>(uid, out var active))
        {
            foreach (var spawned in active.Blades)
            {
                if (!Deleted(spawned))
                    Del(spawned);
            }
            RemComp<CyberMantisActiveComponent>(uid);
            _audio.PlayPvs("/Audio/Items/change_drill.ogg", uid);
            return;
        }

        active = EnsureComp<CyberMantisActiveComponent>(uid);
        var blade = Spawn("CyberMantisBlade", Transform(uid).Coordinates);
        if (_hands.TryPickupAnyHand(uid, blade))
            active.Blades.Add(blade);
        else
            Del(blade);
        _audio.PlayPvs("/Audio/Items/change_drill.ogg", uid);
    }
}

[RegisterComponent]
public sealed partial class CyberMantisActiveComponent : Component
{
    public List<EntityUid> Blades = new();
}












