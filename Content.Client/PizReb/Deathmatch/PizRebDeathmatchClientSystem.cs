using System.Numerics;
using Content.Shared.PizReb.Deathmatch;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.PizReb.Deathmatch;

public sealed partial class PizRebDeathmatchClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly Color KillColor = Color.FromHex("#FFD633");
    private static readonly Color DeathColor = Color.FromHex("#FF4040");
    private static readonly Color DefaultColor = Color.FromHex("#EDEDED");

    private PizRebKillfeedControl? _killfeed;

    public bool DeathmatchActive { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PizRebDeathmatchStatusEvent>(OnStatus);
        SubscribeNetworkEvent<PizRebKillfeedEvent>(OnKillfeed);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        RemoveOverlays();
    }

    private void OnStatus(PizRebDeathmatchStatusEvent ev, EntitySessionEventArgs args)
    {
        DeathmatchActive = ev.Active;
        RemoveOverlays();

        if (!ev.Active)
            return;

        if (ev.KillfeedEnabled)
        {
            _killfeed = new PizRebKillfeedControl
            {
                Lifetime = TimeSpan.FromSeconds(ev.KillfeedLifetime),
                MaxEntries = ev.KillfeedMaxEntries,
            };

            _ui.PopupRoot.AddChild(_killfeed);
            LayoutContainer.SetAnchorPreset(_killfeed, LayoutContainer.LayoutPreset.CenterLeft);
            LayoutContainer.SetMarginLeft(_killfeed, 24);
        }

        if (ev.HealthPercentEnabled)
        {
            _overlays.AddOverlay(new PizRebHealthOverlay
            {
                Offset = ev.HealthTextOffset,
            });
        }
    }

    private void OnKillfeed(PizRebKillfeedEvent ev, EntitySessionEventArgs args)
    {
        if (_killfeed == null)
            return;

        var local = _players.LocalSession?.UserId;
        var color = DefaultColor;

        if (local != null && ev.HasKiller && ev.Killer == local.Value)
            color = KillColor;
        else if (local != null && ev.HasVictim && ev.Victim == local.Value)
            color = DeathColor;

        _killfeed.AddEntry(ev.Text, color);
    }

    private void RemoveOverlays()
    {
        _overlays.RemoveOverlay<PizRebHealthOverlay>();

        if (_killfeed == null)
            return;

        if (!_killfeed.Disposed)
            _killfeed.Orphan();

        _killfeed = null;
    }
}
