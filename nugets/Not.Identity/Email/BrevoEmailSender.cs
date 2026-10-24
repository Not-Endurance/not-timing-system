using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Not.Identity.Email;

/// <summary>
/// Sends the mail through Brevo's transactional API (ADR-0002, #601). The key rides in a header and appears nowhere
/// else. A message the provider refuses, a provider that cannot be reached and one that does not answer in time all
/// fail as an <see cref="EmailDeliveryException"/>, whose message carries the status and the provider's own code but
/// never the key, the body of the mail or the address it was meant for. Not being configured is a different failure:
/// it throws before anything is sent, and it is a mistake of the deployment, not of the provider.
/// </summary>
public sealed class BrevoEmailSender : IEmailSender
{
    const string ENDPOINT = "v3/smtp/email";
    const int MAX_PROVIDER_TEXT_LENGTH = 200;
    static readonly JsonSerializerOptions JSON = CreateJsonOptions();

    readonly HttpClient _client;
    readonly BrevoOptions _options;

    public BrevoEmailSender(HttpClient client, BrevoOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new InvalidOperationException(
                "Brevo is not configured: set Email:Brevo:ApiKey and Email:Brevo:SenderEmail."
            );
        }

        using var request = CreateRequest(message);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var response = await _client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw await RefusedAsync(response, message.To, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException("Brevo did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new EmailDeliveryException("Brevo could not be reached.", ex);
        }
    }

    HttpRequestMessage CreateRequest(EmailMessage message)
    {
        var body = new
        {
            sender = new { name = _options.SenderName, email = _options.SenderEmail },
            to = new[] { new { email = message.To } },
            subject = message.Subject,
            textContent = message.TextBody,
            htmlContent = message.HtmlBody,
        };
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseAddress, ENDPOINT))
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JSON), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    static async Task<EmailDeliveryException> RefusedAsync(
        HttpResponseMessage response,
        string recipient,
        CancellationToken cancellationToken
    )
    {
        string? code = null;
        string? text = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            code = TextOf(document.RootElement, "code");
            text = TextOf(document.RootElement, "message")
                ?.Replace(recipient, "<recipient>", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Not the provider's JSON (a gateway's page, say): the status alone has to do.
        }

        var detail = (code, text) switch
        {
            (null, null) => "",
            (_, null) => $" {code}",
            (null, _) => $": {Shorten(text)}",
            _ => $" {code}: {Shorten(text)}",
        };
        return new EmailDeliveryException($"Brevo refused the message with {(int)response.StatusCode}{detail}.");
    }

    /// <summary>A text of the provider's, on one line: it ends up in a log, and a line break in it would forge a line.</summary>
    static string? TextOf(JsonElement element, string name)
    {
        return
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            ? new string([.. value.GetString()!.Select(x => char.IsControl(x) ? ' ' : x)])
            : null;
    }

    static string Shorten(string? text)
    {
        return text is { Length: > MAX_PROVIDER_TEXT_LENGTH } ? text[..MAX_PROVIDER_TEXT_LENGTH] + "…" : text ?? "";
    }
}

/// <summary>
/// Brevo's settings (<c>Email:Brevo</c>). The key is a secret and comes from the host's secrets, never from a file of
/// the repository. The sender is an identity on a sending subdomain that the provider has verified.
/// </summary>
public sealed class BrevoOptions
{
    public const string SECTION = "Email:Brevo";

    public string? ApiKey { get; set; }

    /// <summary>The address mail is sent from, on the sending subdomain.</summary>
    public string? SenderEmail { get; set; }

    public string? SenderName { get; set; }
    public Uri BaseAddress { get; set; } = new("https://api.brevo.com/");

    /// <summary>How long the provider has to answer, so a slow one cannot hold up a sign-in.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// A message that was not delivered to the provider: it refused it, could not be reached or did not answer. The
/// message says what happened and never holds a secret or the mail itself, so it can be logged as it is.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message)
        : base(message) { }

    public EmailDeliveryException(string message, Exception inner)
        : base(message, inner) { }
}
