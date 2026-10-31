using Content.Server.Corvax.Ghost;
using Content.Shared.Administration;
using Content.Shared.Corvax.CCCVars;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server.Corvax.Ghost;

[AnyCommand]
public sealed class GhostRespawnCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _ent = default!;

    public string Command => "ghostrespawn";
    public string Description => "Вернуться в лобби после смерти, если таймер ожидания истёк.";
    public string Help => "ghostrespawn";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("Команда доступна только игроку.");
            return;
        }

        var system = _ent.System<GhostGoLobbySystem>();
        system.TryGoLobby(player, shell.WriteError);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
