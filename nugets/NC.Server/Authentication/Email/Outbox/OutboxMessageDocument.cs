namespace NC.Server.Authentication.Email.Outbox;

internal sealed class OutboxMessageDocument
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }
}
