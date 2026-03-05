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

                case BeatDetectionMode.OnPeakAndValley:
                    if ((curr > prev && curr >= next) || (curr < prev && curr <= next))
                        beats.Add(actions[i].AtMs);
                    break;
            }
        }

        // MidStroke uses adjacent-pair iteration instead of the 3-point window above
        if (mode == BeatDetectionMode.MidStroke)
        {
            beats.Clear();
            for (int i = 0; i < actions.Count - 1; i++)
            {
                var a = actions[i];
                var b = actions[i + 1];

                // Only consider descending pairs (a.Pos > 50 and b.Pos < 50)
                if (a.Pos > 50 && b.Pos < 50)
                {
                    // Linear interpolation to find when the value crosses 50
                    var ratio = (a.Pos - 50.0) / (a.Pos - b.Pos);
                    var crossingTimeMs = a.AtMs + ratio * (b.AtMs - a.AtMs);
                    beats.Add(Math.Round(crossingTimeMs));
                }
            }
        }

        return beats;
    }
}
