using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Not.Identity.Email;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Who may register where (#601): staging is limited to an allow-list that is configured, and production is open. An
/// address that is not allowed is answered like one that is, and nothing is sent or stored, so the answer tells
/// nothing about the list or about the accounts there are. An account that exists signs in whatever the list says.
/// The addresses and domains are made up for each test: the database is shared by the tests of the class, and a code
/// that is still cooling down for an address would keep a mail from being sent.
/// </summary>
public sealed class RegistrationAllowListTests : IClassFixture<MongoFixture>
{
    const string LIST = "Registration:AllowList";

    readonly MongoFixture _mongo;

    public RegistrationAllowListTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Staging_registers_the_addresses_on_the_list_and_the_domains_it_names_and_answers_everyone_alike()
    {
        var id = Guid.NewGuid().ToString("N");
        var listed = $"tester.{id}@example.test";
        var domain = $"team-{id}.example";
        await using var api = Staging($"{listed}; @{domain}");
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());

        var answers = new[]
        {
            await RegisterAsync(client, listed, country),
            await RegisterAsync(client, $"Other.Tester@{domain.ToUpperInvariant()}", country),
            await RegisterAsync(client, $"stranger.{id}@example.test", country),
            await RegisterAsync(client, $"someone@sub.{domain}", country),
            await RegisterAsync(client, $"someone@{domain}.evil.test", country),
            await RegisterAsync(client, $"someone@not{domain}", country),
        };

        Assert.All(answers, x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.Single(answers.Select(x => x.Content.Headers.ContentType?.ToString()).Distinct());
        Assert.Equal([listed, $"other.tester@{domain}"], Outbox(api).Messages.Select(x => x.To));
    }

    [Fact]
    public async Task A_listed_address_registers_and_signs_in_on_staging_like_anywhere()
    {
        var listed = UserSeed.NewEmail("tester");
        await using var api = Staging(listed);
        using var client = ApiClients.OfBrowser(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        await RegisterAsync(client, listed, country);

        var created = await ApiSessions.CreateSessionAsync(client, listed, ApiSessions.CodeSentTo(api, listed));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(await ApiClients.FindUserAsync(_mongo.ConnectionString, listed));
    }

    [Fact]
    public async Task Staging_without_a_list_is_closed_to_new_addresses()
    {
        await using var api = Staging(null);
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var email = UserSeed.NewEmail("closed");

        var answer = await RegisterAsync(client, email, country);

        Assert.Equal(HttpStatusCode.Accepted, answer.StatusCode);
        Assert.Empty(Outbox(api).Messages);
        var refused = await ApiSessions.CreateSessionAsync(client, email, "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Null(await ApiClients.FindUserAsync(_mongo.ConnectionString, email));
    }

    [Fact]
    public async Task An_account_that_exists_signs_in_through_registration_on_staging_whatever_the_list_says()
    {
        await using var api = Staging($"someone-else.{Guid.NewGuid():N}@example.test");
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var existing = UserSeed.NewEmail("existing");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, existing);
        var stranger = UserSeed.NewEmail("stranger");

        var forExisting = await RegisterAsync(client, existing, country);
        var forStranger = await RegisterAsync(client, stranger, country);

        Assert.Equal(await forExisting.Content.ReadAsStringAsync(), await forStranger.Content.ReadAsStringAsync());
        Assert.Equal(forExisting.StatusCode, forStranger.StatusCode);
        var mail = Assert.Single(Outbox(api).Messages);
        Assert.Equal(existing, mail.To);
        var created = await ApiSessions.CreateSessionAsync(client, existing, ApiSessions.CodeSentTo(api, existing));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Production_is_open_to_any_address_when_no_list_is_configured()
    {
        var sender = new RecordingSender();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            environment: "Production",
            configureServices: services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            }
        );
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var email = UserSeed.NewEmail("open");

        var answer = await RegisterAsync(client, email, country);

        Assert.Equal(HttpStatusCode.Accepted, answer.StatusCode);
        Assert.Equal(email, Assert.Single(sender.Messages).To);
    }

    [Fact]
    public async Task A_list_that_is_configured_restricts_a_host_of_any_environment()
    {
        var domain = $"team-{Guid.NewGuid():N}.example";
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureHost: host => host.UseSetting(LIST, $"@{domain}")
        );
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());

        await RegisterAsync(client, UserSeed.NewEmail("outsider"), country);
        await RegisterAsync(client, $"member@{domain}", country);

        Assert.Equal($"member@{domain}", Assert.Single(Outbox(api).Messages).To);
    }

    ApiFactory Staging(string? list)
    {
        return new ApiFactory(
            _mongo.ConnectionString,
            environment: "Staging",
            configureHost: host =>
            {
                if (list != null)
                {
                    host.UseSetting(LIST, list);
                }
            }
        );
    }

    static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, Guid country)
    {
        return ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email,
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
            }
        );
    }

    static IEmailOutbox Outbox(ApiFactory api)
    {
        return api.Services.GetRequiredService<IEmailOutbox>();
    }

    /// <summary>A sender that keeps what it is given, as a real one does not expose: Production accepts it.</summary>
    sealed class RecordingSender : IEmailSender, IEmailOutbox
    {
        readonly List<EmailMessage> _messages = [];

        public IReadOnlyList<EmailMessage> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            lock (_messages)
            {
                _messages.Add(message);
            }

            return Task.CompletedTask;
        }

        public void Clear()
        {
            lock (_messages)
            {
                _messages.Clear();
            }
        }
    }
}
