using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.PizReb.Deathmatch;

public sealed partial class PizRebKillfeedControl : BoxContainer
{
    [Dependency] private IGameTiming _timing = default!;

    public TimeSpan Lifetime = TimeSpan.FromSeconds(4);
    public int MaxEntries = 5;

    private readonly List<Entry> _entries = new();

    public PizRebKillfeedControl()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void AddEntry(string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var label = new Label
        {
            Text = text,
            FontColorOverride = color,
            MouseFilter = MouseFilterMode.Ignore,
            HorizontalAlignment = HAlignment.Left,
        };

        AddChild(label);
        _entries.Add(new Entry
        {
            Control = label,
            Expires = _timing.CurTime + Lifetime,
        });

        while (_entries.Count > Math.Max(MaxEntries, 1))
        {
            Remove(_entries[0]);
            _entries.RemoveAt(0);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.CurTime;

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Expires > now)
                continue;

            Remove(_entries[i]);
            _entries.RemoveAt(i);
        }
    }

    private void Remove(Entry entry)
    {
        if (!entry.Control.Disposed)
            entry.Control.Orphan();
    }

    private sealed class Entry
    {
        public Control Control = default!;
        public TimeSpan Expires;
    }
}
