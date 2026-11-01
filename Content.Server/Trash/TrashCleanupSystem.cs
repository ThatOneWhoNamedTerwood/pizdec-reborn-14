using System.Threading;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Fluids.Components;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Trash;

/// <summary>
///     Периодически удаляет мусор с тегом Trash и лужи, чтобы сервер не забивался за долгий раунд.
/// </summary>
public sealed partial class TrashCleanupSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private static readonly TimeSpan WarningLead = TimeSpan.FromSeconds(30);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private TagSystem _tags = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("trash.cleanup");

    private bool _enabled = true;
    private TimeSpan _interval = TimeSpan.FromMinutes(5);
    private CancellationTokenSource _timerCancel = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.TrashCleanupEnabled, OnEnabledChanged, true);
        Subs.CVar(_cfg, CCVars.TrashCleanupInterval, OnIntervalChanged, true);
    }

    public override void Shutdown()
    {
        _timerCancel.Cancel();
        base.Shutdown();
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
        Schedule();
    }

    private void OnIntervalChanged(float seconds)
    {
        _interval = TimeSpan.FromSeconds(Math.Max(10f, seconds));
        Schedule();
    }

    private void Schedule()
    {
        _timerCancel.Cancel();
        _timerCancel = new CancellationTokenSource();

        if (!_enabled)
            return;

        var warningDelay = _interval - WarningLead;

        if (warningDelay > TimeSpan.Zero)
            Timer.Spawn(warningDelay, Warn, _timerCancel.Token);

        Timer.Spawn(_interval, () =>
        {
            Cleanup();
            Schedule();
        }, _timerCancel.Token);
    }

    private void Warn()
    {
        _chat.DispatchServerAnnouncement(Loc.GetString("trash-cleanup-warning"));
    }

    public void Cleanup()
    {
        var trash = CleanupTrash();
        var puddles = CleanupPuddles();

        _chat.DispatchServerAnnouncement(Loc.GetString("trash-cleanup-done", ("trash", trash), ("puddles", puddles)));
        _sawmill.Info($"Убрано мусора: {trash}, луж: {puddles}");
    }

    private int CleanupTrash()
    {
        var count = 0;
        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tags, out _))
        {
            if (!_tags.HasTag(tags, TrashTag))
                continue;

            if (IsProtected(uid))
                continue;

            QueueDel(uid);
            count++;
        }

        return count;
    }

    private int CleanupPuddles()
    {
        var count = 0;
        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out _))
        {
            if (IsProtected(uid))
                continue;

            QueueDel(uid);
            count++;
        }

        return count;
    }

    private bool IsProtected(EntityUid uid)
    {
        return _containers.TryGetContainingContainer(uid, out _);
    }
}
