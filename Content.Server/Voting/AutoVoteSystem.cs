using System;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Voting;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Voting;

/// <summary>
///     После окончания раунда запускает голосование за режим, затем за карту, затем перезапускает раунд.
/// </summary>
public sealed partial class AutoVoteSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _mapManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    private bool _enabled = true;
    private bool _running;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.AutoVoteEnabled, value => _enabled = value, true);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PostRound)
        {
            _running = false;

            if (!_enabled)
                return;

            StartPresetVote();
            return;
        }

        _running = false;
    }

    private void StartPresetVote()
    {
        if (_running)
            return;

        if (_playerManager.PlayerCount == 0)
        {
            Finish();
            return;
        }

        _running = true;

        if (!TryCreateVote(StandardVoteType.Preset, out var vote))
        {
            StartMapVote();
            return;
        }

        vote.OnFinished += (_, _) => Defer(StartMapVote);
    }

    private void StartMapVote()
    {
        if (!TryCreateVote(StandardVoteType.Map, out var vote))
        {
            Finish();
            return;
        }

        vote.OnFinished += (_, args) =>
        {
            var winner = args.Winner;

            Defer(() =>
            {
                if (winner is GameMapPrototype map)
                    _mapManager.SelectMap(map.ID);

                Finish();
            });
        };
    }

    private bool TryCreateVote(StandardVoteType type, out IVoteHandle vote)
    {
        _voteManager.CreateStandardVote(null, type);

        var created = _voteManager.ActiveVotes.LastOrDefault();

        if (created == null)
        {
            vote = default!;
            return false;
        }

        vote = created;
        return true;
    }

    private static void Defer(Action action)
    {
        Timer.Spawn(TimeSpan.FromMilliseconds(100), action);
    }

    private void Finish()
    {
        _running = false;
        _ticker.RestartRound();
    }
}
