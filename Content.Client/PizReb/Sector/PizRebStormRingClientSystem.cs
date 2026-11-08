using System.Numerics;
using Content.Client.Clickable;
using Content.Shared.PizReb.Sector;
using Robust.Client.GameObjects;

namespace Content.Client.PizReb.Sector;

public sealed partial class PizRebStormRingClientSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PizRebStormRingComponent, ComponentStartup>(OnRingStartup);
        SubscribeLocalEvent<PizRebStormRingComponent, AfterAutoHandleStateEvent>(OnRingState);
    }

    private void OnRingStartup(Entity<PizRebStormRingComponent> ent, ref ComponentStartup args)
    {
        RemComp<ClickableComponent>(ent);
        ApplyScale(ent);
    }

    private void OnRingState(Entity<PizRebStormRingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyScale(ent);
    }

    private void ApplyScale(Entity<PizRebStormRingComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent, sprite), new Vector2(ent.Comp.Radius / 4f, ent.Comp.Radius / 4f));
    }
}
