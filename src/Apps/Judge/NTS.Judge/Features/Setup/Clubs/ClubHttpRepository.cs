using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Clubs;

public class ClubHttpRepository : HttpRepository<Club>
{
    public ClubHttpRepository(NHttpClient client)
        : base("clubs", client) { }
}
