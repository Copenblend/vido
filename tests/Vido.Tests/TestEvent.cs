namespace Vido.Tests;

internal sealed class TestEvent
{
    /// <summary>
    /// Gets or sets the text payload carried by this test event.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
