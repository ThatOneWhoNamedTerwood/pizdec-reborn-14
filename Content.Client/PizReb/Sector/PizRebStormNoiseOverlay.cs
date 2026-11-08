using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Client.PizReb.Sector;

public sealed partial class PizRebStormNoiseOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Texture _static;

    private static readonly float[] LayerAlpha = { 0f, 0.02f, 0.04f, 0.07f, 0.12f };

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public bool Active;
    public Vector2 Position;
    public int MapId;
    public List<float> Radii = new();

    public PizRebStormNoiseOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _static = _cache.GetResource<TextureResource>("/Textures/PizReb/Storm/static.png");
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Active && _players.LocalEntity != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!Active || _players.LocalEntity is not { } local)
            return;

        if (!_entMan.TryGetComponent<TransformComponent>(local, out var xform) || (int) xform.MapID != MapId)
            return;

        var distance = Vector2.Distance(_transform.GetWorldPosition(xform), Position);
        var layer = -1;

        for (var i = 0; i < Radii.Count; i++)
        {
            if (distance <= Radii[i])
                layer = i;
        }

        if (layer <= 0)
            return;

        var alpha = LayerAlpha[Math.Min(layer, LayerAlpha.Length - 1)];
        var offset = new Vector2(_random.NextFloat(-3f, 3f), _random.NextFloat(-3f, 3f));
        var bounds = new UIBox2(Vector2.Zero + offset, args.Viewport.Size + offset);

        args.ScreenHandle.DrawTextureRect(_static, bounds, new Color(1f, 1f, 1f, alpha));
    }
}
