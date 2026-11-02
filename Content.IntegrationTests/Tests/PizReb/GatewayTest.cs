using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.PizReb.Gateway;
using Content.Server.Construction.Components;
using Content.Server.Power.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.PizReb.Boss;
using Content.Shared.PizReb.Gateway;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.PizReb;

[TestFixture]
public sealed class GatewayTest : GameTest
{
    [Test]
    public async Task MissionMapsLoadableTest()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var mapLoader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        await server.WaitPost(() =>
        {
            var res = server.ResolveDependency<IResourceManager>();
            var options = new Robust.Shared.EntitySerialization.DeserializationOptions { InitializeMaps = true };
            foreach (var mission in prototypes.EnumeratePrototypes<GatewayMissionPrototype>())
            {
                var text = res.ContentFileReadAllText(mission.MapPath.ToString());
                var index = text.IndexOf("category:", StringComparison.Ordinal);
                var isMap = index >= 0 && text.Substring(index, Math.Min(32, text.Length - index)).Contains("Map");
                if (isMap)
                {
                    Assert.That(mapLoader.TryLoadMap(mission.MapPath, out var loadedMap, out _, options), $"Failed to load {mission.MapPath}");
                    mapSystem.DeleteMap(loadedMap!.Value.Comp.MapId);
                }
                else
                {
                    mapSystem.CreateMap(out var mapId);
                    Assert.That(mapLoader.TryLoadGrid(mapId, mission.MapPath, out _, options), $"Failed to load {mission.MapPath}");
                    mapSystem.DeleteMap(mapId);
                }
            }
        });
    }

    [Test]
    public async Task MissionFlowTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid gate = default;
        await server.WaitPost(() =>
        {
            gate = entMan.SpawnEntity("ExpeditionGateway", testMap.GridCoords);
            entMan.GetComponent<ApcPowerReceiverComponent>(gate).NeedsPower = false;
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            var gateway = entMan.GetComponent<ExpeditionGatewayComponent>(gate);
            Assert.That(gateway.Offered, Is.Not.Empty);
            Assert.That(entMan.GetComponent<ApcPowerReceiverComponent>(gate).Load, Is.EqualTo(gateway.IdlePowerLoad));
            Assert.That(entMan.GetComponent<TransformComponent>(gate).Anchored, Is.True);
            entMan.EventBus.RaiseLocalEvent(gate, new ExpeditionGatewayClaimMissionMessage
            {
                Mission = "GatewayMissionTerminal",
                Actor = gate,
            });
            Assert.That(gateway.Mission, Is.Not.Null);
            Assert.That(entMan.HasComponent<GatewayPortalComponent>(gate), Is.True);
            Assert.That(entMan.HasComponent<MapAtmosphereComponent>(gateway.Mission!.Value), Is.True);
            var mission = entMan.GetComponent<GatewayMissionComponent>(gateway.Mission!.Value);
            Assert.That(entMan.GetComponent<ApcPowerReceiverComponent>(gate).Load, Is.EqualTo(gateway.ActivePowerLoad));
            Assert.That(mission.SpawnPoints, Is.Not.Empty);
            Assert.That(mission.ReturnPortal, Is.Not.Null);
            Assert.That(mission.Objective, Is.Not.Null);
            Assert.That(entMan.HasComponent<ComputerComponent>(mission.Objective!.Value), Is.True);
            var missionMapId = entMan.GetComponent<TransformComponent>(gateway.Mission.Value).MapID;
            Assert.That(missionMapId, Is.Not.EqualTo(testMap.MapId));
            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var gatewaySystem = entMan.System<ExpeditionGatewaySystem>();
            Assert.That(gatewaySystem.TrySendToMission(gate, mob), Is.True);
            Assert.That(entMan.GetComponent<TransformComponent>(mob).MapID, Is.EqualTo(missionMapId));
            var objective = mission.Objective!.Value;
            entMan.EventBus.RaiseLocalEvent(objective, new ActivateInWorldEvent(mob, objective, true));
            Assert.That(mission.ObjectiveDone, Is.True);
            Assert.That(gatewaySystem.ReturnFromMission(mob), Is.True);
            Assert.That(entMan.GetComponent<TransformComponent>(mob).MapID, Is.EqualTo(testMap.MapId));
            Assert.That(mission.RewardGiven, Is.True);
            Assert.That(gateway.Mission, Is.Null);
            Assert.That(entMan.GetComponent<ApcPowerReceiverComponent>(gate).Load, Is.EqualTo(gateway.IdlePowerLoad));
            Assert.That(gateway.NextReady, Is.GreaterThan(server.Timing.CurTime));
            entMan.EventBus.RaiseLocalEvent(gate, new ExpeditionGatewayClaimMissionMessage { Mission = "GatewayMissionCarp", Actor = gate });
            Assert.That(gateway.Mission, Is.Null);
        });
        await server.WaitRunTicks(2);
    }

    [Test]
    public async Task PowerConnectionTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid gate = default;
        await server.WaitPost(() =>
        {
            gate = entMan.SpawnEntity("ExpeditionGateway", testMap.GridCoords);
            entMan.SpawnEntity("CableApcExtension", testMap.GridCoords);
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            var receiver = entMan.GetComponent<ExtensionCableReceiverComponent>(gate);
            Assert.That(receiver.Provider, Is.Not.Null);
        });
    }

    [Test]
    public async Task PortalCollisionTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        var mapSystem = entMan.System<SharedMapSystem>();
        var xformSystem = entMan.System<SharedTransformSystem>();
        var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
        var plating = tileDefinitions["Plating"].TileId;
        EntityUid gate = default;
        EntityUid mob = default;
        await server.WaitPost(() =>
        {
            var grid = mapSystem.CreateGridEntity(testMap.MapId);
            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 5; y++)
                    mapSystem.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(plating));
            }
            gate = entMan.SpawnEntity("ExpeditionGateway", new EntityCoordinates(grid.Owner, 2.5f, 2.5f));
            entMan.GetComponent<ApcPowerReceiverComponent>(gate).NeedsPower = false;
        });
        await server.WaitRunTicks(3);
        await server.WaitPost(() =>
        {
            var gateway = entMan.GetComponent<ExpeditionGatewayComponent>(gate);
            gateway.Offered.Clear();
            gateway.Offered.Add("GatewayMissionCarp");
            entMan.EventBus.RaiseLocalEvent(gate, new ExpeditionGatewayClaimMissionMessage
            {
                Mission = "GatewayMissionCarp",
                Actor = gate,
            });
            Assert.That(gateway.Mission, Is.Not.Null);
            mob = entMan.SpawnEntity("MobHuman",
                entMan.GetComponent<TransformComponent>(gate).Coordinates.Offset(new Vector2(-1f, 0f)));
        });
        await server.WaitRunTicks(2);
        await server.WaitPost(() =>
        {
            xformSystem.SetCoordinates(mob, entMan.GetComponent<TransformComponent>(gate).Coordinates);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var portal = entMan.GetComponent<FixturesComponent>(gate).Fixtures["portalFixture"];
            Assert.That(portal.Contacts.Count, Is.GreaterThan(0), "The gateway portal does not collide with mobs");
        });
    }

    [Test]
    public async Task KillMissionSpawnMarkerTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid gate = default;
        await server.WaitPost(() =>
        {
            gate = entMan.SpawnEntity("ExpeditionGateway", testMap.GridCoords);
            entMan.GetComponent<ApcPowerReceiverComponent>(gate).NeedsPower = false;
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            var gateway = entMan.GetComponent<ExpeditionGatewayComponent>(gate);
            gateway.Offered.Clear();
            gateway.Offered.Add("GatewayMissionCarp");
            entMan.EventBus.RaiseLocalEvent(gate, new ExpeditionGatewayClaimMissionMessage
            {
                Mission = "GatewayMissionCarp",
                Actor = gate,
            });
            Assert.That(gateway.Mission, Is.Not.Null);
            var mission = entMan.GetComponent<GatewayMissionComponent>(gateway.Mission!.Value);
            Assert.That(mission.Objective, Is.Not.Null);
            Assert.That(entMan.HasComponent<MobStateComponent>(mission.Objective!.Value), Is.True);
            var xform = entMan.GetComponent<TransformComponent>(mission.Objective.Value);
            Assert.That(xform.LocalPosition.X, Is.EqualTo(20.5f).Within(0.6f));
            Assert.That(xform.LocalPosition.Y, Is.EqualTo(20.5f).Within(0.6f));
        });
    }

    [Test]
    public async Task BurstManMissionSpawnTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid gate = default;
        await server.WaitPost(() =>
        {
            gate = entMan.SpawnEntity("ExpeditionGateway", testMap.GridCoords);
            entMan.GetComponent<ApcPowerReceiverComponent>(gate).NeedsPower = false;
        });
        await server.WaitRunTicks(3);
        await server.WaitAssertion(() =>
        {
            var gateway = entMan.GetComponent<ExpeditionGatewayComponent>(gate);
            gateway.Offered.Clear();
            gateway.Offered.Add("GatewayMissionBurstMan");
            entMan.EventBus.RaiseLocalEvent(gate, new ExpeditionGatewayClaimMissionMessage
            {
                Mission = "GatewayMissionBurstMan",
                Actor = gate,
            });
            Assert.That(gateway.Mission, Is.Not.Null);
            var mission = entMan.GetComponent<GatewayMissionComponent>(gateway.Mission!.Value);
            Assert.That(mission.Objective, Is.Not.Null);
            Assert.That(entMan.HasComponent<BossComponent>(mission.Objective!.Value), Is.True);
            var missionMapId = entMan.GetComponent<TransformComponent>(gateway.Mission.Value).MapID;
            var markerQuery = entMan.EntityQueryEnumerator<GatewayMissionKillSpawnComponent, TransformComponent>();
            EntityCoordinates? markerCoords = null;
            while (markerQuery.MoveNext(out _, out _, out var markerXform))
            {
                if (markerXform.MapID != missionMapId)
                    continue;
                markerCoords = markerXform.Coordinates;
                break;
            }
            Assert.That(markerCoords, Is.Not.Null);
            var objectiveXform = entMan.GetComponent<TransformComponent>(mission.Objective.Value);
            Assert.That(objectiveXform.Coordinates, Is.EqualTo(markerCoords!.Value));
        });
    }
}


