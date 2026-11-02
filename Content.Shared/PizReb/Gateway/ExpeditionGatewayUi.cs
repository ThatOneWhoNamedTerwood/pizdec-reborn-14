using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Gateway;

[NetSerializable, Serializable]
public enum ExpeditionGatewayUiKey : byte
{
    Key,
}

[NetSerializable, Serializable]
public sealed class ExpeditionGatewayBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Active;
    public string? ActiveMission;
    public TimeSpan NextOffer;
    public TimeSpan OfferInterval;
    public TimeSpan ReadyAt;
    public TimeSpan Cooldown;
    public List<string> Missions = new();
}

[NetSerializable, Serializable]
public sealed class ExpeditionGatewayClaimMissionMessage : BoundUserInterfaceMessage
{
    public string Mission = string.Empty;
}
