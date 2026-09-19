using System.Text.RegularExpressions;

namespace Calldown.AppHost.Firefall;

internal static partial class IniFile
{
    public static List<string> SetValue(
        IReadOnlyList<string> lines,
        string section,
        string key,
        string value
    )
    {
        var result = lines.ToList();
        var newLine = $"{key} = {value}";

        var sectionStart = result.FindIndex(l =>
            SectionName(l) is { } name && name.Equals(section, StringComparison.OrdinalIgnoreCase)
        );
        if (sectionStart == -1)
        {
            if (result.Count > 0 && result[^1].Trim().Length > 0)
            {
                result.Add(string.Empty);
            }

            result.Add($"[{section}]");
            result.Add(newLine);
            return result;
        }

        var sectionEnd = result.FindIndex(sectionStart + 1, l => SectionName(l) is not null);
        if (sectionEnd == -1)
        {
            sectionEnd = result.Count;
        }

        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            var match = KeyRegex().Match(result[i]);
            if (
                match.Success
                && match.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase)
            )
            {
                result[i] = newLine;
                return result;
            }
        }

        // Insert after the last non-blank line of the section so a blank separator line stays in place
        var insertAt = sectionEnd;
        while (insertAt > sectionStart + 1 && result[insertAt - 1].Trim().Length == 0)
        {
            insertAt--;
        }

        result.Insert(insertAt, newLine);
        return result;
    }

    public static string? GetValue(IReadOnlyList<string> lines, string section, string key)
    {
        string? current = null;
        foreach (var line in lines)
        {
            if (SectionName(line) is { } name)
            {
                current = name;
                continue;
            }

            var match = KeyRegex().Match(line);
            if (
                current is not null
                && current.Equals(section, StringComparison.OrdinalIgnoreCase)
                && match.Success
                && match.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase)
            )
            {
                return match.Groups["value"].Value.Trim().Trim('"');
            }
        }

        return null;
    }

    private static string? SectionName(string line)
    {
        var match = SectionRegex().Match(line);
        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    [GeneratedRegex(@"^\s*\[(?<name>[^\]]+)\]\s*$")]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^\s*(?<key>[^;#=\s][^=]*?)\s*=(?<value>.*)$")]
    private static partial Regex KeyRegex();
}
