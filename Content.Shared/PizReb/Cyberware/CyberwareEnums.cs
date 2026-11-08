using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Cyberware;

[Serializable, NetSerializable]
public enum CyberSlot : byte
{
    Head,
    Cyberdeck,
    Eyes,
    ArmLeft,
    ArmRight,
    HandLeft,
    HandRight,
    Legs,
    Skin,
    Nervous,
    Immune,
    Circulatory,
    Skeleton,
}

[Serializable, NetSerializable]
public enum CyberwareVisualLayer : byte
{
    Head,
    Torso,
    Eyes,
    ArmLeft,
    ArmRight,
    LegLeft,
    LegRight,
}

[Serializable, NetSerializable]
public struct CyberwareVisualOverride
{
    public CyberwareVisualLayer Layer;
    public string? State;
    public Color? Color;

    public CyberwareVisualOverride(CyberwareVisualLayer layer, string? state, Color? color)
    {
        Layer = layer;
        State = state;
        Color = color;
    }
}

public static class CyberSlotHelper
{
    public const int HeadSlots = 3;
    public const int CyberdeckSlots = 1;
    public const int EyeSlots = 1;
    public const int ArmSlots = 1;
    public const int HandSlots = 1;
    public const int LegSlots = 1;
    public const int SkinSlots = 3;
    public const int NervousSlots = 2;
    public const int ImmuneSlots = 2;
    public const int CirculatorySlots = 3;
    public const int SkeletonSlots = 3;

    public static int Capacity(CyberSlot slot) => slot switch
    {
        CyberSlot.Head => HeadSlots,
        CyberSlot.Cyberdeck => CyberdeckSlots,
        CyberSlot.Eyes => EyeSlots,
        CyberSlot.ArmLeft => ArmSlots,
        CyberSlot.ArmRight => ArmSlots,
        CyberSlot.HandLeft => HandSlots,
        CyberSlot.HandRight => HandSlots,
        CyberSlot.Legs => LegSlots,
        CyberSlot.Skin => SkinSlots,
        CyberSlot.Nervous => NervousSlots,
        CyberSlot.Immune => ImmuneSlots,
        CyberSlot.Circulatory => CirculatorySlots,
        CyberSlot.Skeleton => SkeletonSlots,
        _ => 1,
    };
}



