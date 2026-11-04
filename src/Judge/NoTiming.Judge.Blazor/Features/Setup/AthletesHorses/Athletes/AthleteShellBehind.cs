using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Judge.Contracts.Features.Setup.Athletes;

namespace NoTiming.Judge.Blazor.Features.Setup.AthletesHorses.Athletes;

public class AthleteShellBehind : KrudShell<AthleteFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Lookups.SearchCountries(term, ct);
    }

    protected async Task<IEnumerable<Club?>> SearchClubsSafe(string term, CancellationToken _)
    {
        return await Lookups.SearchClubs(term, CancellationToken.None);
    }

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        return await Lookups.SearchUsers(term, CancellationToken.None);
    }
}
