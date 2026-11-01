using System.Numerics;
using Content.Client.Resources;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost.Components;

using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PizReb.Deathmatch;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;


namespace Content.Client.PizReb.Deathmatch;

public sealed partial class PizRebHealthOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly DamageableSystem _damageable;
    private readonly MobThresholdSystem _thresholds;
    private readonly MobStateSystem _mobState;
    private readonly SharedTransformSystem _transform;
    private readonly SharedInteractionSystem _interaction;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public float Offset = 0.3f;

    private const float HeadHeight = 1.8f;

    public PizRebHealthOverlay()
    {
        IoCManager.InjectDependencies(this);

        _damageable = _entMan.System<DamageableSystem>();
        _thresholds = _entMan.System<MobThresholdSystem>();
        _mobState = _entMan.System<MobStateSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
        _interaction = _entMan.System<SharedInteractionSystem>();
        _font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.MapId != MapId.Nullspace && _players.LocalEntity != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_players.LocalEntity is not { } local)
            return;

        var query = _entMan.AllEntityQueryEnumerator<PizRebDeathmatchFighterComponent, MobStateComponent, DamageableComponent, MobThresholdsComponent>();
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        var viewportSize = args.Viewport.Size;

        while (query.MoveNext(out var uid, out _, out var mobState, out var damageable, out var thresholds))
        {
            if (_entMan.HasComponent<GhostComponent>(uid))
                continue;

            var xform = _entMan.GetComponent<TransformComponent>(uid);

            if (xform.MapID != args.MapId)
                continue;

            if (!_mobState.IsAlive(uid, mobState) && !_mobState.IsCritical(uid, mobState))
                continue;

            if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold, thresholds) || critThreshold == null)
                continue;

            if (uid != local && !_interaction.InRangeUnobstructed(
                    new Entity<TransformComponent?>(local, null),
                    new Entity<TransformComponent?>(uid, null),
                    range: 0f))
                continue;

            var percent = GetPercent(uid, mobState, damageable, critThreshold.Value);
            var color = GetColor(percent);
            var text = $"{percent}%";

            var worldPosition = _transform.GetWorldPosition(xform);
            var rawScreen = _eye.WorldToScreen(worldPosition);
            var meterPixels = (_eye.WorldToScreen(worldPosition + new Vector2(1f, 0f)) - rawScreen).Length();
            var screenPosition = rawScreen - new Vector2(0f, (HeadHeight + Offset) * meterPixels);

            var dimensions = args.ScreenHandle.GetDimensions(_font, text, uiScale);
            var position = screenPosition - new Vector2(dimensions.X / 2f, dimensions.Y);

            if (position.X < 0f || position.X > viewportSize.X || position.Y < 0f || position.Y > viewportSize.Y)
                continue;

            args.ScreenHandle.DrawString(_font, position, text, uiScale, color, TextOutline.Default);
        }
    }

    private int GetPercent(EntityUid uid, MobStateComponent mobState, DamageableComponent damageable, FixedPoint2 critThreshold)
    {
        if (_mobState.IsCritical(uid, mobState))
            return 0;

        var total = _damageable.GetTotalDamage((uid, damageable));
        var ratio = 1f - (total / critThreshold).Float();
        return Math.Clamp((int)MathF.Round(ratio * 100f), 0, 100);
    }

    private static Color GetColor(int percent)
    {
        if (percent <= 0)
            return Color.FromHex("#C61E1E");

        if (percent <= 30)
            return Color.FromHex("#FF4040");

        if (percent <= 60)
            return Color.FromHex("#FFD633");

        return Color.FromHex("#5CF25C");
    }
}

