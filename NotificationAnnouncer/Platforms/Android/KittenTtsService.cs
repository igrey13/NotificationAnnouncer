using Android.Speech.Tts;
using NotificationAnnouncer.Services;

namespace NotificationAnnouncer.Platforms.Android;

/// <summary>
/// KittenTTS — wraps the Android <see cref="TextToSpeech"/> engine to implement
/// <see cref="ITtsService"/>. Speaks one utterance at a time; a new call to
/// <see cref="SpeakAsync"/> will interrupt any currently playing speech.
/// </summary>
public sealed class KittenTtsService : Java.Lang.Object, ITtsService, TextToSpeech.IOnInitListener
{
    private readonly TextToSpeech _tts;
    private TaskCompletionSource<bool>? _initTcs = new();
    private TaskCompletionSource<bool>? _speechTcs;

    public KittenTtsService()
    {
        _tts = new TextToSpeech(global::Android.App.Application.Context, this);
    }

    // ── TextToSpeech.IOnInitListener ────────────────────────────────────────

    void TextToSpeech.IOnInitListener.OnInit(OperationResult status)
    {
        if (status == OperationResult.Success)
            _initTcs?.TrySetResult(true);
        else
            _initTcs?.TrySetException(new InvalidOperationException(
                $"KittenTTS initialisation failed with status: {status}"));
    }

    // ── ITtsService ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Wait for the TTS engine to finish initialising
        if (_initTcs is not null)
            await _initTcs.Task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        // Create a TCS that will be resolved by the utterance progress listener
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _speechTcs = tcs;

        using var reg = cancellationToken.Register(() =>
        {
            _tts.Stop();
            tcs.TrySetCanceled();
        });

        var utteranceId = Guid.NewGuid().ToString("N");
        _tts.SetOnUtteranceProgressListener(new UtteranceListener(tcs));

        var result = _tts.Speak(
            text,
            QueueMode.Flush,
            null,
            utteranceId);

        if (result == OperationResult.Error)
        {
            tcs.TrySetException(new InvalidOperationException("KittenTTS Speak() returned an error."));
        }

        await tcs.Task.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Stop() => _tts.Stop();

    // ── Disposable ──────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tts.Stop();
            _tts.Shutdown();
        }
        base.Dispose(disposing);
    }

    // ── Inner listener ──────────────────────────────────────────────────────

    private sealed class UtteranceListener : UtteranceProgressListener
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public UtteranceListener(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public override void OnStart(string? utteranceId) { }

        public override void OnDone(string? utteranceId) =>
            _tcs.TrySetResult(true);

        public override void OnError(string? utteranceId) =>
            _tcs.TrySetException(new InvalidOperationException(
                $"KittenTTS utterance error for id: {utteranceId}"));
    }
}
