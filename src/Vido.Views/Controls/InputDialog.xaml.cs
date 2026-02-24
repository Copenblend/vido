using System.Windows;
using System.Windows.Input;

namespace Vido.Views.Controls;

/// <summary>
/// A simple text-input dialog styled to match the app's dark theme.
/// Use <see cref="ShowInputDialog"/> to display and retrieve user input.
/// </summary>
public partial class InputDialog : Window
{
    /// <summary>
    /// Gets the text entered by the user, or <c>null</c> if cancelled.
    /// </summary>
    public string? InputText { get; private set; }

    /// <summary>
    /// Creates a new input dialog.
    /// </summary>
    /// <param name="title">Window title.</param>
    /// <param name="prompt">Prompt text shown above the input box.</param>
    public InputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
    }

    /// <summary>
    /// Shows a modal input dialog and returns the entered text, or <c>null</c> if cancelled.
    /// </summary>
    public static string? ShowInputDialog(Window owner, string title, string prompt)
    {
        var dialog = new InputDialog(title, prompt) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        InputText = InputBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InputBox.Focus();
    }
}
