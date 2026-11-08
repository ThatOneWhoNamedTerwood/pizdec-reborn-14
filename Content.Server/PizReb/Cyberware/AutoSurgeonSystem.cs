using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Cyberware;

public sealed partial class AutoSurgeonSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CyberwareSystem _cyberware = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AutoSurgeonComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<AutoSurgeonComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<AutoSurgeonComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AutoSurgeonComponent, AutoSurgeonStepEvent>(OnStep);
        SubscribeLocalEvent<AutoSurgeonComponent, AutoSurgeonStartMessage>(OnStartMessage);
        SubscribeLocalEvent<AutoSurgeonComponent, AutoSurgeonEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<BuckleComponent, UnbuckleAttemptEvent>(OnUnbuckleAttempt);
        SubscribeLocalEvent<BuckleComponent, InteractUsingEvent>(OnPatientInteractUsing);
    }

    private void OnStrapped(Entity<AutoSurgeonComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.Patient = args.Buckle.Owner;
        if (_players.TryGetSessionByEntity(args.Buckle.Owner, out _))
            _ui.TryOpenUi(ent.Owner, AutoSurgeonUiKey.Key, args.Buckle.Owner);
        UpdateUi(ent);
    }

    private void OnUnstrapped(Entity<AutoSurgeonComponent> ent, ref UnstrappedEvent args)
    {
        if (ent.Comp.Operating)
            return;
        ent.Comp.Patient = null;
        UpdateUi(ent);
        _ui.CloseUi(ent.Owner, AutoSurgeonUiKey.Key, args.Buckle.Owner);
    }

    private void OnPatientInteractUsing(Entity<BuckleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.BuckledTo is not { } strap)
            return;
        if (!TryComp<AutoSurgeonComponent>(strap, out var table))
            return;
        args.Handled = true;
        if (table.Operating)
        {
            _popup.PopupEntity(Loc.GetString("cyberware-table-busy"), ent.Owner, args.User);
            return;
        }
        InsertItem((strap, table), args.Used);
    }

    private void OnUnbuckleAttempt(Entity<BuckleComponent> ent, ref UnbuckleAttemptEvent args)
    {
        if (!TryComp<AutoSurgeonComponent>(args.Strap.Owner, out var table) || !table.Operating)
            return;
        args.Cancelled = true;
        if (_players.TryGetSessionByEntity(ent.Owner, out var session))
            _popup.PopupEntity(Loc.GetString("cyberware-table-cannot-unbuckle"), ent.Owner, session);
    }

    private void OnInteractUsing(Entity<AutoSurgeonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!IsAcceptedItem(args.Used))
            return;
        args.Handled = true;
        if (ent.Comp.Operating)
        {
            _popup.PopupEntity(Loc.GetString("cyberware-table-busy"), ent.Owner, args.User);
            return;
        }
        InsertItem(ent, args.Used);
    }

    private bool IsAcceptedItem(EntityUid item)
    {
        if (HasComp<CyberImplantComponent>(item) || HasComp<GasTankComponent>(item))
            return true;
        if (TryComp<ClothingComponent>(item, out var clothing) && (clothing.Slots & SlotFlags.MASK) != 0)
            return true;
        var proto = MetaData(item).EntityPrototype;
        return proto != null && proto.ID.StartsWith("Jug", StringComparison.Ordinal);
    }

    public void InsertItem(Entity<AutoSurgeonComponent> ent, EntityUid item)
    {
        if (ent.Comp.Contents.Contains(item))
            return;
        if (HasComp<CyberImplantComponent>(item) && ent.Comp.Contents.Count >= ent.Comp.MaxImplants)
        {
            _popup.PopupEntity(Loc.GetString("cyberware-table-full"), ent.Owner, ent.Comp.Patient ?? ent.Owner);
            return;
        }
        var container = _cyberware.EnsureContainer(ent.Owner);
        if (!_container.Insert(item, container))
            return;
        ent.Comp.Contents.Add(item);
        UpdateUi(ent);
    }

    private void OnEjectMessage(Entity<AutoSurgeonComponent> ent, ref AutoSurgeonEjectMessage args)
    {
        if (ent.Comp.Operating)
            return;
        if (!TryGetEntity(args.Item, out var item) || !ent.Comp.Contents.Contains(item.Value))
            return;
        EjectItem(ent, item.Value);
    }

    public void EjectItem(Entity<AutoSurgeonComponent> ent, EntityUid item)
    {
        ent.Comp.Contents.Remove(item);
        if (_container.TryGetContainer(ent.Owner, CyberwareSystem.ContainerId, out var container) && container.Contains(item))
            _container.Remove(item, container);
        _transform.SetCoordinates(item, Transform(ent.Owner).Coordinates);
        UpdateUi(ent);
    }

    private void OnStartMessage(Entity<AutoSurgeonComponent> ent, ref AutoSurgeonStartMessage args)
    {
        if (ent.Comp.Operating)
            return;
        if (ent.Comp.Patient is not { } patient || !Exists(patient))
            return;
        if (!_players.TryGetSessionByEntity(patient, out var session) || args.Actor != session.AttachedEntity)
            return;

        EntityUid? selected = null;
        if (args.Item != null)
        {
            if (!TryGetEntity(args.Item.Value, out var item) || !ent.Comp.Contents.Contains(item.Value))
                return;
            selected = item.Value;
        }

        EntityUid? target = null;
        if (args.Target != null && TryGetEntity(args.Target.Value, out var targetEntity))
            target = targetEntity.Value;

        if (!TryStartOperation(ent, patient, args.Mode, selected, target, out var error))
            _popup.PopupEntity(Loc.GetString(error), patient, patient);
    }

    public bool TryStartOperation(Entity<AutoSurgeonComponent> ent, EntityUid patient, AutoSurgeonMode mode, EntityUid? selected, EntityUid? target, out string error)
    {
        error = string.Empty;
        if (ent.Comp.Operating)
        {
            error = "cyberware-table-busy";
            return false;
        }
        if (mode == AutoSurgeonMode.Remove)
        {
            if (selected == null || !HasComp<CyberImplantComponent>(selected.Value))
            {
                error = "cyberware-table-invalid";
                return false;
            }
        }
        else if (mode == AutoSurgeonMode.Replace && target != null &&
                 TryComp<CyberImplantComponent>(target.Value, out var targetComp))
        {
            ent.Comp.ReplaceTarget = target;
            ent.Comp.PreviousSlot = targetComp.Slot;
        }
        else if (selected == null || !TryComp<CyberImplantComponent>(selected.Value, out _))
        {
            error = "cyberware-table-select-implant";
            return false;
        }
        else if (!(mode == AutoSurgeonMode.Replace && target != null) && !_cyberware.CanInstall(patient, selected.Value, out error))
        {
            return false;
        }

        if (IsDressed(patient))
        {
            error = "cyberware-table-dressed";
            return false;
        }

        if (!TryPrepareAnesthesia(ent, patient))
        {
            error = "cyberware-table-no-gas";
            return false;
        }

        ent.Comp.Patient = patient;
        PreparePatient(ent, patient);
        ent.Comp.Operating = true;
        ent.Comp.Mode = mode;
        ent.Comp.SelectedImplant = selected == null ? null : GetNetEntity(selected.Value);
        ent.Comp.PreviousImplant = null;
        ent.Comp.ReplaceTarget = null;
        ent.Comp.NextDamageCheck = _timing.CurTime + TimeSpan.FromSeconds(15);
        PlaySafe(ent.Owner, "/Audio/Effects/beep1.ogg");
        UpdateUi(ent);
        _ui.CloseUi(ent.Owner, AutoSurgeonUiKey.Key, patient);
        QueueStep(ent, TimeSpan.FromSeconds(1), AutoSurgeonStage.Mask);
        return true;
    }

    private static readonly string[] DressSlots =
    {
        "jumpsuit", "outerClothing", "gloves", "shoes", "head", "eyes",
    };

    public bool IsDressed(EntityUid patient)
    {
        foreach (var slot in DressSlots)
        {
            if (_inventory.TryGetSlotEntity(patient, slot, out _))
                return true;
        }
        return false;
    }

    private void PreparePatient(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        foreach (var item in ent.Comp.Contents.ToArray())
        {
            if (ent.Comp.EquippedTank == null && TryComp<GasTankComponent>(item, out _))
            {
                if (_inventory.TryEquip(patient, item, "suitstorage", silent: true, force: true))
                {
                    ent.Comp.EquippedTank = item;
                    ent.Comp.Contents.Remove(item);
                }
                continue;
            }
            if (ent.Comp.EquippedMask == null && TryComp<ClothingComponent>(item, out var clothing) &&
                (clothing.Slots & SlotFlags.MASK) != 0)
            {
                if (_inventory.TryEquip(patient, item, "mask", silent: true, force: true))
                {
                    ent.Comp.EquippedMask = item;
                    ent.Comp.Contents.Remove(item);
                }
            }
        }

        if (ent.Comp.EquippedMask is { } mask && TryComp<InternalsComponent>(patient, out var internals))
            _internals.ConnectBreathTool((patient, internals), mask);
        if (ent.Comp.EquippedTank is { } tank && TryComp<InternalsComponent>(patient, out var internals2))
            _internals.TryConnectTank((patient, internals2), tank);
    }

    private void RestorePatient(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        if (TryComp<InternalsComponent>(patient, out var internals))
        {
            if (ent.Comp.EquippedTank is { } tank)
                _internals.DisconnectTank((patient, internals));
            if (ent.Comp.EquippedMask is { } mask)
                _internals.DisconnectBreathTool((patient, internals), mask, true);
        }

        if (ent.Comp.EquippedTank is { } tankItem)
        {
            if (_inventory.TryUnequip(patient, "suitstorage", out var unequipped, silent: true, force: true) && unequipped != null)
                StoreInTable(ent, unequipped.Value);
            ent.Comp.EquippedTank = null;
        }
        if (ent.Comp.EquippedMask is { } maskItem)
        {
            if (_inventory.TryUnequip(patient, "mask", out var unequipped, silent: true, force: true) && unequipped != null)
                StoreInTable(ent, unequipped.Value);
            ent.Comp.EquippedMask = null;
        }

        for (var i = 0; i < ent.Comp.Stripped.Count; i++)
        {
            var item = ent.Comp.Stripped[i];
            if (!Exists(item))
                continue;
            if (_container.TryGetContainer(ent.Owner, CyberwareSystem.ContainerId, out var container) && container.Contains(item))
                _container.Remove(item, container);
            ent.Comp.Contents.Remove(item);
            var slot = i < ent.Comp.StrippedSlots.Count ? ent.Comp.StrippedSlots[i] : "jumpsuit";
            if (_inventory.TryEquip(patient, item, slot, silent: true, force: true))
                continue;
            StoreInTable(ent, item);
        }
        ent.Comp.Stripped.Clear();
        ent.Comp.StrippedSlots.Clear();
    }

    private bool TryPrepareAnesthesia(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        var hasMask = false;
        var moles = 0f;
        foreach (var item in ent.Comp.Contents)
        {
            if (HasComp<ClothingComponent>(item))
                hasMask = true;
            if (TryComp<GasTankComponent>(item, out var tank))
                moles += tank.Air.GetMoles(Gas.NitrousOxide);
        }
        if (!hasMask)
        {
            _popup.PopupEntity(Loc.GetString("cyberware-table-no-mask"), patient, patient);
            return false;
        }
        if (moles <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("cyberware-table-no-gas"), patient, patient);
            return false;
        }
        return true;
    }

    private void QueueStep(Entity<AutoSurgeonComponent> ent, TimeSpan time, AutoSurgeonStage stage)
    {
        ent.Comp.Pending = true;
        ent.Comp.PendingStage = stage;
        ent.Comp.PendingDuration = time;
        ent.Comp.PendingStart = _timing.CurTime + TimeSpan.FromSeconds(0.05);
    }

    private void OnStep(Entity<AutoSurgeonComponent> ent, ref AutoSurgeonStepEvent args)
    {
        OnStep(ent, args);
    }

    private void OnStep(Entity<AutoSurgeonComponent> ent, AutoSurgeonStepEvent args)
    {
        ent.Comp.DoAfterRunning = false;
        if (!ent.Comp.Operating || ent.Comp.Patient is not { } patient)
            return;
        var stage = ent.Comp.Stage;
        if (args.DoAfter != null && args.Cancelled)
        {
            BeginRevert(ent, patient);
            return;
        }
        switch (stage)
        {
            case AutoSurgeonStage.Mask:
                QueueStep(ent, ent.Comp.SleepDelay, AutoSurgeonStage.Sedate);
                break;
            case AutoSurgeonStage.Sedate:
                _sleeping.TrySleeping(patient);
                Announce(ent, patient, "cyberware-table-sedated");
                QueueStep(ent, ent.Comp.OpenTime, AutoSurgeonStage.Open);
                break;
            case AutoSurgeonStage.Open:
                PlaySafe(ent.Owner, "/Audio/Items/drill_use.ogg");
                Announce(ent, patient, "cyberware-table-opened");
                QueueStep(ent, ent.Comp.PrepareTime, AutoSurgeonStage.Prepare);
                break;
            case AutoSurgeonStage.Prepare:
                Announce(ent, patient, "cyberware-table-preparing");
                var time = ent.Comp.OpenTime;
                if (ent.Comp.SelectedImplant != null && TryGetEntity(ent.Comp.SelectedImplant.Value, out var implant) &&
                    TryComp<CyberImplantComponent>(implant.Value, out var implantComp) &&
                    ent.Comp.Mode != AutoSurgeonMode.Remove)
                    time = implantComp.InstallTime;
                QueueStep(ent, time, AutoSurgeonStage.Implant);
                break;
            case AutoSurgeonStage.Implant:
                PlaySafe(ent.Owner, "/Audio/Items/drill_hit.ogg");
                DoInstallStep(ent, patient);
                QueueStep(ent, ent.Comp.PrepareTime, AutoSurgeonStage.Close);
                break;
            case AutoSurgeonStage.Close:
                Announce(ent, patient, "cyberware-table-closing");
                QueueStep(ent, ent.Comp.CloseTime, AutoSurgeonStage.Finish);
                break;
            case AutoSurgeonStage.Finish:
                FinishOperation(ent, patient);
                break;
            case AutoSurgeonStage.RevertOpen:
                QueueStep(ent, ent.Comp.RevertTime, AutoSurgeonStage.RevertRestore);
                break;
            case AutoSurgeonStage.RevertRestore:
                QueueStep(ent, ent.Comp.RevertTime, AutoSurgeonStage.RevertClose);
                break;
            case AutoSurgeonStage.RevertClose:
                FinishRevert(ent, patient);
                break;
        }
    }

    private void DoInstallStep(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        var failChance = ent.Comp.Durability >= ent.Comp.FailAbove
            ? 0f
            : Math.Clamp((ent.Comp.FailAbove - ent.Comp.Durability) / ent.Comp.FailAbove * 0.8f, 0f, 0.8f);
        if (_random.Prob(failChance))
        {
            Announce(ent, patient, "cyberware-table-failed");
            return;
        }

        if (ent.Comp.Mode == AutoSurgeonMode.Remove)
        {
            if (ent.Comp.SelectedImplant != null && TryGetEntity(ent.Comp.SelectedImplant.Value, out var remove) &&
                _cyberware.TryRemoveImplant(patient, remove.Value))
                ent.Comp.Contents.Add(remove.Value);
            return;
        }

        if (ent.Comp.SelectedImplant == null || !TryGetEntity(ent.Comp.SelectedImplant.Value, out var implant))
            return;
        if (ent.Comp.Mode == AutoSurgeonMode.Replace)
        {
            var occupant = ent.Comp.ReplaceTarget ?? _cyberware.GetSlotImplant(patient, ent.Comp.PreviousSlot);
            if (occupant != null && _cyberware.TryRemoveImplant(patient, occupant.Value))
            {
                ent.Comp.PreviousImplant = occupant.Value;
                ent.Comp.Contents.Add(occupant.Value);
            }
        }
        if (!_cyberware.TryInstallImplant(patient, implant.Value, out var replaced))
            return;
        ent.Comp.Contents.Remove(implant.Value);
        if (replaced != null)
            StoreInTable(ent, replaced.Value);
        if (TryComp<CyberImplantComponent>(implant.Value, out var installed) && installed.ReplacesLimb)
            StoreRemovedLimb(ent, installed.Slot);
    }

    private void StoreRemovedLimb(Entity<AutoSurgeonComponent> ent, CyberSlot slot)
    {
        switch (slot)
        {
            case CyberSlot.ArmLeft:
                StoreInTable(ent, Spawn("OrganHumanArmLeft", Transform(ent.Owner).Coordinates));
                break;
            case CyberSlot.ArmRight:
                StoreInTable(ent, Spawn("OrganHumanArmRight", Transform(ent.Owner).Coordinates));
                break;
            case CyberSlot.Legs:
                StoreInTable(ent, Spawn("OrganHumanLegLeft", Transform(ent.Owner).Coordinates));
                StoreInTable(ent, Spawn("OrganHumanLegRight", Transform(ent.Owner).Coordinates));
                break;
            case CyberSlot.Head:
                StoreInTable(ent, Spawn("OrganHumanHead", Transform(ent.Owner).Coordinates));
                break;
            case CyberSlot.Eyes:
                StoreInTable(ent, Spawn("OrganHumanEyes", Transform(ent.Owner).Coordinates));
                break;
        }
    }

    private void StoreInTable(Entity<AutoSurgeonComponent> ent, EntityUid item)
    {
        var container = _cyberware.EnsureContainer(ent.Owner);
        _container.Insert(item, container);
        ent.Comp.Contents.Add(item);
    }

    private void FinishOperation(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        RestorePatient(ent, patient);
        ent.Comp.Durability = MathF.Max(0f, ent.Comp.Durability - _random.NextFloat(ent.Comp.WearMin, ent.Comp.WearMax));
        ent.Comp.Operating = false;
        ent.Comp.Stage = AutoSurgeonStage.None;
        ent.Comp.SelectedImplant = null;
        _sleeping.TryWaking(patient, true);
        PlaySafe(ent.Owner, "/Audio/Effects/voteding.ogg");
        Announce(ent, patient, "cyberware-table-finished");
    }

    private void BeginRevert(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        if (!ent.Comp.Operating)
            return;
        Announce(ent, patient, "cyberware-table-reverting");
        QueueStep(ent, ent.Comp.RevertTime, AutoSurgeonStage.RevertOpen);
    }

    private void FinishRevert(Entity<AutoSurgeonComponent> ent, EntityUid patient)
    {
        RestorePatient(ent, patient);
        if (ent.Comp.PreviousImplant != null && Exists(ent.Comp.PreviousImplant.Value) &&
            ent.Comp.Contents.Contains(ent.Comp.PreviousImplant.Value))
        {
            _cyberware.TryInstallImplant(patient, ent.Comp.PreviousImplant.Value, out _);
            ent.Comp.Contents.Remove(ent.Comp.PreviousImplant.Value);
        }
        ent.Comp.PreviousImplant = null;
        ent.Comp.ReplaceTarget = null;
        ent.Comp.Operating = false;
        ent.Comp.Stage = AutoSurgeonStage.None;
        _sleeping.TryWaking(patient, true);
        Announce(ent, patient, "cyberware-table-revert-done");
        if (_players.TryGetSessionByEntity(patient, out _))
            _ui.TryOpenUi(ent.Owner, AutoSurgeonUiKey.Key, patient);
        UpdateUi(ent);
    }

    private void PlaySafe(EntityUid uid, string path)
    {
        if (_resource.ContentFileExists(path))
            _audio.PlayPvs(path, uid);
    }

    private void Announce(Entity<AutoSurgeonComponent> ent, EntityUid patient, string message)
    {
        var text = Loc.GetString(message);
        _popup.PopupEntity(text, ent.Owner, patient, PopupType.Large);
        _chatSystem.TrySendInGameICMessage(ent.Owner, text, InGameICChatType.Speak, false);
    }

    private void UpdateUi(Entity<AutoSurgeonComponent> ent)
    {
        if (!TryComp<UserInterfaceComponent>(ent.Owner, out _))
            return;
        _ui.SetUiState(ent.Owner, AutoSurgeonUiKey.Key, BuildState(ent));
    }

    public AutoSurgeonState BuildState(Entity<AutoSurgeonComponent> ent)
    {
        var state = new AutoSurgeonState
        {
            Durability = $"{ent.Comp.Durability:0}%",
            DurabilityValue = ent.Comp.Durability,
            Operating = ent.Comp.Operating,
            OperationStage = ent.Comp.Operating ? ent.Comp.Stage.ToString() : string.Empty,
        };

        if (ent.Comp.Patient is { } patient && TryComp<CyberwareComponent>(patient, out var cyber))
        {
            state.PatientName = Name(patient);
            state.PatientEntity = GetNetEntity(patient);
            state.Dressed = IsDressed(patient);
            state.Capacity = cyber.Capacity;
            state.BrainLoad = cyber.BrainLoad;
            state.Heat = cyber.Heat;
            if (state.Dressed)
                state.Warning = Loc.GetString("cyberware-table-dressed-warning");
            else if (state.BrainLoad >= state.Capacity * 0.85f)
                state.Warning = Loc.GetString("cyberware-table-limit-warning");
            foreach (var implant in cyber.Implants)
            {
                if (!TryComp<CyberImplantComponent>(implant, out var comp))
                    continue;
                state.Implants.Add(new AutoSurgeonImplantEntry(GetNetEntity(implant), Name(implant), Loc.GetString("cyberware-slot-" + comp.Slot.ToString().ToLowerInvariant()), comp.BrainLoad, MetaData(implant).EntityDescription ?? string.Empty, cyber.Heat));
            }
        }

        foreach (var item in ent.Comp.Contents)
        {
            if (TryComp<CyberImplantComponent>(item, out var implant))
            {
                state.Contents.Add(new AutoSurgeonItemEntry(GetNetEntity(item), Name(item), Loc.GetString("cyberware-slot-" + implant.Slot.ToString().ToLowerInvariant()), implant.BrainLoad, "implant", true));
            }
            else if (TryComp<GasTankComponent>(item, out var tank))
            {
                state.HasTank = true;
                state.TankMoles = tank.Air.GetMoles(Gas.NitrousOxide);
                state.Contents.Add(new AutoSurgeonItemEntry(GetNetEntity(item), Name(item), string.Empty, 0, "tank", false));
            }
            else if (HasComp<ClothingComponent>(item))
            {
                state.Contents.Add(new AutoSurgeonItemEntry(GetNetEntity(item), Name(item), string.Empty, 0, "mask", false));
            }
            else if (HasComp<SolutionContainerManagerComponent>(item))
            {
                state.JugCount++;
                state.Contents.Add(new AutoSurgeonItemEntry(GetNetEntity(item), Name(item), string.Empty, 0, "jug", false));
            }
        }
        return state;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<AutoSurgeonComponent>();
        while (query.MoveNext(out var uid, out var table))
        {
            if (!table.Operating || table.Patient is not { } patient || !Exists(patient))
                continue;
            if (table.Pending && !table.DoAfterRunning && _timing.CurTime >= table.PendingStart)
            {
                table.Stage = table.PendingStage;
                if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, table.PendingDuration, new AutoSurgeonStepEvent(), uid)
                {
                    BreakOnMove = false,
                    BreakOnDamage = false,
                    NeedHand = false,
                    Hidden = false,
                }))
                {
                    table.Pending = false;
                    table.DoAfterRunning = true;
                }
                else
                {
                    table.PendingStart = _timing.CurTime + TimeSpan.FromSeconds(0.5);
                }
                continue;
            }
            if (table.Durability >= table.DamageAbove)
                continue;
            if (_timing.CurTime < table.NextDamageCheck)
                continue;
            table.NextDamageCheck = _timing.CurTime + TimeSpan.FromSeconds(15);
            if (!_random.Prob(0.3f))
                continue;
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(_random.NextFloat(15f, 26f));
            _damageable.TryChangeDamage(patient, damage, origin: uid);
            _popup.PopupEntity(Loc.GetString("cyberware-table-oups"), patient, patient);
        }
    }
}













