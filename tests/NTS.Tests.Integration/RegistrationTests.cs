using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Not.Identity.Email;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A new person registers themselves (#601, ADR-0002): email, country and names, a code that proves the address, and
/// only then an account, with a home Tenant from their country and a session. The tests go through the host as a
/// person meets it and read the outbox for the code.
/// </summary>
public sealed class RegistrationTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public RegistrationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Registering_a_new_address_mails_a_code_and_stores_nothing_until_the_code_is_verified()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", iso);
        var email = UserSeed.NewEmail("new");

        var registered = await RegisterAsync(client, email, country);

        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        var mail = Assert.Single(Outbox(api).Messages);
        Assert.Equal(email, mail.To);
        Assert.Matches(@"\b\d{6}\b", mail.TextBody);
        Assert.Null(await ApiClients.FindUserAsync(_mongo.ConnectionString, email));
        Assert.Null(await ApiClients.FindTenantAsync(_mongo.ConnectionString, $"country-{iso.ToLowerInvariant()}"));
    }

    [Fact]
    public async Task Verifying_the_code_creates_the_account_with_a_confirmed_address_a_home_Tenant_and_one_membership_and_signs_in()
    {
        await using var api = NewApi();
        using var client = ApiClients.OfBrowser(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", iso, "BUL");
        var email = UserSeed.NewEmail("new");
        var tenantId = $"country-{iso.ToLowerInvariant()}";
        await RegisterAsync(client, email, country, givenName: " Ana ", surname: "Petrova");

        var created = await ApiSessions.CreateSessionAsync(client, email, ApiSessions.CodeSentTo(api, email));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var session = (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("code", session.GetProperty("method").GetString());
        Assert.Equal(0, session.GetProperty("passkeyCount").GetInt32());
        var user = await ApiClients.FindUserAsync(_mongo.ConnectionString, email);
        Assert.NotNull(user);
        Assert.True(user["EmailConfirmed"].AsBoolean);
        Assert.Equal("Ana", user["GivenName"].AsString);
        Assert.Equal("Petrova", user["Surname"].AsString);
        Assert.Equal("Ana Petrova", user["Name"].AsString);
        Assert.Equal("Bulgaria", user["CountryRegion"].AsString);
        Assert.Equal(tenantId, user["HomeTenantId"].AsString);
        var membership = Assert.Single(user["Memberships"].AsBsonArray).AsBsonDocument;
        Assert.Equal(tenantId, membership["TenantId"].AsString);
        Assert.Empty(membership["Roles"].AsBsonArray);
        Assert.True(user.Contains("SecurityStamp"));
        var tenant = await ApiClients.FindTenantAsync(_mongo.ConnectionString, tenantId);
        Assert.NotNull(tenant);
        Assert.Equal("Bulgaria", tenant["Name"].AsString);
        Assert.Equal("country", tenant["Kind"].AsString);
        var me = await ApiSessions.GetAsync(client, "/api/me", SessionCookie.From(created));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_Tenant_is_created_once_for_a_country_however_many_people_register_from_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.OfBrowser(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Turkey", iso);
        var otherIso = CountrySeed.UniqueIsoCode();
        var other = await CountrySeed.AddAsync(_mongo.ConnectionString, "Greece", otherIso);

        await RegisterAndVerifyAsync(api, client, UserSeed.NewEmail("first"), country);
        await RegisterAndVerifyAsync(api, client, UserSeed.NewEmail("second"), country);
        await RegisterAndVerifyAsync(api, client, UserSeed.NewEmail("third"), other);

        Assert.Equal(
            1,
            await ApiClients.CountTenantsAsync(_mongo.ConnectionString, $"country-{iso.ToLowerInvariant()}")
        );
        Assert.Equal(
            1,
            await ApiClients.CountTenantsAsync(_mongo.ConnectionString, $"country-{otherIso.ToLowerInvariant()}")
        );
    }

    [Fact]
    public async Task Registering_an_address_that_has_an_account_behaves_like_signing_in_and_answers_like_a_new_one()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Turkey", CountrySeed.UniqueIsoCode());
        var existing = await SeedUser();
        var fresh = UserSeed.NewEmail("new");

        var forExisting = await RegisterAsync(client, existing, country, givenName: "Someone", surname: "Else");
        var forFresh = await RegisterAsync(client, fresh, country);

        Assert.Equal(HttpStatusCode.Accepted, forExisting.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forFresh.StatusCode);
        Assert.Equal(await forExisting.Content.ReadAsStringAsync(), await forFresh.Content.ReadAsStringAsync());
        Assert.Equal(forExisting.Content.Headers.ContentType, forFresh.Content.Headers.ContentType);
        Assert.Equal(2, Outbox(api).Messages.Count);

        var created = await ApiSessions.CreateSessionAsync(client, existing, ApiSessions.CodeSentTo(api, existing));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await ApiClients.FindUserAsync(_mongo.ConnectionString, existing);
        Assert.Equal("Ana", user!["GivenName"].AsString); // what was typed to register is not applied to an account
        Assert.Equal("Bulgaria", user["CountryRegion"].AsString);
    }

    [Fact]
    public async Task A_wrong_code_creates_no_account_and_no_Tenant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", iso);
        var email = UserSeed.NewEmail("new");
        await RegisterAsync(client, email, country);
        var code = ApiSessions.CodeSentTo(api, email);

        var refused = await ApiSessions.CreateSessionAsync(client, email, code == "000000" ? "000001" : "000000");

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Null(await ApiClients.FindUserAsync(_mongo.ConnectionString, email));
        Assert.Null(await ApiClients.FindTenantAsync(_mongo.ConnectionString, $"country-{iso.ToLowerInvariant()}"));
    }

    [Fact]
    public async Task A_country_without_an_ISO_code_is_not_offered_and_is_refused()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var offered = await CountrySeed.AddAsync(_mongo.ConnectionString, "Offeredland", CountrySeed.UniqueIsoCode());
        var nameless = await CountrySeed.AddAsync(_mongo.ConnectionString, "Nowhereland", null);

        var page = await ApiClients.PageAsync(client, "/register");
        var refused = await RegisterAsync(client, UserSeed.NewEmail("new"), nameless);

        Assert.Contains("Offeredland", page);
        Assert.Contains(offered.ToString(), page);
        Assert.DoesNotContain("Nowhereland", page);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("invalid-country", await ErrorCodeAsync(refused));
        Assert.Empty(Outbox(api).Messages);
    }

    [Theory]
    [InlineData("not-an-address", "Ana", "Petrova", "valid", "invalid-email")]
    [InlineData("ana@example.org", "", "Petrova", "valid", "invalid-name")]
    [InlineData("ana@example.org", "Ana", "   ", "valid", "invalid-name")]
    [InlineData("ana@example.org", "Ana\nPetrova", "Petrova", "valid", "invalid-name")]
    [InlineData("ana@example.org", "Ana", "Pet\trova", "valid", "invalid-name")]
    [InlineData("ana@example.org", "Ana", "Petrova", "not-a-guid", "invalid-country")]
    [InlineData("ana@example.org", "Ana", "Petrova", "unknown", "invalid-country")]
    [InlineData("ana@example.org", "Ana", "Petrova", "missing", "invalid-country")]
    public async Task A_registration_needs_a_valid_address_both_names_and_a_country_that_can_be_chosen(
        string email,
        string givenName,
        string surname,
        string country,
        string expectedCode
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var known = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var countryId = country switch
        {
            "valid" => known.ToString(),
            "unknown" => Guid.NewGuid().ToString(),
            "missing" => null,
            _ => country,
        };

        var refused = await ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email,
                givenName,
                surname,
                countryId,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(expectedCode, await ErrorCodeAsync(refused));
        Assert.Empty(Outbox(api).Messages);
        Assert.Null(await ApiClients.FindUserAsync(_mongo.ConnectionString, email));
    }

    [Fact]
    public async Task The_registration_page_lists_the_countries_gives_a_one_line_privacy_notice_and_hides_a_trap_for_bots()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Pageland", CountrySeed.UniqueIsoCode());

        var page = await ApiClients.PageAsync(client, "/register");
        var privacy = await ApiClients.PageAsync(client, "/privacy");

        Assert.Contains($"<option value=\"{country}\">Pageland</option>", page);
        Assert.Contains("href=\"/privacy\"", page);
        Assert.Matches(@"<input[^>]*name=""website""[^>]*tabindex=""-1""", page);
        Assert.Contains("id=\"register-form\"", page);
        Assert.Contains("placeholder", privacy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_registration_page_writes_the_name_of_a_country_as_text_and_never_as_markup()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(
            _mongo.ConnectionString,
            "Fish & <script>alert(1)</script> Chips",
            CountrySeed.UniqueIsoCode()
        );

        var page = await ApiClients.PageAsync(client, "/register");

        Assert.Contains(
            $"<option value=\"{country}\">Fish &amp; &lt;script&gt;alert(1)&lt;/script&gt; Chips</option>",
            page
        );
        Assert.DoesNotContain("<script>alert(1)", page);
    }

    [Theory]
    [InlineData("bg", "Регистрация")]
    [InlineData("tr", "Kayıt")]
    [InlineData("de", "Create an account")]
    public async Task The_registration_page_speaks_the_language_of_the_visitor_and_falls_back_to_English(
        string language,
        string heading
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);

        var page = await ApiClients.PageAsync(client, "/register", language);

        Assert.Contains(heading, page);
    }

    [Fact]
    public async Task A_registration_that_fills_the_hidden_field_is_answered_like_any_other_and_does_nothing()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", iso);
        var bot = UserSeed.NewEmail("bot");

        var fromBot = await ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email = bot,
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
                website = "https://spam.example",
            }
        );
        var fromPerson = await RegisterAsync(client, UserSeed.NewEmail("person"), country);

        Assert.Equal(HttpStatusCode.Accepted, fromBot.StatusCode);
        Assert.Equal(await fromPerson.Content.ReadAsStringAsync(), await fromBot.Content.ReadAsStringAsync());
        Assert.Equal(fromPerson.Content.Headers.ContentType, fromBot.Content.Headers.ContentType);
        Assert.Single(Outbox(api).Messages); // the person's, not the bot's
        Assert.Null(await ApiClients.FindUserAsync(_mongo.ConnectionString, bot));

        // Nothing was kept against the address, so a person who has it can still register with it.
        var later = await RegisterAsync(client, bot, country);
        Assert.Equal(HttpStatusCode.Accepted, later.StatusCode);
        Assert.Equal(2, Outbox(api).Messages.Count);
    }

    [Fact]
    public async Task Every_answer_to_a_registration_gives_the_browser_the_same_kind_of_cookie_whatever_the_address()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());

        var forNew = await RegisterAsync(client, UserSeed.NewEmail("new"), country);
        var forExisting = await RegisterAsync(client, await SeedUser(), country);
        var forBot = await ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email = UserSeed.NewEmail("bot"),
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
                website = "https://spam.example",
            }
        );

        var values = new List<string>();
        foreach (var answer in new[] { forNew, forExisting, forBot })
        {
            Assert.Equal(HttpStatusCode.Accepted, answer.StatusCode);
            var cookie = RegistrationCookie.From(answer);
            Assert.NotNull(cookie);
            Assert.Matches("^[A-Za-z0-9_-]{43}$", cookie.Value);
            Assert.Contains("httponly", cookie.Attributes);
            Assert.Contains("secure", cookie.Attributes);
            Assert.Contains("samesite=strict", cookie.Attributes);
            Assert.Contains("path=/", cookie.Attributes);
            Assert.DoesNotContain(cookie.Attributes, x => x.StartsWith("domain=", StringComparison.Ordinal));
            values.Add(cookie.Value);
        }

        Assert.Equal(3, values.Distinct().Count()); // a browser that has none is given one of its own
    }

    [Fact]
    public async Task A_browser_keeps_the_value_of_its_registration_cookie_and_the_cookie_is_cleared_when_the_account_exists()
    {
        await using var api = NewApi();
        using var browser = ApiClients.OfBrowser(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var first = UserSeed.NewEmail("first");

        var firstAnswer = await RegisterAsync(browser, first, country);
        var secondAnswer = await RegisterAsync(browser, UserSeed.NewEmail("second"), country);
        var created = await ApiSessions.CreateSessionAsync(browser, first, ApiSessions.CodeSentTo(api, first));

        Assert.Equal(RegistrationCookie.From(firstAnswer)!.Value, RegistrationCookie.From(secondAnswer)!.Value);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("", RegistrationCookie.From(created)!.Value);
    }

    [Fact]
    public async Task A_second_request_inside_the_cooldown_from_the_same_browser_keeps_its_details_usable()
    {
        await using var api = NewApi();
        using var browser = ApiClients.OfBrowser(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", iso);
        var email = UserSeed.NewEmail("impatient");

        await RegisterAsync(browser, email, country);
        await RegisterAsync(browser, email, country); // asked again at once: no second mail, no new details
        var created = await ApiSessions.CreateSessionAsync(browser, email, ApiSessions.CodeSentTo(api, email));

        Assert.Single(Outbox(api).Messages);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.Equal("Ana", user["GivenName"].AsString);
        Assert.Equal($"country-{iso.ToLowerInvariant()}", user["HomeTenantId"].AsString);
    }

    [Fact]
    public async Task Details_that_another_browser_left_for_an_address_are_not_used_when_the_person_it_belongs_to_proves_it()
    {
        await using var api = NewApi();
        using var attacker = ApiClients.OfBrowser(api);
        using var person = ApiClients.OfBrowser(api);
        var attackerIso = CountrySeed.UniqueIsoCode();
        var attackerCountry = await CountrySeed.AddAsync(_mongo.ConnectionString, "Attackland", attackerIso);
        var personIso = CountrySeed.UniqueIsoCode();
        var personCountry = await CountrySeed.AddAsync(_mongo.ConnectionString, "Homeland", personIso);
        var email = UserSeed.NewEmail("victim");
        await RegisterAsync(attacker, email, attackerCountry, givenName: "Mallory", surname: "Mischief");
        await RegisterAsync(person, email, personCountry); // inside the cooldown: no second mail, the first details stand

        var created = await ApiSessions.CreateSessionAsync(person, email, ApiSessions.CodeSentTo(api, email));

        Assert.Single(Outbox(api).Messages);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); // the person proved the address: they are in
        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.True(user["EmailConfirmed"].AsBoolean);
        foreach (var field in new[] { "GivenName", "Surname", "Name", "CountryRegion", "HomeTenantId", "Memberships" })
        {
            Assert.False(user.Contains(field), $"the account has the field {field} that the other browser typed");
        }

        Assert.Null(
            await ApiClients.FindTenantAsync(_mongo.ConnectionString, $"country-{attackerIso.ToLowerInvariant()}")
        );
        Assert.Null(
            await ApiClients.FindTenantAsync(_mongo.ConnectionString, $"country-{personIso.ToLowerInvariant()}")
        );
    }

    [Fact]
    public async Task A_browser_that_sends_no_registration_cookie_gets_its_account_without_the_details()
    {
        await using var api = NewApi();
        using var registered = ApiClients.OfBrowser(api);
        using var cookieless = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var email = UserSeed.NewEmail("elsewhere");
        await RegisterAsync(registered, email, country);

        var created = await ApiSessions.CreateSessionAsync(cookieless, email, ApiSessions.CodeSentTo(api, email));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        Assert.True(user["EmailConfirmed"].AsBoolean);
        Assert.False(user.Contains("HomeTenantId"));
        Assert.False(user.Contains("GivenName"));
    }

    [Fact]
    public async Task A_registration_is_a_write_that_needs_the_json_api_media_type()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);

        var asPlainJson = await client.PostAsync(
            "/api/registrations",
            new StringContent("""{"data":{"type":"registrations","attributes":{"email":"a@b.test"}}}""")
        );
        var wrongType = await ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "sessions",
            new { email = "a@b.test" }
        );

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, asPlainJson.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(wrongType));
    }

    async Task RegisterAndVerifyAsync(ApiFactory api, HttpClient client, string email, Guid country)
    {
        await RegisterAsync(client, email, country);
        var created = await ApiSessions.CreateSessionAsync(client, email, ApiSessions.CodeSentTo(api, email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        Guid country,
        string givenName = "Ana",
        string surname = "Petrova",
        string? language = null
    )
    {
        return ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email,
                givenName,
                surname,
                countryId = country.ToString(),
            },
            language
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    static IEmailOutbox Outbox(ApiFactory api)
    {
        return api.Services.GetRequiredService<IEmailOutbox>();
    }

    ApiFactory NewApi(TimeProvider? time = null)
    {
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    async Task<string> SeedUser()
    {
        var email = UserSeed.NewEmail();
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        return email;
    }

    /// <summary>The cookie that ties a registration to the browser that made it, as a response sets it.</summary>
    sealed class RegistrationCookie
    {
        const string NAME = "__Host-NoTiming-Registration";

        public static RegistrationCookie? From(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                return null;
            }

            var header = values.FirstOrDefault(x => x.StartsWith(NAME + "=", StringComparison.Ordinal));
            if (header == null)
            {
                return null;
            }

            var parts = header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return new RegistrationCookie(
                parts[0][(NAME.Length + 1)..],
                [.. parts.Skip(1).Select(x => x.ToLowerInvariant())]
            );
        }

        RegistrationCookie(string value, IReadOnlyList<string> attributes)
        {
            Value = value;
            Attributes = attributes;
        }

        public string Value { get; }

        /// <summary>The attributes after the value, in lower case: <c>httponly</c>, <c>samesite=strict</c>, <c>path=/</c>.</summary>
        public IReadOnlyList<string> Attributes { get; }
    }
}
