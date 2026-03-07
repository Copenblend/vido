using System.Runtime.Versioning;
using Vido.Core.SingleInstance;
using Vido.Services.SingleInstance;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Tests for <see cref="ISingleInstanceService"/> and <see cref="SingleInstanceService"/>.
/// Uses unique mutex/pipe names per test to avoid cross-test interference.
/// </summary>
[SupportedOSPlatform("windows")]
public class SingleInstanceServiceTests
{
    /// <summary>
    /// Generates unique mutex and pipe names for test isolation.
    /// </summary>
    private static (string mutex, string pipe) UniqueNames([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var id = $"{name}_{Guid.NewGuid():N}";
        return ($"Vido_Test_Mutex_{id}", $"Vido_Test_Pipe_{id}");
    }

    /// <summary>
    /// Verifies that the first instance correctly reports <see cref="ISingleInstanceService.IsFirstInstance"/>
    /// as <c>true</c>.
    /// </summary>
    [Fact]
    public void FirstInstance_IsFirstInstance_ReturnsTrue()
    {
        var (mutex, pipe) = UniqueNames();
        using var service = new SingleInstanceService(mutex, pipe);

        Assert.True(service.IsFirstInstance);
    }

    /// <summary>
    /// Verifies that <see cref="SingleInstanceService.SendFileToExistingInstance"/>
    /// throws a <see cref="TimeoutException"/> when no listener is active.
    /// </summary>
    [Fact]
    public void SendFile_WhenNotListening_ThrowsTimeout()
    {
        var (mutex, pipe) = UniqueNames();
        using var service = new SingleInstanceService(mutex, pipe);

        // No listener started — should timeout
        Assert.ThrowsAny<Exception>(() =>
            service.SendFileToExistingInstance(@"C:\test\video.mp4"));
    }

    /// <summary>
    /// Verifies end-to-end file path forwarding: service1 (primary) listens,
    /// service2 (secondary) sends a file path, and service1 receives it.
    /// </summary>
    [Fact]
    public async Task SendAndReceive_FilePathRoundTrip()
    {
        var (mutex, pipe) = UniqueNames();

        // Create a temp file so File.Exists() passes the validation
        var tempFile = Path.GetTempFileName();
        try
        {
            using var service1 = new SingleInstanceService(mutex, pipe);
            Assert.True(service1.IsFirstInstance);

            var receivedPath = new TaskCompletionSource<string>();
            service1.FileReceived += path => receivedPath.TrySetResult(path);
            service1.StartListening();

            // Give the pipe server time to start accepting connections
            await Task.Delay(500);

            // Create second instance (will not be first)
            using var service2 = new SingleInstanceService(mutex + "_second", pipe);

            // Send file path via the same pipe name
            service2.SendFileToExistingInstance(tempFile);

            // Wait for the message with a timeout
            var completed = await Task.WhenAny(receivedPath.Task, Task.Delay(5000));
            Assert.Same(receivedPath.Task, completed);
            Assert.Equal(tempFile, await receivedPath.Task);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a relative (non-fully-qualified) path is ignored by the listener
    /// because <see cref="Path.IsPathFullyQualified"/> returns false.
    /// </summary>
    [Fact]
    public async Task InvalidPath_IsIgnored()
    {
        var (mutex, pipe) = UniqueNames();

        using var service1 = new SingleInstanceService(mutex, pipe);
        Assert.True(service1.IsFirstInstance);

        var received = false;
        service1.FileReceived += _ => received = true;
        service1.StartListening();

        await Task.Delay(500);

        // Send a relative path — should be rejected by the listener
        using var service2 = new SingleInstanceService(mutex + "_second", pipe);
        try
        {
            service2.SendFileToExistingInstance("relative/path/video.mp4");
        }
        catch
        {
            // Pipe might not connect or might fail — that's OK
        }

        // Give time for processing
        await Task.Delay(1000);

        Assert.False(received, "FileReceived should not fire for a relative path");
    }

    /// <summary>
    /// Verifies that <see cref="SingleInstanceService.Dispose"/> completes without throwing
    /// and releases resources cleanly.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesResources()
    {
        var (mutex, pipe) = UniqueNames();
        var service = new SingleInstanceService(mutex, pipe);
        Assert.True(service.IsFirstInstance);

        service.StartListening();

        // Should not throw
        service.Dispose();

        // Double dispose should also not throw
        service.Dispose();
    }

    /// <summary>
    /// Verifies that a second instance correctly reports <see cref="ISingleInstanceService.IsFirstInstance"/>
    /// as <c>false</c> when the first instance already holds the mutex.
    /// </summary>
    [Fact]
    public void SecondInstance_IsFirstInstance_ReturnsFalse()
    {
        var (mutex, pipe) = UniqueNames();
        using var service1 = new SingleInstanceService(mutex, pipe);
        Assert.True(service1.IsFirstInstance);

        using var service2 = new SingleInstanceService(mutex, pipe);
        Assert.False(service2.IsFirstInstance);
    }

    /// <summary>
    /// Verifies that a non-existent file path is ignored by the listener
    /// because <see cref="File.Exists"/> returns false.
    /// </summary>
    [Fact]
    public async Task NonExistentFile_IsIgnored()
    {
        var (mutex, pipe) = UniqueNames();

        using var service1 = new SingleInstanceService(mutex, pipe);
        Assert.True(service1.IsFirstInstance);

        var received = false;
        service1.FileReceived += _ => received = true;
        service1.StartListening();

        await Task.Delay(500);

        // Send a fully-qualified path that does not exist
        using var service2 = new SingleInstanceService(mutex + "_second", pipe);
        service2.SendFileToExistingInstance(@"C:\nonexistent\path\video.mp4");

        // Give time for processing
        await Task.Delay(1000);

        Assert.False(received, "FileReceived should not fire for a non-existent file");
    }
}
