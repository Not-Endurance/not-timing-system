namespace Not.Identity.Email;

/// <summary>What a test reads to find the code that was "sent".</summary>
public interface IEmailOutbox
{
    IReadOnlyList<EmailMessage> Messages { get; }

    void Clear();
}

/// <summary>Keeps the mail in memory instead of sending it, for tests.</summary>
public sealed class OutboxEmailSender : IDevelopmentEmailSender, IEmailOutbox
{
    readonly object _lock = new();
    readonly List<EmailMessage> _messages = [];

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }
}
