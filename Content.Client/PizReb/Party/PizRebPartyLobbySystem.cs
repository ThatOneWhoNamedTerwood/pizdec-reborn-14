using Content.Client.GameTicking;
using Content.Client.Lobby;
using Content.Shared.PizReb.Party;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client.PizReb.Party;

public sealed partial class PizRebPartyLobbySystem : EntitySystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IStateManager _state = default!;

    private PizRebPartyStateEvent? _party;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PizRebPartyStateEvent>(OnState);

        PizRebPartyLobbyInfo.SetReady = pressed =>
            _net.SendSystemNetworkMessage(new PizRebPartyReadyEvent { Ready = pressed });
    }

    private void OnState(PizRebPartyStateEvent ev, EntitySessionEventArgs args)
    {
        _party = ev;
        Sync();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_state.CurrentState is LobbyState)
            Sync();
    }

    private void Sync()
    {
        var active = _party is { InParty: true, IsHost: false } && _ticker.IsGameStarted;

        if (PizRebPartyLobbyInfo.Active == active &&
            PizRebPartyLobbyInfo.SelfReady == (_party?.SelfReady ?? false))
        {
            return;
        }

        PizRebPartyLobbyInfo.Active = active;
        PizRebPartyLobbyInfo.SelfReady = _party?.SelfReady ?? false;

        if (_state.CurrentState is LobbyState lobby)
            lobby.RefreshLobbyUi();
    }
}
