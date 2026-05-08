using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Officials;

namespace NTS.Judge.Blazor.Features.Setup.ConfigureEvents.Officials;

public class OfficialShellBehind : KrudShell<OfficialFormModel>
{
    [Inject]
    protected IJudgeSetupLookupService Lookups { get; set; } = default!;

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        return (await Lookups.SearchUsers(term, CancellationToken.None)).Cast<User?>();
    }
}
