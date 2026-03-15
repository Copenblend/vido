using System.IO;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Finds matching .funscript files for a given video file using naming conventions.
/// Convention: <c>video.funscript → L0</c>, <c>video.twist.funscript → R0</c>,
/// <c>video.roll.funscript → R1</c>, <c>video.pitch.funscript → R2</c>.
/// Only supports the 4 OSR2+ axes (no L1/surge, L2/sway).
/// </summary>
public class FunscriptMatcher
{
    /// <summary>
    /// Maps axis suffixes to axis IDs.
    /// The default (no suffix) maps to L0 (stroke).
    /// </summary>
    private static readonly Dictionary<string, string> SuffixToAxis = new(StringComparer.OrdinalIgnoreCase)
    {
        { "",      "L0" },  // video.funscript → L0 (stroke)
        { "twist", "R0" },  // video.twist.funscript → R0
        { "roll",  "R1" },  // video.roll.funscript → R1
        { "pitch", "R2" },  // video.pitch.funscript → R2
    };

    /// <summary>
    /// Determines the axis ID from a funscript filename using suffix conventions.
    /// Returns "L0" for base ".funscript" files, "R0" for ".twist.funscript", etc.
    /// </summary>
    /// <param name="filePath">Path to the funscript file.</param>
    /// <returns>The axis ID ("L0", "R0", "R1", "R2").</returns>
    public static string GetAxisIdForFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName))
            return "L0";

        // Strip .funscript extension
        if (fileName.EndsWith(".funscript", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^".funscript".Length];

        // Check for axis suffix: "something.twist" → "twist"
        var dotIndex = fileName.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var suffix = fileName[(dotIndex + 1)..];
            foreach (var (s, axisId) in SuffixToAxis)
            {
                if (!string.IsNullOrEmpty(s) &&
                    string.Equals(suffix, s, StringComparison.OrdinalIgnoreCase))
                    return axisId;
            }
        }

        return "L0";
    }

    /// <summary>
    /// Finds matching funscript files for the given video file.
    /// Searches the same directory as the video using case-insensitive matching.
    /// </summary>
    /// <param name="videoPath">Full path to the video file.</param>
    /// <returns>
    /// Dictionary of axisId → funscript file path. Empty if no matches found
    /// or the video path is null/empty.
    /// </returns>
    public Dictionary<string, string> FindMatchingScripts(string videoPath)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(videoPath))
            return result;

        var directory = Path.GetDirectoryName(videoPath);
        if (directory == null || !Directory.Exists(directory))
            return result;

        var videoNameNoExt = Path.GetFileNameWithoutExtension(videoPath);

        // Get all .funscript files in the directory for case-insensitive matching
        var funscriptFiles = Directory.GetFiles(directory, "*.funscript", SearchOption.TopDirectoryOnly);

        foreach (var (suffix, axisId) in SuffixToAxis)
        {
            string expectedFileName = string.IsNullOrEmpty(suffix)
                ? $"{videoNameNoExt}.funscript"
                : $"{videoNameNoExt}.{suffix}.funscript";

            // Case-insensitive match against actual files in directory
            var match = Array.Find(funscriptFiles,
                f => string.Equals(Path.GetFileName(f), expectedFileName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                result[axisId] = match;
            }
        }

        return result;
    }
}
