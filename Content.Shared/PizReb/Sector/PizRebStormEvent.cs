using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.PizReb.Sector;

[Serializable, NetSerializable]
public sealed class PizRebStormEvent : EntityEventArgs
{
    public bool Active;
    public Vector2 Position;
    public int MapId;
    public List<float> Radii = new();
}
