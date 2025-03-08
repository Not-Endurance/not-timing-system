using Not.Application.HTTP;
using NTS.Domain.Aggregates;

namespace NTS.Judge.HTTP;

public class ClubHttpRepository : HttpRepository<Club>
{
    public ClubHttpRepository(NHttpClient client) : base("clubs", client)
    {
    }
}
