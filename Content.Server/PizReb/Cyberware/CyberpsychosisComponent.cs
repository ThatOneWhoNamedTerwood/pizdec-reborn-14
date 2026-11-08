namespace Content.Server.PizReb.Cyberware;

public enum CyberpsychosisStage : byte
{
    None,
    Tremor,
    Eyes,
    Aggression,
    Laugh,
    Antagonist,
    Monster,
}

[RegisterComponent]
public sealed partial class CyberpsychosisComponent : Component
{
    [DataField]
    public TimeSpan TremorDelayMin = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan TremorDelayMax = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan EyesDelay = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan AggressionDelay = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan LaughDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan AntagonistDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MonsterDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public bool Permanent;

    [ViewVariables]
    public CyberpsychosisStage Stage;

    [ViewVariables]
    public TimeSpan NextStage;

    [ViewVariables]
    public bool AntagonistGiven;

    [ViewVariables]
    public Color OriginalEyeColor;
}
