namespace Vido.Core.Events;

/// <summary>
/// Published when a funscript file has been generated from Pulse beat data.
/// Subscribers (e.g. axis control) should reload scripts for the video.
/// </summary>
public readonly record struct FunscriptGeneratedEvent
{
    /// <summary>Full path to the generated .funscript file.</summary>
    public string FilePath { get; init; }

    /// <summary>Full path to the video file the funscript was generated for.</summary>
    public string VideoPath { get; init; }
}
