using System.Text.Json;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Strings;
using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using static NoTiming.Ui.Features.Console.ApiAnswers;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>
/// What the forms of the Setup and of the registry pick from (#646). The Tenant's own Athletes, Horses and Clubs are read
/// and matched here; for a term with a word of three characters or more the registries of every Tenant are searched too
/// (ADR-0010, ADR-0012), which is one request the Api limits per account, so it is asked only for what the person typed, and
/// for the first six words of it, which is all the Api takes. A row of another Tenant is used by its id and is not edited:
/// the Api tells only what names it (its name, its FEI ID, the names of its country and club), which is what a Combination
/// needs of it. The countries are read once.
/// </summary>
public class ConsoleLookups : IConsoleLookups, IScoped
{
    const string ALL_TENANTS = "all-tenants";

    readonly IAccountSession _account;
    readonly JsonApiClient _api;
    readonly IRepository<Country> _countries;
    readonly IRepository<Athlete> _athletes;
    readonly IRepository<Horse> _horses;
    readonly IRepository<Club> _clubs;
    readonly IRepository<Loop> _loops;
    readonly IRepository<Combination> _combinations;
    IReadOnlyList<Country>? _read;

    public ConsoleLookups(
        IAccountSession account,
        JsonApiClient api,
        IRepository<Country> countries,
        IRepository<Athlete> athletes,
        IRepository<Horse> horses,
        IRepository<Club> clubs,
        IRepository<Loop> loops,
        IRepository<Combination> combinations
    )
    {
        _account = account;
        _api = api;
        _countries = countries;
        _athletes = athletes;
        _horses = horses;
        _clubs = clubs;
        _loops = loops;
        _combinations = combinations;
    }

    public async Task<IReadOnlyList<Country>> SearchCountries(string term)
    {
        var countries = await Countries();
        return [.. countries.Where(x => string.IsNullOrWhiteSpace(term) || x.Name.NContains(term))];
    }

    public async Task<Country?> TenantCountry()
    {
        await _account.Load();
        var tenantId = _account.Current?.CurrentTenantId;
        return string.IsNullOrWhiteSpace(tenantId)
            ? null
            : (await Countries()).FirstOrDefault(x => Tenant.ForCountry(x).Id == tenantId);
    }

    public async Task<IReadOnlyList<Club>> SearchClubs(string term)
    {
        var own = (await _clubs.ReadMany()).Where(x => string.IsNullOrWhiteSpace(term) || x.Name.NContains(term));
        var elsewhere = await OtherTenants("clubs", term, match => new Club(match.Name, match.Id));
        return Merged(own, elsewhere);
    }

    public async Task<IReadOnlyList<Athlete>> SearchAthletes(string term)
    {
        var own = Named(await _athletes.ReadMany(), term);
        var fallback = await TenantCountry();
        var countries = await Countries();
        var elsewhere = await OtherTenants(
            "athletes",
            term,
            match =>
            {
                var country = countries.FirstOrDefault(x => x.Matches(match.Country)) ?? fallback;
                var club = string.IsNullOrWhiteSpace(match.Club) ? null : new Club(match.Club);
                return new Athlete(match.Name, match.NameEnglish, match.FeiId, country, club, match.Id);
            }
        );
        return Merged(own, elsewhere);
    }

    public async Task<IReadOnlyList<Horse>> SearchHorses(string term)
    {
        var own = Named(await _horses.ReadMany(), term);
        var elsewhere = await OtherTenants(
            "horses",
            term,
            match => new Horse(match.Name, match.NameEnglish, match.FeiId, match.Id)
        );
        return Merged(own, elsewhere);
    }

    public async Task<IReadOnlyList<Loop>> GetLoops()
    {
        return [.. await _loops.ReadMany()];
    }

    public async Task<IReadOnlyList<Combination>> SearchCombinations(string term)
    {
        var combinations = await _combinations.ReadMany();
        return
        [
            .. string.IsNullOrWhiteSpace(term) ? combinations : combinations.Where(x => x.ToString().NContains(term)),
        ];
    }

    async Task<IReadOnlyList<Country>> Countries()
    {
        return _read ??= [.. await _countries.ReadMany()];
    }

    static IEnumerable<T> Named<T>(IEnumerable<T> rows, string term)
        where T : INamed
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return rows;
        }

        return rows.Where(x => Contains(x.Name, term) || Contains(x.NameEnglish, term) || Contains(x.ToString(), term));
    }

    static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.InvariantCultureIgnoreCase) == true;
    }

    /// <summary>The Tenant's own rows first, then those of the other Tenants that are not among them.</summary>
    static IReadOnlyList<T> Merged<T>(IEnumerable<T> own, IEnumerable<T> elsewhere)
        where T : Entity
    {
        var rows = own.ToList();
        return [.. rows, .. elsewhere.Where(x => rows.All(y => y.Id != x.Id))];
    }

    /// <summary>
    /// The rows of the other Tenants whose name has the term in it, as the entity the form needs. A search that the Api does
    /// not take (a limit that was reached) or cannot answer finds nothing: it is a help to the person who is typing, and the
    /// Tenant's own rows are there without it.
    /// </summary>
    async Task<IReadOnlyList<T>> OtherTenants<T>(string registry, string term, Func<Match, T> entityOf)
    {
        if (!ParticipantService.IsSearchable(term))
        {
            return [];
        }

        try
        {
            var words = string.Join(' ', ParticipantService.WordsOf(term));
            var filter = Uri.EscapeDataString($"contains(name,'{words.Replace("'", "''")}')");
            var response = await _api.Send(HttpMethod.Get, $"{registry}/{ALL_TENANTS}?filter={filter}");
            if (!response.IsSuccess)
            {
                return [];
            }

            var found = new List<T>();
            foreach (var resource in response.Document!.Value.GetProperty("data").EnumerateArray())
            {
                if (MatchOf(resource) is not { } match)
                {
                    continue;
                }

                try
                {
                    found.Add(entityOf(match));
                }
                catch (Exception ex) when (ex is DomainException or ArgumentException)
                {
                    // A row that the domain does not take as it was told cannot be used in a Setup: it is left out.
                }
            }

            return found;
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    static Match? MatchOf(JsonElement resource)
    {
        if (!Guid.TryParse(resource.GetProperty("id").GetString(), out var id))
        {
            return null;
        }

        var attributes = resource.GetProperty("attributes");
        return Text(attributes, "name") is { } name
            ? new Match(
                id,
                name,
                Text(attributes, "nameEnglish"),
                Text(attributes, "feiId"),
                Text(attributes, "club"),
                Text(attributes, "country")
            )
            : null;
    }

    sealed class Match
    {
        public Match(Guid id, string name, string? nameEnglish, string? feiId, string? club, string? country)
        {
            Id = id;
            Name = name;
            NameEnglish = nameEnglish;
            FeiId = feiId;
            Club = club;
            Country = country;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string? NameEnglish { get; }
        public string? FeiId { get; }
        public string? Club { get; }
        public string? Country { get; }
    }
}
