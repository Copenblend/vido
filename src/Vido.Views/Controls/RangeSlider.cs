using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Vido.Views.Controls;

/// <summary>
/// A dual-thumb range slider control for selecting a min/max range within a given interval.
/// Supports mouse drag on individual thumbs or the selected range, and keyboard navigation.
/// </summary>
[TemplatePart(Name = "PART_TrackCanvas", Type = typeof(Canvas))]
[TemplatePart(Name = "PART_TrackBackground", Type = typeof(Rectangle))]
[TemplatePart(Name = "PART_TrackFill", Type = typeof(Rectangle))]
[TemplatePart(Name = "PART_MinThumb", Type = typeof(Ellipse))]
[TemplatePart(Name = "PART_MaxThumb", Type = typeof(Ellipse))]
public class RangeSlider : Control
{
    // ── Constants ────────────────────────────────────────────
    private const double ThumbDiameter = 14;
    private const double TrackHeight = 3;
    private const double MinGap = 1;

    // ── Template parts ───────────────────────────────────────
    private Canvas? _trackCanvas;
    private Rectangle? _trackBackground;
    private Rectangle? _trackFill;
    private Ellipse? _minThumb;
    private Ellipse? _maxThumb;

    // ── Drag state ───────────────────────────────────────────
    private bool _isDraggingMin;
    private bool _isDraggingMax;
    private bool _isDraggingRange;
    private double _dragStartX;
    private double _dragStartMin;
    private double _dragStartMax;

    // ══════════════════════════════════════════════════════════
    //  Dependency Properties
    // ══════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="Minimum"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(0.0, OnRangeChanged));

    /// <summary>Identifies the <see cref="Maximum"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeSlider),
            new PropertyMetadata(100.0, OnRangeChanged));

    /// <summary>Identifies the <see cref="MinValue"/> dependency property.</summary>
    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRangeChanged, CoerceMinValue));

    /// <summary>Identifies the <see cref="MaxValue"/> dependency property.</summary>
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(RangeSlider),
            new FrameworkPropertyMetadata(100.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRangeChanged, CoerceMaxValue));

    /// <summary>Identifies the <see cref="TrackColor"/> dependency property.</summary>
    public static readonly DependencyProperty TrackColorProperty =
        DependencyProperty.Register(nameof(TrackColor), typeof(Brush), typeof(RangeSlider),
            new PropertyMetadata(Brushes.DodgerBlue, OnRangeChanged));

    /// <summary>Gets or sets the minimum allowed value of the range.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Gets or sets the maximum allowed value of the range.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Gets or sets the currently selected minimum value.</summary>
    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    /// <summary>Gets or sets the currently selected maximum value.</summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>Gets or sets the brush used for the selected range fill.</summary>
    public Brush TrackColor
    {
        get => (Brush)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    // ══════════════════════════════════════════════════════════
    //  Constructor
    // ══════════════════════════════════════════════════════════

    static RangeSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RangeSlider),
            new FrameworkPropertyMetadata(typeof(RangeSlider)));
    }

    // ══════════════════════════════════════════════════════════
    //  Template Application
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _trackCanvas = GetTemplateChild("PART_TrackCanvas") as Canvas;
        _trackBackground = GetTemplateChild("PART_TrackBackground") as Rectangle;
        _trackFill = GetTemplateChild("PART_TrackFill") as Rectangle;
        _minThumb = GetTemplateChild("PART_MinThumb") as Ellipse;
        _maxThumb = GetTemplateChild("PART_MaxThumb") as Ellipse;

        if (_minThumb != null)
        {
            _minThumb.MouseLeftButtonDown += MinThumb_MouseDown;
        }

        if (_maxThumb != null)
        {
            _maxThumb.MouseLeftButtonDown += MaxThumb_MouseDown;
        }

        if (_trackFill != null)
        {
            _trackFill.MouseLeftButtonDown += TrackFill_MouseDown;
        }

        if (_trackCanvas != null)
        {
            _trackCanvas.SizeChanged += (_, _) => UpdateLayout();
        }

        UpdateLayout();
    }

    // ══════════════════════════════════════════════════════════
    //  Coercion
    // ══════════════════════════════════════════════════════════

    private static object CoerceMinValue(DependencyObject d, object baseValue)
    {
        var slider = (RangeSlider)d;
        var value = (double)baseValue;
        value = Math.Max(value, slider.Minimum);
        value = Math.Min(value, slider.MaxValue - MinGap);
        return value;
    }

    private static object CoerceMaxValue(DependencyObject d, object baseValue)
    {
        var slider = (RangeSlider)d;
        var value = (double)baseValue;
        value = Math.Min(value, slider.Maximum);
        value = Math.Max(value, slider.MinValue + MinGap);
        return value;
    }

    // ══════════════════════════════════════════════════════════
    //  Layout
    // ══════════════════════════════════════════════════════════

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RangeSlider)d).UpdateLayout();
    }

    private new void UpdateLayout()
    {
        if (_trackCanvas == null || _trackBackground == null ||
            _trackFill == null || _minThumb == null || _maxThumb == null)
            return;

        var canvasWidth = _trackCanvas.ActualWidth;
        var canvasHeight = _trackCanvas.ActualHeight;
        if (canvasWidth <= 0)
            return;

        var range = Maximum - Minimum;
        if (range <= 0)
            return;

        var usableWidth = canvasWidth - ThumbDiameter;
        var thumbRadius = ThumbDiameter / 2;

        // Position the track background (full width, centered vertically).
        Canvas.SetLeft(_trackBackground, thumbRadius);
        Canvas.SetTop(_trackBackground, (canvasHeight - TrackHeight) / 2);
        _trackBackground.Width = usableWidth;
        _trackBackground.Height = TrackHeight;

        // Position the fill between min and max.
        var minFraction = (MinValue - Minimum) / range;
        var maxFraction = (MaxValue - Minimum) / range;
        var fillLeft = thumbRadius + minFraction * usableWidth;
        var fillRight = thumbRadius + maxFraction * usableWidth;

        Canvas.SetLeft(_trackFill, fillLeft);
        Canvas.SetTop(_trackFill, (canvasHeight - TrackHeight) / 2);
        _trackFill.Width = Math.Max(0, fillRight - fillLeft);
        _trackFill.Height = TrackHeight;

        // Position thumbs.
        Canvas.SetLeft(_minThumb, minFraction * usableWidth);
        Canvas.SetTop(_minThumb, (canvasHeight - ThumbDiameter) / 2);

        Canvas.SetLeft(_maxThumb, maxFraction * usableWidth);
        Canvas.SetTop(_maxThumb, (canvasHeight - ThumbDiameter) / 2);
    }

    // ══════════════════════════════════════════════════════════
    //  Mouse Handling
    // ══════════════════════════════════════════════════════════

    private void MinThumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingMin = true;
        _dragStartX = e.GetPosition(_trackCanvas).X;
        _dragStartMin = MinValue;
        _minThumb?.CaptureMouse();
        e.Handled = true;
    }

    private void MaxThumb_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingMax = true;
        _dragStartX = e.GetPosition(_trackCanvas).X;
        _dragStartMax = MaxValue;
        _maxThumb?.CaptureMouse();
        e.Handled = true;
    }

    private void TrackFill_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingRange = true;
        _dragStartX = e.GetPosition(_trackCanvas).X;
        _dragStartMin = MinValue;
        _dragStartMax = MaxValue;
        _trackFill?.CaptureMouse();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDraggingMin && !_isDraggingMax && !_isDraggingRange)
            return;

        if (_trackCanvas == null)
            return;

        var pos = e.GetPosition(_trackCanvas);
        var canvasWidth = _trackCanvas.ActualWidth;
        if (canvasWidth <= 0)
            return;

        var usableWidth = canvasWidth - ThumbDiameter;
        if (usableWidth <= 0)
            return;

        var range = Maximum - Minimum;
        var deltaX = pos.X - _dragStartX;
        var deltaValue = deltaX / usableWidth * range;

        if (_isDraggingMin)
        {
            var newMin = Math.Round(_dragStartMin + deltaValue);
            newMin = Math.Max(Minimum, Math.Min(newMin, MaxValue - MinGap));
            MinValue = newMin;
        }
        else if (_isDraggingMax)
        {
            var newMax = Math.Round(_dragStartMax + deltaValue);
            newMax = Math.Min(Maximum, Math.Max(newMax, MinValue + MinGap));
            MaxValue = newMax;
        }
        else if (_isDraggingRange)
        {
            var width = _dragStartMax - _dragStartMin;
            var newMin = Math.Round(_dragStartMin + deltaValue);
            var newMax = newMin + width;

            if (newMin < Minimum)
            {
                newMin = Minimum;
                newMax = newMin + width;
            }

            if (newMax > Maximum)
            {
                newMax = Maximum;
                newMin = newMax - width;
            }

            MinValue = newMin;
            MaxValue = newMax;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isDraggingMin)
        {
            _isDraggingMin = false;
            _minThumb?.ReleaseMouseCapture();
        }

        if (_isDraggingMax)
        {
            _isDraggingMax = false;
            _maxThumb?.ReleaseMouseCapture();
        }

        if (_isDraggingRange)
        {
            _isDraggingRange = false;
            _trackFill?.ReleaseMouseCapture();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Keyboard Handling
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5.0 : 1.0;

        switch (e.Key)
        {
            case Key.Left:
                MinValue = Math.Max(Minimum, MinValue - step);
                e.Handled = true;
                break;
            case Key.Right:
                MinValue = Math.Min(MaxValue - MinGap, MinValue + step);
                e.Handled = true;
                break;
            case Key.Up:
                MaxValue = Math.Min(Maximum, MaxValue + step);
                e.Handled = true;
                break;
            case Key.Down:
                MaxValue = Math.Max(MinValue + MinGap, MaxValue - step);
                e.Handled = true;
                break;
        }
    }
}
