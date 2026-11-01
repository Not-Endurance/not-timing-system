using System.Net;
using System.Text.Json;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Profile;
using NTS.Domain.Aggregates;

namespace NoTiming.Ui.Features.Profile;

/// <summary>
/// The profile of the person at the Ui (#645): what the host keeps of them, at <c>/api/me/profile</c>, and the countries to
/// choose from. It follows the account: a visitor has none, and a person who has just signed in is read, so the drawer does
/// not render without its header until the next full page load.
/// </summary>
public class WitnessProfileContext : AccountAwareContext, IWitnessProfileContext, IScoped
{
    readonly IRepository<Country> _countries;
    readonly JsonApiClient _api;
    readonly SemaphoreSlim _linking = new(1, 1); // a link is made after the one before it, so that the profile that is kept is the latest
    IReadOnlyList<Country> _countryList = [];

    public WitnessProfileContext(IAccountSession account, IRepository<Country> countries, JsonApiClient api)
        : base(account)
    {
        _countries = countries;
        _api = api;
    }

    public AccountProfile? Profile { get; private set; }

    public bool RequiresProfileCompletion => Account.Current is { ProfileComplete: false };

    public string WelcomeName => WitnessProfilePolicy.ResolveWelcomeName(Profile, Account.Current);

    protected override async Task<bool> InitializeState()
    {
        await Account.Load();
        if (!Account.IsKnown)
        {
            return false; // the host has not said who is signed in: ask again
        }

        if (Account.IsSignedIn)
        {
            var response = await _api.Send(HttpMethod.Get, "me/profile");
            if (response.IsSuccess)
            {
                // What was there is replaced once the new is known, so that nothing is shown as a visitor's meanwhile.
                var profile = ProfileOf(response.Document!.Value);
                _countryList = [.. (await _countries.ReadMany()).OrderBy(country => country.Name)];
                Profile = profile;
                return true;
            }

            if (response.Status != HttpStatusCode.Unauthorized)
            {
                throw response.ToException();
            }
        }

        Profile = null; // a visitor, or a session that ended meanwhile, which the account is told of by its own refresh
        _countryList = [];
        return true;
    }

    public WitnessProfileFormModel CreateFormModel()
    {
        return WitnessProfileFormModel.From(Profile, ResolveCountry(Profile));
    }

    public Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult<IEnumerable<Country>>(_countryList);
        }

        var normalizedTerm = term.Trim();
        return Task.FromResult<IEnumerable<Country>>(
            _countryList.Where(country =>
                Contains(country.Name, normalizedTerm)
                || Contains(country.IsoCode, normalizedTerm)
                || Contains(country.NfCode, normalizedTerm)
            )
        );
    }

    public async Task<AccountProfile> Save(WitnessProfileFormModel model)
    {
        var account =
            Account.Current
            ?? throw new InvalidOperationException("Cannot save a profile before somebody is signed in.");
        if (!WitnessProfilePolicy.IsComplete(model))
        {
            throw new InvalidOperationException("Country, first name and last name are required.");
        }

        // Every member is named, so that one the person emptied is taken away, as a change names what it changes.
        var document = JsonSerializer.SerializeToElement(
            new
            {
                data = new
                {
                    type = "profiles",
                    id = account.Id.ToString(),
                    attributes = new
                    {
                        givenName = model.GivenName?.Trim(),
                        middleName = model.MiddleName?.Trim(),
                        surname = model.Surname?.Trim(),
                        countryId = model.Country!.Id,
                        club = model.Club?.Trim(),
                        feiId = model.FeiId?.Trim(),
                    },
                },
            },
            JsonApiClient.WriteOptions
        );
        var response = await _api.Send(HttpMethod.Patch, "me/profile", document);
        if (!response.IsSuccess)
        {
            throw response.ToException();
        }

        var saved = ProfileOf(response.Document!.Value);
        Profile = saved;
        EmitChanged();
        await Account.Refresh(); // the account says whether the profile is complete, and the host may have placed the person
        return saved;
    }

    public Task LinkAthlete(Guid athleteId)
    {
        return Change(HttpMethod.Post, $"athletes/{athleteId}/link");
    }

    public async Task UnlinkAthlete()
    {
        if (Profile?.Athlete is { } athlete)
        {
            await Change(HttpMethod.Delete, $"athletes/{athlete.Id}/link");
        }
    }

    public Task AddHorse(Guid horseId)
    {
        return Change(HttpMethod.Post, $"horses/{horseId}/link");
    }

    public Task RemoveHorse(Guid horseId)
    {
        return Change(HttpMethod.Delete, $"horses/{horseId}/link");
    }

    /// <summary>
    /// Makes or takes away a link at the host and reads the profile again, so that what is shown is what the host keeps and
    /// not what the person asked for. A link that is gone when it is taken away is taken away all the same.
    /// </summary>
    async Task Change(HttpMethod method, string link)
    {
        if (Profile == null)
        {
            throw new InvalidOperationException("Cannot link before somebody is signed in.");
        }

        await _linking.WaitAsync();
        try
        {
            var changed = await _api.Send(method, link);
            if (!changed.IsSuccess && !(method == HttpMethod.Delete && changed.IsNotFound))
            {
                throw changed.ToException();
            }

            var read = await _api.Send(HttpMethod.Get, "me/profile");
            if (!read.IsSuccess)
            {
                throw read.ToException();
            }

            Profile = ProfileOf(read.Document!.Value);
            EmitChanged();
        }
        finally
        {
            _linking.Release();
        }
    }

    Country? ResolveCountry(AccountProfile? profile)
    {
        return _countryList.FirstOrDefault(country => country.Id == profile?.CountryId)
            ?? _countryList.FirstOrDefault(country => country.Matches(profile?.CountryRegion));
    }

    static AccountProfile ProfileOf(JsonElement document)
    {
        var attributes = document.GetProperty("data").GetProperty("attributes");
        return new AccountProfile
        {
            GivenName = Text(attributes, "givenName"),
            MiddleName = Text(attributes, "middleName"),
            Surname = Text(attributes, "surname"),
            CountryId = Text(attributes, "countryId") is { } id && Guid.TryParse(id, out var country) ? country : null,
            CountryRegion = Text(attributes, "countryRegion"),
            Club = Text(attributes, "club"),
            FeiId = Text(attributes, "feiId"),
            Complete =
                attributes.TryGetProperty("complete", out var complete) && complete.ValueKind == JsonValueKind.True,
            Athlete =
                attributes.TryGetProperty("athlete", out var athlete) && athlete.ValueKind == JsonValueKind.Object
                    ? ParticipantJson.NameIn(athlete)
                    : null,
            Horses =
                attributes.TryGetProperty("horses", out var horses) && horses.ValueKind == JsonValueKind.Array
                    ? [.. horses.EnumerateArray().Select(ParticipantJson.NameIn)]
                    : [],
        };
    }

    static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
