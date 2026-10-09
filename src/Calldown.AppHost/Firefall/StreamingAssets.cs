namespace Calldown.AppHost.Firefall;

// The client streamed the virtual texture levels and the dialogue media banks from dl.firefall.com,
// RIN serves them from the streaming-assets-path drop folder instead
internal static class StreamingAssets
{
    // Keep in sync with RIN.WebAPI's StreamingAssets.RequestPath
    private const string RequestPath = "/dl";

    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Calldown",
        "streaming-assets"
    );

    // firefall.ini [FilePaths] keys, the client appends <N> to VTRemotePath and <block>/<id>.bnk to AssetStreamPath
    public static IReadOnlyList<(string Key, string Value)> IniValues(string operatorHost) =>
        [
            ("VTRemotePath", $"{operatorHost}{RequestPath}/vtex/static.vtex"),
            ("AssetStreamPath", $"{operatorHost}{RequestPath}/AssetStream/"),
        ];
}
