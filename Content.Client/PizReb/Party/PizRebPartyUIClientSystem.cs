using System;
using System.Linq;
using Content.Shared.PizReb.Party;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.PizReb.Party;

public sealed partial class PizRebPartyUIClientSystem : EntitySystem
{
    private const string OutlineShaderId = "PizRebPartyOutline";

    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private PizRebPartyWindow? _window;
    private ShaderInstance? _outlineShader;
    private readonly HashSet<EntityUid> _outlined = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PizRebPartyStateEvent>(OnState);
        SubscribeNetworkEvent<PizRebPmReceivedEvent>(OnPm);
        SubscribeNetworkEvent<PizRebPartyChatReceivedEvent>(OnPartyChat);
        SubscribeNetworkEvent<PizRebFriendRequestedEvent>(OnFriendRequested);
        SubscribeNetworkEvent<PizRebPartyInvitedEvent>(OnPartyInvited);
        _console.RegisterCommand("party", "Открыть окно команды", "", (_, _, _) => Open());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ClearOutlines();
        _outlineShader?.Dispose();
    }

    private void Open()
    {
        _window ??= new PizRebPartyWindow(Send);
        _net.SendSystemNetworkMessage(new PizRebPartyStateRequestEvent());
        _window.OpenCentered();
    }

    private void Send(EntityEventArgs ev)
    {
        _net.SendSystemNetworkMessage(ev);
    }

    private void OnState(PizRebPartyStateEvent ev, EntitySessionEventArgs args)
    {
        _window?.UpdateState(ev);
        UpdateOutlines(ev.MemberEntities);
    }

    private void UpdateOutlines(List<NetEntity> members)
    {
        var desired = new HashSet<EntityUid>();

        foreach (var member in members)
        {
            if (!TryGetEntity(member, out var uidNullable) || uidNullable is not { } uid)
                continue;

            desired.Add(uid);
        }

        foreach (var uid in _outlined.ToArray())
        {
            if (desired.Contains(uid) && !Deleted(uid))
                continue;

            RemoveOutline(uid);
        }

        foreach (var uid in desired)
            AddOutline(uid);
    }

    private void AddOutline(EntityUid uid)
    {
        if (uid == _players.LocalEntity || _outlined.Contains(uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(OutlineShaderId, GetOutlineShader()));
        _outlined.Add(uid);
    }

    private void RemoveOutline(EntityUid uid)
    {
        if (_outlined.Remove(uid) && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.RemovePostShader((uid, sprite), OutlineShaderId);
    }

    private void ClearOutlines()
    {
        foreach (var uid in _outlined.ToArray())
            RemoveOutline(uid);
    }

    private ShaderInstance GetOutlineShader()
    {
        if (_outlineShader != null)
            return _outlineShader;

        var shader = _proto.Index<ShaderPrototype>("SelectionOutline").InstanceUnique();
        shader.SetParameter("outline_width", 3f);
        shader.SetParameter("outline_color", new Color(1f, 0.58f, 0.1f, 0.9f));
        _outlineShader = shader;
        return shader;
    }

    private void OnPm(PizRebPmReceivedEvent ev, EntitySessionEventArgs args)
    {
        _window?.AddPm(ev.From, ev.Text);
    }

    private void OnPartyChat(PizRebPartyChatReceivedEvent ev, EntitySessionEventArgs args)
    {
        _window?.AddPartyChat(ev.From, ev.Text);
    }

    private void OnFriendRequested(PizRebFriendRequestedEvent ev, EntitySessionEventArgs args)
    {
        ShowRequest(
            $"Игрок {ev.From} приглашает вас в друзья",
            new PizRebFriendAcceptEvent { From = ev.From },
            new PizRebFriendDeclineEvent { From = ev.From });
    }

    private void OnPartyInvited(PizRebPartyInvitedEvent ev, EntitySessionEventArgs args)
    {
        ShowRequest(
            $"Игрок {ev.HostName} приглашает вас в команду",
            new PizRebPartyAcceptEvent { HostName = ev.HostName },
            new PizRebPartyInviteDeclineEvent { HostName = ev.HostName });
    }

    private void ShowRequest(string text, EntityEventArgs accept, EntityEventArgs decline)
    {
        var popup = new PizRebRequestPopup(text, () => Send(accept), () => Send(decline));
        popup.OpenCentered();
    }
}

public sealed class PizRebRequestPopup : DefaultWindow
{
    public PizRebRequestPopup(string text, Action accept, Action decline)
    {
        Title = "Запрос";
        MinWidth = 320;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        root.AddChild(new Label { Text = text });

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var yes = new Button { Text = "Принять" };
        yes.OnPressed += _ =>
        {
            accept();
            Close();
        };
        row.AddChild(yes);

        var no = new Button { Text = "Отклонить" };
        no.OnPressed += _ =>
        {
            decline();
            Close();
        };
        row.AddChild(no);

        root.AddChild(row);
        Contents.AddChild(root);
    }
}

public sealed partial class PizRebPartyWindow : DefaultWindow
{
    private static readonly Color UnreadColor = Color.FromHex("#FFD633");

    private readonly Action<EntityEventArgs> _send;

    private readonly Label _status;
    private readonly BoxContainer _members;
    private readonly BoxContainer _invites;
    private readonly BoxContainer _friends;
    private readonly RichTextLabel _partyLog;
    private readonly RichTextLabel _pmLog;
    private readonly LineEdit _partyInput;
    private readonly LineEdit _pmInput;
    private readonly Label _pmTargetLabel;
    private readonly TabContainer _chatTabs;

    private readonly Dictionary<string, int> _unreadPm = new();
    private readonly Dictionary<string, int> _unreadParty = new();

    private PizRebPartyStateEvent? _lastState;
    private bool _isHost;
    private string _pmTarget = string.Empty;

    public PizRebPartyWindow(Action<EntityEventArgs> send)
    {
        _send = send;
        Title = "Команда";
        MinWidth = 460;
        MinHeight = 440;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6),
        };

        _status = new Label { Text = "Нет команды", HorizontalExpand = true };
        header.AddChild(_status);

        var create = new Button { Text = "Создать" };
        create.OnPressed += _ => _send(new PizRebPartyCreateEvent());
        header.AddChild(create);

        var leave = new Button { Text = "Покинуть" };
        leave.OnPressed += _ => _send(new PizRebPartyLeaveEvent());
        header.AddChild(leave);
        root.AddChild(header);

        var inviteRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6),
        };
        var inviteName = new LineEdit { PlaceHolder = "Ник игрока", HorizontalExpand = true };
        inviteRow.AddChild(inviteName);
        var invite = new Button { Text = "Пригласить" };
        invite.OnPressed += _ => _send(new PizRebPartyInviteEvent { MemberName = inviteName.Text });
        inviteRow.AddChild(invite);
        root.AddChild(inviteRow);

        root.AddChild(new Label { Text = "Участники" });
        _members = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_members);

        _invites = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_invites);

        root.AddChild(new Label { Text = "Друзья" });
        var friendRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6),
        };
        var friendName = new LineEdit { PlaceHolder = "Ник друга", HorizontalExpand = true };
        friendRow.AddChild(friendName);
        var addFriend = new Button { Text = "Заявка" };
        addFriend.OnPressed += _ => _send(new PizRebFriendAddEvent { MemberName = friendName.Text });
        friendRow.AddChild(addFriend);
        root.AddChild(friendRow);

        _friends = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_friends);

        _chatTabs = new TabContainer { VerticalExpand = true };
        root.AddChild(_chatTabs);

        var partyTab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _partyLog = new RichTextLabel { VerticalExpand = true };
        partyTab.AddChild(new ScrollContainer { VerticalExpand = true, Children = { _partyLog } });
        _partyInput = new LineEdit { PlaceHolder = "Сообщение команде" };
        _partyInput.OnTextEntered += _ => SendPartyChat();
        partyTab.AddChild(_partyInput);
        _chatTabs.AddChild(partyTab);
        _chatTabs.SetTabTitle(0, "Команда");

        var pmTab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _pmTargetLabel = new Label { Text = "Личные: выбери друга" };
        pmTab.AddChild(_pmTargetLabel);
        _pmLog = new RichTextLabel { VerticalExpand = true };
        pmTab.AddChild(new ScrollContainer { VerticalExpand = true, Children = { _pmLog } });
        _pmInput = new LineEdit { PlaceHolder = "Личное сообщение" };
        _pmInput.OnTextEntered += _ => SendPm();
        pmTab.AddChild(_pmInput);
        _chatTabs.AddChild(pmTab);
        _chatTabs.SetTabTitle(1, "Личные");
        _chatTabs.SetTabVisible(1, false);
        _chatTabs.OnTabChanged += OnTabChanged;

        Contents.AddChild(root);
    }

    private void OnTabChanged(int tab)
    {
        if (tab == 0)
            _unreadParty.Clear();

        if (tab == 1 && !string.IsNullOrEmpty(_pmTarget))
            _unreadPm.Remove(_pmTarget);

        Refresh();
    }

    private void SendPartyChat()
    {
        if (string.IsNullOrWhiteSpace(_partyInput.Text))
            return;

        _send(new PizRebPartyChatSendEvent { Text = _partyInput.Text });
        _partyInput.Clear();
    }

    private void SendPm()
    {
        if (string.IsNullOrWhiteSpace(_pmInput.Text) || string.IsNullOrWhiteSpace(_pmTarget))
            return;

        _send(new PizRebPmSendEvent { TargetName = _pmTarget, Text = _pmInput.Text });
        _pmInput.Clear();
    }

    private void SelectPmTarget(string name)
    {
        _pmTarget = name;
        _pmTargetLabel.Text = $"Личные: {name}";
        _unreadPm.Remove(name);
        _chatTabs.SetTabVisible(1, true);
        _chatTabs.CurrentTab = 1;
        Refresh();
    }

    private void Refresh()
    {
        if (_lastState != null)
            UpdateState(_lastState);
    }

    public void UpdateState(PizRebPartyStateEvent ev)
    {
        _lastState = ev;
        _isHost = ev.IsHost;
        _status.Text = ev.InParty ? $"Команда: {ev.HostName}" : "Нет команды";

        _members.DisposeAllChildren();
        _invites.DisposeAllChildren();
        _friends.DisposeAllChildren();

        foreach (var member in ev.Members)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(NameWithUnread(member, _unreadParty.GetValueOrDefault(member)));

            if (ev.Ready.Contains(member))
                row.AddChild(new Label { Text = " (готов)", Modulate = Color.FromHex("#7CFF7C") });

            if (_isHost && member != ev.HostName)
            {
                var kick = new Button { Text = "Выгнать" };
                kick.OnPressed += _ => _send(new PizRebPartyKickEvent { MemberName = member });
                row.AddChild(kick);
            }

            _members.AddChild(row);
        }

        foreach (var host in ev.Invites)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(new Label { Text = $"Приглашение от {host}", HorizontalExpand = true });

            var accept = new Button { Text = "Принять" };
            var inviteHost = host;
            accept.OnPressed += _ => _send(new PizRebPartyAcceptEvent { HostName = inviteHost });
            row.AddChild(accept);

            var decline = new Button { Text = "Отклонить" };
            decline.OnPressed += _ => _send(new PizRebPartyInviteDeclineEvent { HostName = inviteHost });
            row.AddChild(decline);

            _invites.AddChild(row);
        }

        foreach (var request in ev.FriendRequests)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(new Label { Text = $"{request} хочет в друзья", HorizontalExpand = true });

            var requester = request;

            var accept = new Button { Text = "Принять" };
            accept.OnPressed += _ => _send(new PizRebFriendAcceptEvent { From = requester });
            row.AddChild(accept);

            var decline = new Button { Text = "Отклонить" };
            decline.OnPressed += _ => _send(new PizRebFriendDeclineEvent { From = requester });
            row.AddChild(decline);

            _friends.AddChild(row);
        }

        foreach (var friend in ev.Friends)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(NameWithUnread(friend, _unreadPm.GetValueOrDefault(friend)));

            var write = new Button { Text = "Написать" };
            write.OnPressed += _ => SelectPmTarget(friend);
            row.AddChild(write);

            var remove = new Button { Text = "Удалить" };
            remove.OnPressed += _ => _send(new PizRebFriendRemoveEvent { MemberName = friend });
            row.AddChild(remove);

            _friends.AddChild(row);
        }
    }

    private static Control NameWithUnread(string name, int unread)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        box.AddChild(new Label { Text = name });

        if (unread > 0)
            box.AddChild(new Label { Text = $" ({unread})", Modulate = UnreadColor });

        return box;
    }

    public void AddPartyChat(string from, string text)
    {
        _partyLog.Text += $"[color=#9ecbff]{from}[/color]: {text}\n";

        if (_chatTabs.CurrentTab != 0)
            _unreadParty[from] = _unreadParty.GetValueOrDefault(from) + 1;

        Refresh();
    }

    public void AddPm(string from, string text)
    {
        _pmLog.Text += $"[color=#ffb066]{from}[/color]: {text}\n";

        if (_chatTabs.CurrentTab != 1 || _pmTarget != from)
            _unreadPm[from] = _unreadPm.GetValueOrDefault(from) + 1;

        Refresh();
    }
}
