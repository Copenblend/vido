using System.Windows;
using Microsoft.Win32;
using Vido.Services.Playlists;

namespace Vido.Views.Playlists;

/// <summary>
/// Concrete <see cref="IDialogService"/> implementation using Win32 file dialogs
/// and WPF <see cref="MessageBox"/> for confirmation dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    private const string PlaylistFilter = "Vido Playlist (*.vidpl)|*.vidpl";

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string defaultName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = string.IsNullOrEmpty(filter) ? PlaylistFilter : filter,
            DefaultExt = ".vidpl"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowOpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = string.IsNullOrEmpty(filter) ? PlaylistFilter : filter,
            DefaultExt = ".vidpl"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public bool? ShowConfirmationDialog(string message, string title)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }
}
