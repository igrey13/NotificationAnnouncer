using NotificationAnnouncer.Services;

namespace NotificationAnnouncer;

public partial class MainPage : ContentPage
{
    private readonly MessageQueue _messageQueue;
    private System.Timers.Timer? _queueRefreshTimer;

    public MainPage(MessageQueue messageQueue)
    {
        InitializeComponent();
        _messageQueue = messageQueue;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshServiceStatus();
        StartQueueRefreshTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _queueRefreshTimer?.Stop();
    }

    private void StartQueueRefreshTimer()
    {
        _queueRefreshTimer = new System.Timers.Timer(1000);
        _queueRefreshTimer.Elapsed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(RefreshQueueStatus);
        };
        _queueRefreshTimer.Start();
    }

    private void RefreshServiceStatus()
    {
#if ANDROID
        bool running = Platforms.Android.AnnouncerForegroundService.IsRunning;
        ServiceStatusLabel.Text = running ? "✅ Running" : "⏹ Stopped";
        ServiceStatusLabel.TextColor = running ? Colors.Green : Colors.Gray;
#else
        ServiceStatusLabel.Text = "Not supported on this platform";
#endif
    }

    private void RefreshQueueStatus()
    {
        int count = _messageQueue.Count;
        QueueStatusLabel.Text = count == 0
            ? "Queue is empty"
            : $"{count} message(s) pending";
    }

    private void OnStartServiceClicked(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AnnouncerForegroundService.Start();
        RefreshServiceStatus();
#endif
    }

    private void OnStopServiceClicked(object sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.AnnouncerForegroundService.Stop();
        RefreshServiceStatus();
#endif
    }

    private void OnOpenNotificationAccessClicked(object sender, EventArgs e)
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
#endif
    }
}
