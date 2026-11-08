using Content.Shared.Hands;
using Content.Shared.PizReb.Cyberware;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.PizReb.Cyberware;

public sealed partial class CyberwareGunSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<GunComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<GunComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!TryComp<CyberwareComponent>(args.User, out var cyber) || cyber.SpreadMultiplier >= 1f)
            return;
        var mod = EnsureComp<GunSpreadModifierComponent>(ent.Owner);
        var tracked = EnsureComp<CyberwareGunSpreadComponent>(ent.Owner);
        tracked.Original = mod.Spread;
        mod.Spread = tracked.Original * cyber.SpreadMultiplier;
        Dirty(ent.Owner, mod);
    }

    private void OnUnequipped(Entity<GunComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!TryComp<CyberwareGunSpreadComponent>(ent.Owner, out var tracked))
            return;
        if (TryComp<GunSpreadModifierComponent>(ent.Owner, out var mod))
        {
            mod.Spread = tracked.Original;
            Dirty(ent.Owner, mod);
        }
        RemComp<CyberwareGunSpreadComponent>(ent.Owner);
    }
}

[RegisterComponent]
public sealed partial class CyberwareGunSpreadComponent : Component
{
    public float Original = 1f;
}
