using Robust.Client.Graphics;

namespace Content.Client.PizReb.Boss;

public sealed partial class BossBarSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new BossBarOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<BossBarOverlay>();
    }
}
