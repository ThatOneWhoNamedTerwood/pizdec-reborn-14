using Content.Server.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PizReb.Boss;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.PizReb.Boss;

[RegisterComponent]
public sealed partial class BossDarknessComponent : Component
{
    [DataField]
    public SoundSpecifier DeleteSound = new SoundPathSpecifier("/Audio/Effects/gateway_off.ogg");

    [DataField]
    public float Radius = 0.6f;

    [DataField]
    public float Interval = 0.15f;

    public TimeSpan NextCheck;
}

public sealed partial class BossDarknessSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BossDarknessComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var block, out var xform))
        {
            if (now < block.NextCheck)
                continue;
            block.NextCheck = now + TimeSpan.FromSeconds(block.Interval);
            var coords = xform.Coordinates;
            foreach (var other in _lookup.GetEntitiesInRange(coords, block.Radius, LookupFlags.Dynamic))
            {
                if (other == uid || HasComp<BossComponent>(other) || HasComp<GhostComponent>(other))
                    continue;
                if (!TryComp<MobStateComponent>(other, out var mobState) || _mobState.IsDead(other, mobState))
                    continue;
                Kill(other, uid, block);
            }
        }
    }

    private void Kill(EntityUid victim, EntityUid block, BossDarknessComponent component)
    {
        _audio.PlayPvs(component.DeleteSound, block);
        if (TryComp<MindContainerComponent>(victim, out var container) && container.Mind is { } mind)
            _ghost.OnGhostAttempt(mind, true);
        QueueDel(victim);
    }
}




