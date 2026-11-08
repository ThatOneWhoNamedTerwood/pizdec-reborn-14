namespace Content.Server.PizReb.Sector;

[RegisterComponent]
public sealed partial class PizRebPodComponent : Component
{
    [DataField]
    public string Faction = "Independent";

    [DataField]
    public int Capacity = 1;
}
