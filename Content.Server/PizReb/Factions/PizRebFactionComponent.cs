namespace Content.Server.PizReb.Factions;

[RegisterComponent]
public sealed partial class PizRebFactionComponent : Component
{
    [DataField]
    public string Faction = "Independent";
}
