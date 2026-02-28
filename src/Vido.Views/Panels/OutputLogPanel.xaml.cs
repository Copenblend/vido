using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// Output log panel. Displays color-coded, timestamped log entries
/// with auto-scroll, level filtering, and a clear button.
/// </summary>
public partial class OutputLogPanel : UserControl
{
    /// <summary>
    /// Sets up the output log panel and registers loaded/unloaded handlers for auto-scroll wiring.
    /// </summary>
    public OutputLogPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to collection changes for auto-scroll
        if (DataContext is OutputLogViewModel vm)
        {
            vm.Entries.CollectionChanged += OnEntriesChanged;

            // Scroll to bottom if there are existing entries
            if (vm.Entries.Count > 0 && vm.IsAutoScrollEnabled)
                ScrollToBottom();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OutputLogViewModel vm)
        {
            vm.Entries.CollectionChanged -= OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add
            && DataContext is OutputLogViewModel { IsAutoScrollEnabled: true })
        {
            // Defer scroll to after layout update
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, ScrollToBottom);
        }
    }

    private void ScrollToBottom()
    {
        if (LogListBox.Items.Count > 0)
        {
            LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        }
    }

    // ─── Clipboard & context menu ────────────────────────────────────────────────────────────────

    private void OnLogListBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopySelectedLines();
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            LogListBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopySelectedLines();

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OutputLogViewModel vm || vm.Entries.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var entry in vm.Entries)
            sb.AppendLine(entry.FormattedLine);

        SetClipboardText(sb.ToString());
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => LogListBox.SelectAll();

    private void CopySelectedLines()
    {
        if (LogListBox.SelectedItems.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in LogListBox.SelectedItems)
        {
            if (item is LogEntryViewModel entry)
                sb.AppendLine(entry.FormattedLine);
        }

        SetClipboardText(sb.ToString());
    }

    private static void SetClipboardText(string text)
    {
        try
        {
            Clipboard.SetDataObject(text, true);
        }
        catch
        {
            // Clipboard can fail if locked by another process
        }
    }
}
