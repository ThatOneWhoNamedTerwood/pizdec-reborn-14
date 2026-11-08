using System.Numerics;
using Content.Shared.Spawners.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.PizReb.Sector;

public sealed partial class PizRebPodSystem : EntitySystem
{
    private static readonly ResPath BasePath = new("/Maps/PizReb/free_base.yml");

    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public EntityUid CreatePod(MapId mapId, Vector2 position, int capacity, EntProtoId beacon)
    {
        if (!_loader.TryLoadGrid(mapId, BasePath, out var grid, offset: position))
            return EntityUid.Invalid;

        var pod = EnsureComp<PizRebPodComponent>(grid.Value.Owner);
        pod.Faction = beacon.Id;
        pod.Capacity = capacity;

        Spawn(beacon, GetSpawnCoordinates(grid.Value.Owner));

        return grid.Value.Owner;
    }

    public EntityCoordinates GetSpawnCoordinates(EntityUid pod)
    {
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == pod)
                return xform.Coordinates;
        }

        return new EntityCoordinates(pod, Vector2.Zero);
    }
}
