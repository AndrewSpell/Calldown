namespace Calldown.AppHost;

internal sealed record CollisionScope(string Name, bool Enabled, bool IncludeAssets)
{
    public static readonly CollisionScope Off = new("off", false, false);
    public static readonly CollisionScope Zone = new("zone", true, false);
    public static readonly CollisionScope ZoneAndAssets = new("zone+assets", true, true);

    public static CollisionScope Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "zone+assets" => ZoneAndAssets,
            "zone" => Zone,
            "off" => Off,
            _ => throw new InvalidOperationException(
                $"Unknown collision scope '{value}', use off, zone or zone+assets"
            ),
        };
}
