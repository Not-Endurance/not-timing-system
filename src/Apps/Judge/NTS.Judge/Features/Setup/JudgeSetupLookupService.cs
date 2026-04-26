using Not.Application.Cache;
using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Strings;
using NTS.Application.Setup;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup;

public class JudgeSetupLookupService : IJudgeSetupLookupService, ITransient
{
    readonly IReadMany<Athlete> _athletes;
    readonly IReadMany<Club> _clubs;
    readonly ICache<Country> _countries;
    readonly IReadMany<Combination> _combinations;
    readonly IReadMany<Horse> _horses;
    readonly IReadMany<Loop> _loops;
    readonly IUserEmailLookup _users;

    public JudgeSetupLookupService(
        ICache<Country> countries,
        IReadMany<Club> clubs,
        IUserEmailLookup users,
        IReadMany<Athlete> athletes,
        IReadMany<Horse> horses,
        IReadMany<Loop> loops,
        IReadMany<Combination> combinations
    )
    {
        _countries = countries;
        _clubs = clubs;
        _users = users;
        _athletes = athletes;
        _horses = horses;
        _loops = loops;
        _combinations = combinations;
    }

    public async Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct)
    {
        var items = await _countries.List();
        return items.Where(x => x.Name.NContains(term));
    }

    public async Task<IEnumerable<Club>> SearchClubs(string term, CancellationToken ct)
    {
        var items = await _clubs.ReadMany();
        return items.Where(x => term == string.Empty || x.Name.NContains(term));
    }

    public Task<IEnumerable<User>> SearchUsers(string term, CancellationToken ct)
    {
        return _users.Search(term);
    }

    public async Task<IEnumerable<Athlete>> SearchAthletes(string term, CancellationToken ct)
    {
        return Search(await _athletes.ReadMany(), term);
    }

    public async Task<IEnumerable<Horse>> SearchHorses(string term, CancellationToken ct)
    {
        return Search(await _horses.ReadMany(), term);
    }

    public Task<IEnumerable<Loop>> GetLoops(CancellationToken ct)
    {
        return _loops.ReadMany();
    }

    public async Task<IEnumerable<Combination>> SearchCombinations(string term, CancellationToken ct)
    {
        var values = await _combinations.ReadMany();
        if (string.IsNullOrWhiteSpace(term))
        {
            return values;
        }

        return values.Where(x => StringExtensions.NContains(x.ToString()!, term));
    }

    static IEnumerable<T> Search<T>(IEnumerable<T> values, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return values;
        }

        return values.Where(x =>
            x != null && x.ToString()!.Contains(term, StringComparison.InvariantCultureIgnoreCase)
        );
    }
}
