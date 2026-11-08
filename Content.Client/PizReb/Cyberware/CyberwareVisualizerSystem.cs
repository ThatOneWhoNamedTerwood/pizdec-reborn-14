using Content.Shared.Humanoid;
using Content.Shared.PizReb.Cyberware;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Client.Graphics;

namespace Content.Client.PizReb.Cyberware;

public sealed partial class CyberwareVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberwareComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyberwareComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<CyberwareComponent> ent, ref ComponentStartup args)
    {
        Apply(ent.Owner, ent.Comp);
    }

    private void OnState(Entity<CyberwareComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent.Owner, ent.Comp);
    }

    private void Apply(EntityUid uid, CyberwareComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        foreach (var visual in comp.Visuals)
        {
            var layer = visual.Layer switch
            {
                CyberwareVisualLayer.Head => HumanoidVisualLayers.Head,
                CyberwareVisualLayer.Torso => HumanoidVisualLayers.Chest,
                CyberwareVisualLayer.Eyes => HumanoidVisualLayers.Eyes,
                CyberwareVisualLayer.ArmLeft => HumanoidVisualLayers.LArm,
                CyberwareVisualLayer.ArmRight => HumanoidVisualLayers.RArm,
                CyberwareVisualLayer.LegLeft => HumanoidVisualLayers.LLeg,
                CyberwareVisualLayer.LegRight => HumanoidVisualLayers.RLeg,
                _ => HumanoidVisualLayers.Special,
            };
            if (!_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                continue;
            if (visual.Color is { } color)
                _sprite.LayerSetColor((uid, sprite), index, color);
            if (!string.IsNullOrEmpty(visual.State))
                _sprite.LayerSetRsiState((uid, sprite), index, new RSI.StateId(visual.State));
        }
    }
}



