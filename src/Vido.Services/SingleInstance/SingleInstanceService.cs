using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using Vido.Core.SingleInstance;

namespace Vido.Services.SingleInstance;

/// <summary>
/// Enforces single-instance application behavior using a named Mutex for instance
/// detection and named pipes for forwarding file paths from secondary instances
/// to the primary instance.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string MutexName = "Vido_SingleInstance_Mutex";
    private const string PipeName = "Vido_SingleInstance_Pipe";
    private const int PipeTimeoutMs = 3000;

    private readonly Mutex _mutex;
    private readonly bool _isFirstInstance;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsFirstInstance => _isFirstInstance;

    /// <inheritdoc />
    public event Action<string>? FileReceived;

    /// <summary>
    /// Initializes a new instance of <see cref="SingleInstanceService"/>.
    /// Acquires (or attempts to acquire) the named mutex to determine
    /// whether this is the first instance.
    /// </summary>
    public SingleInstanceService()
    {
        _mutex = new Mutex(true, MutexName, out _isFirstInstance);
    }

    /// <summary>
    /// Initializes a new instance with the specified mutex and pipe names.
    /// Used for testing to avoid collisions with real instances.
    /// </summary>
    /// <param name="mutexName">The name of the mutex to use.</param>
    /// <param name="pipeName">The name of the named pipe to use.</param>
    internal SingleInstanceService(string mutexName, string pipeName)
    {
        MutexNameOverride = mutexName;
        PipeNameOverride = pipeName;
        _mutex = new Mutex(true, mutexName, out _isFirstInstance);
    }

    /// <summary>
    /// Override pipe name used by tests for isolation.
    /// </summary>
    internal string? PipeNameOverride { get; }

    /// <summary>
    /// Override mutex name used by tests for isolation.
    /// </summary>
    internal string? MutexNameOverride { get; }

    /// <summary>
    /// Gets the effective pipe name (override or default).
    /// </summary>
    private string EffectivePipeName => PipeNameOverride ?? PipeName;

    /// <inheritdoc />
    public void SendFileToExistingInstance(string filePath)
    {
        using var client = new NamedPipeClientStream(".", EffectivePipeName, PipeDirection.Out);
        client.Connect(PipeTimeoutMs);

        var bytes = Encoding.UTF8.GetBytes(filePath);
        client.Write(bytes, 0, bytes.Length);
        client.Flush();
        client.WaitForPipeDrain();
    }

    /// <inheritdoc />
    public void StartListening()
    {
        if (!_isFirstInstance) return;

        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    /// <summary>
    /// Background loop that accepts named pipe connections from secondary instances
    /// and raises <see cref="FileReceived"/> with the received file path.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the listen loop.</param>
    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    EffectivePipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadToEndAsync(ct);

                server.Disconnect();
                server.Dispose();
                server = null;

                if (!string.IsNullOrWhiteSpace(message) &&
                    Path.IsPathFullyQualified(message) &&
                    File.Exists(message))
                {
                    // Fire on a ThreadPool thread to avoid blocking the listen loop
                    ThreadPool.QueueUserWorkItem(_ => FileReceived?.Invoke(message));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown — exit loop
                server?.Dispose();
                break;
            }
            catch (Exception)
            {
                // Pipe error — dispose and restart listener
                server?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        try
        {
            if (_isFirstInstance)
                _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex was not owned — ignore
        }

        _mutex.Dispose();
    }
}
