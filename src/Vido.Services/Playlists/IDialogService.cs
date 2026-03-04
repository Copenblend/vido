namespace Vido.Services.Playlists;

/// <summary>
/// Abstraction over file dialogs and confirmation dialogs for testability.
/// Implementations use platform-specific dialogs (e.g., Win32 file dialogs, WPF MessageBox).
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="defaultName">The default file name to display.</param>
    /// <param name="filter">The file type filter string (e.g., "Playlist (*.vidpl)|*.vidpl").</param>
    /// <returns>The selected file path, or <c>null</c> if the user cancelled.</returns>
    string? ShowSaveFileDialog(string defaultName, string filter);

    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="filter">The file type filter string.</param>
    /// <returns>The selected file path, or <c>null</c> if the user cancelled.</returns>
    string? ShowOpenFileDialog(string filter);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No/Cancel buttons.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns><c>true</c> for Yes, <c>false</c> for No, <c>null</c> for Cancel.</returns>
    bool? ShowConfirmationDialog(string message, string title);
}
