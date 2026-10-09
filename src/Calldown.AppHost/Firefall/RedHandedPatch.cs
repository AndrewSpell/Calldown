using FauFau.Hax;
using FauFau.Hax.Patches;

namespace Calldown.AppHost.Firefall;

internal enum RedHandedState
{
    Unknown,
    Unpatched,
    Patched,
}

internal static class RedHandedPatch
{
    public static RedHandedState Detect(string exePath)
    {
        if (!File.Exists(exePath))
        {
            return RedHandedState.Unknown;
        }

        var patcher = new Patcher(exePath);
        var bypass = new RedHandedBypass();
        if (bypass.IsApplied(patcher))
        {
            return RedHandedState.Patched;
        }

        // Only patches the bytes in memory, the file stays as it is
        return bypass.Apply(patcher).Success ? RedHandedState.Unpatched : RedHandedState.Unknown;
    }

    // Applies FauFau's bypass in place, the file stays untouched when one of its patterns is missing
    public static PatchResult Apply(string exePath)
    {
        var patcher = new Patcher(exePath);
        var result = patcher.ApplyPatch(new RedHandedBypass());
        if (result.Success)
        {
            File.WriteAllBytes(exePath, patcher.FileData);
        }

        return result;
    }
}
