using System.IO.Ports;
using System.Text;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Serial port transport for TCode output. Sends TCode commands
/// over a COM port to a physically connected OSR2+ device.
/// </summary>
public class SerialTransportService : ITransportService
{
    private readonly object _lock = new();
    private readonly object _sendLock = new();
    private SerialPort? _port;

    /// <inheritdoc/>
    public bool IsConnected
    {
        get { lock (_lock) { return _port?.IsOpen ?? false; } }
    }

    /// <inheritdoc/>
    public string? ConnectionLabel
    {
        get { lock (_lock) { return _port?.IsOpen == true ? $"COM:{_port.PortName}" : null; } }
    }

    /// <inheritdoc/>
    public event Action<bool>? ConnectionChanged;

    /// <inheritdoc/>
    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// Returns the list of available serial port names on this machine.
    /// </summary>
    /// <returns>An array of available serial port names (e.g. "COM3", "COM4").</returns>
    public static string[] ListPorts() => SerialPort.GetPortNames();

    /// <summary>
    /// Opens a serial connection on the specified port.
    /// </summary>
    /// <param name="portName">COM port name (e.g. "COM3").</param>
    /// <param name="baudRate">Baud rate (default 115200).</param>
    /// <returns><c>true</c> if connection succeeded; <c>false</c> on error.</returns>
    public bool Connect(string portName, int baudRate = 115200)
    {
        try
        {
            Disconnect();

            lock (_lock)
            {
                _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _port.ErrorReceived += (_, e) =>
                {
                    ErrorOccurred?.Invoke($"Serial error: {e.EventType}");
                };

                _port.Open();
            }

            ConnectionChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Serial connect error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public void Send(string data)
    {
        Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetMaxByteCount(data.Length)];
        var written = Encoding.UTF8.GetBytes(data.AsSpan(), buffer);
        Send(buffer[..written]);
    }

    /// <inheritdoc/>
    public void Send(ReadOnlySpan<byte> data)
    {
        SerialPort? port;
        lock (_lock) { port = _port; }
        if (port?.IsOpen != true) return;

        lock (_sendLock)
        {
            try
            {
                if (port.IsOpen)
                    port.BaseStream.Write(data);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException or UnauthorizedAccessException)
            {
                ErrorOccurred?.Invoke($"Serial send error: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public void Disconnect()
    {
        SerialPort? portToClose;
        lock (_lock)
        {
            portToClose = _port;
            _port = null;               // Remove reference immediately so Send() sees null
        }

        if (portToClose is null) return;

        var wasConnected = false;
        try
        {
            wasConnected = portToClose.IsOpen;
            // Close on ThreadPool to avoid blocking UI if a Write is in progress
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    portToClose.Close();
                    portToClose.Dispose();
                }
                catch (Exception)
                {
                    // Port may already be dead — best-effort cleanup
                }
            });
        }
        catch (Exception)
        {
            // Ignore — port is being abandoned
        }

        if (wasConnected)
            ConnectionChanged?.Invoke(false);
    }

    /// <summary>
    /// Disposes the transport, disconnecting if connected.
    /// </summary>
    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
