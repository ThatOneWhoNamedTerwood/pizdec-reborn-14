using Content.Shared.PizReb.Cyberware;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.PizReb.Cyberware;

public sealed class AutoSurgeonBoundUserInterface : BoundUserInterface
{
    private AutoSurgeonWindow? _window;

    public AutoSurgeonBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AutoSurgeonWindow>();
        _window.StartPressed += (mode, item, target) => SendMessage(new AutoSurgeonStartMessage(item, mode) { Target = target });
        _window.OnClose += () => _window = null;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not AutoSurgeonState surgeon)
            return;
        if (_window == null)
            return;
        _window.AllowClose = surgeon.Operating || string.IsNullOrEmpty(surgeon.PatientName);
        _window.UpdateState(surgeon);
    }
}

