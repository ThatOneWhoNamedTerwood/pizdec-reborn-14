using System.Numerics;
using Content.Client.Resources;
using Content.Shared.PizReb.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.PizReb.Boss;

public sealed partial class BossBarOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly Dictionary<int, Font> _fonts = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public BossBarOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _sprite = _entMan.System<SpriteSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _players.LocalEntity != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } local)
            return;
        if (!_entMan.TryGetComponent<TransformComponent>(local, out var localXform))
            return;
        var localPosition = _transform.GetWorldPosition(localXform);
        var localMap = localXform.MapID;

        EntityUid? best = null;
        BossComponent? bestComponent = null;
        BossPrototype? bestPrototype = null;
        var bestDistance = float.MaxValue;
        var query = _entMan.AllEntityQueryEnumerator<BossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boss, out var xform))
        {
            if (!boss.Active || xform.MapID != localMap)
                continue;
            if (!_proto.TryIndex(boss.Boss, out var prototype))
                continue;
            var distance = (_transform.GetWorldPosition(xform) - localPosition).Length();
            if (distance > prototype.DisplayRange || distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = uid;
            bestComponent = boss;
            bestPrototype = prototype;
        }
        if (best == null || bestComponent == null || bestPrototype == null)
            return;

        var bounds = args.ViewportBounds;
        var viewportWidth = bounds.Width;
        var viewportHeight = bounds.Height;
        var height = Math.Clamp(viewportHeight * 0.05f, 24f, 56f);
        var width = Math.Clamp(viewportWidth * 0.55f, 320f, 900f);
        var iconSize = height * 1.45f;
        var left = (viewportWidth - width) / 2f + iconSize * 0.35f;
        var top = viewportHeight * 0.03f;
        var right = left + width;
        var bottom = top + height;
        var handle = args.ScreenHandle;

        var background = new Color(0.07f, 0.07f, 0.08f, 0.72f);
        var border = new Color(0.75f, 0.75f, 0.75f, 0.55f);
        var fillColor = new Color(0.62f, 0.09f, 0.09f, 0.90f);
        handle.DrawRect(new UIBox2(left, top, right, bottom), background);
        var fraction = bestComponent.MaxHealth <= 0f
            ? 0f
            : Math.Clamp(bestComponent.Health / bestComponent.MaxHealth, 0f, 1f);
        if (fraction > 0f)
            handle.DrawRect(new UIBox2(left, top, left + width * fraction, bottom), fillColor);
        handle.DrawRect(new UIBox2(left, top, right, bottom), border, false);

        var icon = _sprite.Frame0(bestPrototype.Icon);
        var iconLeft = left - iconSize * 0.85f;
        var iconTop = top + (height - iconSize) / 2f;
        handle.DrawTextureRect(icon, new UIBox2(iconLeft, iconTop, iconLeft + iconSize, iconTop + iconSize));

        var text = MathF.Round(bestComponent.Health).ToString("0");
        var font = GetFont((int)MathF.Max(height * 0.6f, 12f));
        var textSize = handle.GetDimensions(font, text, 1f);
        var textPosition = new Vector2(
            (left + right) / 2f - textSize.X / 2f,
            (top + bottom) / 2f - textSize.Y / 2f);
        handle.DrawString(font, textPosition, text, 1f, Color.White, TextOutline.Default);
    }

    private Font GetFont(int size)
    {
        if (_fonts.TryGetValue(size, out var font))
            return font;
        font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", size);
        _fonts[size] = font;
        return font;
    }
}

