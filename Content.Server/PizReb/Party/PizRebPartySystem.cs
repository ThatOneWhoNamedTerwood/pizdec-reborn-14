using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared.PizReb.Party;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.PizReb.Party;

public sealed partial class PizRebPartySystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;

    private const int MaxMembers = 10;
    private const string FriendsFile = "pizreb_friends.json";

    public sealed class Party
    {
        public NetUserId Host;
        public string Faction = "Independent";
        public readonly HashSet<NetUserId> Members = new();
    }

    public static readonly string[] Factions =
    {
        "Nanotrasen",
        "Syndicate",
        "Gorlex",
        "Pirates",
        "Independent",
    };

    private readonly Dictionary<NetUserId, Party> _partiesByHost = new();
    private readonly Dictionary<NetUserId, Party> _partyByMember = new();
    private readonly Dictionary<NetUserId, NetUserId> _invites = new();
    private readonly Dictionary<NetUserId, HashSet<NetUserId>> _friends = new();
    private readonly Dictionary<NetUserId, HashSet<NetUserId>> _friendRequests = new();
    private readonly Dictionary<NetUserId, string> _names = new();
    private readonly Dictionary<NetUserId, bool> _ready = new();
    private readonly HashSet<NetUserId> _hostSpawned = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public override void Initialize()
    {
        base.Initialize();

        LoadFriends();

        SubscribeNetworkEvent<PizRebPartyCreateEvent>(OnCreate);
        SubscribeNetworkEvent<PizRebPartyStateRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<PizRebPartyInviteEvent>(OnInvite);
        SubscribeNetworkEvent<PizRebPartyInviteDeclineEvent>(OnDecline);
        SubscribeNetworkEvent<PizRebPartyAcceptEvent>(OnAccept);
        SubscribeNetworkEvent<PizRebPartyLeaveEvent>(OnLeave);
        SubscribeNetworkEvent<PizRebPartyKickEvent>(OnKick);
        SubscribeNetworkEvent<PizRebPartyFactionEvent>(OnFaction);
        SubscribeNetworkEvent<PizRebPartyReadyEvent>(OnReady);
        SubscribeNetworkEvent<PizRebFriendAddEvent>(OnFriendRequest);
        SubscribeNetworkEvent<PizRebFriendAcceptEvent>(OnFriendAccept);
        SubscribeNetworkEvent<PizRebFriendDeclineEvent>(OnFriendDecline);
        SubscribeNetworkEvent<PizRebFriendRemoveEvent>(OnFriendRemove);
        SubscribeNetworkEvent<PizRebPmSendEvent>(OnPmSend);
        SubscribeNetworkEvent<PizRebPartyChatSendEvent>(OnPartyChat);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAttached);
    }

    private void OnFaction(PizRebPartyFactionEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (!_partyByMember.TryGetValue(user, out var party) || party.Host != user)
            return;

        if (!Factions.Contains(ev.Faction))
            return;

        party.Faction = ev.Faction;
        Broadcast(party);
    }

    public void SetFaction(NetUserId host, string faction)
    {
        if (!_partiesByHost.TryGetValue(host, out var party) || !Factions.Contains(faction))
            return;

        if (party.Faction == faction)
            return;

        party.Faction = faction;
        Broadcast(party);
    }

    public string GetFaction(NetUserId user)
    {
        return _partyByMember.TryGetValue(user, out var party) ? party.Faction : "Independent";
    }

    public IEnumerable<Party> GetParties()
    {
        return _partiesByHost.Values;
    }

    public Party? GetParty(NetUserId user)
    {
        return _partyByMember.TryGetValue(user, out var party) ? party : null;
    }

    private void OnAttached(PlayerAttachedEvent ev)
    {
        Remember(ev.Player.UserId, ev.Player.Name);
        SendState(ev.Player.UserId);
    }

    private void OnStateRequest(PizRebPartyStateRequestEvent ev, EntitySessionEventArgs args)
    {
        SendState(args.SenderSession.UserId);
    }

    private void OnCreate(PizRebPartyCreateEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (_partyByMember.ContainsKey(user))
            return;

        var party = new Party { Host = user };
        party.Members.Add(user);
        _partiesByHost[user] = party;
        _partyByMember[user] = party;

        Broadcast(party);
    }

    private void OnInvite(PizRebPartyInviteEvent ev, EntitySessionEventArgs args)
    {
        var inviter = args.SenderSession.UserId;

        if (!_partyByMember.TryGetValue(inviter, out var party) || party.Host != inviter)
            return;

        Invite(inviter, party, ev.MemberName);
    }

    public void Invite(NetUserId inviter, Party party, string name)
    {
        if (party.Members.Count >= MaxMembers)
            return;

        var target = FindSession(name);

        if (target == null || _partyByMember.ContainsKey(target.UserId))
            return;

        _invites[target.UserId] = inviter;
        RaiseNetworkEvent(new PizRebPartyInvitedEvent { HostName = Display(inviter) }, target);
        SendState(target.UserId);
    }

    private void OnDecline(PizRebPartyInviteDeclineEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (_invites.Remove(user))
            SendState(user);
    }

    private void OnAccept(PizRebPartyAcceptEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (!_invites.Remove(user, out var host))
            return;

        if (!_partiesByHost.TryGetValue(host, out var party))
            return;

        if (party.Members.Count >= MaxMembers)
            return;

        party.Members.Add(user);
        _partyByMember[user] = party;
        Broadcast(party);
    }

    private void OnLeave(PizRebPartyLeaveEvent ev, EntitySessionEventArgs args)
    {
        RemoveFromParty(args.SenderSession.UserId);
    }

    private void OnReady(PizRebPartyReadyEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (!_partyByMember.TryGetValue(user, out var party))
            return;

        _ready[user] = ev.Ready;
        Broadcast(party);
    }

    public bool IsReady(NetUserId user)
    {
        return _ready.TryGetValue(user, out var ready) && ready;
    }

    public IEnumerable<NetUserId> GetReadyMembers(Party party)
    {
        return party.Members.Where(member => member != party.Host && IsReady(member));
    }

    public bool IsHostSpawned(NetUserId host)
    {
        return _hostSpawned.Contains(host);
    }

    public void MarkHostSpawned(NetUserId host)
    {
        _hostSpawned.Add(host);
    }

    private void OnKick(PizRebPartyKickEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;

        if (!_partyByMember.TryGetValue(user, out var party) || party.Host != user)
            return;

        var target = FindSession(ev.MemberName);

        if (target == null || target.UserId == user || !party.Members.Contains(target.UserId))
            return;

        RemoveFromParty(target.UserId);
    }

    private void RemoveFromParty(NetUserId user)
    {
        if (!_partyByMember.Remove(user, out var party))
            return;

        party.Members.Remove(user);
        _ready.Remove(user);
        SendState(user);

        if (party.Members.Count == 0)
        {
            _partiesByHost.Remove(party.Host);
            _hostSpawned.Remove(party.Host);
            return;
        }

        if (party.Host == user)
        {
            _partiesByHost.Remove(party.Host);
            _hostSpawned.Remove(party.Host);
            party.Host = party.Members.First();
            _partiesByHost[party.Host] = party;
        }

        Broadcast(party);
    }

    private void OnFriendRequest(PizRebFriendAddEvent ev, EntitySessionEventArgs args)
    {
        RequestFriend(args.SenderSession.UserId, ev.MemberName);
    }

    public void RequestFriend(NetUserId user, string name)
    {
        var target = FindSession(name);

        if (target == null || target.UserId == user)
            return;

        if (IsFriend(user, target.UserId))
            return;

        if (!_friendRequests.TryGetValue(target.UserId, out var requests))
        {
            requests = new HashSet<NetUserId>();
            _friendRequests[target.UserId] = requests;
        }

        requests.Add(user);
        RaiseNetworkEvent(new PizRebFriendRequestedEvent { From = Display(user) }, target);
        SendState(target.UserId);
    }

    private void OnFriendAccept(PizRebFriendAcceptEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        var requester = FindSession(ev.From);

        if (requester == null)
            return;

        if (_friendRequests.TryGetValue(user, out var requests))
            requests.Remove(requester.UserId);

        AddFriend(user, requester.UserId);
        AddFriend(requester.UserId, user);

        SaveFriends();
        SendState(user);
        SendState(requester.UserId);
    }

    private void OnFriendDecline(PizRebFriendDeclineEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        var requester = FindSession(ev.From);

        if (requester == null)
            return;

        if (_friendRequests.TryGetValue(user, out var requests))
            requests.Remove(requester.UserId);

        SendState(user);
    }

    private void OnFriendRemove(PizRebFriendRemoveEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        var target = FindSession(ev.MemberName);

        if (target == null)
            return;

        RemoveFriend(user, target.UserId);
        RemoveFriend(target.UserId, user);

        SaveFriends();
        SendState(user);
        SendState(target.UserId);
    }

    private void AddFriend(NetUserId user, NetUserId friend)
    {
        if (!_friends.TryGetValue(user, out var friends))
        {
            friends = new HashSet<NetUserId>();
            _friends[user] = friends;
        }

        friends.Add(friend);
    }

    private void RemoveFriend(NetUserId user, NetUserId friend)
    {
        if (_friends.TryGetValue(user, out var friends))
            friends.Remove(friend);
    }

    private bool IsFriend(NetUserId user, NetUserId other)
    {
        return _friends.TryGetValue(user, out var friends) && friends.Contains(other);
    }

    private void OnPmSend(PizRebPmSendEvent ev, EntitySessionEventArgs args)
    {
        var sender = args.SenderSession;
        var target = FindSession(ev.TargetName);

        if (target == null || string.IsNullOrWhiteSpace(ev.Text))
            return;

        var from = Display(sender.UserId);
        var text = ev.Text;

        RaiseNetworkEvent(new PizRebPmReceivedEvent { From = from, Text = text }, target);
        RaiseNetworkEvent(new PizRebPmReceivedEvent { From = from, Text = text }, sender);
    }

    private void OnPartyChat(PizRebPartyChatSendEvent ev, EntitySessionEventArgs args)
    {
        var sender = args.SenderSession;

        if (!_partyByMember.TryGetValue(sender.UserId, out var party) || string.IsNullOrWhiteSpace(ev.Text))
            return;

        var received = new PizRebPartyChatReceivedEvent { From = Display(sender.UserId), Text = ev.Text };

        foreach (var member in party.Members)
        {
            if (_players.TryGetSessionById(member, out var session))
                RaiseNetworkEvent(received, session);
        }
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!ev.CanAccess || !_players.TryGetSessionByEntity(ev.User, out var actor) ||
            !_players.TryGetSessionByEntity(ev.Target, out var target))
            return;

        if (actor.UserId == target.UserId)
            return;

        var targetName = Display(target.UserId);

        if (!IsFriend(actor.UserId, target.UserId))
        {
            ev.Verbs.Add(new Verb
            {
                Text = "Добавить в друзья",
                Act = () => RequestFriend(actor.UserId, targetName),
            });
        }

        if (_partyByMember.TryGetValue(actor.UserId, out var party) && party.Host == actor.UserId &&
            !party.Members.Contains(target.UserId))
        {
            ev.Verbs.Add(new Verb
            {
                Text = "Добавить в команду",
                Act = () => Invite(actor.UserId, party, targetName),
            });
        }
    }

    private ICommonSession? FindSession(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var query = name.Trim();

        foreach (var session in _players.Sessions)
        {
            Remember(session.UserId, session.Name);

            if (string.Equals(session.Name, query, StringComparison.OrdinalIgnoreCase))
                return session;

            if (string.Equals(ShortName(session.Name), query, StringComparison.OrdinalIgnoreCase))
                return session;

            if (string.Equals(session.UserId.ToString(), query, StringComparison.OrdinalIgnoreCase))
                return session;
        }

        return null;
    }

    private static string ShortName(string name)
    {
        var index = name.IndexOf('@');
        return index >= 0 ? name[(index + 1)..] : name;
    }

    private void Remember(NetUserId user, string name)
    {
        _names[user] = ShortName(name);
    }

    private string Display(NetUserId user)
    {
        if (_names.TryGetValue(user, out var name))
            return name;

        if (_players.TryGetSessionById(user, out var session))
        {
            Remember(user, session.Name);
            return ShortName(session.Name);
        }

        return user.ToString();
    }

    public void Broadcast(Party party)
    {
        foreach (var member in party.Members)
            SendState(member);
    }

    private void SendState(NetUserId user)
    {
        var inParty = _partyByMember.TryGetValue(user, out var party);
        var state = new PizRebPartyStateEvent { InParty = inParty, SelfReady = IsReady(user) };

        if (inParty && party != null)
        {
            state.HostName = Display(party.Host);
            state.IsHost = party.Host == user;
            state.HostSpawned = IsHostSpawned(party.Host);
            state.Faction = party.Faction;

            foreach (var member in party.Members)
            {
                state.Members.Add(Display(member));

                if (IsReady(member))
                    state.Ready.Add(Display(member));

                if (_players.TryGetSessionById(member, out var memberSession) &&
                    memberSession.AttachedEntity is { } attached)
                {
                    state.MemberEntities.Add(GetNetEntity(attached));
                }
            }
        }

        foreach (var invite in _invites.Where(pair => pair.Key == user))
            state.Invites.Add(Display(invite.Value));

        if (_friends.TryGetValue(user, out var friends))
        {
            foreach (var friend in friends)
                state.Friends.Add(Display(friend));
        }

        if (_friendRequests.TryGetValue(user, out var requests))
        {
            foreach (var request in requests)
                state.FriendRequests.Add(Display(request));
        }

        if (_players.TryGetSessionById(user, out var session))
            RaiseNetworkEvent(state, session);
    }

    private sealed class FriendsData
    {
        public Dictionary<string, List<string>> Friends { get; set; } = new();
        public Dictionary<string, string> Names { get; set; } = new();
    }

    private void LoadFriends()
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), FriendsFile);

            if (!File.Exists(path))
                return;

            var text = File.ReadAllText(path);
            FriendsData? data = null;

            try
            {
                data = JsonSerializer.Deserialize<FriendsData>(text, JsonOptions);
            }
            catch (JsonException)
            {
                var legacy = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(text, JsonOptions);

                if (legacy != null)
                {
                    data = new FriendsData { Friends = legacy };
                }
            }

            if (data == null)
                return;

            foreach (var (user, name) in data.Names)
            {
                if (Guid.TryParse(user, out var nameGuid))
                    _names[new NetUserId(nameGuid)] = name;
            }

            foreach (var (user, friends) in data.Friends)
            {
                if (!Guid.TryParse(user, out var userGuid))
                    continue;

                var userId = new NetUserId(userGuid);

                foreach (var friend in friends)
                {
                    if (Guid.TryParse(friend, out var friendGuid))
                        AddFriend(userId, new NetUserId(friendGuid));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load PizReb friends: {ex}");
        }
    }

    private void SaveFriends()
    {
        try
        {
            var data = new FriendsData
            {
                Friends = _friends.ToDictionary(
                    pair => pair.Key.ToString(),
                    pair => pair.Value.Select(friend => friend.ToString()).ToList()),
                Names = _names.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            };

            var path = Path.Combine(Directory.GetCurrentDirectory(), FriendsFile);
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save PizReb friends: {ex}");
        }
    }
}
