using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> TrashCleanupEnabled =
        CVarDef.Create("trash.cleanup_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> TrashCleanupInterval =
        CVarDef.Create("trash.cleanup_interval", 300f, CVar.SERVERONLY);
}
