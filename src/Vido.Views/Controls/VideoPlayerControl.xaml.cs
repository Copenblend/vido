using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vido.Core.Playback;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Displays decoded video frames in a WriteableBitmap and hosts transport controls.
/// Subscribes to <see cref="VideoPlayerViewModel.FrameReady"/> to render frames.
/// Supports fullscreen overlay mode where controls float over the video.
/// </summary>
public partial class VideoPlayerControl : UserControl
{
    private WriteableBitmap? _bitmap;
    private VideoPlayerViewModel? _viewModel;
    private Storyboard? _loadingSpinnerStoryboard;
    private FrameData? _pendingFrame;
    private int _renderQueued;

    /// <summary>
    /// Cached render callback delegate to avoid allocating a new method-group delegate
    /// for each dispatcher enqueue on the frame-render hot path.
    /// </summary>
    private readonly Action _renderAction;

    /// <summary>
    /// Cached gradient brush for fullscreen overlay (transparent→black).
    /// </summary>
    private static readonly LinearGradientBrush FullscreenOverlayGradient = CreateFullscreenGradient();

    private static LinearGradientBrush CreateFullscreenGradient()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(200, 0, 0, 0), 0.5));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(230, 0, 0, 0), 1.0));
        gradient.Freeze();
        return gradient;
    }

    /// <summary>
    /// Raised when the user double-clicks the video area to toggle fullscreen.
    /// </summary>
    public event Action? FullscreenToggleRequested;

    /// <summary>
    /// Raised when files or folders are dropped onto the player area.
    /// </summary>
    public event Action<string[]>? FilesDropped;
    
    /// <summary>
    /// Sets up the video player control, wiring mouse event handlers for seek/volume sliders,
    /// double-click fullscreen, and data context change monitoring.
    /// </summary>
    public VideoPlayerControl()
    {
        _renderAction = RenderPendingFrame;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Double-click on video surface or empty state toggles fullscreen
        VideoSurface.MouseLeftButtonDown += OnVideoSurfaceMouseDown;
        RootGrid.MouseLeftButtonDown += OnRootGridMouseDown;

        // Seek slider: click to jump, hold-and-drag to scrub continuously.
        // Must use AddHandler with handledEventsToo because Slider's IsMoveToPointEnabled
        // marks PreviewMouseLeftButtonDown as Handled in its class handler.
        SeekSlider.AddHandler(
            PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnSeekSliderMouseDown),
            handledEventsToo: true);
        SeekSlider.AddHandler(
            PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnSeekSliderMouseUp),
            handledEventsToo: true);
        SeekSlider.AddHandler(
            PreviewMouseMoveEvent,
            new MouseEventHandler(OnSeekSliderMouseMove),
            handledEventsToo: true);

        // Volume slider: click sets position, hold-and-drag adjusts continuously.
        VolumeSlider.AddHandler(
            PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnVolumeSliderMouseDown),
            handledEventsToo: true);
        VolumeSlider.AddHandler(
            PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnVolumeSliderMouseUp),
            handledEventsToo: true);
        VolumeSlider.AddHandler(
            PreviewMouseMoveEvent,
            new MouseEventHandler(OnVolumeSliderMouseMove),
            handledEventsToo: true);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FrameReady -= OnFrameReady;
            _viewModel.PropertyChanged -= OnVmPropertyChanged;
        }

        _viewModel = e.NewValue as VideoPlayerViewModel;

        if (_viewModel is not null)
        {
            _viewModel.FrameReady += OnFrameReady;
            _viewModel.PropertyChanged += OnVmPropertyChanged;
            UpdateVisualState(_viewModel.HasMedia);
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoPlayerViewModel.HasMedia) && _viewModel is not null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var hasMedia = _viewModel.HasMedia;
                UpdateVisualState(hasMedia);

                // Clear the bitmap when media is unloaded (Stop)
                if (!hasMedia)
                {
                    var pending = Interlocked.Exchange(ref _pendingFrame, null);
                    pending?.Dispose();
                    _bitmap = null;
                    VideoSurface.Source = null;
                }
            });
        }
        else if (e.PropertyName == nameof(VideoPlayerViewModel.IsLoadingMedia) && _viewModel is not null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateLoadingState(_viewModel.IsLoadingMedia);
            });
        }
    }

    private void UpdateVisualState(bool hasMedia)
    {
        EmptyState.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        VideoSurface.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLoadingState(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

        if (isLoading)
        {
            // Hide empty state while loading
            EmptyState.Visibility = Visibility.Collapsed;
            StartLoadingSpinner();
        }
        else
        {
            StopLoadingSpinner();
        }
    }

    /// <summary>
    /// Starts the loading spinner storyboard if not already running.
    /// </summary>
    private void StartLoadingSpinner()
    {
        if (_loadingSpinnerStoryboard is not null) return;

        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(1),
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(animation, LoadingSpinnerRotation);
        Storyboard.SetTargetProperty(animation, new PropertyPath(RotateTransform.AngleProperty));

        _loadingSpinnerStoryboard = new Storyboard();
        _loadingSpinnerStoryboard.Children.Add(animation);
        _loadingSpinnerStoryboard.Begin();
    }

    /// <summary>
    /// Stops the loading spinner storyboard and resets rotation angle.
    /// </summary>
    private void StopLoadingSpinner()
    {
        _loadingSpinnerStoryboard?.Stop();
        _loadingSpinnerStoryboard = null;
        LoadingSpinnerRotation.Angle = 0;
    }

    /// <summary>
    /// Renders a decoded BGRA32 frame into the WriteableBitmap.
    /// Called from the engine's decode thread — marshals to the UI thread.
    /// Disposes the FrameData after copying pixels to return the pooled buffer.
    /// </summary>
    private void OnFrameReady(FrameData frame)
    {
        var stale = Interlocked.Exchange(ref _pendingFrame, frame);
        stale?.Dispose();

        if (Interlocked.Exchange(ref _renderQueued, 1) == 0)
            Dispatcher.BeginInvoke(_renderAction, DispatcherPriority.Render);
    }

    /// <summary>
    /// Renders the most recent decoded frame and drops stale pending frames.
    /// Ensures at most one render callback is queued at a time.
    /// </summary>
    private void RenderPendingFrame()
    {
        try
        {
            var frame = Interlocked.Exchange(ref _pendingFrame, null);
            if (frame is null)
                return;

            try
            {
                if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
                {
                    _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
                    VideoSurface.Source = _bitmap;
                }

                _bitmap.Lock();
                try
                {
                    _bitmap.WritePixels(
                        new Int32Rect(0, 0, frame.Width, frame.Height),
                        frame.PixelData,
                        frame.Stride,
                        0);
                }
                finally
                {
                    _bitmap.Unlock();
                }
            }
            finally
            {
                frame.Dispose();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            if (Volatile.Read(ref _pendingFrame) is not null && Interlocked.Exchange(ref _renderQueued, 1) == 0)
                Dispatcher.BeginInvoke(_renderAction, DispatcherPriority.Render);
        }
    }

    // ── Seek slider events ──

    private bool _isSeekDragging;
    private long _lastSeekTimestamp;
    private static readonly long SeekMinInterval = Stopwatch.Frequency / 30;

    /// <summary>
    /// On mouse-down: suppress engine position updates, seek to the clicked position,
    /// and capture the mouse so we receive move events for drag-to-scrub.
    /// </summary>
    private void OnSeekSliderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || !_viewModel.HasMedia) return;

        _isSeekDragging = true;
        _viewModel.BeginSeek();
        SeekSlider.CaptureMouse();

        // IsMoveToPointEnabled already moved the slider value on this click.
        _viewModel.ApplySeek();

        // Prevent the slider's built-in thumb drag machinery.
        e.Handled = true;
    }

    /// <summary>
    /// While dragging, compute seek position from mouse X and seek continuously.
    /// </summary>
    private void OnSeekSliderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSeekDragging || _viewModel is null) return;

        var pos = e.GetPosition(SeekSlider);
        var ratio = Math.Clamp(pos.X / SeekSlider.ActualWidth, 0, 1);
        SeekSlider.Value = SeekSlider.Minimum + ratio * (SeekSlider.Maximum - SeekSlider.Minimum);

        var now = Stopwatch.GetTimestamp();
        if (now - _lastSeekTimestamp >= SeekMinInterval)
        {
            _lastSeekTimestamp = now;
            _viewModel.ApplySeek();
        }
    }

    /// <summary>
    /// On mouse-up: release capture and resume engine position updates.
    /// </summary>
    private void OnSeekSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeekDragging) return;
        _isSeekDragging = false;
        SeekSlider.ReleaseMouseCapture();
        _viewModel?.ApplySeek();
        _viewModel?.EndSeek();
    }

    // ── Volume slider events ──

    private bool _isVolumeDragging;

    /// <summary>
    /// Capture the mouse on the slider so we receive MouseMove while held.
    /// IsMoveToPointEnabled already set the value on this click.
    /// </summary>
    private void OnVolumeSliderMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isVolumeDragging = true;
        VolumeSlider.CaptureMouse();
        e.Handled = true; // Prevent thumb's internal drag machinery
    }

    /// <summary>
    /// While dragging, compute volume from mouse X position relative to the slider.
    /// </summary>
    private void OnVolumeSliderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isVolumeDragging) return;

        var pos = e.GetPosition(VolumeSlider);
        var ratio = Math.Clamp(pos.X / VolumeSlider.ActualWidth, 0, 1);
        VolumeSlider.Value = VolumeSlider.Minimum + ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum);
    }

    /// <summary>
    /// Release capture and stop drag tracking.
    /// </summary>
    private void OnVolumeSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isVolumeDragging) return;
        _isVolumeDragging = false;
        VolumeSlider.ReleaseMouseCapture();
    }

    // ── Double-click to toggle fullscreen ──

    /// <summary>
    /// Detects double-clicks on the video surface to toggle fullscreen.
    /// </summary>
    private void OnVideoSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            FullscreenToggleRequested?.Invoke();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Detects double-clicks on the root grid (covers empty state area)
    /// to toggle fullscreen when no video is loaded.
    /// </summary>
    private void OnRootGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Only handle if the click was NOT on the video surface (handled above)
        // and NOT on the controls overlay (buttons, sliders, etc.)
        if (e.ClickCount == 2 && !IsDescendantOf(e.OriginalSource, ControlsOverlay)
                              && e.OriginalSource != VideoSurface)
        {
            FullscreenToggleRequested?.Invoke();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Checks if the original source of an event is a descendant of a given parent element.
    /// </summary>
    private static bool IsDescendantOf(object source, DependencyObject parent)
    {
        if (source is not DependencyObject depObj) return false;
        var current = depObj;
        while (current is not null)
        {
            if (current == parent) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    // ── Playback speed ──

    private void OnSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void OnSpeedContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || DataContext is not VideoPlayerViewModel vm)
            return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is string tagStr && double.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed))
            {
                item.IsChecked = Math.Abs(vm.PlaybackSpeed - speed) < 0.01;
            }
        }
    }

    private void OnSpeedItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tagStr
            && double.TryParse(tagStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed)
            && DataContext is VideoPlayerViewModel vm)
        {
            vm.SetPlaybackSpeed(speed);
        }
    }

    // ── Drag and drop ──

    /// <summary>
    /// Checks if the dragged data contains files or folders and sets the appropriate effect.
    /// Shows the drag overlay when valid data is detected.
    /// </summary>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DragOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    /// <summary>
    /// Processes dropped files. Passes all paths to the parent for classification and handling.
    /// </summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            FilesDropped?.Invoke(paths);

        e.Handled = true;
    }

    // ── Fullscreen overlay mode ──

    /// <summary>
    /// Switches the controls overlay to fullscreen mode with a semi-transparent
    /// gradient background and no top border.
    /// </summary>
    public void EnterFullscreenOverlay()
    {
        // Black background behind video for cinema-style letterboxing
        Background = System.Windows.Media.Brushes.Black;

        // Move controls into the video row so they overlay
        Grid.SetRow(ControlsOverlay, 0);

        // Use cached gradient background for overlay appearance
        ControlsOverlay.Background = FullscreenOverlayGradient;
        ControlsOverlay.BorderThickness = new Thickness(0);
        ControlsOverlay.Padding = new Thickness(16, 8, 16, 8);

        // Show the video name overlay if it has text
        if (!string.IsNullOrEmpty(VideoNameText.Text))
        {
            VideoNameOverlay.Visibility = Visibility.Visible;
            VideoNameOverlay.Opacity = 1.0;
        }
    }

    /// <summary>
    /// Restores the controls overlay to normal mode with a solid background
    /// and top border.
    /// </summary>
    public void ExitFullscreenOverlay()
    {
        // Restore background from fullscreen black
        SetResourceReference(BackgroundProperty, "EditorBackgroundBrush");

        // Move controls back to their own row below the video
        Grid.SetRow(ControlsOverlay, 1);

        ControlsOverlay.SetResourceReference(Border.BackgroundProperty, "EditorBackgroundBrush");
        ControlsOverlay.BorderBrush = (Brush)FindResource("PrimaryBorderBrush");
        ControlsOverlay.BorderThickness = new Thickness(0, 1, 0, 0);
        ControlsOverlay.Padding = new Thickness(8, 4, 8, 4);

        // Hide the video name overlay when leaving fullscreen
        VideoNameOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Sets the video name displayed in the fullscreen overlay (without extension).
    /// </summary>
    /// <param name="videoName">The video name to display, or null to clear.</param>
    public void SetVideoName(string? videoName)
    {
        VideoNameText.Text = videoName ?? string.Empty;
    }

    /// <summary>
    /// Gets the video name overlay border for animation purposes.
    /// </summary>
    public Border VideoNameOverlayElement => VideoNameOverlay;

    /// <summary>
    /// Gets the controls overlay border for animation purposes.
    /// </summary>
    public Border ControlsOverlayElement => ControlsOverlay;

    // ── Plugin control bar items ──

    private readonly Dictionary<string, UIElement> _pluginControlBarItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UIElement> _pluginOverlays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _pendingOverlayVisibility = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a plugin-provided control bar item (left of the loop button).
    /// </summary>
    /// <param name="fullId">Unique identifier for the control bar item (e.g. "plugin.myPlugin.beatbar").</param>
    /// <param name="element">The UI element to display in the control bar.</param>
    public void AddPluginControlBarItem(string fullId, UIElement element)
    {
        if (_pluginControlBarItems.ContainsKey(fullId)) return;
        _pluginControlBarItems[fullId] = element;
        PluginControlBarPanel.Items.Add(element);
    }

    /// <summary>
    /// Removes a plugin control bar item by its full ID.
    /// </summary>
    /// <param name="fullId">The unique identifier of the control bar item to remove.</param>
    public void RemovePluginControlBarItem(string fullId)
    {
        if (!_pluginControlBarItems.TryGetValue(fullId, out var element)) return;
        PluginControlBarPanel.Items.Remove(element);
        _pluginControlBarItems.Remove(fullId);
    }

    /// <summary>
    /// Adds a plugin video overlay element (e.g. a beat bar).
    /// Overlays are layered on top of the video surface and are
    /// not hit-test visible by default.
    /// </summary>
    /// <param name="fullId">Unique identifier for the overlay (e.g. "plugin.myPlugin.beatbar").</param>
    /// <param name="overlay">The UI element to layer on top of the video surface.</param>
    public void AddPluginOverlay(string fullId, UIElement overlay)
    {
        if (_pluginOverlays.ContainsKey(fullId)) return;

        // Apply pending visibility if a toggle was requested before the overlay
        // was materialized (vb-017: plugin activation order race).
        if (_pendingOverlayVisibility.Remove(fullId, out var pendingVisible))
            overlay.Visibility = pendingVisible ? Visibility.Visible : Visibility.Collapsed;
        else
            overlay.Visibility = Visibility.Collapsed;

        _pluginOverlays[fullId] = overlay;
        PluginOverlayContainer.Children.Add(overlay);
    }

    /// <summary>
    /// Removes a plugin overlay by its full ID.
    /// </summary>
    /// <param name="fullId">The unique identifier of the overlay to remove.</param>
    public void RemovePluginOverlay(string fullId)
    {
        if (!_pluginOverlays.TryGetValue(fullId, out var overlay)) return;
        PluginOverlayContainer.Children.Remove(overlay);
        _pluginOverlays.Remove(fullId);
    }

    /// <summary>
    /// Toggles visibility of a plugin overlay.
    /// </summary>
    /// <param name="fullId">The unique identifier of the overlay to show or hide.</param>
    /// <param name="visible">Whether the overlay should be visible.</param>
    public void SetPluginOverlayVisible(string fullId, bool visible)
    {
        if (_pluginOverlays.TryGetValue(fullId, out var overlay))
            overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        else
            // Overlay not yet materialized — store for when AddPluginOverlay is called (vb-017)
            _pendingOverlayVisibility[fullId] = visible;
    }

    /// <summary>
    /// Returns whether a plugin control bar item with the given ID exists.
    /// </summary>
    /// <param name="fullId">The unique identifier to look up.</param>
    public bool HasPluginControlBarItem(string fullId) =>
        _pluginControlBarItems.ContainsKey(fullId);
}
