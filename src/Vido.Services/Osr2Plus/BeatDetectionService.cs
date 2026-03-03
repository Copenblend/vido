using Vido.Core.Models.Osr2Plus;

namespace Vido.Services.Osr2Plus;

/// <summary>
/// Detects beat timestamps (peaks or valleys) from funscript action data.
/// Used by the beat bar visualization to highlight rhythmic points during playback.
/// </summary>
public class BeatDetectionService
{
    /// <summary>
    /// Detects beat timestamps from funscript actions based on the selected detection mode.
    /// A peak is a local maximum (higher than both neighbors), and a valley is a local minimum
    /// (lower than both neighbors).
    /// </summary>
    /// <param name="script">The parsed funscript data (typically L0 axis). Can be <c>null</c>.</param>
    /// <param name="mode">Whether to detect peaks or valleys.</param>
    /// <returns>Sorted list of beat times in milliseconds. Empty if fewer than 3 actions.</returns>
    public List<double> DetectBeats(FunscriptData? script, BeatDetectionMode mode)
    {
        if (script is null || script.Actions.Count < 3)
            return new List<double>();

        var actions = script.Actions;
        var beats = new List<double>();

        for (int i = 1; i < actions.Count - 1; i++)
        {
            var prev = actions[i - 1].Pos;
            var curr = actions[i].Pos;
            var next = actions[i + 1].Pos;

            switch (mode)
            {
                case BeatDetectionMode.OnPeak when curr > prev && curr >= next:
                    beats.Add(actions[i].AtMs);
                    break;

                case BeatDetectionMode.OnValley when curr < prev && curr <= next:
                    beats.Add(actions[i].AtMs);
                    break;
            }
        }

        return beats;
    }
}
