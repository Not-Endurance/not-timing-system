using Not.Identity.Email;

namespace NTS.Tests.Integration.Harness;

/// <summary>What a test reads to find the code that was "sent".</summary>
internal interface IEmailOutbox
{
    IReadOnlyList<EmailMessage> Messages { get; }

    void Clear();
}

/// <summary>Keeps the mail in memory instead of sending it.</summary>
internal sealed class OutboxEmailSender : IEmailSender, IEmailOutbox
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
