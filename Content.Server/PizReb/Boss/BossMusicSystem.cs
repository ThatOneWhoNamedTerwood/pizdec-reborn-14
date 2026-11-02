using System.Linq;
using Content.Shared.PizReb.Boss;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Boss;

public sealed partial class BossMusicSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<BossComponent> ent, ref ComponentShutdown args)
    {
        StopStreams(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + TimeSpan.FromSeconds(0.5);
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var boss))
        {
            if (!ProtoMan.TryIndex(boss.Boss, out var prototype))
                continue;
            if (!boss.Active)
            {
                StopStreams(boss);
                continue;
            }
            if (boss.MusicPhase != boss.Phase)
            {
                StopStreams(boss);
                boss.MusicPhase = boss.Phase;
            }
            var music = boss.Phase < prototype.Phases.Count ? prototype.Phases[boss.Phase].Music : null;
            if (music == null)
            {
                StopStreams(boss);
                continue;
            }
            var bossXform = Transform(uid);
            var bossPosition = _transform.GetWorldPosition(bossXform);
            var desired = new List<ICommonSession>();
            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } mob)
                    continue;
                if (!TryComp(mob, out TransformComponent? xform) || xform.MapID != bossXform.MapID)
                    continue;
                if ((_transform.GetWorldPosition(xform) - bossPosition).Length() > prototype.DisplayRange)
                    continue;
                desired.Add(session);
            }
            foreach (var (userId, stream) in boss.MusicStreams.ToArray())
            {
                if (desired.Exists(x => x.UserId == userId))
                    continue;
                _audio.Stop(stream);
                boss.MusicStreams.Remove(userId);
            }
            foreach (var session in desired)
            {
                if (boss.MusicStreams.ContainsKey(session.UserId))
                    continue;
                var played = _audio.PlayGlobal(music, Filter.Empty().AddPlayer(session), recordReplay: false,
                    new AudioParams().WithVolume(-5f).WithLoop(true));
                if (played != null)
                    boss.MusicStreams[session.UserId] = played.Value.Entity;
            }
        }
    }

    private void StopStreams(BossComponent boss)
    {
        foreach (var stream in boss.MusicStreams.Values)
            _audio.Stop(stream);
        boss.MusicStreams.Clear();
    }
}

