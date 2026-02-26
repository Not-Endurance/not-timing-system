using Not.Application.CRUD.Ports;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Strings;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents.Officials;

namespace NTS.Judge.Blazor.Features.Setup.UpcomingEvents.Officials;

public class OfficialShellBehind : KrudShell<OfficialFormModel>
{
    [Inject]
    IRepository<User> Users { get; set; } = default!;

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        var users = await Users.ReadMany();
        return users.Where(x =>
            term == string.Empty || x.Name.NContains(term) || x.Email.NContains(term)
        );
    }
}
