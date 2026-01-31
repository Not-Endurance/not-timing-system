using Not.Application.Services;
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

    public async Task<IEnumerable<Club>> Search(string term)
    {
        var items = await _clubBehind.ReadMany();
        return items.Where(x => term == string.Empty || x.Name.NContains(term));
    }
}
