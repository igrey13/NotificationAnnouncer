namespace NotificationAnnouncer.Services;

/// <summary>
/// Represents a single captured notification message.
/// </summary>
public sealed record NotificationMessage(string PackageName, string Title, string Text, DateTimeOffset Timestamp);
