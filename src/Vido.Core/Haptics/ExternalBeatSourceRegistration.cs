namespace Vido.Core.Haptics;

/// <summary>
/// Published on <c>IEventBus</c> when an external beat source is registered or unregistered.
/// The haptic transport subscribes to these to dynamically populate its BeatBar mode list.
/// </summary>
/// <remarks>
/// A registering event should provide a non-null <see cref="Source"/>.
/// For <c>default(ExternalBeatSourceRegistration)</c>, <see cref="Source"/> is null.
/// </remarks>
public readonly record struct ExternalBeatSourceRegistration
{
    /// <summary>The beat source being registered or unregistered.</summary>
    public IExternalBeatSource? Source { get; init; }

    /// <summary><c>true</c> to register, <c>false</c> to unregister.</summary>
    public bool IsRegistering { get; init; }
}
