using MudBlazor;
using Not.Application.CRUD.Ports;
using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Strings;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Setup.Athletes;

namespace NTS.Judge.Blazor.Features.Setup.AthletesHorses.Athletes;

public class AthleteShellBehind : KrudShell<AthleteFormModel>
{
    [Inject]
    IRepository<Club> ClubService { get; set; } = default!;

    [Inject]
    IRepository<User> Users { get; set; } = default!;

    [Inject]
    ISeeker<Country> Countries { get; set; } = default!;

    protected async Task<IEnumerable<Country?>> SearchCountriesSafe(string term, CancellationToken ct)
    {
        return await Countries.Search(term, ct);
    }

    protected async Task<IEnumerable<Club?>> SearchClubsSafe(string term, CancellationToken _)
    {
        var items = await ClubService.ReadMany();
        return items.Where(x => term == string.Empty || x.Name.NContains(term));
    }

    protected async Task<IEnumerable<User?>> SearchUsersSafe(string term, CancellationToken _)
    {
        var users = await Users.ReadMany();
        return users.Where(x =>
            term == string.Empty || x.Name.NContains(term) || x.Email.NContains(term)
        );
    }
}
