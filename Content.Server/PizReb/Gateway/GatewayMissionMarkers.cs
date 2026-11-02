namespace Content.Server.PizReb.Gateway;

[RegisterComponent]
public sealed partial class GatewayPortalComponent : Component
{
    [DataField]
    public string FixtureId = "portalFixture";
}

[RegisterComponent]
public sealed partial class GatewayMissionReturnPortalComponent : Component
{
    [DataField]
    public string FixtureId = "portalFixture";

    public EntityUid Gateway;
}

[RegisterComponent]
public sealed partial class GatewayPortalCooldownComponent : Component
{
    public TimeSpan Expires;
}

[RegisterComponent]
public sealed partial class GatewayMissionSpawnPointComponent : Component;

[RegisterComponent]
public sealed partial class GatewayMissionObjectiveSpawnComponent : Component;

[RegisterComponent]
public sealed partial class GatewayMissionObjectiveDeviceComponent : Component;
