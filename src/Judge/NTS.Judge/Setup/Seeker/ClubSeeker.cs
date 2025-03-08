using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.Ports;
using Not.Strings;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Setup.Seeker;

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
