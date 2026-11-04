using NC.Injection;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Contracts.Features.Setup;

public interface IJudgeSetupLookupService : ITransient
{
    Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct);
    Task<IEnumerable<Club>> SearchClubs(string term, CancellationToken ct);
    Task<IEnumerable<User>> SearchUsers(string term, CancellationToken ct);
    Task<IEnumerable<Athlete>> SearchAthletes(string term, CancellationToken ct);
    Task<IEnumerable<Horse>> SearchHorses(string term, CancellationToken ct);
    Task<IEnumerable<Loop>> GetLoops(CancellationToken ct);
    Task<IEnumerable<Combination>> SearchCombinations(string term, CancellationToken ct);
}
