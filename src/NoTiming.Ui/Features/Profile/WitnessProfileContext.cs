using System.Net;
using System.Text.Json;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Participants;
using NoTiming.Ui.Storage.REST;
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
    readonly AccountApiRepository _accounts;
    readonly SemaphoreSlim _linking = new(1, 1); // a link is made after the one before it, so that the profile that is kept is the latest
    IReadOnlyList<Country> _countryList = [];

    public WitnessProfileContext(IAccountSession account, IRepository<Country> countries, AccountApiRepository accounts)
        : base(account)
    {
        _countries = countries;
        _accounts = accounts;
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

        if (Account.IsSignedIn && await ProfileOfSignedIn() is { } profile)
        {
            // What was there is replaced once the new is known, so that nothing is shown as a visitor's meanwhile.
            _countryList = [.. (await _countries.ReadMany()).OrderBy(country => country.Name)];
            Profile = profile;
            return true;
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
        var saved = ProfileOf(
            await _accounts.SaveProfile(
                account.Id,
                new
                {
                    givenName = model.GivenName?.Trim(),
                    middleName = model.MiddleName?.Trim(),
                    surname = model.Surname?.Trim(),
                    countryId = model.Country!.Id,
                    club = model.Club?.Trim(),
                    feiId = model.FeiId?.Trim(),
                }
            )
        );
        Profile = saved;
        EmitChanged();
        await Account.Refresh(); // the account says whether the profile is complete, and the host may have placed the person
        return saved;
    }

    public Task LinkAthlete(Guid athleteId)
    {
        return Change(() => _accounts.LinkAthlete(athleteId));
    }

    public async Task UnlinkAthlete()
    {
        if (Profile?.Athlete is { } athlete)
        {
            await Change(() => _accounts.UnlinkAthlete(athlete.Id));
        }
    }

    public Task AddHorse(Guid horseId)
    {
        return Change(() => _accounts.LinkHorse(horseId));
    }

    public Task RemoveHorse(Guid horseId)
    {
        return Change(() => _accounts.UnlinkHorse(horseId));
    }

    /// <summary>
    /// Makes or takes away a link at the host and reads the profile again, so that what is shown is what the host keeps and
    /// not what the person asked for. A link that is gone when it is taken away is taken away all the same.
    /// </summary>
    async Task Change(Func<Task> change)
    {
        if (Profile == null)
        {
            throw new InvalidOperationException("Cannot link before somebody is signed in.");
        }

        await _linking.WaitAsync();
        try
        {
            await change();
            Profile = ProfileOf(await _accounts.Profile());
            EmitChanged();
        }
        finally
        {
            _linking.Release();
        }
    }

    /// <summary>The profile the host keeps of the person who is signed in; none when the session has ended meanwhile (the Api answers 401).</summary>
    async Task<AccountProfile?> ProfileOfSignedIn()
    {
        try
        {
            return ProfileOf(await _accounts.Profile());
        }
        catch (JsonApiException refused) when (refused.Response.Status == HttpStatusCode.Unauthorized)
        {
            return null;
        }
    }

    Country? ResolveCountry(AccountProfile? profile)
    {
        return _countryList.FirstOrDefault(country => country.Id == profile?.CountryId)
            ?? _countryList.FirstOrDefault(country => country.Matches(profile?.CountryRegion));
    }

    static AccountProfile ProfileOf(JsonElement data)
    {
        var attributes = data.GetProperty("attributes");
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
                attributes.TryGetProperty("complete", out var complete)
                && complete.ValueKind == JsonValueKind.True,
            Athlete =
                attributes.TryGetProperty("athlete", out var athlete)
                && athlete.ValueKind == JsonValueKind.Object
                    ? ParticipantJson.NameIn(athlete)
                    : null,
            Horses =
                attributes.TryGetProperty("horses", out var horses)
                && horses.ValueKind == JsonValueKind.Array
                    ? [.. horses.EnumerateArray().Select(ParticipantJson.NameIn)]
                    : [],
        };
    }

    static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
