namespace Vido.Services.Playlists;

/// <summary>
/// Provides toast notification display capabilities.
/// Implemented by <c>ToastService</c> in the Views layer.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Shows an info toast notification.
    /// Auto-dismisses after a short delay.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    void Show(string message, string? boldSuffix = null);

    /// <summary>
    /// Shows an error toast notification.
    /// Auto-dismisses after a short delay.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    void ShowError(string message, string? boldSuffix = null);

    /// <summary>
    /// Shows an actionable info toast with a close button and click handler.
    /// The toast body is clickable and invokes the specified callback.
    /// Auto-dismisses after the specified duration.
    /// </summary>
    /// <param name="message">Primary toast message.</param>
    /// <param name="boldSuffix">Optional highlighted suffix text.</param>
    /// <param name="onClick">Action invoked when the toast body is clicked.</param>
    /// <param name="durationSeconds">Custom auto-dismiss duration in seconds.</param>
    void ShowActionable(string message, string? boldSuffix, Action onClick, double durationSeconds = 10.0);
}
