using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Officials;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents.Officials;

public class OfficialShellBehind : KrudShell<OfficialFormModel>
{
    [Inject]
    protected IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchUsers(term, CancellationToken.None)).Cast<User?>();
    }
}
