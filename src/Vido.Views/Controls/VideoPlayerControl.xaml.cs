using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vido.Core.Playback;
using Vido.ViewModels;

namespace Vido.Views.Controls;

/// <summary>
/// Displays decoded video frames in a WriteableBitmap and hosts transport controls.
/// Subscribes to <see cref="VideoPlayerViewModel.FrameReady"/> to render frames.
/// </summary>
public partial class VideoPlayerControl : UserControl
{
    private WriteableBitmap? _bitmap;
    private VideoPlayerViewModel? _viewModel;

    public VideoPlayerControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

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
            Dispatcher.Invoke(() =>
            {
                var hasMedia = _viewModel.HasMedia;
                UpdateVisualState(hasMedia);

                // Clear the bitmap when media is unloaded (Stop)
                if (!hasMedia)
                {
                    _bitmap = null;
                    VideoSurface.Source = null;
                }
            });
        }
    }

    private void UpdateVisualState(bool hasMedia)
    {
        EmptyState.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        VideoSurface.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Renders a decoded BGRA32 frame into the WriteableBitmap.
    /// Called from the engine's decode thread — marshals to the UI thread.
    /// </summary>
    private void OnFrameReady(FrameData frame)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Recreate bitmap if dimensions changed
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
        });
    }

    // ── Seek slider events ──

    private bool _isSeekDragging;

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
        _viewModel.ApplySeek();
    }

    /// <summary>
    /// On mouse-up: release capture and resume engine position updates.
    /// </summary>
    private void OnSeekSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeekDragging) return;
        _isSeekDragging = false;
        SeekSlider.ReleaseMouseCapture();
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
}
