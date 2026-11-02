using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PizReb.Gateway;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Gateway;

public sealed partial class ExpeditionGatewaySystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedAmbientSoundSystem _ambience = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpeditionGatewayComponent, ComponentStartup>(OnGatewayStartup);
        SubscribeLocalEvent<ExpeditionGatewayComponent, ComponentShutdown>(OnGatewayShutdown);
        SubscribeLocalEvent<ExpeditionGatewayComponent, BoundUIOpenedEvent>(OnGatewayUiOpened);
        SubscribeLocalEvent<ExpeditionGatewayComponent, ExpeditionGatewayClaimMissionMessage>(OnClaimMission);
        SubscribeLocalEvent<GatewayPortalComponent, StartCollideEvent>(OnGatewayPortalCollide);
        SubscribeLocalEvent<GatewayMissionReturnPortalComponent, StartCollideEvent>(OnReturnPortalCollide);
        SubscribeLocalEvent<GatewayMissionObjectiveDeviceComponent, ActivateInWorldEvent>(OnObjectiveActivated);
    }

    private void OnGatewayStartup(Entity<ExpeditionGatewayComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            receiver.Load = ent.Comp.IdlePowerLoad;
        _ambience.SetAmbience(ent.Owner, false);
        _light.SetEnabled(ent.Owner, false);
        RollOffers(ent);
    }

    private void OnGatewayShutdown(Entity<ExpeditionGatewayComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Mission != null && !Deleted(ent.Comp.Mission.Value))
            QueueDel(ent.Comp.Mission.Value);
        ent.Comp.Mission = null;
    }

    private void OnGatewayUiOpened(Entity<ExpeditionGatewayComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void RollOffers(Entity<ExpeditionGatewayComponent> ent)
    {
        var pool = ProtoMan.EnumeratePrototypes<GatewayMissionPrototype>()
            .Select(x => new ProtoId<GatewayMissionPrototype>(x.ID))
            .ToList();
        _random.Shuffle(pool);
        ent.Comp.Offered.Clear();
        for (var i = 0; i < ent.Comp.OfferCount && i < pool.Count; i++)
            ent.Comp.Offered.Add(pool[i]);
        ent.Comp.NextOffer = _timing.CurTime + ent.Comp.OfferInterval;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<ExpeditionGatewayComponent> ent)
    {
        string? activeMission = null;
        if (ent.Comp.Mission != null && TryComp(ent.Comp.Mission.Value, out GatewayMissionComponent? mission))
            activeMission = mission.Mission.Id;
        _ui.SetUiState(ent.Owner, ExpeditionGatewayUiKey.Key, new ExpeditionGatewayBoundUserInterfaceState
        {
            Active = activeMission != null,
            ActiveMission = activeMission,
            NextOffer = ent.Comp.NextOffer,
            OfferInterval = ent.Comp.OfferInterval,
            ReadyAt = ent.Comp.NextReady,
            Cooldown = ent.Comp.Cooldown,
            Missions = ent.Comp.Offered.Select(x => x.Id).ToList(),
        });
    }

    private void OnClaimMission(Entity<ExpeditionGatewayComponent> ent, ref ExpeditionGatewayClaimMissionMessage args)
    {
        if (ent.Comp.Mission != null)
            return;
        if (!ProtoMan.TryIndex<GatewayMissionPrototype>(args.Mission, out var proto) ||
            !ent.Comp.Offered.Any(x => x.Id == proto.ID))
        {
            _popup.PopupEntity(Loc.GetString("gateway-mission-unavailable"), ent.Owner, args.Actor);
            return;
        }
        if (!_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("gateway-mission-no-power"), ent.Owner, args.Actor);
            return;
        }
        if (_timing.CurTime < ent.Comp.NextReady)
        {
            var remaining = ent.Comp.NextReady - _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("gateway-mission-cooldown", ("time", $"{remaining.Minutes:00}:{remaining.Seconds:00}")), ent.Owner, args.Actor);
            return;
        }
        if (!TryStartMission(ent, proto))
            _popup.PopupEntity(Loc.GetString("gateway-mission-failed"), ent.Owner, args.Actor);
    }

    private bool IsMapCategory(ResPath path)
    {
        var text = _resource.ContentFileReadAllText(path.ToString());
        var index = text.IndexOf("category:", StringComparison.Ordinal);
        return index >= 0 && text.Substring(index, Math.Min(32, text.Length - index)).Contains("Map");
    }

    private bool TryStartMission(Entity<ExpeditionGatewayComponent> ent, GatewayMissionPrototype proto)
    {
        var options = new DeserializationOptions { InitializeMaps = true };
        Entity<MapComponent>? mapEnt;
        if (IsMapCategory(proto.MapPath))
        {
            if (!_mapLoader.TryLoadMap(proto.MapPath, out var loaded, out _, options))
            {
                Log.Error($"Failed to load gateway mission map {proto.MapPath}");
                return false;
            }
            mapEnt = loaded;
        }
        else
        {
            if (!_mapLoader.TryLoadGrid(proto.MapPath, out var loadedGrid, out _, options))
            {
                Log.Error($"Failed to load gateway mission map {proto.MapPath}");
                return false;
            }
            mapEnt = loadedGrid;
        }
        var mapUid = mapEnt.Value.Owner;
        var mapId = mapEnt.Value.Comp.MapId;
        var atmosphere = EnsureComp<MapAtmosphereComponent>(mapUid);
        var atmosphereSystem = EntityManager.System<AtmosphereSystem>();
        atmosphereSystem.SetMapSpace(mapUid, false, atmosphere);
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824879f;
        moles[(int)Gas.Nitrogen] = 82.10312f;
        atmosphereSystem.SetMapGasMixture(mapUid, new GasMixture(moles, 293.15f), atmosphere);
        Dirty(mapUid, atmosphere);
        var mission = AddComp<GatewayMissionComponent>(mapUid);
        mission.Mission = proto.ID;
        mission.Gateway = ent.Owner;
        mission.IdleSince = _timing.CurTime;
        var spawnQuery = EntityQueryEnumerator<GatewayMissionSpawnPointComponent, TransformComponent>();
        while (spawnQuery.MoveNext(out var spawnUid, out _, out var spawnXform))
        {
            if (spawnXform.MapID == mapId)
                mission.SpawnPoints.Add(spawnUid);
        }
        var returnQuery = EntityQueryEnumerator<GatewayMissionReturnPortalComponent, TransformComponent>();
        while (returnQuery.MoveNext(out var portalUid, out var portal, out var portalXform))
        {
            if (portalXform.MapID != mapId)
                continue;
            portal.Gateway = ent.Owner;
            mission.ReturnPortal = portalUid;
        }
        SpawnObjective(mapUid, mapId, proto, mission);
        if (mission.SpawnPoints.Count == 0 || mission.ReturnPortal == null || mission.Objective == null)
        {
            Log.Error($"Gateway mission {proto.ID} is missing a spawn point, return portal or objective marker");
            Del(mapUid);
            return false;
        }
        ent.Comp.Mission = mapUid;
        EnsureComp<GatewayPortalComponent>(ent);
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            receiver.Load = ent.Comp.ActivePowerLoad;
        _appearance.SetData(ent.Owner, ExpeditionGatewayVisuals.Active, true);
        _light.SetEnabled(ent.Owner, true);
        _ambience.SetAmbience(ent.Owner, true);
        _audio.PlayPvs(ent.Comp.OpenSound, ent.Owner);
        UpdateUi(ent);
        return true;
    }

    private void SpawnObjective(EntityUid mapUid, MapId mapId, GatewayMissionPrototype proto, GatewayMissionComponent mission)
    {
        EntityCoordinates? coords = null;
        if (proto.Objective == GatewayMissionObjective.Kill)
            coords = FindMarker<GatewayMissionKillSpawnComponent>(mapId);
        coords ??= FindMarker<GatewayMissionObjectiveSpawnComponent>(mapId);
        if (coords == null)
            return;
        var objective = Spawn(proto.ObjectiveEntity, coords.Value);
        if (proto.Objective == GatewayMissionObjective.Activate)
            EnsureComp<GatewayMissionObjectiveDeviceComponent>(objective);
        mission.Objective = objective;
    }

    private EntityCoordinates? FindMarker<T>(MapId mapId) where T : IComponent
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == mapId)
                return xform.Coordinates;
        }
        return null;
    }

    private void OnGatewayPortalCollide(Entity<GatewayPortalComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;
        var subject = args.OtherEntity;
        if (!HasComp<ActorComponent>(subject) || HasComp<GatewayPortalCooldownComponent>(subject))
            return;
        TrySendToMission(ent.Owner, subject);
    }

    public bool TrySendToMission(EntityUid gateway, EntityUid subject)
    {
        if (!TryComp(gateway, out ExpeditionGatewayComponent? gate) || gate.Mission == null)
            return false;
        if (!TryComp(gate.Mission.Value, out GatewayMissionComponent? mission) || mission.SpawnPoints.Count == 0)
            return false;
        var marker = _random.Pick(mission.SpawnPoints);
        _transform.SetCoordinates(subject, Transform(marker).Coordinates);
        EnsureComp<GatewayPortalCooldownComponent>(subject).Expires = _timing.CurTime + TimeSpan.FromSeconds(2);
        mission.Entered = true;
        mission.IdleSince = TimeSpan.Zero;
        var proto = ProtoMan.Index(mission.Mission);
        if (_playerManager.TryGetSessionByEntity(subject, out var session))
            _chat.DispatchServerMessage(session, Loc.GetString("gateway-mission-entered", ("goal", Loc.GetString(proto.Goal))));
        return true;
    }

    private void OnReturnPortalCollide(Entity<GatewayMissionReturnPortalComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;
        var subject = args.OtherEntity;
        if (!HasComp<ActorComponent>(subject) || HasComp<GatewayPortalCooldownComponent>(subject))
            return;
        ReturnFromMission(subject);
    }

    public bool ReturnFromMission(EntityUid subject)
    {
        var xform = Transform(subject);
        if (xform.MapUid == null || !TryComp(xform.MapUid.Value, out GatewayMissionComponent? mission))
            return false;
        var mapUid = xform.MapUid.Value;
        var mapId = xform.MapID;
        if (!Exists(mission.Gateway))
            return false;
        var proto = ProtoMan.Index(mission.Mission);
        if (proto.Objective == GatewayMissionObjective.Retrieve && mission.Objective != null &&
            IsObjectiveCarried(mission.Objective.Value, subject, mapId))
            CompleteObjective(mapUid, mission);
        GiveReward(mission);
        _transform.SetCoordinates(subject, Transform(mission.Gateway).Coordinates);
        EnsureComp<GatewayPortalCooldownComponent>(subject).Expires = _timing.CurTime + TimeSpan.FromSeconds(2);
        if (CountPlayers(mapId) == 0)
            CloseMission(mapUid, mission);
        return true;
    }

    private bool IsObjectiveCarried(EntityUid objective, EntityUid subject, MapId missionMap)
    {
        if (Transform(objective).MapID != missionMap)
            return true;
        var parent = Transform(objective).ParentUid;
        for (var i = 0; i < 16 && parent.IsValid(); i++)
        {
            if (parent == subject)
                return true;
            parent = Transform(parent).ParentUid;
        }
        return false;
    }

    private void OnObjectiveActivated(Entity<GatewayMissionObjectiveDeviceComponent> ent, ref ActivateInWorldEvent args)
    {
        var xform = Transform(ent);
        if (xform.MapUid == null || !TryComp(xform.MapUid.Value, out GatewayMissionComponent? mission))
            return;
        if (mission.ObjectiveDone)
            return;
        args.Handled = true;
        CompleteObjective(xform.MapUid.Value, mission);
    }

    private void CompleteObjective(EntityUid mapUid, GatewayMissionComponent mission)
    {
        if (mission.ObjectiveDone)
            return;
        mission.ObjectiveDone = true;
        AnnounceToMap(mapUid, Loc.GetString("gateway-mission-objective-done"));
    }

    private void GiveReward(GatewayMissionComponent mission)
    {
        if (mission.RewardGiven || !mission.ObjectiveDone || !Exists(mission.Gateway))
            return;
        mission.RewardGiven = true;
        var proto = ProtoMan.Index(mission.Mission);
        var coords = Transform(mission.Gateway).Coordinates;
        foreach (var reward in proto.Rewards)
            Spawn(reward, coords);
        if (Transform(mission.Gateway).MapUid is { } gateMap)
            AnnounceToMap(gateMap, Loc.GetString("gateway-mission-reward"));
    }

    private void CloseMission(EntityUid mapUid, GatewayMissionComponent mission)
    {
        var gateway = mission.Gateway;
        if (Exists(gateway) && TryComp(gateway, out ExpeditionGatewayComponent? gate))
        {
            gate.Mission = null;
            RemComp<GatewayPortalComponent>(gateway);
            if (TryComp<ApcPowerReceiverComponent>(gateway, out var receiver))
                receiver.Load = gate.IdlePowerLoad;
            _appearance.SetData(gateway, ExpeditionGatewayVisuals.Active, false);
            _light.SetEnabled(gateway, false);
            _ambience.SetAmbience(gateway, false);
            _audio.PlayPvs(gate.CloseSound, gateway);
            if (mission.ObjectiveDone)
                gate.NextReady = _timing.CurTime + gate.Cooldown;
            UpdateUi((gateway, gate));
        }
        if (!Deleted(mapUid))
            QueueDel(mapUid);
    }

    private void UpdateObjective(EntityUid mapUid, GatewayMissionComponent mission)
    {
        if (mission.ObjectiveDone || mission.Objective == null)
            return;
        var proto = ProtoMan.Index(mission.Mission);
        if (proto.Objective != GatewayMissionObjective.Kill)
            return;
        if (!Deleted(mission.Objective.Value) &&
            (!TryComp(mission.Objective.Value, out MobStateComponent? mobState) || !_mobState.IsDead(mission.Objective.Value, mobState)))
            return;
        CompleteObjective(mapUid, mission);
    }

    private int CountPlayers(MapId mapId)
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob)
                continue;
            if (!TryComp(mob, out TransformComponent? xform) || xform.MapID != mapId)
                continue;
            count++;
        }
        return count;
    }

    private void AnnounceToMap(EntityUid mapUid, string text)
    {
        var mapId = Transform(mapUid).MapID;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob)
                continue;
            if (!TryComp(mob, out TransformComponent? xform) || xform.MapID != mapId)
                continue;
            _chat.DispatchServerMessage(session, text);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var gateQuery = EntityQueryEnumerator<ExpeditionGatewayComponent>();
        while (gateQuery.MoveNext(out var uid, out var gate))
        {
            if (gate.Mission == null)
            {
                if (gate.Offered.Count == 0 || now >= gate.NextOffer)
                    RollOffers((uid, gate));
                continue;
            }
            if (!TryComp(gate.Mission.Value, out GatewayMissionComponent? mission))
            {
                gate.Mission = null;
                RemComp<GatewayPortalComponent>(uid);
                if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                    receiver.Load = gate.IdlePowerLoad;
                _appearance.SetData(uid, ExpeditionGatewayVisuals.Active, false);
                _light.SetEnabled(uid, false);
                _ambience.SetAmbience(uid, false);
                UpdateUi((uid, gate));
                continue;
            }
            UpdateObjective(gate.Mission.Value, mission);
            var mapId = Comp<MapComponent>(gate.Mission.Value).MapId;
            if (CountPlayers(mapId) > 0)
            {
                mission.Entered = true;
                mission.IdleSince = TimeSpan.Zero;
                continue;
            }
            if (mission.IdleSince == TimeSpan.Zero)
                mission.IdleSince = now;
            var timeout = mission.Entered ? TimeSpan.FromSeconds(1) : gate.EntryTimeout;
            if (now - mission.IdleSince < timeout)
                continue;
            CloseMission(gate.Mission.Value, mission);
        }
        var cooldownQuery = EntityQueryEnumerator<GatewayPortalCooldownComponent>();
        while (cooldownQuery.MoveNext(out var mob, out var cooldown))
        {
            if (now >= cooldown.Expires)
                RemCompDeferred<GatewayPortalCooldownComponent>(mob);
        }
    }
}





