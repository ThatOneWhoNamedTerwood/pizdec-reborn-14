using Content.Shared.PizReb.Gateway;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.PizReb.Gateway;

[UsedImplicitly]
public sealed partial class ExpeditionGatewayBoundUserInterface : BoundUserInterface
{
    private ExpeditionGatewayWindow? _window;

    public ExpeditionGatewayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<ExpeditionGatewayWindow>();
        _window.AcceptPressed += id => SendMessage(new ExpeditionGatewayClaimMissionMessage { Mission = id });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ExpeditionGatewayBoundUserInterfaceState current)
            return;
        _window?.UpdateState(current);
    }
}
