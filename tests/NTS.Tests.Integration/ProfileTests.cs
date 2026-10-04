using System.Net;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A signed-in person reads and edits their own profile (#602, ADR-0002, ADR-0012): country, names, club and FEI ID. It
/// needs a country, a first name and a surname. Editing it never moves the home Tenant, though a person who has none yet
/// is placed when they first pick a country. The routes act on the caller and on nobody else: there is no way to name
/// another account. Every country has a name and an ISO code of its own, because the countries are shared by the tests
/// of the class.
/// </summary>
public sealed class ProfileTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ProfileTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_profile_is_the_callers_own_with_the_country_it_names_resolved_and_nobody_elses()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        var boris = await SignedInAsync(
            api,
            client,
            "Boris",
            "Ivanov",
            "Nowhere",
            club: "Other Club",
            feiId: "20202020"
        );

        var anasView = await ReadAsync(ana.Page);
        var borissView = await ReadAsync(boris.Page);

        Assert.Equal(HttpStatusCode.OK, anasView.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(anasView)).GetProperty("data");
        Assert.Equal("profiles", resource.GetProperty("type").GetString());
        Assert.Equal(ana.Id.ToString(), resource.GetProperty("id").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal("Ana", attributes.GetProperty("givenName").GetString());
        Assert.Equal("Petrova", attributes.GetProperty("surname").GetString());
        Assert.Equal("K.", attributes.GetProperty("middleName").GetString());
        Assert.Equal("Rider Club", attributes.GetProperty("club").GetString());
        Assert.Equal("10012345", attributes.GetProperty("feiId").GetString());
        Assert.Equal(land.Name, attributes.GetProperty("countryRegion").GetString());
        Assert.Equal(land.Id.ToString(), attributes.GetProperty("countryId").GetString());
        Assert.True(attributes.GetProperty("complete").GetBoolean());
        var other = (await ApiSessions.ReadJsonAsync(borissView)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("Boris", other.GetProperty("givenName").GetString());
        Assert.Equal("Other Club", other.GetProperty("club").GetString());
        Assert.False(other.TryGetProperty("countryId", out _)); // a country that no country of ours matches stays unresolved
        Assert.Equal("Nowhere", other.GetProperty("countryRegion").GetString());
    }

    [Fact]
    public async Task The_account_of_the_caller_says_whether_the_profile_is_complete_and_which_Tenant_is_home()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var complete = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        var bare = await SignedInAsync(api, client, null, null, null);

        var completeMe = (await ApiSessions.ReadJsonAsync(await complete.Page.GetAsync("/api/me")))
            .GetProperty("data")
            .GetProperty("attributes");
        var bareMe = (await ApiSessions.ReadJsonAsync(await bare.Page.GetAsync("/api/me")))
            .GetProperty("data")
            .GetProperty("attributes");

        Assert.True(completeMe.GetProperty("profileComplete").GetBoolean());
        Assert.Equal($"country-{land.Iso.ToLowerInvariant()}", completeMe.GetProperty("homeTenantId").GetString());
        Assert.False(bareMe.GetProperty("profileComplete").GetBoolean());
        Assert.False(bareMe.TryGetProperty("homeTenantId", out _));
    }

    [Fact]
    public async Task Editing_the_profile_changes_what_it_names_and_leaves_the_home_Tenant_and_the_rest_of_the_account_alone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var home = await NewCountryAsync();
        var elsewhere = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", home.Name);
        var before = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;

        var saved = await PatchAsync(
            ana.Page,
            new
            {
                givenName = " Anna ",
                middleName = "Maria",
                surname = "Petrova-Ivanova",
                countryId = elsewhere.Id.ToString(),
                club = "New Club",
                feiId = "10099999",
            }
        );

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(saved)).GetProperty("data");
        Assert.Equal("profiles", resource.GetProperty("type").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal("Anna", attributes.GetProperty("givenName").GetString());
        Assert.Equal(elsewhere.Id.ToString(), attributes.GetProperty("countryId").GetString());
        var after = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;
        Assert.Equal("Anna Maria Petrova-Ivanova", after["Name"].AsString);
        Assert.Equal("Anna", after["GivenName"].AsString);
        Assert.Equal("Maria", after["MiddleName"].AsString);
        Assert.Equal("Petrova-Ivanova", after["Surname"].AsString);
        Assert.Equal(elsewhere.Name, after["CountryRegion"].AsString);
        Assert.Equal("New Club", after["Club"].AsString);
        Assert.Equal("10099999", after["FeiId"].AsString);
        // The country of the profile moved; the Tenant of the person did not, and nothing else of the document did.
        Assert.Equal($"country-{home.Iso.ToLowerInvariant()}", after["HomeTenantId"].AsString);
        foreach (
            var field in new[] { "_id", "Email", "Roles", "TenantId", "DisplayName", "Memberships", "SecurityStamp" }
        )
        {
            Assert.Equal(before[field], after[field]);
        }
    }

    [Fact]
    public async Task A_member_that_is_not_named_is_left_as_it_is_and_an_optional_one_that_is_blank_or_null_is_cleared()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);

        var onlyTheClub = await PatchAsync(ana.Page, new { club = "Just the club" });
        var afterClub = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;
        var cleared = await PatchAsync(
            ana.Page,
            new
            {
                middleName = (string?)null,
                club = "  ",
                feiId = "",
            }
        );
        var afterClear = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;

        Assert.Equal(HttpStatusCode.OK, onlyTheClub.StatusCode);
        Assert.Equal("Just the club", afterClub["Club"].AsString);
        Assert.Equal("Ana", afterClub["GivenName"].AsString);
        Assert.Equal("K.", afterClub["MiddleName"].AsString);
        Assert.Equal("10012345", afterClub["FeiId"].AsString);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.False(afterClear.Contains("MiddleName"));
        Assert.False(afterClear.Contains("Club"));
        Assert.False(afterClear.Contains("FeiId"));
        Assert.Equal("Ana Petrova", afterClear["Name"].AsString);
    }

    [Fact]
    public async Task Only_the_members_that_are_named_are_written_so_a_name_that_is_not_the_three_names_put_together_stays()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        // A name that somebody wrote by hand: it is not what the three names make, and an edit of the club leaves it.
        await UserSeed
            .Users(_mongo.ConnectionString)
            .UpdateOneAsync(
                new BsonDocument("Email", ana.Email),
                MongoDB.Driver.Builders<BsonDocument>.Update.Set("Name", "Ana Petrova (the rider)")
            );

        var club = await PatchAsync(ana.Page, new { club = "Only the club" });
        var afterClub = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;
        var renamed = await PatchAsync(ana.Page, new { surname = "Ivanova" });
        var afterRename = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;

        Assert.Equal(HttpStatusCode.OK, club.StatusCode);
        Assert.Equal("Ana Petrova (the rider)", afterClub["Name"].AsString);
        Assert.Equal("Only the club", afterClub["Club"].AsString);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Ana K. Ivanova", afterRename["Name"].AsString); // the names changed: the name is made again
    }

    [Fact]
    public async Task A_member_is_found_whatever_its_case_like_the_members_of_every_other_document()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);

        var saved = await PatchAsync(
            ana.Page,
            new Dictionary<string, object?> { ["GivenName"] = "Anna", ["CLUB"] = "Loud Club" }
        );

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var after = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;
        Assert.Equal("Anna", after["GivenName"].AsString);
        Assert.Equal("Loud Club", after["Club"].AsString);
    }

    [Theory]
    [InlineData("givenName", "", "invalid-name")]
    [InlineData("givenName", "   ", "invalid-name")]
    [InlineData("givenName", null, "invalid-name")]
    [InlineData("givenName", 7, "invalid-name")]
    [InlineData("givenName", "Ana\nPetrova", "invalid-name")]
    [InlineData("surname", "", "invalid-name")]
    [InlineData("surname", null, "invalid-name")]
    [InlineData("middleName", "K\t.", "invalid-name")]
    [InlineData("countryId", "not-a-guid", "invalid-country")]
    [InlineData("countryId", "", "invalid-country")]
    [InlineData("countryId", null, "invalid-country")]
    [InlineData("countryId", "unknown", "invalid-country")]
    [InlineData("club", "Club\r\nforged", "invalid-club")]
    [InlineData("feiId", "1\n2", "invalid-fei-id")]
    public async Task A_member_that_is_not_valid_is_refused_and_nothing_is_saved(
        string member,
        object? value,
        string expectedCode
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        var before = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;
        var attributes = new Dictionary<string, object?>
        {
            ["club"] = "Would be saved",
            [member] = value is "unknown" ? Guid.NewGuid().ToString() : value,
        };

        var refused = await PatchAsync(ana.Page, attributes);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(expectedCode, await ErrorCodeAsync(refused));
        Assert.Equal(before, await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email));
    }

    [Fact]
    public async Task A_name_a_club_and_an_FEI_ID_have_a_longest_length_and_the_longest_is_accepted()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);

        var longest = await PatchAsync(
            ana.Page,
            new
            {
                givenName = new string('a', 100),
                club = new string('c', 100),
                feiId = new string('1', 20),
            }
        );
        var tooLongName = await PatchAsync(ana.Page, new { surname = new string('s', 101) });
        var tooLongClub = await PatchAsync(ana.Page, new { club = new string('c', 101) });
        var tooLongFeiId = await PatchAsync(ana.Page, new { feiId = new string('1', 21) });

        Assert.Equal(HttpStatusCode.OK, longest.StatusCode);
        Assert.Equal("invalid-name", await ErrorCodeAsync(tooLongName));
        Assert.Equal("invalid-club", await ErrorCodeAsync(tooLongClub));
        Assert.Equal("invalid-fei-id", await ErrorCodeAsync(tooLongFeiId));
    }

    [Fact]
    public async Task A_country_without_an_ISO_code_cannot_be_chosen_for_a_profile()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        var noIso = await CountrySeed.AddAsync(_mongo.ConnectionString, $"Noisoland {Guid.NewGuid():N}", null);

        var refused = await PatchAsync(ana.Page, new { countryId = noIso.ToString() });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("invalid-country", await ErrorCodeAsync(refused));
    }

    [Fact]
    public async Task A_profile_that_would_lack_a_country_a_first_name_or_a_surname_is_refused_as_incomplete_and_nothing_is_saved()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var bare = await SignedInAsync(api, client, null, null, null);
        var before = (await ApiClients.FindUserAsync(_mongo.ConnectionString, bare.Email))!;

        var onlyTheClub = await PatchAsync(bare.Page, new { club = "A club" });
        var noSurname = await PatchAsync(bare.Page, new { givenName = "Ana", countryId = land.Id.ToString() });
        var nothing = await PatchAsync(bare.Page, new { });

        foreach (var refused in new[] { onlyTheClub, noSurname, nothing })
        {
            Assert.Equal((HttpStatusCode)422, refused.StatusCode);
            Assert.Equal("profile-incomplete", await ErrorCodeAsync(refused));
        }

        Assert.Equal(before, await ApiClients.FindUserAsync(_mongo.ConnectionString, bare.Email));
    }

    [Fact]
    public async Task A_person_without_a_home_Tenant_is_placed_when_they_first_complete_the_profile_and_never_moved_after()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var first = await NewCountryAsync();
        var second = await NewCountryAsync();
        var bare = await SignedInAsync(api, client, null, null, null);

        var completed = await PatchAsync(
            bare.Page,
            new
            {
                givenName = "Ana",
                surname = "Petrova",
                countryId = first.Id.ToString(),
            }
        );
        var afterFirst = (await ApiClients.FindUserAsync(_mongo.ConnectionString, bare.Email))!;
        await PatchAsync(bare.Page, new { countryId = second.Id.ToString() });
        var afterSecond = (await ApiClients.FindUserAsync(_mongo.ConnectionString, bare.Email))!;

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var tenant = $"country-{first.Iso.ToLowerInvariant()}";
        Assert.Equal(tenant, afterFirst["HomeTenantId"].AsString);
        Assert.Equal(tenant, Assert.Single(afterFirst["Memberships"].AsBsonArray)["TenantId"].AsString);
        Assert.Equal(second.Name, afterSecond["CountryRegion"].AsString);
        Assert.Equal(tenant, afterSecond["HomeTenantId"].AsString);
        Assert.NotNull(await ApiClients.FindTenantAsync(_mongo.ConnectionString, tenant));
    }

    [Fact]
    public async Task The_caller_cannot_name_another_account_because_a_body_that_names_one_is_refused_and_it_stays_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var mallory = await SignedInAsync(api, client, "Mallory", "Mischief", land.Name);
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);
        var before = (await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email))!;

        var response = await WriteRawAsync(mallory.Page, Document(ana.Id, new { givenName = "Hacked" }));
        var ownId = await WriteRawAsync(mallory.Page, Document(mallory.Id, new { club = "Fine" }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("profile-id-mismatch", await ErrorCodeAsync(response));
        Assert.Equal(before, await ApiClients.FindUserAsync(_mongo.ConnectionString, ana.Email));
        Assert.Equal(HttpStatusCode.OK, ownId.StatusCode); // the caller's own id is fine
    }

    [Fact]
    public async Task The_profile_routes_need_a_session_the_write_header_and_the_json_api_media_type()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var land = await NewCountryAsync();
        var ana = await SignedInAsync(api, client, "Ana", "Petrova", land.Name);

        var anonymousRead = await client.GetAsync("/api/me/profile");
        using var anonymousPatch = new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = new StringContent(
                """{"data":{"type":"profiles","attributes":{"club":"x"}}}""",
                Encoding.UTF8,
                ApiSessions.MEDIA_TYPE
            ),
        };
        var anonymousWrite = await client.SendAsync(anonymousPatch);
        ana.Page.WriteHeader = null;
        var withoutHeader = await PatchAsync(ana.Page, new { club = "x" });
        ana.Page.WriteHeader = "NoTiming";
        var plainJson = await WriteRawAsync(
            ana.Page,
            """{"data":{"type":"profiles","attributes":{}}}""",
            "application/json"
        );
        var wrongType = await WriteRawAsync(ana.Page, """{"data":{"type":"accounts","attributes":{}}}""");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrite.StatusCode);
        Assert.Equal("request-header-required", await ErrorCodeAsync(withoutHeader));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, plainJson.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal("malformed-request", await ErrorCodeAsync(wrongType));
    }

    async Task<NewCountry> NewCountryAsync()
    {
        var name = $"Profileland {Guid.NewGuid():N}";
        var iso = CountrySeed.UniqueIsoCode();
        var id = await CountrySeed.AddAsync(_mongo.ConnectionString, name, iso);
        return new NewCountry(id, name, iso);
    }

    /// <summary>A person who has signed in, with the profile they had before Tenants (none of it for a null first name).</summary>
    async Task<SignedInPerson> SignedInAsync(
        ApiFactory api,
        HttpClient client,
        string? givenName,
        string? surname,
        string? country,
        string club = "Rider Club",
        string feiId = "10012345"
    )
    {
        var email = UserSeed.NewEmail("profile");
        var id = await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document =>
            {
                foreach (
                    var field in new[]
                    {
                        "Name",
                        "GivenName",
                        "Surname",
                        "MiddleName",
                        "CountryRegion",
                        "Club",
                        "FeiId",
                    }
                )
                {
                    document.Remove(field);
                }

                if (givenName != null)
                {
                    document["GivenName"] = givenName;
                    document["Surname"] = surname;
                    document["MiddleName"] = "K.";
                    document["Name"] = $"{givenName} K. {surname}";
                    document["Club"] = club;
                    document["FeiId"] = feiId;
                }

                if (country != null)
                {
                    document["CountryRegion"] = country;
                }
            }
        );
        var page = new PageClient(client);
        page.Set(await ApiSessions.SignInAsync(api, client, email));
        return new SignedInPerson(id, email, page);
    }

    static Task<HttpResponseMessage> ReadAsync(PageClient page)
    {
        return page.GetAsync("/api/me/profile");
    }

    static Task<HttpResponseMessage> PatchAsync(PageClient page, object attributes)
    {
        return page.WriteAsync(HttpMethod.Patch, "/api/me/profile", "profiles", attributes);
    }

    /// <summary>A document that names the id of the account it is meant for.</summary>
    static string Document(Guid id, object attributes)
    {
        return JsonSerializer.Serialize(
            new
            {
                data = new
                {
                    type = "profiles",
                    id,
                    attributes,
                },
            }
        );
    }

    static Task<HttpResponseMessage> WriteRawAsync(
        PageClient page,
        string json,
        string contentType = ApiSessions.MEDIA_TYPE
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/me/profile")
        {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };
        return page.SendAsync(request);
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class NewCountry
    {
        public NewCountry(Guid id, string name, string iso)
        {
            Id = id;
            Name = name;
            Iso = iso;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Iso { get; }
    }

    sealed class SignedInPerson
    {
        public SignedInPerson(Guid id, string email, PageClient page)
        {
            Id = id;
            Email = email;
            Page = page;
        }

        public Guid Id { get; }
        public string Email { get; }
        public PageClient Page { get; }
    }
}
