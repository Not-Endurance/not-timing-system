namespace NC.Server.Authentication.Email;

/// <summary>Writes to the terminal of a developer. Non-production only.</summary>
internal sealed class ConsoleEmailSender : IEmailSender
{
    public Task Send(EmailMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[email] to {message.To}: {message.Subject}");
        Console.WriteLine(message.TextBody);
        return Task.CompletedTask;
    }
}
