using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Not.Identity.Email;

/// <summary>
/// Writes the mail to the log, for a developer who has no inbox to read: the code is in the clear, and a code in a log
/// is a way into an account (ADR-0002). Production refuses to have one.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger, IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                $"Production refuses the {nameof(LogEmailSender)}: it prints the one-time codes it sends. Configure a real email provider."
            );
        }

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
