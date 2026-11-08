using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Store;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Email;

/// <summary>Keeps every message in a collection, so a test can read the code the person would have received. Non-production only.</summary>
public sealed class OutboxEmailSender : IEmailSender
{
    readonly IMongoCollection<OutboxMessageDocument> _outbox;

    public OutboxEmailSender(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _outbox = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<OutboxMessageDocument>(settings.OutboxCollection);
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        return _outbox.InsertOneAsync(
            new OutboxMessageDocument
            {
                To = message.To,
                Subject = message.Subject,
                TextBody = message.TextBody,
                SentAtUtc = DateTime.UtcNow,
            },
            cancellationToken: cancellationToken
        );
    }
}
