using Not.Application.Services;
using Not.Blazor.Ports;
using Not.Strings;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Services;

public class ClubSeeker : ISeeker<Club>
{
    readonly IListBehind<Club> _clubBehind;

    public ClubSeeker(IListBehind<Club> clubBehind)
    {
        _clubBehind = clubBehind;
    }

    public Task<IEnumerable<Club>> Search(string term)
    {
        var results = _clubBehind.Items.Where(x => term == string.Empty || x.Name.NContains(term));
        return Task.FromResult(results);
    }
}
