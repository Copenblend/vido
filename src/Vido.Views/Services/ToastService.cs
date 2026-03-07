using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Vido.Core.Settings;
using Vido.Services.Playlists;

namespace Vido.Views.Services;

/// <summary>
/// Shows VS Code-style toast notifications in the bottom-right corner
/// of the Vido main window, above the status bar.
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly SolidColorBrush WhiteBrush = CreateFrozenBrush(Colors.White);
    private static readonly SolidColorBrush InfoBackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
    private static readonly SolidColorBrush InfoBorderBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0x5A, 0x9E));
    private static readonly SolidColorBrush ErrorBackgroundBrush = CreateFrozenBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly SolidColorBrush ErrorBorderBrush = CreateFrozenBrush(Color.FromRgb(0x9E, 0x22, 0x16));
    private static readonly SolidColorBrush CloseButtonHoverBrush = CreateFrozenBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
    private static readonly SolidColorBrush TransparentBrush = CreateFrozenBrush(Colors.Transparent);
    private static readonly System.Windows.Media.Effects.DropShadowEffect SharedShadowEffect = CreateFrozenShadowEffect();

    private readonly ISettingsService? _settingsService;
    private Border? _currentToast;
    private DispatcherTimer? _hideTimer;

    /// <summary>
    /// Creates a new ToastService, optionally reading toast duration from settings.
    /// </summary>
    /// <param name="settingsService">Optional settings service for configurable toast duration.</param>
    public ToastService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Shows an info toast (blue accent background).
    /// Auto-dismisses after the configured duration (default 3 seconds) with a fade animation.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    public void Show(string message, string? boldSuffix = null)
    {
        ShowInternal(message, boldSuffix,
            background: InfoBackgroundBrush,
            border: InfoBorderBrush,
            icon: "\uE946"); // info icon
    }

    /// <summary>
    /// Shows an error toast (red background matching Vido's close button).
    /// Auto-dismisses after 3 seconds with a fade animation.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    public void ShowError(string message, string? boldSuffix = null)
    {
        ShowInternal(message, boldSuffix,
            background: ErrorBackgroundBrush,
            border: ErrorBorderBrush,
            icon: "\uEA39"); // error/warning icon
    }

    /// <summary>
    /// Shows an actionable info toast with a close button and click handler.
    /// The toast body is clickable and invokes the specified callback.
    /// Auto-dismisses after the specified duration.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    /// <param name="onClick">Action invoked when the toast body is clicked.</param>
    /// <param name="durationSeconds">Custom auto-dismiss duration in seconds.</param>
    public void ShowActionable(string message, string? boldSuffix, Action onClick, double durationSeconds = 10.0)
    {
        ShowInternal(message, boldSuffix,
            background: InfoBackgroundBrush,
            border: InfoBorderBrush,
            icon: "\uE946",
            onClick: onClick,
            durationOverride: durationSeconds);
    }

    private void ShowInternal(string message, string? boldSuffix, Brush background, Brush border, string icon,
        Action? onClick = null, double? durationOverride = null)
    {
        var app = Application.Current;
        if (app is null) return;

        app.Dispatcher.Invoke(() =>
        {
            var mainWindow = app.MainWindow;
            if (mainWindow?.Content is not Border windowBorder) return;
            if (windowBorder.Child is not Grid rootGrid) return;

            // Remove any existing toast
            RemoveCurrentToast(rootGrid);

            // Notification icon
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Message text
            var textBlock = new TextBlock
            {
                Foreground = WhiteBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 280,
                VerticalAlignment = VerticalAlignment.Center
            };

            textBlock.Inlines.Add(new Run(message));
            if (!string.IsNullOrEmpty(boldSuffix))
            {
                textBlock.Inlines.Add(new Run(boldSuffix) { FontWeight = FontWeights.Bold });
            }

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { iconBlock, textBlock }
            };

            // Build toast content: either simple panel or grid with close button
            UIElement toastChild;
            if (onClick is not null)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                contentPanel.Cursor = Cursors.Hand;
                Grid.SetColumn(contentPanel, 0);
                grid.Children.Add(contentPanel);

                var closeButton = CreateCloseButton();
                Grid.SetColumn(closeButton, 1);
                grid.Children.Add(closeButton);

                toastChild = grid;
            }
            else
            {
                toastChild = contentPanel;
            }

            // Notification container
            var toast = new Border
            {
                Background = background,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 12, 8),
                IsHitTestVisible = onClick is not null,
                Opacity = 0,
                Effect = SharedShadowEffect,
                Child = toastChild
            };

            // Place in row 1 (content area) so it sits above the status bar (row 2)
            Grid.SetRow(toast, 1);

            // Wire up click and close handlers for actionable toasts
            if (onClick is not null)
            {
                contentPanel.MouseLeftButtonDown += (_, _) =>
                {
                    _hideTimer?.Stop();
                    rootGrid.Children.Remove(toast);
                    if (ReferenceEquals(_currentToast, toast))
                        _currentToast = null;
                    try { onClick(); } catch { /* Toast interactions must never crash the app. */ }
                };

                var closeButton = ((Grid)toast.Child).Children.OfType<Button>().First();
                closeButton.Click += (_, _) =>
                {
                    _hideTimer?.Stop();
                    FadeOutAndRemove(toast, rootGrid);
                };
            }

            rootGrid.Children.Add(toast);
            _currentToast = toast;

            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Auto-dismiss timer
            _hideTimer?.Stop();
            var duration = durationOverride ?? _settingsService?.Current.ToastDurationSeconds ?? 3.0;
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(duration) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                FadeOutAndRemove(toast, rootGrid);
            };
            _hideTimer.Start();
        });
    }

    private void FadeOutAndRemove(Border toast, Grid rootGrid)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            rootGrid.Children.Remove(toast);
            if (ReferenceEquals(_currentToast, toast))
                _currentToast = null;
        };
        toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void RemoveCurrentToast(Grid rootGrid)
    {
        if (_currentToast is not null)
        {
            _hideTimer?.Stop();
            rootGrid.Children.Remove(_currentToast);
            _currentToast = null;
        }
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Button CreateCloseButton()
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = WhiteBrush
            },
            Width = 24,
            Height = 24,
            Background = TransparentBrush,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(0)
        };

        // Style: transparent background, hover to dark gray
        button.Template = CreateCloseButtonTemplate();

        return button;
    }

    private static ControlTemplate CreateCloseButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "CloseButtonBorder";
        borderFactory.SetValue(Border.BackgroundProperty, TransparentBrush);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        borderFactory.SetValue(FrameworkElement.WidthProperty, 24.0);
        borderFactory.SetValue(FrameworkElement.HeightProperty, 24.0);

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);

        template.VisualTree = borderFactory;

        // Hover trigger
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, CloseButtonHoverBrush, "CloseButtonBorder"));
        template.Triggers.Add(hoverTrigger);

        return template;
    }

    private static System.Windows.Media.Effects.DropShadowEffect CreateFrozenShadowEffect()
    {
        var shadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 10,
            ShadowDepth = 2,
            Opacity = 0.5
        };
        shadow.Freeze();
        return shadow;
    }
}
