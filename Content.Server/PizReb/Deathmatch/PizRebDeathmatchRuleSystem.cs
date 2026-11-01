using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.Points;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.PizReb.Deathmatch;
using Content.Shared.Points;
using Content.Shared.Preferences;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.PizReb.Deathmatch;

public sealed partial class PizRebDeathmatchRuleSystem : GameRuleSystem<PizRebDeathmatchRuleComponent>
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private PointSystem _points = default!;
    [Dependency] private ServerStationJobsSystem _stationJobs = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private TagSystem _tags = default!;

    private readonly Dictionary<NetUserId, int> _streaks = new();
    private readonly Dictionary<NetUserId, TimeSpan> _respawnQueue = new();
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _profiles = new();
    private readonly Dictionary<NetUserId, EntityUid> _bodies = new();
    private readonly Dictionary<NetUserId, EntityUid> _stations = new();
    private readonly List<EntProtoId> _weapons = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_respawnQueue.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var user in _respawnQueue.Keys.ToArray())
        {
            if (_respawnQueue[user] > now)
                continue;

            RespawnPlayer(user);
        }
    }

    protected override void Started(Entity<PizRebDeathmatchRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        RefreshWeapons(ent.Comp1);
        _streaks.Clear();
        SetupStationJobs(ent.Comp1);
        RaiseNetworkEvent(BuildStatus(ent.Comp1));
    }

    protected override void Ended(Entity<PizRebDeathmatchRuleComponent> rule, ref GameRuleEndedEvent args)
    {
        base.Ended(rule, ref args);

        _streaks.Clear();
        _respawnQueue.Clear();
        _profiles.Clear();
        _bodies.Clear();
        _stations.Clear();
        _weapons.Clear();
        RaiseNetworkEvent(new PizRebDeathmatchStatusEvent());
    }

    private void SetupStationJobs(PizRebDeathmatchRuleComponent component)
    {
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out var jobs))
        {
            foreach (var job in jobs.JobList.Keys.ToList())
            {
                _stationJobs.TrySetJobSlot(station, job, 0, stationJobs: jobs);
            }

            _stationJobs.MakeJobUnlimited(station, component.FighterJob.Id, stationJobs: jobs);
        }
    }

    private void RefreshWeapons(PizRebDeathmatchRuleComponent component)
    {
        _weapons.Clear();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract)
                continue;

            if (!_tags.HasTag(prototype, component.WeaponTag))
                continue;

            if (InheritsWeaponTag(prototype, component.WeaponTag))
                continue;

            _weapons.Add(prototype.ID);
        }
    }

    private bool InheritsWeaponTag(EntityPrototype prototype, ProtoId<TagPrototype> tag)
    {
        if (prototype.Parents == null)
            return false;

        foreach (var parentId in prototype.Parents)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(parentId, out var parent))
                continue;

            if (_tags.HasTag(parent, tag) || InheritsWeaponTag(parent, tag))
                return true;
        }

        return false;
    }

    private PizRebDeathmatchStatusEvent BuildStatus(PizRebDeathmatchRuleComponent component)
    {
        return new PizRebDeathmatchStatusEvent
        {
            Active = true,
            KillfeedEnabled = component.KillfeedEnabled,
            HealthPercentEnabled = component.HealthPercentEnabled,
            KillfeedAnchor = component.KillfeedAnchor,
            KillfeedLifetime = (float)component.KillfeedLifetime.TotalSeconds,
            KillfeedMaxEntries = component.KillfeedMaxEntries,
            HealthTextOffset = component.HealthTextOffset,
        };
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var dm, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;

            RaiseNetworkEvent(BuildStatus(dm), ev.Player);
            break;
        }
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, PointManagerComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var dm, out var point, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((ruleUid, rule)))
                continue;

            if (_station.GetStations().FirstOrNull() is not { } station)
                break;

            var players = ev.PlayerPool.ToArray();

            foreach (var player in players)
            {
                if (!ev.Profiles.TryGetValue(player.UserId, out var profile))
                    continue;

                GameTicker.PlayerJoinGame(player, silent: true);

                var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(station, dm.FighterJob, profile);
                if (mobMaybe is not { } mob)
                    continue;

                var newMind = _mind.CreateMind(player.UserId, profile.Name);
                _mind.SetUserId(newMind, player.UserId);
                _mind.TransferTo(newMind, mob);

                _profiles[player.UserId] = profile;
                _stations[player.UserId] = station;
                SetupFighter(mob, player.UserId, ruleUid, dm, point);

                ev.PlayerPool.Remove(player);
            }

            break;
        }
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)    {
        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, PointManagerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var dm, out var point, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;

            _profiles[ev.Player.UserId] = ev.Profile;
            _stations[ev.Player.UserId] = ev.Station;

            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);

            var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(ev.Station, dm.FighterJob, ev.Profile);
            if (mobMaybe is not { } mob)
                break;

            _mind.TransferTo(newMind, mob);
            SetupFighter(mob, ev.Player.UserId, uid, dm, point);

            ev.Handled = true;
            break;
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, PointManagerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var dm, out var point, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;

            _profiles[ev.Player.UserId] = ev.Profile;
            _stations[ev.Player.UserId] = ev.Station;
            SetupFighter(ev.Mob, ev.Player.UserId, uid, dm, point);
            break;
        }
    }

    private void SetupFighter(EntityUid mob, NetUserId userId, EntityUid ruleUid, PizRebDeathmatchRuleComponent dm, PointManagerComponent point)
    {
        EnsureComp<KillTrackerComponent>(mob);
        _points.EnsurePlayer(userId, ruleUid, point);
        _streaks.Remove(userId);
        _bodies[userId] = mob;

        if (HasComp<PizRebDeathmatchFighterComponent>(mob))
            return;

        EnsureComp<PizRebDeathmatchFighterComponent>(mob);
        GiveWeapon(mob, dm);
        _audio.PlayPvs(dm.RespawnSound, mob);
    }

    private void GiveWeapon(EntityUid mob, PizRebDeathmatchRuleComponent component)
    {
        if (_weapons.Count == 0)
            return;

        var coordinates = Transform(mob).Coordinates;
        var proto = _random.Pick(_weapons);
        var weapon = Spawn(proto, coordinates);
        _hands.TryPickupAnyHand(mob, weapon, checkActionBlocker: false);

        if (!component.WeaponExtras.TryGetValue(proto, out var extras))
            return;

        foreach (var extra in extras)
        {
            var extraEntity = Spawn(extra, coordinates);
            _hands.TryPickupAnyHand(mob, extraEntity, checkActionBlocker: false);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!HasComp<PizRebDeathmatchFighterComponent>(args.Target))
            return;

        if (!TryComp<ActorComponent>(args.Target, out var actor))
            return;

        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var dm, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;

            QueueRespawn(actor.PlayerSession, args.Target, dm);
            break;
        }
    }

    private void QueueRespawn(ICommonSession session, EntityUid body, PizRebDeathmatchRuleComponent component)
    {
        var user = session.UserId;
        _bodies[user] = body;

        if (_respawnQueue.ContainsKey(user))
            return;

        _respawnQueue[user] = _timing.CurTime + component.RespawnDelay;

        var message = Loc.GetString("rule-respawn-in-seconds", ("second", (int)component.RespawnDelay.TotalSeconds));
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, body, false, session.Channel, Color.LimeGreen);
    }

    private void RespawnPlayer(NetUserId user)
    {
        _respawnQueue.Remove(user);

        if (!_players.TryGetSessionById(user, out var session))
            return;

        if (!_profiles.TryGetValue(user, out var profile))
            return;

        if (!_stations.TryGetValue(user, out var station))
        {
            if (_station.GetStations().FirstOrNull() is not { } fallback)
                return;

            station = fallback;
        }

        if (_bodies.TryGetValue(user, out var body) && EntityManager.EntityExists(body))
            QueueDel(body);

        _bodies.Remove(user);

        var hasMind = _mind.TryGetMind(session, out var mindId, out var mind);

        if (hasMind && mind?.OwnedEntity is { } owned && EntityManager.EntityExists(owned))
            QueueDel(owned);

        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, PointManagerComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var dm, out var point, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((ruleUid, rule)))
                continue;

            var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(station, dm.FighterJob, profile);
            if (mobMaybe is not { } mob)
                break;

            if (hasMind)
            {
                _mind.TransferTo(mindId, mob);
            }
            else
            {
                var newMind = _mind.CreateMind(user, profile.Name);
                _mind.SetUserId(newMind, user);
                _mind.TransferTo(newMind, mob);
            }

            SetupFighter(mob, user, ruleUid, dm, point);
            break;
        }
    }

    private void OnKillReported(ref KillReportedEvent ev)
    {
        var query = EntityQueryEnumerator<PizRebDeathmatchRuleComponent, PointManagerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var dm, out var point, out var rule))
        {
            if (!GameTicker.IsGameRuleActive((uid, rule)))
                continue;

            if (TryComp<ActorComponent>(ev.Entity, out var victimActor))
                _streaks.Remove(victimActor.PlayerSession.UserId);

            if (ev.Primary is not KillPlayerSource player)
            {
                _points.AdjustPointValue(ev.Entity, -1, uid, point);
                BroadcastKillfeed(dm, ev, null);
                break;
            }

            _points.AdjustPointValue(player.PlayerId, 1, uid, point);

            if (ev.Assist is KillPlayerSource assist)
                _points.AdjustPointValue(assist.PlayerId, 1, uid, point);

            var streak = _streaks.GetValueOrDefault(player.PlayerId) + 1;
            _streaks[player.PlayerId] = streak;
            AnnounceKillStreak(dm, uid, player.PlayerId, streak);

            BroadcastKillfeed(dm, ev, player);
            break;
        }
    }

    private void BroadcastKillfeed(PizRebDeathmatchRuleComponent component, KillReportedEvent ev, KillPlayerSource? killer)
    {
        if (!component.KillfeedEnabled)
            return;

        var text = GetCallout(component, ev);
        if (string.IsNullOrEmpty(text))
            return;

        var message = new PizRebKillfeedEvent
        {
            Text = text,
        };

        if (killer != null)
        {
            message.HasKiller = true;
            message.Killer = killer.PlayerId;
        }

        if (TryComp<ActorComponent>(ev.Entity, out var victim))
        {
            message.HasVictim = true;
            message.Victim = victim.PlayerSession.UserId;
        }

        RaiseNetworkEvent(message);
    }

    private string GetCallout(PizRebDeathmatchRuleComponent component, KillReportedEvent ev)
    {
        if (ev.Primary is KillEnvironmentSource || ev.Suicide)
        {
            if (component.SelfKillCalloutAmount <= 0)
                return string.Empty;

            var selfCallout = $"{component.SelfKillCalloutPrefix}{_random.Next(component.SelfKillCalloutAmount)}";
            return Loc.GetString(selfCallout, ("victim", GetName(ev.Entity)));
        }

        if (component.KillCalloutAmount <= 0)
            return string.Empty;

        var primary = GetName(ev.Primary);
        var killerString = primary;

        if (ev.Assist != null)
        {
            var secondary = GetName(ev.Assist);
            killerString = Loc.GetString("death-match-assist", ("primary", primary), ("secondary", secondary));
        }

        var callout = $"{component.KillCalloutPrefix}{_random.Next(component.KillCalloutAmount)}";
        return Loc.GetString(callout, ("killer", killerString), ("victim", GetName(ev.Entity)));
    }

    private string GetName(KillSource source)
    {
        switch (source)
        {
            case KillPlayerSource player:
                if (_players.TryGetSessionById(player.PlayerId, out var session) && session.AttachedEntity is { } attached)
                    return MetaData(attached).EntityName;
                break;
            case KillNpcSource npc:
                if (EntityManager.EntityExists(npc.NpcEnt))
                    return MetaData(npc.NpcEnt).EntityName;
                break;
        }

        return string.Empty;
    }

    private string GetName(EntityUid uid)
    {
        return EntityManager.EntityExists(uid) ? MetaData(uid).EntityName : string.Empty;
    }

    private void AnnounceKillStreak(PizRebDeathmatchRuleComponent component, EntityUid ruleUid, NetUserId player, int streak)
    {
        var announcement = FindKillStreak(component, streak);
        if (announcement == null || announcement.Messages.Count == 0)
            return;

        var name = GetPlayerName(player);
        if (string.IsNullOrEmpty(name))
            return;

        var message = Loc.GetString(_random.Pick(announcement.Messages), ("player", name));
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToAll(ChatChannel.Server, message, wrapped, ruleUid, false, true, Color.Red);
    }

    private static KillStreakAnnouncement? FindKillStreak(PizRebDeathmatchRuleComponent component, int streak)
    {
        foreach (var announcement in component.KillStreaks)
        {
            if (announcement.Kills == streak)
                return announcement;
        }

        if (streak <= 15 || streak % 5 != 0)
            return null;

        foreach (var announcement in component.KillStreaks)
        {
            if (announcement.Kills == 15)
                return announcement;
        }

        return null;
    }

    private string GetPlayerName(NetUserId id)
    {
        if (_players.TryGetSessionById(id, out var session) && session.AttachedEntity is { } attached)
            return MetaData(attached).EntityName;

        return _players.TryGetPlayerData(id, out var data) ? data.UserName : string.Empty;
    }

    protected override void AppendRoundEndText(Entity<PizRebDeathmatchRuleComponent> entity, ref RoundEndTextAppendEvent args)
    {
        if (!TryComp<PointManagerComponent>(entity, out var point))
            return;

        args.AddLine(Loc.GetString("point-scoreboard-header"));
        args.AddLine(new FormattedMessage(point.Scoreboard).ToMarkup());
        args.AddLine("");
    }
}
