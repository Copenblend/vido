namespace Vido.Core.Logging;

/// <summary>
/// Severity levels for log entries.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Verbose diagnostic information for developers.
    /// </summary>
    Debug,

    /// <summary>
    /// General informational messages about application flow.
    /// </summary>
    Info,

    /// <summary>
    /// Potentially harmful conditions that merit attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Error events that indicate failures or unexpected behavior.
    /// </summary>
    Error
}
