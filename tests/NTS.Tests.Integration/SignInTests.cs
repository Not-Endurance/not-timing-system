using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Email;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Signing in with an emailed code and the server-side session it starts (#599, ADR-0002), through the host as a person
/// meets it. Every test signs in the way a person does: it asks for a code and reads it from the outbox.
/// </summary>
public sealed class SignInTests : IClassFixture<MongoFixture>
{
    static readonly TimeSpan TEN_MINUTES = TimeSpan.FromMinutes(10);

    readonly MongoFixture _mongo;

    public SignInTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Asking_for_a_code_mails_it_to_an_address_with_an_account_and_answers_alike_for_every_address()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var known = await SeedUser();
        var unknown = UserSeed.NewEmail("unknown");

        var forKnown = await ApiSessions.RequestCodeAsync(client, known);
        var forUnknown = await ApiSessions.RequestCodeAsync(client, unknown);

        Assert.Equal(HttpStatusCode.Accepted, forKnown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forUnknown.StatusCode);
        Assert.Equal(await forKnown.Content.ReadAsStringAsync(), await forUnknown.Content.ReadAsStringAsync());
        Assert.Equal(forKnown.Content.Headers.ContentType, forUnknown.Content.Headers.ContentType);
        var mail = Assert.Single(api.Services.GetRequiredService<IEmailOutbox>().Messages);
        Assert.Equal(known, mail.To);
        Assert.Matches(@"\b\d{6}\b", mail.TextBody);
        Assert.Equal("en", mail.Language);
    }

    [Fact]
    public async Task The_address_is_matched_whatever_its_capitalisation()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var shouted = email.ToUpperInvariant();

        await ApiSessions.RequestCodeAsync(client, shouted);
        var created = await ApiSessions.CreateSessionAsync(client, shouted, ApiSessions.CodeSentTo(api, email));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task A_code_works_once()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        var first = await ApiSessions.CreateSessionAsync(client, email, code);
        var second = await ApiSessions.CreateSessionAsync(client, email, code);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task A_code_expires_after_ten_minutes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        time.Advance(TEN_MINUTES + TimeSpan.FromSeconds(1));
        var late = await ApiSessions.CreateSessionAsync(client, email, code);

        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);
    }

    [Fact]
    public async Task A_code_still_works_just_before_it_expires()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        time.Advance(TEN_MINUTES - TimeSpan.FromSeconds(1));
        var inTime = await ApiSessions.CreateSessionAsync(client, email, code);

        Assert.Equal(HttpStatusCode.Created, inTime.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_invalidate_the_code_even_for_the_right_one()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);
        var wrong = WrongCode(code);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var refused = await ApiSessions.CreateSessionAsync(client, email, wrong);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        var tooLate = await ApiSessions.CreateSessionAsync(client, email, code);
        Assert.Equal(HttpStatusCode.Unauthorized, tooLate.StatusCode);
    }

    [Fact]
    public async Task The_right_code_still_works_after_four_wrong_ones()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await ApiSessions.CreateSessionAsync(client, email, WrongCode(code));
        }

        var created = await ApiSessions.CreateSessionAsync(client, email, code);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Of_simultaneous_verifications_with_the_right_code_only_one_signs_in()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => ApiSessions.CreateSessionAsync(client, email, code))
        );

        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Wrong_codes_for_one_address_never_block_another_and_a_wrong_code_for_an_unknown_one_is_refused_alike()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var other = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        await ApiSessions.RequestCodeAsync(client, other);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await ApiSessions.CreateSessionAsync(client, email, "000000");
        }

        var created = await ApiSessions.CreateSessionAsync(client, other, ApiSessions.CodeSentTo(api, other));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var unknown = await ApiSessions.CreateSessionAsync(client, UserSeed.NewEmail("nobody"), "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var body = await ApiSessions.ReadJsonAsync(unknown);
        Assert.Equal("invalid-code", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_second_request_inside_the_cooldown_sends_nothing_and_the_first_code_stays_valid()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);

        time.Advance(TimeSpan.FromSeconds(30));
        var again = await ApiSessions.RequestCodeAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Single(api.Services.GetRequiredService<IEmailOutbox>().Messages);
        var created = await ApiSessions.CreateSessionAsync(client, email, code);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task A_request_after_the_cooldown_replaces_the_previous_code()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        var first = await RequestCodeAfterCooldown(api, client, time, email);
        var second = await RequestCodeAfterCooldown(api, client, time, email);
        while (second == first) // one in a million
        {
            second = await RequestCodeAfterCooldown(api, client, time, email);
        }

        var replaced = await ApiSessions.CreateSessionAsync(client, email, first);
        var current = await ApiSessions.CreateSessionAsync(client, email, second);

        Assert.Equal(HttpStatusCode.Unauthorized, replaced.StatusCode);
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
    }

    [Fact]
    public async Task Verifying_a_code_sets_the_session_cookie_and_confirms_the_address()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        var userId = await UserIdOf(email);

        var cookie = await ApiSessions.SignInAsync(api, client, email);

        Assert.Equal("__Host-NoTiming", cookie.Name);
        Assert.Contains("httponly", cookie.Attributes);
        Assert.Contains("secure", cookie.Attributes);
        Assert.Contains("samesite=lax", cookie.Attributes);
        Assert.Contains("path=/", cookie.Attributes);
        Assert.DoesNotContain(cookie.Attributes, x => x.StartsWith("domain=", StringComparison.Ordinal));
        var expires = cookie.Expires();
        Assert.NotNull(expires); // persistent, not a session cookie
        Assert.InRange((expires.Value - time.GetUtcNow()).TotalDays, 29.9, 30.1);

        var stored = await Users().Find(Is(userId)).SingleAsync();
        Assert.True(stored["EmailConfirmed"].AsBoolean);
        Assert.False(string.IsNullOrEmpty(stored["SecurityStamp"].AsString));
        var session = await Sessions().Find(new BsonDocument("UserId", Binary(userId))).SingleAsync();
        Assert.NotEqual(cookie.Value, session["_id"].AsString); // the cookie carries a protected key, never the key
        Assert.True(session["ExpiresAt"].ToUniversalTime() > time.GetUtcNow().UtcDateTime.AddDays(29));
    }

    [Fact]
    public async Task A_session_slides_while_it_is_used_and_expires_when_it_is_not_used_for_thirty_days()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(time);
        using var client = ClientOf(api);
        var email = await SeedUser();
        var userId = await UserIdOf(email);
        var cookie = await ApiSessions.SignInAsync(api, client, email);

        time.Advance(TimeSpan.FromDays(20));
        Assert.Equal(HttpStatusCode.OK, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);
        var slid = await Sessions().Find(new BsonDocument("UserId", Binary(userId))).SingleAsync();
        Assert.InRange((slid["ExpiresAt"].ToUniversalTime() - time.GetUtcNow().UtcDateTime).TotalDays, 29.9, 30.1);

        time.Advance(TimeSpan.FromDays(20)); // forty days after signing in, twenty after the last use
        Assert.Equal(HttpStatusCode.OK, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);

        time.Advance(TimeSpan.FromDays(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task Me_describes_a_signed_in_user_and_is_401_for_everyone_else()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var userId = await UserIdOf(email);
        var cookie = await ApiSessions.SignInAsync(api, client, email);

        var signedIn = await ApiSessions.GetAsync(client, "/api/me", cookie);
        var anonymous = await ApiSessions.GetAsync(client, "/api/me", null);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(ApiSessions.MEDIA_TYPE, signedIn.Content.Headers.ContentType?.MediaType);
        var data = (await ApiSessions.ReadJsonAsync(signedIn)).GetProperty("data");
        Assert.Equal("accounts", data.GetProperty("type").GetString());
        Assert.Equal(userId.ToString(), data.GetProperty("id").GetString());
        var attributes = data.GetProperty("attributes");
        Assert.Equal(email, attributes.GetProperty("email").GetString());
        Assert.True(attributes.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal("Ana Petrova", attributes.GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(anonymous)).GetProperty("errors")[0];
        Assert.Equal("401", error.GetProperty("status").GetString());
        Assert.Equal("not-signed-in", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Signing_out_deletes_the_ticket_and_clears_the_cookie()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var userId = await UserIdOf(email);
        var cookie = await ApiSessions.SignInAsync(api, client, email);

        using var request = ApiSessions.Request(HttpMethod.Delete, "/api/sessions/current", cookie);
        var signedOut = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        var cleared = SessionCookie.From(signedOut);
        Assert.NotNull(cleared);
        Assert.Equal(string.Empty, cleared.Value);
        Assert.True(cleared.Expires() < DateTimeOffset.UtcNow);
        Assert.Equal(0, await Sessions().CountDocumentsAsync(new BsonDocument("UserId", Binary(userId))));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_without_a_session_is_not_an_error()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);

        using var request = ApiSessions.Request(HttpMethod.Delete, "/api/sessions/current");
        var signedOut = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_ticket_document_ends_the_session_on_the_next_request()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var userId = await UserIdOf(email);
        var cookie = await ApiSessions.SignInAsync(api, client, email);
        Assert.Equal(HttpStatusCode.OK, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);

        await Sessions().DeleteManyAsync(new BsonDocument("UserId", Binary(userId)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiSessions.GetAsync(client, "/api/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task Rotating_the_security_stamp_ends_every_session_of_that_user_and_only_theirs()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();
        var other = await SeedUser();
        var onPhone = await ApiSessions.SignInAsync(api, client, email);
        var onLaptop = await ApiSessions.SignInAsync(api, client, email);
        var someoneElse = await ApiSessions.SignInAsync(api, client, other);

        using (var scope = api.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.True((await users.UpdateSecurityStampAsync(user!)).Succeeded);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiSessions.GetAsync(client, "/api/me", onPhone)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiSessions.GetAsync(client, "/api/me", onLaptop)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ApiSessions.GetAsync(client, "/api/me", someoneElse)).StatusCode);
    }

    [Fact]
    public async Task Sessions_survive_a_host_restart()
    {
        var keys = ApiFactory.NewDataProtectionKeys();
        var email = await SeedUser();
        SessionCookie cookie;
        await using (var first = NewApi(keys: keys))
        {
            using var client = ClientOf(first);
            cookie = await ApiSessions.SignInAsync(first, client, email);
        }

        await using var second = NewApi(keys: keys);
        using var again = ClientOf(second);
        var response = await ApiSessions.GetAsync(again, "/api/me", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_user_from_before_identity_signs_in_and_keeps_every_field_of_the_document()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = UserSeed.NewEmail("legacy");
        var userId = await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            tenantId: "another-tenant",
            shape: document => document.Add("SomethingElse", new BsonDocument("nested", new BsonArray { 1, "two" }))
        );
        var before = await Users().Find(Is(userId)).SingleAsync();
        Assert.False(before.Contains("SecurityStamp")); // nothing of identity yet

        await ApiSessions.SignInAsync(api, client, email); // the email lookup knows no Tenant

        var after = await Users().Find(Is(userId)).SingleAsync();
        foreach (var field in before)
        {
            Assert.Equal(field.Value, after[field.Name]);
        }

        Assert.True(after["EmailConfirmed"].AsBoolean);
        Assert.True(after.Contains("SecurityStamp"));
        Assert.True(after.Contains("ConcurrencyStamp"));
    }

    [Theory]
    [InlineData("Outbox")]
    [InlineData("Console")]
    public async Task Production_refuses_to_start_with_an_email_sender_that_exposes_the_codes(string sender)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString, environment: "Production", emailSender: sender);

        var refused = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("Production refuses", refused.ToString());
    }

    [Fact]
    public async Task Production_starts_without_one_and_a_code_it_cannot_send_fails_loudly()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString, environment: "Production");
        using var client = api.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }
        );
        var email = await SeedUser();

        // A real host answers 500; the in-memory test host hands the exception to the caller.
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApiSessions.RequestCodeAsync(client, email)
        );

        Assert.Contains("No email provider is configured", refused.Message);
    }

    [Fact]
    public async Task A_write_needs_the_json_api_media_type_and_a_well_formed_document()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);
        var email = await SeedUser();

        var asForm = await client.PostAsync(
            "/api/sessions",
            new FormUrlEncodedContent([new("email", email), new("code", "123456")])
        );
        var asPlainJson = await client.PostAsync(
            "/api/code-challenges",
            new StringContent(
                "{\"data\":{\"type\":\"code-challenges\",\"attributes\":{\"email\":\"" + email + "\"}}}",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        var notJson = await client.PostAsync(
            "/api/code-challenges",
            new StringContent("not json", System.Text.Encoding.UTF8, ApiSessions.MEDIA_TYPE)
        );
        var wrongType = await ApiSessions.PostAsync(client, "/api/code-challenges", "sessions", new { email });
        var badEmail = await ApiSessions.RequestCodeAsync(client, "not an email");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, asForm.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, asPlainJson.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        var error = (await ApiSessions.ReadJsonAsync(badEmail)).GetProperty("errors")[0];
        Assert.Equal("invalid-email", error.GetProperty("code").GetString());
        Assert.Empty(api.Services.GetRequiredService<IEmailOutbox>().Messages);
    }

    [Fact]
    public async Task The_sign_in_page_speaks_the_language_of_the_visitor_and_falls_back_to_English()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);

        var english = await client.GetStringAsync("/sign-in");
        var bulgarian = await PageAsync(client, "/sign-in", "bg-BG,bg;q=0.9,en;q=0.8");
        var turkish = await PageAsync(client, "/sign-in?lang=tr", "bg");
        var unsupported = await PageAsync(client, "/sign-in", "de-DE,de;q=0.9");

        Assert.Contains("<html lang=\"en\">", english);
        Assert.Contains("Send me a code", english);
        Assert.Contains("<html lang=\"bg\">", bulgarian);
        Assert.Contains("Изпрати ми код", bulgarian);
        Assert.Contains("<html lang=\"tr\">", turkish);
        Assert.Contains("Bana kod gönder", turkish);
        Assert.Contains("<html lang=\"en\">", unsupported);
    }

    [Theory]
    [InlineData("/startlist", "/startlist")]
    [InlineData("/historic-events/7?tab=results", "/historic-events/7?tab=results")]
    [InlineData("//evil.example/path", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("javascript:alert(1)", "/")]
    public async Task The_sign_in_page_sends_a_visitor_back_only_to_a_path_of_the_site(string requested, string used)
    {
        await using var api = NewApi();
        using var client = ClientOf(api);

        var page = await client.GetStringAsync("/sign-in?returnUrl=" + Uri.EscapeDataString(requested));

        Assert.Contains($"data-return-url=\"{System.Net.WebUtility.HtmlEncode(used)}\"", page);
    }

    [Fact]
    public async Task The_page_assets_are_served_and_nothing_else_is()
    {
        await using var api = NewApi();
        using var client = ClientOf(api);

        var script = await client.GetAsync("/account/assets/sign-in.js");
        var style = await client.GetAsync("/account/assets/account.css");
        var other = await client.GetAsync("/account/assets/sign-in.html");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, style.StatusCode);
        Assert.Equal("text/css", style.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    static string WrongCode(string code)
    {
        return code == "000000" ? "000001" : "000000";
    }

    static HttpClient ClientOf(ApiFactory api)
    {
        // Https, so that the Secure session cookie is one a browser would take; the cookie itself is carried by hand.
        return api.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
    }

    static async Task<string> PageAsync(HttpClient client, string path, string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    static BsonDocument Is(Guid id)
    {
        return new BsonDocument("_id", Binary(id));
    }

    ApiFactory NewApi(TimeProvider? time = null, string? keys = null)
    {
        return new ApiFactory(_mongo.ConnectionString, time: time, dataProtectionKeys: keys);
    }

    async Task<string> SeedUser()
    {
        var email = UserSeed.NewEmail();
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        return email;
    }

    async Task<Guid> UserIdOf(string email)
    {
        var user = await Users().Find(new BsonDocument("Email", email)).SingleAsync();
        return user["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard);
    }

    IMongoCollection<BsonDocument> Users()
    {
        return UserSeed.Users(_mongo.ConnectionString);
    }

    IMongoCollection<BsonDocument> Sessions()
    {
        return new MongoClient(_mongo.ConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("auth_sessions");
    }

    static async Task<string> RequestCodeAfterCooldown(
        ApiFactory api,
        HttpClient client,
        FakeTimeProvider time,
        string email
    )
    {
        time.Advance(TimeSpan.FromSeconds(61));
        await ApiSessions.RequestCodeAsync(client, email);
        return ApiSessions.CodeSentTo(api, email);
    }
}
