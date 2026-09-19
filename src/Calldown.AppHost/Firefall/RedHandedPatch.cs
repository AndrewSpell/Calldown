namespace Calldown.AppHost.Firefall;

internal enum RedHandedState
{
    Unknown,
    Unpatched,
    Patched,
}

internal static class RedHandedPatch
{
    // The call site alone shows up more than once, only together with the jump it's unique
    private static readonly byte[] Original =
    [
        0xFF,
        0x15,
        0xC0,
        0x43,
        0xAC,
        0x01,
        0x85,
        0xC0,
        0x74,
        0x13,
    ];
    private static readonly byte[] Bypassed =
    [
        0xFF,
        0x15,
        0xC0,
        0x43,
        0xAC,
        0x01,
        0x85,
        0xC0,
        0x90,
        0x90,
    ];

    public static RedHandedState Detect(string exePath)
    {
        if (!File.Exists(exePath))
        {
            return RedHandedState.Unknown;
        }

        ReadOnlySpan<byte> data = File.ReadAllBytes(exePath);
        if (data.IndexOf(Bypassed) != -1)
        {
            return RedHandedState.Patched;
        }

        return data.IndexOf(Original) != -1 ? RedHandedState.Unpatched : RedHandedState.Unknown;
    }
}
