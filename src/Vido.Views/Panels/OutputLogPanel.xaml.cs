using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Vido.ViewModels;

namespace Vido.Views.Panels;

/// <summary>
/// Output log panel. Displays color-coded, timestamped log entries
/// with auto-scroll, level filtering, and a clear button.
/// </summary>
public partial class OutputLogPanel : UserControl
{
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
}
