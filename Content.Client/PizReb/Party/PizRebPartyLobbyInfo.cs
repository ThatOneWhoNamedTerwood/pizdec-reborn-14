namespace Content.Client.PizReb.Party;

public static class PizRebPartyLobbyInfo
{
    public static bool Active;
    public static bool SelfReady;
    public static Action<bool>? SetReady;
}
