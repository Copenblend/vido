namespace Vido.Services.Osr2Plus;

/// <summary>
/// Abstraction for sending TCode commands to an OSR2+ device
/// via different transport mechanisms (Serial, UDP).
/// </summary>
public interface ITransportService : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected and ready to send data.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets a human-readable label describing the current connection (e.g. "COM:COM3", "UDP:8888").
    /// <c>null</c> when not connected.
    /// </summary>
    string? ConnectionLabel { get; }

    /// <summary>
    /// Fired when the connection state changes. The <see cref="bool"/> parameter is the new
    /// <see cref="IsConnected"/> value.
    /// </summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>
    /// Fired when a transport error occurs (send failure, connection drop, etc.).
    /// The <see cref="string"/> parameter is the error message.
    /// </summary>
    event Action<string>? ErrorOccurred;

    /// <summary>
    /// Sends a TCode command string to the connected device.
    /// </summary>
    /// <param name="data">The TCode command string to send.</param>
    void Send(string data);

    /// <summary>
    /// Sends pre-encoded TCode bytes to the connected device.
    /// Preferred for allocation-free hot paths.
    /// </summary>
    /// <param name="data">The pre-encoded UTF-8 TCode bytes to send.</param>
    void Send(ReadOnlySpan<byte> data);

    /// <summary>
    /// Disconnects from the device and releases transport resources.
    /// Safe to call when already disconnected.
    /// </summary>
    void Disconnect();
}
