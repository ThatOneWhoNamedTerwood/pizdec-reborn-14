using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Deathmatch;

[Serializable, NetSerializable]
public sealed class PizRebDeathmatchStatusEvent : EntityEventArgs
{
    public bool Active;
    public bool KillfeedEnabled = true;
    public bool HealthPercentEnabled = true;
    public Vector2 KillfeedAnchor = new(0.015f, 0.62f);
    public float KillfeedLifetime = 4f;
    public int KillfeedMaxEntries = 5;
    public float HealthTextOffset = 0.55f;
}

[Serializable, NetSerializable]
public sealed class PizRebKillfeedEvent : EntityEventArgs
{
    public string Text = string.Empty;
    public bool HasKiller;
    public NetUserId Killer;
    public bool HasVictim;
    public NetUserId Victim;
}
