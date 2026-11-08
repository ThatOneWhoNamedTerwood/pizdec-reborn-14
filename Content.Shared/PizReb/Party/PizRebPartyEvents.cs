using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Party;

[Serializable, NetSerializable]
public sealed class PizRebPartyStateEvent : EntityEventArgs
{
    public bool InParty;
    public string HostName = string.Empty;
    public bool IsHost;
    public List<string> Members = new();
    public List<NetEntity> MemberEntities = new();
    public List<string> Invites = new();
    public List<string> Friends = new();
    public List<string> FriendRequests = new();
    public List<string> Ready = new();
    public bool SelfReady;
    public bool HostSpawned;
    public string Faction = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyFactionEvent : EntityEventArgs
{
    public string Faction = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyReadyEvent : EntityEventArgs
{
    public bool Ready;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyCreateEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PizRebPartyStateRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PizRebPartyInviteEvent : EntityEventArgs
{
    public string MemberName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyInvitedEvent : EntityEventArgs
{
    public string HostName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyInviteDeclineEvent : EntityEventArgs
{
    public string HostName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyAcceptEvent : EntityEventArgs
{
    public string HostName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyLeaveEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PizRebPartyKickEvent : EntityEventArgs
{
    public string MemberName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebFriendAddEvent : EntityEventArgs
{
    public string MemberName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebFriendRequestedEvent : EntityEventArgs
{
    public string From = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebFriendAcceptEvent : EntityEventArgs
{
    public string From = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebFriendDeclineEvent : EntityEventArgs
{
    public string From = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebFriendRemoveEvent : EntityEventArgs
{
    public string MemberName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPmSendEvent : EntityEventArgs
{
    public string TargetName = string.Empty;
    public string Text = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPmReceivedEvent : EntityEventArgs
{
    public string From = string.Empty;
    public string Text = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyChatSendEvent : EntityEventArgs
{
    public string Text = string.Empty;
}

[Serializable, NetSerializable]
public sealed class PizRebPartyChatReceivedEvent : EntityEventArgs
{
    public string From = string.Empty;
    public string Text = string.Empty;
}
