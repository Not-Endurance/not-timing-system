using Not.Krud.Blazor.Components.Abstractions;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents.Officials;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Officials;

public class OfficialShellBehind : KrudShell<OfficialFormModel>
{
    [Inject]
    protected IUserEmailLookup Users { get; set; } = default!;

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        return (await Users.Search(term)).Cast<User?>();
    }
}
