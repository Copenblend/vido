namespace Vido.Core.DragDrop;

/// <summary>
/// Classifies a dropped item for drag-and-drop handling.
/// </summary>
public enum DropClassification
{
    /// <summary>
    /// The dropped path is a directory/folder.
    /// </summary>
    Folder,

    /// <summary>
    /// The dropped path is a recognized video file.
    /// </summary>
    VideoFile,

    /// <summary>
    /// The dropped path is a file with an unsupported extension.
    /// </summary>
    UnsupportedFile,

    /// <summary>
    /// The dropped path does not exist or is invalid.
    /// </summary>
    Invalid
}
