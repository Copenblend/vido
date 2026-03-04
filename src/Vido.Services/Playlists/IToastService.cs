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
}
