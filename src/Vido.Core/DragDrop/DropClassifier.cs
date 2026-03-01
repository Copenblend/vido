using Vido.Core.FileSystem;

namespace Vido.Core.DragDrop;

/// <summary>
/// Helper methods for classifying and processing dropped file paths.
/// Encapsulates the logic for determining how to handle a dropped item.
/// </summary>
public static class DropClassifier
{
    /// <summary>
    /// Classifies a dropped path as a folder, video file, unsupported file, or invalid.
    /// </summary>
    /// <param name="path">The file system path to classify.</param>
    /// <returns>The classification of the dropped path.</returns>
    public static DropClassification Classify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DropClassification.Invalid;

        if (Directory.Exists(path))
            return DropClassification.Folder;

        if (!File.Exists(path))
            return DropClassification.Invalid;

        var ext = Path.GetExtension(path);
        return FileNode.VideoExtensions.Contains(ext)
            ? DropClassification.VideoFile
            : DropClassification.UnsupportedFile;
    }

    /// <summary>
    /// Classifies all dropped paths and returns only the valid items
    /// (folders, video files, and unsupported files — excluding invalid paths).
    /// </summary>
    /// <param name="paths">The array of dropped paths.</param>
    /// <returns>An array of valid (classification, path) pairs.</returns>
    public static (DropClassification Classification, string Path)[] ClassifyAll(string[]? paths)
    {
        if (paths is null || paths.Length == 0)
            return [];

        var validCount = 0;
        for (var i = 0; i < paths.Length; i++)
        {
            var classification = Classify(paths[i]);
            if (classification != DropClassification.Invalid)
                validCount++;
        }

        if (validCount == 0)
            return [];

        var results = new (DropClassification, string)[validCount];
        var index = 0;
        for (var i = 0; i < paths.Length; i++)
        {
            var classification = Classify(paths[i]);
            if (classification != DropClassification.Invalid)
                results[index++] = (classification, paths[i]);
        }

        return results;
    }
}
