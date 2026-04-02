using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Microsoft.Extensions.DependencyInjection;
using NotificationAnnouncer.Services;

namespace NotificationAnnouncer.Platforms.Android;

/// <summary>
/// Android <see cref="NotificationListenerService"/> that captures notifications
/// from subscribed apps, enqueues the message text, and immediately dismisses
/// the notification so it does not pile up in the notification shade.
/// </summary>
/// <remarks>
/// The user must grant <em>Notification Access</em> via
/// <c>Settings → Apps → Special App Access → Notification Access</c> before
/// this service is invoked by the system.
/// </remarks>
[Service(
    Name = "com.igrey13.notificationannouncer.AppNotificationListenerService",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public class AppNotificationListenerService : NotificationListenerService
{
    // Package names of apps whose notifications should be announced.
    // An empty set means ALL apps are announced; populate to restrict.
    private static readonly HashSet<string> _subscribedPackages = new(StringComparer.Ordinal);

    private MessageQueue? _messageQueue;

    public override void OnCreate()
    {
        base.OnCreate();
        _messageQueue = MauiApplication.Current.Services.GetService<MessageQueue>();
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        if (sbn is null)
            return;

        // Filter by subscribed packages when the allowlist is non-empty
        if (_subscribedPackages.Count > 0 &&
            !_subscribedPackages.Contains(sbn.PackageName ?? string.Empty))
            return;

        var extras = sbn.Notification?.Extras;
        if (extras is null)
            return;

        string title = extras.GetString(global::Android.App.Notification.ExtraTitle) ?? string.Empty;
        string text  = extras.GetString(global::Android.App.Notification.ExtraText)  ?? string.Empty;

        // Skip silent / empty notifications
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text))
            return;

        var message = new NotificationMessage(
            PackageName: sbn.PackageName ?? string.Empty,
            Title:       title,
            Text:        text,
            Timestamp:   DateTimeOffset.Now);

        _messageQueue?.Enqueue(message);

        // Dismiss the notification so it does not accumulate
        try { CancelNotification(sbn.Key); }
        catch { /* Ignore — notification may have already been dismissed */ }
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn) { }

    /// <summary>
    /// Adds a package name to the allowlist of apps whose notifications are announced.
    /// Call with no arguments (or an empty collection) to announce notifications from all apps.
    /// </summary>
    public static void SubscribeToPackages(IEnumerable<string> packageNames)
    {
        _subscribedPackages.Clear();
        foreach (var pkg in packageNames)
            _subscribedPackages.Add(pkg);
    }
}
