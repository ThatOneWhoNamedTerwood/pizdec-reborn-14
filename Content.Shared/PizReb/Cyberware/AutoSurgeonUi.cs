using Content.Shared.PizReb.Cyberware;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Cyberware;

[Serializable, NetSerializable]
public enum AutoSurgeonUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum AutoSurgeonMode : byte
{
    Install,
    Replace,
    Remove,
}

[Serializable, NetSerializable]
public enum AutoSurgeonUiTab : byte
{
    Status,
    Install,
    Replace,
    Service,
}

[Serializable, NetSerializable]
public sealed class AutoSurgeonItemEntry
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Slot = string.Empty;
    public int BrainLoad;
    public string Kind = string.Empty;
    public bool IsImplant;

    public AutoSurgeonItemEntry()
    {
    }

    public AutoSurgeonItemEntry(NetEntity entity, string name, string slot, int brainLoad, string kind, bool isImplant)
    {
        Entity = entity;
        Entity = entity;
        Name = name;
        Slot = slot;
        BrainLoad = brainLoad;
        Kind = kind;
        IsImplant = isImplant;
    }
}

[Serializable, NetSerializable]
public sealed class AutoSurgeonImplantEntry
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Slot = string.Empty;
    public int BrainLoad;
    public string Description = string.Empty;
    public int Heat;

    public AutoSurgeonImplantEntry()
    {
    }

    public AutoSurgeonImplantEntry(NetEntity entity, string name, string slot, int brainLoad, string description, int heat)
    {
        Entity = entity;
        Name = name;
        Slot = slot;
        BrainLoad = brainLoad;
        Description = description;
        Heat = heat;
    }
}

[Serializable, NetSerializable]
public sealed class AutoSurgeonState : BoundUserInterfaceState
{
    public string PatientName = string.Empty;
    public NetEntity? PatientEntity;
    public string Durability = string.Empty;
    public int Capacity;
    public int BrainLoad;
    public int Heat;
    public float DurabilityValue;
    public bool Operating;
    public bool Dressed;
    public string OperationStage = string.Empty;
    public string? Warning;
    public bool HasTank;
    public float TankMoles;
    public int JugCount;
    public List<AutoSurgeonImplantEntry> Implants = new();
    public List<AutoSurgeonItemEntry> Contents = new();
}

[Serializable, NetSerializable]
public sealed class AutoSurgeonEjectMessage : BoundUserInterfaceMessage
{
    public NetEntity Item;

    public AutoSurgeonEjectMessage(NetEntity item)
    {
        Item = item;
    }
}

[Serializable, NetSerializable]
public sealed class AutoSurgeonStartMessage : BoundUserInterfaceMessage
{
    public NetEntity? Item;
    public NetEntity? Target;
    public AutoSurgeonMode Mode;

    public AutoSurgeonStartMessage(NetEntity? item, AutoSurgeonMode mode)
    {
        Item = item;
        Mode = mode;
    }
}






