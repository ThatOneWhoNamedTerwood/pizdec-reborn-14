using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Cyberware;

[Serializable, NetSerializable]
public enum AutoSurgeonStage : byte
{
    None,
    Mask,
    Sedate,
    Open,
    Prepare,
    Implant,
    Close,
    Finish,
    RevertOpen,
    RevertRestore,
    RevertClose,
}

[Serializable, NetSerializable]
public sealed partial class AutoSurgeonStepEvent : SimpleDoAfterEvent;
