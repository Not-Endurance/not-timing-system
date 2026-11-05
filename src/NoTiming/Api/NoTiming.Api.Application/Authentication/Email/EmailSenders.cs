using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Email;

public sealed class EmailMessage
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string TextBody { get; init; } = string.Empty;
}

/// <summary>The one seam through which mail leaves the application. Changing provider is one class and configuration.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Sends through Brevo. Used in staging and production.</summary>
public sealed class BrevoEmailSender : IEmailSender
{
    const string ENDPOINT = "https://api.brevo.com/v3/smtp/email";

    readonly HttpClient _http;
    readonly IOptions<AuthenticationSettings> _settings;

    public BrevoEmailSender(HttpClient http, IOptions<AuthenticationSettings> settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ENDPOINT);
        request.Headers.Add("api-key", _settings.Value.BrevoApiKey);
        request.Content = JsonContent.Create(
            new
            {
                sender = new { email = _settings.Value.EmailSenderAddress, name = "NoTiming" },
                to = new[] { new { email = message.To } },
                subject = message.Subject,
                textContent = message.TextBody,
            }
        );

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Writes to the terminal of a developer. Non-production only.</summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[email] to {message.To}: {message.Subject}");
        Console.WriteLine(message.TextBody);
        return Task.CompletedTask;
    }
}

/// <summary>Keeps every message in a collection, so a test can read the code the person would have received. Non-production only.</summary>
public sealed class OutboxEmailSender : IEmailSender
{
    readonly IMongoCollection<OutboxMessageDocument> _outbox;

    public OutboxEmailSender(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _outbox = context.Client.GetDatabase(settings.Database).GetCollection<OutboxMessageDocument>(settings.OutboxCollection);
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

[BsonIgnoreExtraElements]
public sealed class OutboxMessageDocument
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string TextBody { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAtUtc { get; set; }
}
