using System.Net;
using System.Net.Sockets;
using Vido.Services.Osr2Plus;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for the OSR2+ transport service types:
/// <see cref="ITransportService"/>, <see cref="SerialTransportService"/>,
/// and <see cref="UdpTransportService"/>.
/// </summary>
public class TransportServiceTests
{
    // ──────────────────────────────────────────────
    //  SerialTransportService
    // ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="SerialTransportService.ListPorts"/> returns
    /// without throwing (may return an empty array in CI environments).
    /// </summary>
    [Fact]
    public void SerialTransport_ListPorts_ReturnsWithoutThrowing()
    {
        var ports = SerialTransportService.ListPorts();
        Assert.NotNull(ports);
    }

    /// <summary>
    /// Verifies default state of a new <see cref="SerialTransportService"/>.
    /// </summary>
    [Fact]
    public void SerialTransport_DefaultState_NotConnected()
    {
        using var transport = new SerialTransportService();

        Assert.False(transport.IsConnected);
        Assert.Null(transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that sending on a disconnected serial transport is a no-op (no throw).
    /// </summary>
    [Fact]
    public void SerialTransport_SendWhenDisconnected_NoOp()
    {
        using var transport = new SerialTransportService();

        // Should not throw
        transport.Send("L0000I0000\n");
        transport.Send(System.Text.Encoding.UTF8.GetBytes("L0000I0000\n"));
    }

    /// <summary>
    /// Verifies that disconnecting when already disconnected is safe.
    /// </summary>
    [Fact]
    public void SerialTransport_DisconnectWhenNotConnected_NoOp()
    {
        using var transport = new SerialTransportService();

        // Should not throw
        transport.Disconnect();
        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// Verifies that connecting to an invalid COM port returns false and fires error event.
    /// </summary>
    [Fact]
    public void SerialTransport_ConnectInvalidPort_ReturnsFalseAndFiresError()
    {
        using var transport = new SerialTransportService();

        string? errorMessage = null;
        transport.ErrorOccurred += msg => errorMessage = msg;

        var result = transport.Connect("INVALID_PORT_ZZZZZ");

        Assert.False(result);
        Assert.False(transport.IsConnected);
        Assert.NotNull(errorMessage);
        Assert.Contains("Serial connect error", errorMessage);
    }

    /// <summary>
    /// Verifies that Dispose calls Disconnect.
    /// </summary>
    [Fact]
    public void SerialTransport_Dispose_DisconnectsCleanly()
    {
        var transport = new SerialTransportService();
        transport.Dispose();

        // After dispose, should still be safe to query state
        Assert.False(transport.IsConnected);
        Assert.Null(transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that multiple Dispose calls are safe.
    /// </summary>
    [Fact]
    public void SerialTransport_MultipleDispose_NoThrow()
    {
        var transport = new SerialTransportService();
        transport.Dispose();
        transport.Dispose(); // Should not throw
    }

    /// <summary>
    /// Verifies that the <see cref="SerialTransportService"/> implements <see cref="ITransportService"/>.
    /// </summary>
    [Fact]
    public void SerialTransport_ImplementsITransportService()
    {
        using var transport = new SerialTransportService();
        Assert.IsAssignableFrom<ITransportService>(transport);
    }

    // ──────────────────────────────────────────────
    //  UdpTransportService
    // ──────────────────────────────────────────────

    /// <summary>
    /// Verifies default state of a new <see cref="UdpTransportService"/>.
    /// </summary>
    [Fact]
    public void UdpTransport_DefaultState_NotConnected()
    {
        using var transport = new UdpTransportService();

        Assert.False(transport.IsConnected);
        Assert.Null(transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that <see cref="UdpTransportService.Connect(int)"/> sets connected state.
    /// </summary>
    [Fact]
    public void UdpTransport_Connect_SetsConnectedState()
    {
        using var transport = new UdpTransportService();

        var result = transport.Connect(8888);

        Assert.True(result);
        Assert.True(transport.IsConnected);
        Assert.Equal("UDP:8888", transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that connecting fires the ConnectionChanged event.
    /// </summary>
    [Fact]
    public void UdpTransport_Connect_FiresConnectionChanged()
    {
        using var transport = new UdpTransportService();

        bool? connectionState = null;
        transport.ConnectionChanged += state => connectionState = state;

        transport.Connect(8888);

        Assert.True(connectionState);
    }

    /// <summary>
    /// Verifies full connect/send/disconnect lifecycle.
    /// </summary>
    [Fact]
    public void UdpTransport_ConnectSendDisconnect_FullLifecycle()
    {
        using var transport = new UdpTransportService();

        // Connect
        Assert.True(transport.Connect(18888));
        Assert.True(transport.IsConnected);

        // Send string — should not throw
        transport.Send("L0500I1000\n");

        // Send bytes — should not throw
        transport.Send(System.Text.Encoding.UTF8.GetBytes("L0500I1000\n"));

        // Disconnect
        transport.Disconnect();
        Assert.False(transport.IsConnected);
        Assert.Null(transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that disconnecting fires the ConnectionChanged event with false.
    /// </summary>
    [Fact]
    public void UdpTransport_Disconnect_FiresConnectionChangedFalse()
    {
        using var transport = new UdpTransportService();
        transport.Connect(8888);

        bool? lastState = null;
        transport.ConnectionChanged += state => lastState = state;

        transport.Disconnect();

        Assert.False(lastState);
    }

    /// <summary>
    /// Verifies that disconnecting when already disconnected is safe and does not fire event.
    /// </summary>
    [Fact]
    public void UdpTransport_DisconnectWhenNotConnected_NoOp()
    {
        using var transport = new UdpTransportService();

        bool eventFired = false;
        transport.ConnectionChanged += _ => eventFired = true;

        transport.Disconnect();

        Assert.False(eventFired);
    }

    /// <summary>
    /// Verifies that sending on a disconnected UDP transport is a no-op (no throw).
    /// </summary>
    [Fact]
    public void UdpTransport_SendWhenDisconnected_NoOp()
    {
        using var transport = new UdpTransportService();

        // Should not throw
        transport.Send("L0000I0000\n");
        transport.Send(System.Text.Encoding.UTF8.GetBytes("L0000I0000\n"));
    }

    /// <summary>
    /// Verifies that reconnecting replaces the previous connection.
    /// </summary>
    [Fact]
    public void UdpTransport_Reconnect_ReplacesConnection()
    {
        using var transport = new UdpTransportService();

        transport.Connect(8001);
        Assert.Equal("UDP:8001", transport.ConnectionLabel);

        transport.Connect(8002);
        Assert.Equal("UDP:8002", transport.ConnectionLabel);
        Assert.True(transport.IsConnected);
    }

    /// <summary>
    /// Verifies that the <see cref="UdpTransportService"/> implements <see cref="ITransportService"/>.
    /// </summary>
    [Fact]
    public void UdpTransport_ImplementsITransportService()
    {
        using var transport = new UdpTransportService();
        Assert.IsAssignableFrom<ITransportService>(transport);
    }

    /// <summary>
    /// Verifies that Dispose disconnects the UDP transport.
    /// </summary>
    [Fact]
    public void UdpTransport_Dispose_DisconnectsCleanly()
    {
        var transport = new UdpTransportService();
        transport.Connect(8888);
        Assert.True(transport.IsConnected);

        transport.Dispose();

        Assert.False(transport.IsConnected);
        Assert.Null(transport.ConnectionLabel);
    }

    /// <summary>
    /// Verifies that multiple Dispose calls are safe.
    /// </summary>
    [Fact]
    public void UdpTransport_MultipleDispose_NoThrow()
    {
        var transport = new UdpTransportService();
        transport.Connect(8888);
        transport.Dispose();
        transport.Dispose(); // Should not throw
    }

    /// <summary>
    /// Verifies that a UDP listener receives data sent by the transport.
    /// </summary>
    [Fact]
    public void UdpTransport_SendString_DataReceivedByListener()
    {
        // Bind a listener on an ephemeral port
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var listenerPort = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var transport = new UdpTransportService();
        transport.Connect(listenerPort);

        // Send TCode command
        transport.Send("L0500I1000\n");

        // Receive and verify
        var remote = new IPEndPoint(IPAddress.Any, 0);
        var received = listener.Receive(ref remote);
        var text = System.Text.Encoding.UTF8.GetString(received);
        Assert.Equal("L0500I1000\n", text);
    }

    /// <summary>
    /// Verifies that a UDP listener receives byte data sent by the transport.
    /// </summary>
    [Fact]
    public void UdpTransport_SendBytes_DataReceivedByListener()
    {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var listenerPort = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var transport = new UdpTransportService();
        transport.Connect(listenerPort);

        var payload = System.Text.Encoding.UTF8.GetBytes("R0750I500\n");
        transport.Send(payload);

        var remote = new IPEndPoint(IPAddress.Any, 0);
        var received = listener.Receive(ref remote);
        Assert.Equal(payload, received);
    }

    /// <summary>
    /// Verifies that the reconnect fires disconnect event for the old connection
    /// and connect event for the new one.
    /// </summary>
    [Fact]
    public void UdpTransport_Reconnect_FiresDisconnectThenConnect()
    {
        using var transport = new UdpTransportService();
        transport.Connect(8001);

        var states = new List<bool>();
        transport.ConnectionChanged += state => states.Add(state);

        transport.Connect(8002);

        // Reconnect should fire: false (disconnect old), true (connect new)
        Assert.Equal([false, true], states);
    }
}
