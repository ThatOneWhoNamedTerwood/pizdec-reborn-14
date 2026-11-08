using Content.Shared.PizReb.Sector;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client.PizReb.Sector;

public sealed partial class PizRebStormClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private PizRebStormNoiseOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PizRebStormEvent>(OnStorm);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnStorm(PizRebStormEvent ev, EntitySessionEventArgs args)
    {
        _overlay ??= new PizRebStormNoiseOverlay();

        _overlay.Active = ev.Active;
        _overlay.Position = ev.Position;
        _overlay.MapId = ev.MapId;
        _overlay.Radii = ev.Radii;

        if (!ev.Active)
        {
            _overlays.RemoveOverlay(_overlay);
            return;
        }

        if (!_overlays.HasOverlay<PizRebStormNoiseOverlay>())
            _overlays.AddOverlay(_overlay);
    }
}
