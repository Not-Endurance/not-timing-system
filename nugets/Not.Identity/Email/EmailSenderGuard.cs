using Microsoft.Extensions.Hosting;

namespace Not.Identity.Email;

/// <summary>
/// Refuses to start a Production host that would print or keep its one-time codes (ADR-0002): a code in a log or in
/// memory is a way into an account.
/// </summary>
public sealed class EmailSenderGuard : IHostedService
{
    readonly IEmailSender _sender;
    readonly IHostEnvironment _environment;

    public EmailSenderGuard(IEmailSender sender, IHostEnvironment environment)
    {
        _sender = sender;
        _environment = environment;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsProduction() && _sender is IDevelopmentEmailSender)
        {
            throw new InvalidOperationException(
                $"Production refuses to start with the {_sender.GetType().Name}: it prints or keeps the one-time codes it sends. Configure a real email provider."
            );
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
