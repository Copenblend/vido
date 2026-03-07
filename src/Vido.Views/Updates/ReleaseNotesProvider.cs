using System.IO;

namespace Vido.Views.Updates;

/// <summary>
/// Reads the embedded RELEASENOTES.md and extracts the section for a specific version.
/// </summary>
public static class ReleaseNotesProvider
{
    private const string FileName = "RELEASENOTES.md";

    /// <summary>
    /// Gets the release notes markdown for the specified version.
    /// Returns null if the file doesn't exist or the version section isn't found.
    /// </summary>
    public static string? GetNotesForVersion(string version)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        if (!File.Exists(path)) return null;

        var content = File.ReadAllText(path);
        return ExtractVersionSection(content, version);
    }

    /// <summary>
    /// Extracts the markdown section for a specific version from the full file content.
    /// Looks for a ## [version] header and returns everything until the next ## header.
    /// </summary>
    internal static string? ExtractVersionSection(string content, string version)
    {
        var normalizedVersion = version.TrimStart('v');
        var lines = content.Split('\n');
        var capturing = false;
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (capturing) break;

                if (line.Contains(normalizedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    capturing = true;
                    continue;
                }
            }
            else if (capturing)
            {
                result.Add(line);
            }
        }

        if (result.Count == 0) return null;

        var text = string.Join('\n', result).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
