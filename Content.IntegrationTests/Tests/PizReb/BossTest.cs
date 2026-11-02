using Content.IntegrationTests.Fixtures;
using Content.Server.PizReb.Boss;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.PizReb.Boss;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.PizReb;

[TestFixture]
public sealed class BossTest : GameTest
{
    [Test]
    public async Task BossPhaseTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        var damageable = entMan.System<DamageableSystem>();
        EntityUid boss = default;
        EntityUid marker = default;
        await server.WaitPost(() =>
        {
            marker = entMan.SpawnEntity("BossPhaseTwoMarker", testMap.GridCoords.Offset(new System.Numerics.Vector2(3f, 0f)));
            boss = entMan.SpawnEntity("MobTenHourBurstMan", testMap.GridCoords);
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var bossComponent = entMan.GetComponent<BossComponent>(boss);
            Assert.That(bossComponent.Active, Is.True);
            Assert.That(bossComponent.MaxHealth, Is.EqualTo(1500f));
            Assert.That(bossComponent.Health, Is.EqualTo(1500f));
            Assert.That(bossComponent.Phase, Is.EqualTo(0));
            Assert.That(entMan.HasComponent<Content.Shared.Body.Components.BloodstreamComponent>(boss), Is.False);
            Assert.That(entMan.HasComponent<Content.Shared.Damage.Components.SlowOnDamageComponent>(boss), Is.False);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(800f);
            damageable.TryChangeDamage(boss, damage, ignoreResistances: true);
        });
        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var bossComponent = entMan.GetComponent<BossComponent>(boss);
            Assert.That(bossComponent.Health, Is.LessThan(800f));
            Assert.That(bossComponent.Transitioning, Is.True);
            var bossXform = entMan.GetComponent<TransformComponent>(boss);
            var markerXform = entMan.GetComponent<TransformComponent>(marker);
            Assert.That(bossXform.Coordinates, Is.EqualTo(markerXform.Coordinates));
        });
        await server.WaitRunTicks(220);
        await server.WaitAssertion(() =>
        {
            var bossComponent = entMan.GetComponent<BossComponent>(boss);
            Assert.That(bossComponent.Transitioning, Is.False);
            Assert.That(bossComponent.Phase, Is.EqualTo(1));
            Assert.That(bossComponent.Health, Is.EqualTo(1500f));
            Assert.That(bossComponent.DamageTakenMultiplier, Is.EqualTo(2f));
        });
    }

    [Test]
    public async Task BossDarknessTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid block = default;
        EntityUid mob = default;
        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            block = entMan.SpawnEntity("BossDarknessBlock", testMap.GridCoords);
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            boss = entMan.SpawnEntity("MobTenHourBurstMan", testMap.GridCoords.Offset(new System.Numerics.Vector2(5f, 0f)));
        });
        await server.WaitRunTicks(15);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(mob), Is.False);
            Assert.That(entMan.EntityExists(boss), Is.True);
        });
    }

    [Test]
    public async Task BossSummonsTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid parasite = default;
        await server.WaitPost(() =>
        {
            parasite = entMan.SpawnEntity("MobBossHoloparasite", testMap.GridCoords);
            entMan.SpawnEntity("BossArcaneBarrage", testMap.GridCoords);
            entMan.SpawnEntity("BossShockwave", testMap.GridCoords);
            entMan.SpawnEntity("MobTenHourBurstMan", testMap.GridCoords.Offset(new System.Numerics.Vector2(2f, 0f)));
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(parasite), Is.True);
        });
    }

    [Test]
    public async Task BossProjectileTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("MobTenHourBurstMan", testMap.GridCoords);
        });
        await server.WaitRunTicks(3);
        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(boss).Coordinates;
            var bullet = entMan.SpawnEntity("PelletShotgun", coords.Offset(new System.Numerics.Vector2(-3f, 0f)));
            entMan.System<Content.Shared.Weapons.Ranged.Systems.SharedGunSystem>()
                .ShootProjectile(bullet, new System.Numerics.Vector2(1f, 0f), System.Numerics.Vector2.Zero, null, null, 20f);
        });
        await server.WaitRunTicks(25);
        await server.WaitAssertion(() =>
        {
            var damageable = entMan.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(boss);
            var total = entMan.System<DamageableSystem>().GetTotalDamage((boss, damageable));
            Assert.That(total, Is.GreaterThan(FixedPoint2.Zero));
        });
    }
}








