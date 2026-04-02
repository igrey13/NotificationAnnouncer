using System.Collections.Concurrent;

namespace NotificationAnnouncer.Services;

/// <summary>
/// Thread-safe FIFO queue for captured notification messages.
/// Raises <see cref="MessageAdded"/> whenever a new message is enqueued.
/// </summary>
public sealed class MessageQueue
{
    private readonly ConcurrentQueue<NotificationMessage> _queue = new();

    /// <summary>Raised on the thread that called <see cref="Enqueue"/> when a message is added.</summary>
    public event EventHandler<NotificationMessage>? MessageAdded;

    /// <summary>Returns the approximate number of messages currently in the queue.</summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Adds <paramref name="message"/> to the back of the queue and fires <see cref="MessageAdded"/>.
    /// </summary>
    public void Enqueue(NotificationMessage message)
    {
        _queue.Enqueue(message);
        MessageAdded?.Invoke(this, message);
    }

    /// <summary>
    /// Attempts to remove and return the message at the front of the queue.
    /// </summary>
    /// <param name="message">The dequeued message, or <c>null</c> if the queue was empty.</param>
    /// <returns><c>true</c> if a message was dequeued; otherwise <c>false</c>.</returns>
    public bool TryDequeue(out NotificationMessage? message) =>
        _queue.TryDequeue(out message);

    /// <summary>Returns <c>true</c> if the queue contains no messages.</summary>
    public bool IsEmpty => _queue.IsEmpty;
}
