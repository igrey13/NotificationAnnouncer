using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using NotificationAnnouncer.Services;

namespace NotificationAnnouncer.Platforms.Android;

/// <summary>
/// Android Foreground Service that keeps the app alive in the background,
/// subscribes to the <see cref="MessageQueue"/>, and reads each message aloud
/// via <see cref="KittenTtsService"/> — one message at a time.
/// </summary>
[Service(
    Name = "com.igrey13.notificationannouncer.AnnouncerForegroundService",
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public class AnnouncerForegroundService : Service
{
    private const string ChannelId   = "announcer_foreground";
    private const string ChannelName = "Announcer Service";
    private const int    NotificationId = 1001;

    private static volatile bool _isRunning;

    /// <summary>Returns <c>true</c> when the foreground service is active.</summary>
    public static bool IsRunning => _isRunning;

    private MessageQueue?      _messageQueue;
    private KittenTtsService?  _tts;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _drainTrigger = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void OnCreate()
    {
        base.OnCreate();
        _messageQueue = MauiApplication.Current.Services.GetService<MessageQueue>();
        _tts          = new KittenTtsService();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        StartForeground(NotificationId, BuildNotification());

        _isRunning = true;
        _cts = new CancellationTokenSource();

        // Subscribe to the queue and process messages on a background thread
        if (_messageQueue is not null)
            _messageQueue.MessageAdded += OnMessageAdded;

        // Drain any messages that arrived before the service started
        _ = Task.Run(() => DrainQueueAsync(_cts.Token), _cts.Token);

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _isRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_messageQueue is not null)
            _messageQueue.MessageAdded -= OnMessageAdded;

        _tts?.Stop();
        _tts?.Dispose();
        _tts = null;

        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    // ── Static helpers (called from UI) ─────────────────────────────────────

    public static void Start()
    {
        var context = global::Android.App.Application.Context;
        var intent  = new Intent(context, typeof(AnnouncerForegroundService));
        context.StartForegroundService(intent);
    }

    public static void Stop()
    {
        var context = global::Android.App.Application.Context;
        var intent  = new Intent(context, typeof(AnnouncerForegroundService));
        context.StopService(intent);
    }

    // ── Message processing ───────────────────────────────────────────────────

    private void OnMessageAdded(object? sender, NotificationMessage message)
    {
        // Signal the drain loop (it will pick up the new item on its next iteration)
        // Using a dedicated trigger avoids spawning a new task per notification.
        _drainTrigger?.TrySetResult(true);
    }

    private async Task DrainQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_messageQueue is null || _messageQueue.IsEmpty)
            {
                // Wait until a new message is signalled
                _drainTrigger = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                try
                {
                    await _drainTrigger.Task.WaitAsync(cancellationToken)
                                            .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            while (_messageQueue is not null &&
                   _messageQueue.TryDequeue(out var msg) &&
                   msg is not null)
            {
                try
                {
                    string announcement = BuildAnnouncement(msg);
                    if (_tts is not null)
                        await _tts.SpeakAsync(announcement, cancellationToken)
                                  .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Continue processing remaining messages even if one fails
                }
            }
        }
    }

    private static string BuildAnnouncement(NotificationMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.Title) && !string.IsNullOrWhiteSpace(msg.Text))
            return $"{msg.Title}. {msg.Text}";

        return string.IsNullOrWhiteSpace(msg.Title) ? msg.Text : msg.Title;
    }

    // ── Notification helpers ─────────────────────────────────────────────────

    private void CreateNotificationChannel()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Low)
            {
                Description = "Keeps Notification Announcer running in the background"
            };

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
    }

    private Notification BuildNotification()
    {
        var intent      = new Intent(this, typeof(MainActivity));
        var pendingFlags = Build.VERSION.SdkInt >= BuildVersionCodes.M
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, pendingFlags);

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("Notification Announcer")
            .SetContentText("Listening for notifications…")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .Build()!;
    }
}
