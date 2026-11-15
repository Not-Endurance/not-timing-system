using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NC.Server.Authentication.Email;

internal sealed class EmailDispatchService : BackgroundService
{
    readonly EmailDispatchQueue _queue;
    readonly IEmailSender _sender;
    readonly ILogger<EmailDispatchService> _logger;

    public EmailDispatchService(EmailDispatchQueue queue, IEmailSender sender, ILogger<EmailDispatchService> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _sender.Send(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The address and the code are never written here: only the kind of failure.
                _logger.LogWarning("An email could not be sent: {FailureKind}.", ex.GetType().Name);
            }
        }
    }
}
