using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.Ports;
using Not.Strings;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Setup.Searchers;

public class ClubSearcher : ISeeker<Club>
{
    readonly IListBehind<Club> _clubBehind;

    public ClubSearcher(IListBehind<Club> clubBehind)
    {
        _clubBehind = clubBehind;
    }

    public Task<IEnumerable<Club>> Search(string term)
    {
        var results = _clubBehind.Items.Where(x => term == string.Empty || x.Name.NContains(term));
        return Task.FromResult(results);
    }
}
