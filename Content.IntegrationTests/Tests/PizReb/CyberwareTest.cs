using Content.IntegrationTests.Fixtures;
using Content.Server.PizReb.Cyberware;
using Content.Shared.FixedPoint;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.PizReb;

[TestFixture]
public sealed class CyberwareTest : GameTest
{
    [Test]
    public async Task ImplantInstallTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid mob = default;
        EntityUid gorilla = default;
        EntityUid leroi = default;
        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            gorilla = entMan.SpawnEntity("CyberImplantGorillaArms", testMap.GridCoords);
            leroi = entMan.SpawnEntity("CyberImplantLeroisTendons", testMap.GridCoords);
        });
        await server.WaitRunTicks(3);
        var cyberware = entMan.System<CyberwareSystem>();
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<CyberwareComponent>(mob);
            comp.Capacity = 35;
            Assert.That(comp.Capacity, Is.EqualTo(35));
            Assert.That(cyberware.TryInstallImplant(mob, gorilla, out _), Is.True);
            Assert.That(cyberware.TryInstallImplant(mob, leroi, out _), Is.True);
            Assert.That(comp.BrainLoad, Is.EqualTo(12));
            Assert.That(comp.SpeedMultiplier, Is.EqualTo(1.2f).Within(0.001f));
            var melee = entMan.GetComponent<MeleeWeaponComponent>(mob);
            Assert.That(melee.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(cyberware.TryRemoveImplant(mob, gorilla), Is.True);
            Assert.That(comp.BrainLoad, Is.EqualTo(4));
            Assert.That(melee.Damage.GetTotal(), Is.LessThan(FixedPoint2.New(25)));
        });
    }

    [Test]
    [Explicit]
    public async Task AutoSurgeonOperationTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid table = default;
        EntityUid mob = default;
        EntityUid implant = default;
        EntityUid mask = default;
        EntityUid tank = default;
        await server.WaitPost(() =>
        {
            table = entMan.SpawnEntity("AutoSurgeonTable", testMap.GridCoords);
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            implant = entMan.SpawnEntity("CyberImplantGorillaArms", testMap.GridCoords);
            mask = entMan.SpawnEntity("ClothingMaskBreath", testMap.GridCoords);
            tank = entMan.SpawnEntity("NitrousOxideTankFilled", testMap.GridCoords);
        });
        await server.WaitRunTicks(3);
        var system = entMan.System<Content.Server.PizReb.Cyberware.AutoSurgeonSystem>();
        await server.WaitAssertion(() =>
        {
            var tableComp = entMan.GetComponent<Content.Server.PizReb.Cyberware.AutoSurgeonComponent>(table);
            tableComp.SleepDelay = TimeSpan.FromSeconds(1);
            tableComp.OpenTime = TimeSpan.FromSeconds(1);
            tableComp.PrepareTime = TimeSpan.FromSeconds(1);
            tableComp.CloseTime = TimeSpan.FromSeconds(1);
            tableComp.Patient = mob;
            entMan.GetComponent<CyberImplantComponent>(implant).InstallTime = TimeSpan.FromSeconds(1);
            system.InsertItem((table, tableComp), implant);
            system.InsertItem((table, tableComp), mask);
            system.InsertItem((table, tableComp), tank);
            Assert.That(system.TryStartOperation((table, tableComp), mob, AutoSurgeonMode.Install, implant, null, out var error), Is.True, error);
            Assert.That(tableComp.Operating, Is.True);
        });
        await server.WaitRunTicks(20);
        await server.WaitAssertion(() =>
        {
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(mob, "mask", out _), Is.True);
        });
        await server.WaitRunTicks(400);
        await server.WaitAssertion(() =>
        {
            var tableComp = entMan.GetComponent<Content.Server.PizReb.Cyberware.AutoSurgeonComponent>(table);
            var cyber = entMan.GetComponent<CyberwareComponent>(mob);
            Assert.That(tableComp.Operating, Is.False);
            Assert.That(cyber.Implants, Does.Contain(implant));
            Assert.That(tableComp.Contents, Does.Not.Contain(implant));
            Assert.That(tableComp.Durability, Is.LessThan(100f));
            var inventory = entMan.System<Content.Shared.Inventory.InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(mob, "mask", out _), Is.False);
        });
    }

    [Test]
    public async Task CyberwareActionGrantTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var testMap = await pair.CreateTestMap();
        EntityUid mob = default;
        EntityUid implant = default;
        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            implant = entMan.SpawnEntity("CyberImplantProjectileSystem", testMap.GridCoords);
        });
        await server.WaitRunTicks(3);
        var system = entMan.System<CyberwareSystem>();
        await server.WaitAssertion(() =>
        {
            Assert.That(system.TryInstallImplant(mob, implant, out _), Is.True);
            Assert.That(entMan.TryGetComponent<Content.Shared.Actions.Components.ActionsContainerComponent>(mob, out var container), Is.True);
            Assert.That(container.Container.ContainedEntities.Count, Is.GreaterThan(0));
        });
    }
}
