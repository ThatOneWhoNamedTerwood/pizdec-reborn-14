using System.Linq;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Cyberware;

public sealed partial class CyberwareSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NPCSystem _npc = default!;

    public const string ContainerId = "cyberware_implants";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberwareComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CyberwareComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyberwareComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<CyberwareComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(Entity<CyberwareComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.CapacityRolled)
        {
            ent.Comp.Capacity = _random.Next(35, 73);
            ent.Comp.CapacityRolled = true;
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnShutdown(Entity<CyberwareComponent> ent, ref ComponentShutdown args)
    {
        if (!_container.TryGetContainer(ent.Owner, ContainerId, out var container))
            return;
        foreach (var implant in container.ContainedEntities.ToArray())
            _container.Remove(implant, container);
    }

    private void OnDamageModify(EntityUid uid, CyberwareComponent cyber, DamageModifyEvent args)
    {
        if (cyber.ArmorPercent <= 0f)
            return;
        var factor = 1f - Math.Clamp(cyber.ArmorPercent, 0f, 0.9f);
        foreach (var type in new[] { "Blunt", "Slash", "Piercing" })
        {
            if (args.Damage.DamageDict.TryGetValue(type, out var value))
                args.Damage.DamageDict[type] = value * factor;
        }
    }

    private void OnMeleeHit(Entity<CyberwareComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.PushOnHit || args.HitEntities.Count == 0)
            return;
        var origin = _transform.GetWorldPosition(ent.Owner);
        foreach (var target in args.HitEntities)
        {
            var direction = _transform.GetWorldPosition(target) - origin;
            if (direction.LengthSquared() < 0.001f)
                continue;
            _throwing.TryThrow(target, direction.Normalized(), ent.Comp.PushStrength, ent.Owner, pushbackRatio: 0f);
            if (ent.Comp.StaminaDamage > 0f)
                _stamina.TakeStaminaDamage(target, ent.Comp.StaminaDamage);
        }
    }

    public Container EnsureContainer(EntityUid uid)
    {
        return _container.EnsureContainer<Container>(uid, ContainerId);
    }

    public bool CanInstall(EntityUid patient, EntityUid implant, out string error)
    {
        error = string.Empty;
        if (!TryComp<CyberwareComponent>(patient, out var cyber))
        {
            error = "cyberware-error-not-capable";
            return false;
        }
        if (!TryComp<CyberImplantComponent>(implant, out var comp))
        {
            error = "cyberware-error-not-implant";
            return false;
        }
        var slot = comp.Slot;
        if (slot is CyberSlot.ArmLeft or CyberSlot.ArmRight)
        {
            if (HasFreeArm(cyber, CyberSlot.ArmLeft) || HasFreeArm(cyber, CyberSlot.ArmRight))
                return true;
        }
        else
        {
            var used = cyber.Slots.TryGetValue(slot, out var list) ? list.Count : 0;
            if (used < CyberSlotHelper.Capacity(slot))
                return true;
        }
        error = "cyberware-error-slot-full";
        return false;
    }

    private bool HasFreeArm(CyberwareComponent cyber, CyberSlot slot)
    {
        return !cyber.Slots.TryGetValue(slot, out var list) || list.Count == 0;
    }

    private void GrantActions(EntityUid patient, CyberwareComponent cyber, EntityUid implant)
    {
        if (!TryComp<CyberActionGrantComponent>(implant, out var grant))
            return;
        EnsureComp<ActionsContainerComponent>(patient);
        var list = new List<EntityUid>();
        foreach (var action in grant.Actions)
        {
            EntityUid? actionUid = null;
            _actionsSystem.AddAction(patient, ref actionUid, action);
            if (actionUid != null)
                list.Add(actionUid.Value);
        }
        if (list.Count > 0)
            cyber.ActionEntities[implant] = list;
    }

    private void RemoveActions(CyberwareComponent cyber, EntityUid implant)
    {
        if (!cyber.ActionEntities.Remove(implant, out var list))
            return;
        foreach (var action in list)
        {
            if (!Deleted(action))
                _actionsSystem.RemoveAction(action);
        }
    }

    public EntityUid? GetSlotImplant(EntityUid patient, CyberSlot slot)
    {
        if (!TryComp<CyberwareComponent>(patient, out var cyber))
            return null;
        return cyber.Slots.TryGetValue(slot, out var list) && list.Count > 0 ? list[0] : null;
    }

    public bool TryInstallImplant(EntityUid patient, EntityUid implant, out EntityUid? replaced)
    {
        replaced = null;
        if (!TryComp<CyberwareComponent>(patient, out var cyber) || !TryComp<CyberImplantComponent>(implant, out var implantComp))
            return false;

        var slot = implantComp.Slot;
        if (slot is CyberSlot.ArmLeft or CyberSlot.ArmRight)
            slot = HasFreeArm(cyber, CyberSlot.ArmLeft) ? CyberSlot.ArmLeft : CyberSlot.ArmRight;
        if (cyber.Slots.TryGetValue(slot, out var existing) && existing.Count > 0 && implantComp.ReplacesLimb)
        {
            replaced = existing[0];
            RemoveImplantInternal(patient, cyber, replaced.Value, false);
        }

        var container = EnsureContainer(patient);
        if (!_container.Insert(implant, container))
            return false;

        cyber.Implants.Add(implant);
        cyber.AssignedSlots[implant] = slot;
        if (!cyber.Slots.TryGetValue(slot, out var list))
        {
            list = new List<EntityUid>();
            cyber.Slots[slot] = list;
        }
        list.Add(implant);
        GrantActions(patient, cyber, implant);
        Recalculate(patient, cyber);
        return true;
    }

    public bool TryRemoveImplant(EntityUid patient, EntityUid implant)
    {
        if (!TryComp<CyberwareComponent>(patient, out var cyber))
            return false;
        return RemoveImplantInternal(patient, cyber, implant, true);
    }

    private bool RemoveImplantInternal(EntityUid patient, CyberwareComponent cyber, EntityUid implant, bool eject)
    {
        if (!cyber.Implants.Contains(implant))
            return false;
        cyber.Implants.Remove(implant);
        cyber.AssignedSlots.Remove(implant);
        foreach (var (slot, list) in cyber.Slots)
        {
            if (!list.Remove(implant))
                continue;
            if (list.Count == 0)
                cyber.Slots.Remove(slot);
            break;
        }
        RemoveActions(cyber, implant);
        if (_container.TryGetContainer(patient, ContainerId, out var container) && container.Contains(implant))
            _container.Remove(implant, container);
        if (eject)
            _transform.SetCoordinates(implant, Transform(patient).Coordinates);
        Recalculate(patient, cyber);
        return true;
    }

    public void Recalculate(EntityUid patient, CyberwareComponent? cyber = null)
    {
        if (!Resolve(patient, ref cyber, false))
            return;

        var load = 0;
        var meleeBonus = 0f;
        var speed = 1f;
        var spread = 1f;
        var push = false;
        var pushStrength = 0f;
        var stamina = 0f;
        var armor = 0f;
        var visuals = new List<CyberwareVisualOverride>();

        foreach (var implant in cyber.Implants.ToArray())
        {
            if (!TryComp<CyberImplantComponent>(implant, out var comp))
                continue;
            load += comp.BrainLoad;
            meleeBonus += comp.MeleeDamageBonus;
            speed *= comp.SpeedMultiplier;
            spread *= comp.SpreadMultiplier;
            push |= comp.PushOnHit;
            pushStrength = MathF.Max(pushStrength, comp.PushStrength);
            stamina += comp.StaminaDamage;
            armor = MathF.Max(armor, comp.ArmorPercent);
            var slot = cyber.AssignedSlots.TryGetValue(implant, out var assigned) ? assigned : comp.Slot;
            AddVisuals(visuals, comp, slot);
        }

        cyber.BrainLoad = load;
        cyber.MeleeDamageBonus = meleeBonus;
        cyber.SpeedMultiplier = speed;
        cyber.SpreadMultiplier = spread;
        cyber.PushOnHit = push;
        cyber.PushStrength = pushStrength;
        cyber.StaminaDamage = stamina;
        cyber.ArmorPercent = armor;
        cyber.Visuals = visuals;
        ApplyMelee(patient, meleeBonus);
        ApplySpeed(patient, cyber, speed);
        Dirty(patient, cyber);
        RaiseLocalEvent(patient, new CyberwareChangedEvent());
    }

    private void AddVisuals(List<CyberwareVisualOverride> visuals, CyberImplantComponent comp, CyberSlot slot)
    {
        var color = comp.LayerColor;
        var state = comp.LayerState;
        switch (slot)
        {
            case CyberSlot.ArmLeft:
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.ArmLeft, state, color));
                break;
            case CyberSlot.ArmRight:
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.ArmRight, state, color));
                break;
            case CyberSlot.Legs:
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.LegLeft, state, color));
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.LegRight, state, color));
                break;
            case CyberSlot.Head:
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.Head, state, color));
                break;
            case CyberSlot.Eyes:
                visuals.Add(new CyberwareVisualOverride(CyberwareVisualLayer.Eyes, state, color));
                break;
        }
    }

    private void ApplyMelee(EntityUid patient, float bonus)
    {
        if (!TryComp<MeleeWeaponComponent>(patient, out var melee))
            return;
        if (bonus <= 0f)
        {
            if (!TryComp<CyberwareComponent>(patient, out var cyber))
                return;
            melee.Damage = cyber.OriginalMelee ?? new DamageSpecifier();
            cyber.OriginalMelee = null;
            Dirty(patient, melee);
            return;
        }
        if (!TryComp<CyberwareComponent>(patient, out var ware))
            return;
        ware.OriginalMelee ??= melee.Damage;
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(bonus);
        melee.Damage = damage;
        Dirty(patient, melee);
    }

    private void ApplySpeed(EntityUid patient, CyberwareComponent cyber, float multiplier)
    {
        if (!TryComp<MovementSpeedModifierComponent>(patient, out var movement))
            return;
        if (!cyber.OriginalSpeedsStored)
        {
            cyber.OriginalWalk = movement.BaseWalkSpeed;
            cyber.OriginalSprint = movement.BaseSprintSpeed;
            cyber.OriginalSpeedsStored = true;
        }
        _speed.ChangeBaseSpeed(patient, cyber.OriginalWalk * multiplier, cyber.OriginalSprint * multiplier, movement.BaseAcceleration);
    }

    public void AddHeat(EntityUid patient, float heat)
    {
        if (!TryComp<CyberwareComponent>(patient, out var cyber) || heat <= 0f)
            return;
        cyber.Heat = Math.Clamp(cyber.Heat + (int) heat, 0, 100);
        Dirty(patient, cyber);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CyberwareComponent>();
        while (query.MoveNext(out var uid, out var cyber))
        {
            if (cyber.Heat <= 0)
                continue;
            if (_timing.CurTime < cyber.NextHeatTick)
                continue;
            cyber.NextHeatTick = _timing.CurTime + TimeSpan.FromSeconds(1);
            cyber.Heat = Math.Max(0, cyber.Heat - 1);
            if (cyber.Heat < 100)
                continue;
            var damage = new DamageSpecifier();
            damage.DamageDict["Heat"] = FixedPoint2.New(2f);
            _damageable.TryChangeDamage(uid, damage, origin: uid);
            Dirty(uid, cyber);
        }
    }
}

public sealed class CyberwareChangedEvent : EntityEventArgs;










