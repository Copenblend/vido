namespace Vido.Core.SingleInstance;

/// <summary>
/// Enforces single-instance application behavior using a named Mutex
/// and named pipe IPC for forwarding file paths between instances.
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>
    /// Returns <c>true</c> if this is the first (primary) instance.
    /// </summary>
    bool IsFirstInstance { get; }

    /// <summary>
    /// Raised on the primary instance when a second instance sends a file path.
    /// The string argument is the absolute file path.
    /// </summary>
    event Action<string>? FileReceived;

    /// <summary>
    /// Sends the given file path to the already-running primary instance via named pipe.
    /// Blocks for up to 3 seconds. Throws <see cref="TimeoutException"/> or
    /// <see cref="System.IO.IOException"/> on failure.
    /// </summary>
    /// <param name="filePath">The absolute file path to forward to the primary instance.</param>
    void SendFileToExistingInstance(string filePath);

    /// <summary>
    /// Starts listening for messages from secondary instances.
    /// Call only on the primary instance after UI is ready.
    /// </summary>
    void StartListening();
}
