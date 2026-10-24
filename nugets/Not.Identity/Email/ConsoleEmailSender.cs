using Microsoft.Extensions.Logging;

namespace Not.Identity.Email;

/// <summary>Writes the mail to the log, for a developer who has no inbox to read: the code is in the clear.</summary>
public sealed class ConsoleEmailSender : IDevelopmentEmailSender
{
    readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email to {To} [{Language}]: {Subject}{NewLine}{Body}",
            message.To,
            message.Language ?? "-",
            message.Subject,
            Environment.NewLine,
            message.TextBody
        );
        return Task.CompletedTask;
    }
}
