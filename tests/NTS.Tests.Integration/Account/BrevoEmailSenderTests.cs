using System.Net;
using System.Text;
using System.Text.Json;
using Not.Identity.Email;

namespace NTS.Tests.Integration;

/// <summary>
/// The Brevo sender behind the email seam (ADR-0002, #601), tested at its HTTP boundary: what it asks the provider to
/// do, and what it does when the provider refuses, cannot be reached or is not configured. Nothing here calls the
/// real provider.
/// </summary>
public sealed class BrevoEmailSenderTests
{
    const string API_KEY = "xkeysib-0123456789abcdef-SECRET";

    [Fact]
    public async Task A_message_is_posted_to_the_transactional_endpoint_with_the_key_in_a_header_and_the_sender_identity()
    {
        var provider = new StubProvider(HttpStatusCode.Created, """{"messageId":"<1@example>"}""");
        var sender = NewSender(provider);

        await sender.SendAsync(
            new EmailMessage("rider@example.org", "Your code", "Your code is 123456.", language: "bg")
        );

        var request = Assert.Single(provider.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", request.Uri.ToString());
        Assert.Equal(API_KEY, Assert.Single(request.Headers["api-key"]));
        Assert.Contains("application/json", Assert.Single(request.Headers["Accept"]));
        Assert.StartsWith("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("NoTiming", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("no-reply@mail.example.org", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal(
            "rider@example.org",
            Assert.Single(root.GetProperty("to").EnumerateArray()).GetProperty("email").GetString()
        );
        Assert.Equal("Your code", root.GetProperty("subject").GetString());
        Assert.Equal("Your code is 123456.", root.GetProperty("textContent").GetString());
        Assert.False(root.TryGetProperty("htmlContent", out _));
    }

    [Fact]
    public async Task A_message_that_has_an_HTML_body_sends_it_too()
    {
        var provider = new StubProvider(HttpStatusCode.Created, "{}");
        var sender = NewSender(provider);

        await sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text", "<p>text</p>"));

        using var body = JsonDocument.Parse(Assert.Single(provider.Requests).Body);
        Assert.Equal("<p>text</p>", body.RootElement.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task The_key_is_in_the_header_and_nowhere_else()
    {
        var provider = new StubProvider(HttpStatusCode.Created, "{}");
        var sender = NewSender(provider);

        await sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"));

        var request = Assert.Single(provider.Requests);
        Assert.DoesNotContain(API_KEY, request.Body);
        Assert.DoesNotContain(API_KEY, request.Uri.ToString());
    }

    [Theory]
    [InlineData(
        HttpStatusCode.Unauthorized,
        """{"code":"unauthorized","message":"Key not found"}""",
        "401",
        "unauthorized"
    )]
    [InlineData(
        HttpStatusCode.BadRequest,
        """{"code":"invalid_parameter","message":"bad"}""",
        "400",
        "invalid_parameter"
    )]
    [InlineData(
        HttpStatusCode.TooManyRequests,
        """{"code":"too_many_requests","message":"slow down"}""",
        "429",
        "too_many_requests"
    )]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "502", "")]
    public async Task A_message_the_provider_refuses_fails_with_the_status_and_its_code_and_nothing_secret(
        HttpStatusCode status,
        string body,
        string expectedStatus,
        string expectedCode
    )
    {
        var sender = NewSender(new StubProvider(status, body));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "Your code is 123456."))
        );

        Assert.Contains(expectedStatus, failure.Message);
        Assert.Contains(expectedCode, failure.Message);
        Assert.DoesNotContain(API_KEY, failure.ToString());
        Assert.DoesNotContain("123456", failure.ToString());
    }

    [Fact]
    public async Task A_provider_message_that_repeats_the_recipient_is_not_passed_on_with_it()
    {
        var provider = new StubProvider(
            HttpStatusCode.BadRequest,
            """{"code":"invalid_parameter","message":"Invalid email rider@example.org"}"""
        );
        var sender = NewSender(provider);

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"))
        );

        Assert.DoesNotContain("rider@example.org", failure.Message);
        Assert.Contains("invalid_parameter", failure.Message);
    }

    [Fact]
    public async Task A_provider_message_with_line_breaks_stays_on_one_line_so_it_cannot_forge_a_line_of_the_log()
    {
        var provider = new StubProvider(
            HttpStatusCode.BadRequest,
            """{"code":"invalid\nFAKE","message":"first\r\n2026-01-01 ERROR forged\tline"}"""
        );
        var sender = NewSender(provider);

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"))
        );

        Assert.All(failure.Message, x => Assert.False(char.IsControl(x), "a control character in the message"));
        Assert.Contains("forged", failure.Message);
    }

    [Fact]
    public async Task A_provider_that_cannot_be_reached_fails_the_same_way_as_one_that_refuses()
    {
        var sender = NewSender(new StubProvider(new HttpRequestException("No such host is known.")));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"))
        );

        Assert.DoesNotContain(API_KEY, failure.ToString());
    }

    [Fact]
    public async Task A_provider_that_does_not_answer_in_time_fails_the_same_way()
    {
        var sender = NewSender(StubProvider.Silent(), timeout: TimeSpan.FromMilliseconds(100));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"))
        );

        Assert.Contains("did not answer", failure.Message);
    }

    [Fact]
    public async Task A_caller_that_cancels_gets_the_cancellation_and_not_a_delivery_failure()
    {
        var sender = NewSender(StubProvider.Silent());
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"), cancelled.Token)
        );
    }

    [Theory]
    [InlineData(null, "no-reply@mail.example.org")]
    [InlineData("", "no-reply@mail.example.org")]
    [InlineData(API_KEY, null)]
    [InlineData(API_KEY, " ")]
    public async Task A_sender_without_a_key_or_a_sender_address_is_a_configuration_error_and_calls_nobody(
        string? key,
        string? address
    )
    {
        var provider = new StubProvider(HttpStatusCode.Created, "{}");
        var sender = NewSender(provider, key: key, address: address);

        var misconfigured = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new EmailMessage("rider@example.org", "Hi", "text"))
        );

        Assert.IsNotType<EmailDeliveryException>(misconfigured);
        Assert.Contains("Email:Brevo", misconfigured.Message);
        Assert.Empty(provider.Requests);
    }

    static BrevoEmailSender NewSender(
        StubProvider provider,
        string? key = API_KEY,
        string? address = "no-reply@mail.example.org",
        TimeSpan? timeout = null
    )
    {
        return new BrevoEmailSender(
            new HttpClient(provider),
            new BrevoOptions
            {
                ApiKey = key,
                SenderEmail = address,
                SenderName = "NoTiming",
                Timeout = timeout ?? TimeSpan.FromSeconds(10),
            }
        );
    }

    /// <summary>A provider that answers what it is told to, and remembers what it was asked.</summary>
    sealed class StubProvider : HttpMessageHandler
    {
        public static StubProvider Silent()
        {
            return new StubProvider(null, null, null, silent: true);
        }

        readonly HttpStatusCode? _status;
        readonly string? _body;
        readonly Exception? _failure;
        readonly bool _silent;

        StubProvider(HttpStatusCode? status, string? body, Exception? failure, bool silent)
        {
            _status = status;
            _body = body;
            _failure = failure;
            _silent = silent;
        }

        public StubProvider(HttpStatusCode status, string body)
            : this(status, body, null, silent: false) { }

        public StubProvider(Exception failure)
            : this(null, null, failure, silent: false) { }

        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(await Seen.Of(request));
            if (_silent)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return _failure != null
                ? throw _failure
                : new HttpResponseMessage(_status!.Value)
                {
                    Content = new StringContent(_body!, Encoding.UTF8, "application/json"),
                };
        }
    }

    sealed class Seen
    {
        public static async Task<Seen> Of(HttpRequestMessage request)
        {
            var headers = request.Headers.ToDictionary(
                x => x.Key,
                x => x.Value.ToList(),
                StringComparer.OrdinalIgnoreCase
            );
            return new Seen
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Headers = headers,
                ContentType = request.Content?.Headers.ContentType?.ToString() ?? "",
                Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(),
            };
        }

        public HttpMethod Method { get; private init; } = HttpMethod.Get;
        public Uri Uri { get; private init; } = default!;
        public Dictionary<string, List<string>> Headers { get; private init; } = [];
        public string ContentType { get; private init; } = "";
        public string Body { get; private init; } = "";
    }
}
