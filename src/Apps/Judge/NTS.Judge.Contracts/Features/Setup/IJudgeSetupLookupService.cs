using Not.Injection;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Contracts.Features.Setup;

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
