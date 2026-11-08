using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Lightning;
using Content.Server.Mind;
using Content.Server.PizReb.Party;
using Content.Server.Station.Systems;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.PizReb.Sector;
using Content.Server.PizReb.Factions;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Systems;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.PizReb.Sector;

public sealed partial class PizRebSectorRuleSystem : GameRuleSystem<PizRebSectorRuleComponent>
{
    private static readonly Dictionary<string, string> JobFactions = new()
    {
        { "PizRebFactionNanotrasen", "Nanotrasen" },
        { "PizRebLeaderNanotrasen", "Nanotrasen" },
        { "PizRebFactionSyndicate", "Syndicate" },
        { "PizRebLeaderSyndicate", "Syndicate" },
        { "PizRebFactionGorlex", "Gorlex" },
        { "PizRebLeaderGorlex", "Gorlex" },
        { "PizRebFactionPirates", "Pirates" },
        { "PizRebLeaderPirates", "Pirates" },
        { "PizRebFactionIndependent", "Independent" },
    };

    private static readonly Dictionary<string, (EntProtoId Beacon, ProtoId<JobPrototype> Job)> FactionData = new()
    {
        { "Nanotrasen", ("PizRebBeaconNanotrasen", "PizRebFactionNanotrasen") },
        { "Syndicate", ("PizRebBeaconSyndicate", "PizRebFactionSyndicate") },
        { "Gorlex", ("PizRebBeaconGorlex", "PizRebFactionGorlex") },
        { "Pirates", ("PizRebBeaconPirate", "PizRebFactionPirates") },
        { "Independent", ("PizRebBeaconIndependent", "PizRebFactionIndependent") },
    };

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private PizRebPodSystem _pods = default!;
    [Dependency] private PizRebPartySystem _party = default!;
    [Dependency] private MindSystem _mind = default!;

    private PizRebSectorRuleComponent? _rule;
    private EntityUid? _epicenter;
    private MapId _mapId;
    private TimeSpan _openAt;
    private float _moteTimer;
    private readonly List<EntityUid> _rings = new();
    private readonly Dictionary<EntityUid, int> _playerLayers = new();
    private readonly Dictionary<NetUserId, EntityUid> _partyPods = new();
    private readonly List<NetUserId> _pendingPartySpawns = new();
    private readonly Dictionary<EntityUid, EntityUid> _ambienceStreams = new();
    private readonly Dictionary<EntityUid, int> _playerBands = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
    }

    protected override void Started(Entity<PizRebSectorRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        var component = ent.Comp1;
        _rule = component;
        _mapId = EntityManager.System<ServerGameTicker>().DefaultMap;

        var epicenter = Spawn(component.EpicenterPrototype, new MapCoordinates(component.EpicenterPosition, _mapId));
        EnsureComp<PizRebEpicenterComponent>(epicenter);
        _epicenter = epicenter;

        SpawnRings(component);

        _openAt = _timing.CurTime + component.EpicenterOpenTime;
        RaiseNetworkEvent(BuildStormEvent(component, _mapId));
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<PizRebSectorRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive((uid, gameRule)))
                continue;

            foreach (var session in ev.PlayerPool.ToArray())
            {
                if (!ev.Profiles.TryGetValue(session.UserId, out var profile))
                    continue;

                GameTicker.PlayerJoinGame(session, silent: true);

                var party = _party.GetParty(session.UserId);
                string faction;
                ProtoId<JobPrototype> job;

                if (party != null && party.Host == session.UserId)
                {
                    faction = PreferredFaction(profile) ?? party.Faction;
                    _party.SetFaction(session.UserId, faction);
                    job = BaseJob(faction);
                    _party.MarkHostSpawned(session.UserId);

                    foreach (var member in _party.GetReadyMembers(party))
                        _pendingPartySpawns.Add(member);
                }
                else if (party != null)
                {
                    faction = party.Faction;
                    job = BaseJob(faction);
                }
                else
                {
                    faction = PreferredFaction(profile) ?? "Independent";
                    job = PreferredJob(profile) ?? BaseJob(faction);
                }

                var pod = GetOrCreatePod(rule, session.UserId, faction);
                SpawnInPod(pod, session.UserId, profile, job, faction);
                ev.PlayerPool.Remove(session);
            }

            break;
        }
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.Player.AttachedEntity is { } attached && HasComp<GhostComponent>(attached))
            return;

        var query = EntityQueryEnumerator<PizRebSectorRuleComponent, GameRuleComponent>();

        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive((uid, gameRule)))
                continue;

            var party = _party.GetParty(ev.Player.UserId);
            string faction;
            ProtoId<JobPrototype> job;

            if (party != null && party.Host == ev.Player.UserId)
            {
                faction = FactionFromJob(ev.JobId) ?? party.Faction;
                _party.SetFaction(ev.Player.UserId, faction);
                job = BaseJob(faction);
            }
            else if (party != null)
            {
                faction = party.Faction;
                job = BaseJob(faction);
            }
            else
            {
                faction = FactionFromJob(ev.JobId) ?? "Independent";
                job = IsPizRebFactionJob(ev.JobId) ? new ProtoId<JobPrototype>(ev.JobId!) : BaseJob(faction);
            }

            var pod = GetOrCreatePod(rule, ev.Player.UserId, faction);
            SpawnInPod(pod, ev.Player.UserId, ev.Profile, job, faction);
            ev.Handled = true;

            if (party != null && party.Host == ev.Player.UserId)
            {
                _party.MarkHostSpawned(party.Host);

                foreach (var member in _party.GetReadyMembers(party))
                    _pendingPartySpawns.Add(member);
            }

            break;
        }
    }

    private void QueueReadyPartyMembers()
    {
        foreach (var party in _party.GetParties())
        {
            if (!_party.IsHostSpawned(party.Host) || !_partyPods.ContainsKey(party.Host))
                continue;

            foreach (var member in _party.GetReadyMembers(party))
            {
                if (!_players.TryGetSessionById(member, out var session) || session.AttachedEntity != null)
                    continue;

                if (!_pendingPartySpawns.Contains(member))
                    _pendingPartySpawns.Add(member);
            }
        }
    }

    private void ProcessPendingPartySpawns()
    {
        for (var i = _pendingPartySpawns.Count - 1; i >= 0; i--)
        {
            var userId = _pendingPartySpawns[i];

            if (!_players.TryGetSessionById(userId, out var session))
            {
                _pendingPartySpawns.RemoveAt(i);
                continue;
            }

            if (session.AttachedEntity is not null)
            {
                _pendingPartySpawns.RemoveAt(i);
                continue;
            }

            var party = _party.GetParty(userId);

            if (party == null || !_partyPods.TryGetValue(party.Host, out var pod) || !Exists(pod))
            {
                _pendingPartySpawns.RemoveAt(i);
                continue;
            }

            GameTicker.PlayerJoinGame(session, silent: true);

            var pendingFaction = party.Faction;
            SpawnInPod(pod, userId, HumanoidCharacterProfile.Random(), BaseJob(pendingFaction), pendingFaction);

            _party.Broadcast(party);
            _pendingPartySpawns.RemoveAt(i);
        }
    }

    private EntityUid GetOrCreatePod(PizRebSectorRuleComponent rule, NetUserId userId, string faction)
    {
        var party = _party.GetParty(userId);

        if (party != null && _partyPods.TryGetValue(party.Host, out var existing) && Exists(existing))
            return existing;

        var capacity = party?.Members.Count ?? rule.PodCapacity;
        var pod = CreatePod(rule, Math.Clamp(capacity, 1, 10), faction);

        if (party != null)
            _partyPods[party.Host] = pod;

        return pod;
    }

    private EntityUid CreatePod(PizRebSectorRuleComponent rule, int capacity, string faction)
    {
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var position = rule.EpicenterPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * rule.PodDistance;
        var beacon = FactionData.TryGetValue(faction, out var data) ? data.Beacon : FactionData["Independent"].Beacon;

        return _pods.CreatePod(_mapId, position, capacity, beacon);
    }

    private void SpawnInPod(EntityUid pod, NetUserId userId, HumanoidCharacterProfile profile, ProtoId<JobPrototype> job, string faction)
    {
        var mob = _stationSpawning.SpawnPlayerMob(_pods.GetSpawnCoordinates(pod), job, profile, null);
        var mind = _mind.CreateMind(userId, profile.Name);
        _mind.SetUserId(mind, userId);
        _mind.TransferTo(mind, mob);

        var factionComp = EnsureComp<PizRebFactionComponent>(mob);
        factionComp.Faction = faction;
    }

    private static string? PreferredFaction(HumanoidCharacterProfile profile)
    {
        return BestFactionJob(profile) is { } job && JobFactions.TryGetValue(job, out var faction) ? faction : null;
    }

    private static ProtoId<JobPrototype>? PreferredJob(HumanoidCharacterProfile profile)
    {
        if (BestFactionJob(profile) is not { } job)
            return null;

        return new ProtoId<JobPrototype>(job);
    }

    private static string? BestFactionJob(HumanoidCharacterProfile profile)
    {
        string? best = null;
        var bestPriority = -1;

        foreach (var (jobProto, priority) in profile.JobPriorities)
        {
            if (priority == JobPriority.Never)
                continue;

            if (!JobFactions.ContainsKey(jobProto.Id))
                continue;

            var value = (int) priority;

            if (value <= bestPriority)
                continue;

            bestPriority = value;
            best = jobProto.Id;
        }

        return best;
    }

    private static bool IsPizRebFactionJob(string? jobId)
    {
        return jobId != null && JobFactions.ContainsKey(jobId);
    }

    private static string? FactionFromJob(string? jobId)
    {
        return jobId != null && JobFactions.TryGetValue(jobId, out var faction) ? faction : null;
    }

    private static ProtoId<JobPrototype> BaseJob(string faction)
    {
        return FactionData.TryGetValue(faction, out var data) ? data.Job : FactionData["Independent"].Job;
    }

    private void SpawnRings(PizRebSectorRuleComponent component)
    {
        for (var i = 0; i < component.ZoneRadii.Count; i++)
        {
            var proto = i < component.StormRingPrototypes.Count
                ? component.StormRingPrototypes[i]
                : component.StormRingPrototypes[^1];

            var ring = Spawn(proto, new MapCoordinates(component.EpicenterPosition, _mapId));
            var ringComp = EnsureComp<PizRebStormRingComponent>(ring);
            ringComp.Radius = component.ZoneRadii[i];
            Dirty(ring, ringComp);
            _pvs.AddGlobalOverride(ring);
            _rings.Add(ring);
        }
    }

    protected override void Ended(Entity<PizRebSectorRuleComponent> rule, ref GameRuleEndedEvent args)
    {
        base.Ended(rule, ref args);

        _rule = null;
        _epicenter = null;
        _rings.Clear();
        _playerLayers.Clear();
        _playerBands.Clear();
        _partyPods.Clear();
        _pendingPartySpawns.Clear();

        foreach (var stream in _ambienceStreams.Values)
            _audio.Stop(stream);

        _ambienceStreams.Clear();
        _moteTimer = 0f;
        _openAt = TimeSpan.Zero;
        RaiseNetworkEvent(new PizRebStormEvent());
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_rule == null || _epicenter == null)
            return;

        RaiseNetworkEvent(BuildStormEvent(_rule, _mapId), ev.Player);
    }

    private static PizRebStormEvent BuildStormEvent(PizRebSectorRuleComponent component, MapId mapId)
    {
        return new PizRebStormEvent
        {
            Active = true,
            Position = component.EpicenterPosition,
            MapId = (int) mapId,
            Radii = component.ZoneRadii,
        };
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_rule is not { } rule || _epicenter is null)
            return;

        QueueReadyPartyMembers();
        ProcessPendingPartySpawns();
        UpdateRings(rule);
        UpdatePlayers(rule, frameTime);
        UpdateMotes(rule, frameTime);

        if (_openAt == TimeSpan.Zero || _timing.CurTime < _openAt)
            return;

        OpenEpicenter(rule);
    }

    private void UpdateRings(PizRebSectorRuleComponent rule)
    {
        var angle = new Angle((float) (_timing.CurTime.TotalSeconds * rule.RingRotationSpeed));

        foreach (var ring in _rings)
        {
            if (!Exists(ring))
                continue;

            _transform.SetLocalRotation(Transform(ring), angle);
        }
    }

    private void UpdatePlayers(PizRebSectorRuleComponent rule, float frameTime)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || !Exists(player))
                continue;

            var position = _transform.GetWorldPosition(player);
            var distance = Vector2.Distance(position, rule.EpicenterPosition);
            var layer = GetLayer(rule, distance);

            _playerLayers.TryGetValue(player, out var previousLayer);

            if (layer != previousLayer)
                UpdateAmbience(player, rule, layer);

            _playerLayers[player] = layer;

            if (layer < 0)
            {
                _playerBands[player] = -1;
                continue;
            }

            var band = GetBand(rule, distance);

            _playerBands.TryGetValue(player, out var previousBand);

            if (band >= 0 && band != previousBand)
                _audio.PlayPvs(rule.RingCrossSound, player);

            _playerBands[player] = band;

            var layerChance = ChanceAt(rule.LightningChancePerLayer, layer);

            if (_random.Prob(layerChance * frameTime))
            {
                var angle = _random.NextFloat(0f, MathF.PI * 2f);
                var strikePosition = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.NextFloat(10f, 25f);
                var strike = Spawn(rule.StormStrikePrototype, new MapCoordinates(strikePosition, _mapId));
                var target = player;

                if (Transform(player).GridUid is { } grid && HasComp<ShuttleComponent>(grid))
                    target = grid;

                _lightning.ShootLightning(strike, target);
            }

            if (rule.MeteorPrototypes.Count > 0 && _random.Prob(ChanceAt(rule.MeteorChancePerLayer, layer) * frameTime))
                SpawnMeteor(rule, position);

            if (_random.Prob(ChanceAt(rule.RodChancePerLayer, layer) * frameTime))
                SpawnRod(rule, position);
        }
    }

    private static int GetLayer(PizRebSectorRuleComponent rule, float distance)
    {
        var layer = -1;

        for (var i = 0; i < rule.ZoneRadii.Count; i++)
        {
            if (distance <= rule.ZoneRadii[i])
                layer = i;
        }

        return layer;
    }

    private static int GetBand(PizRebSectorRuleComponent rule, float distance)
    {
        for (var i = 0; i < rule.ZoneRadii.Count; i++)
        {
            if (MathF.Abs(distance - rule.ZoneRadii[i]) <= rule.BandWidth / 2f)
                return i;
        }

        return -1;
    }

    private static float ChanceAt(List<float> chances, int layer)
    {
        if (chances.Count == 0 || layer < 0)
            return 0f;

        return chances[Math.Min(layer, chances.Count - 1)];
    }

    private void SpawnMeteor(PizRebSectorRuleComponent rule, Vector2 target)
    {
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var spawnPosition = target + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 30f;
        var proto = rule.MeteorPrototypes[_random.Next(rule.MeteorPrototypes.Count)];
        var meteor = Spawn(proto, new MapCoordinates(spawnPosition, _mapId));

        if (!TryComp<PhysicsComponent>(meteor, out var physics))
            return;

        var direction = (target - spawnPosition).Normalized();
        _physics.ApplyLinearImpulse(meteor, direction * rule.MeteorVelocity * physics.Mass, body: physics);
    }

    private void SpawnRod(PizRebSectorRuleComponent rule, Vector2 target)
    {
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var spawnPosition = target + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 60f;
        var rod = Spawn(rule.RodPrototype, new MapCoordinates(spawnPosition, _mapId));
        var direction = target - spawnPosition;
        _transform.SetLocalRotation(Transform(rod), new Angle(MathF.Atan2(direction.Y, direction.X)));
    }

    private void UpdateAmbience(EntityUid player, PizRebSectorRuleComponent rule, int layer)
    {
        if (_ambienceStreams.Remove(player, out var oldStream))
            _audio.Stop(oldStream);

        if (layer < 0 || rule.PhaseAmbience.Count == 0)
            return;

        if (!_players.TryGetSessionByEntity(player, out var session))
            return;

        var sound = rule.PhaseAmbience[Math.Min(layer, rule.PhaseAmbience.Count - 1)];
        var audioParams = new AudioParams { Volume = rule.AmbienceVolume, Loop = true };

        if (_audio.PlayGlobal(sound, session, audioParams) is { } played)
            _ambienceStreams[player] = played.Entity;
    }

    private void UpdateMotes(PizRebSectorRuleComponent rule, float frameTime)
    {
        if (rule.ZoneRadii.Count == 0)
            return;

        _moteTimer += frameTime;

        while (_moteTimer >= rule.MoteInterval)
        {
            _moteTimer -= rule.MoteInterval;

            var radius = rule.ZoneRadii[_random.Next(rule.ZoneRadii.Count)];
            var angle = _random.NextFloat(0f, MathF.PI * 2f);

            var distance = _random.Prob(0.5f)
                ? radius + _random.NextFloat(-rule.BandWidth / 2f, rule.BandWidth / 2f)
                : radius * MathF.Sqrt(_random.NextFloat());

            var position = rule.EpicenterPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var mote = Spawn(rule.StormMotePrototype, new MapCoordinates(position, _mapId));
            _transform.SetLocalRotation(Transform(mote), new Angle(_random.NextFloat(0f, MathF.PI * 2f)));
        }
    }

    private Vector2 RandomRingPosition(PizRebSectorRuleComponent rule, float radius)
    {
        var angle = _random.NextFloat(0f, MathF.PI * 2f);
        var distance = radius + _random.NextFloat(-rule.BandWidth / 2f, rule.BandWidth / 2f);

        return rule.EpicenterPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    public bool TryGetStorm(out Vector2 position, out List<float> radii)
    {
        position = default;
        radii = new List<float>();

        if (_rule == null || _epicenter == null)
            return false;

        position = _rule.EpicenterPosition;
        radii = _rule.ZoneRadii;
        return true;
    }

    private void OpenEpicenter(PizRebSectorRuleComponent rule)
    {
        _openAt = TimeSpan.Zero;

        if (_epicenter is not { } epicenter)
            return;

        if (TryComp<PizRebEpicenterComponent>(epicenter, out var component) && component.Opened)
            return;

        if (component != null)
            component.Opened = true;

        _chat.DispatchServerAnnouncement(Loc.GetString("pizreb-sector-epicenter-open"));
        _audio.PlayPvs(rule.OpenSound, epicenter);
    }
}
