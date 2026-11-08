using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.Email;

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
