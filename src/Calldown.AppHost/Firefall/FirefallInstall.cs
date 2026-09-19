using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Calldown.AppHost.Firefall;

internal sealed record FirefallInstall(string Root)
{
    public string SystemDir => Path.Combine(Root, "system");
    public string BinDir => Path.Combine(SystemDir, "bin");
    public string ClientExe => Path.Combine(BinDir, "FirefallClient.exe");
    public string CaBundle => Path.Combine(BinDir, "curl-ca-bundle.crt");
    public string StaticDb => Path.Combine(SystemDir, "db", "clientdb.sd2");
    public string MapsDir => Path.Combine(SystemDir, "maps");
    public string AssetDbDir => Path.Combine(SystemDir, "assetdb");
    public string FirefallIni => Path.Combine(Root, "firefall.ini");

    public string WritePath
    {
        get
        {
            var configured = File.Exists(FirefallIni)
                ? IniFile.GetValue(File.ReadAllLines(FirefallIni), "FilePaths", "WritePath")
                : null;

            var path = string.IsNullOrWhiteSpace(configured)
                ? @"%LocalAppData%\Red 5 Studios\Firefall"
                : configured;

            // A relative WritePath, e.g. the commented out "localassets", sits inside the install
            return Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(path.Replace('/', '\\')),
                Root
            );
        }
    }

    public string SettingsCon => Path.Combine(WritePath, "settings.con");
    public string Keybinds => Path.Combine(WritePath, "keybinds.json");

    public bool IsValid => File.Exists(StaticDb);

    public static string? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return new[] { FromShellCommand() }
            .Concat(FromSteamLibraries())
            .FirstOrDefault(p => p is not null && new FirefallInstall(p).IsValid);
    }

    private static string? FromShellCommand()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var shellKey = Registry.ClassesRoot.OpenSubKey(@"firefall\shell\open\command");
        if (shellKey?.GetValue(string.Empty) is not string command)
        {
            return null;
        }

        var match = Regex.Match(command, "\\/D\"(.*)\" \"");
        if (!match.Success)
        {
            return null;
        }

        var exe = match.Groups[1].Value;
        var cut = exe.LastIndexOf(@"\system\", StringComparison.OrdinalIgnoreCase);
        return cut == -1 ? null : exe[..cut];
    }

    private static IEnumerable<string> FromSteamLibraries()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        using var key = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Valve\Steam");
        if (key?.GetValue("InstallPath") is not string steam)
        {
            yield break;
        }

        var libraries = new List<string> { steam };

        // Additional libraries are listed as "path" "D:\\SteamLibrary" entries
        var libraryFolders = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFolders))
        {
            libraries.AddRange(
                Regex
                    .Matches(File.ReadAllText(libraryFolders), "\"path\"\\s+\"([^\"]+)\"")
                    .Select(m => m.Groups[1].Value.Replace(@"\\", @"\"))
            );
        }

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(library, "steamapps", "appmanifest_227700.acf")))
            {
                yield return Path.Combine(library, "steamapps", "common", "Firefall");
            }
        }
    }
}
