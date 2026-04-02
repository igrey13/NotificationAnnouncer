namespace NotificationAnnouncer.Services;

/// <summary>
/// Abstraction over a text-to-speech engine.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Speak <paramref name="text"/> asynchronously.
    /// The returned <see cref="Task"/> completes when the utterance finishes (or is interrupted).
    /// </summary>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Stops any currently playing utterance.</summary>
    void Stop();
}
