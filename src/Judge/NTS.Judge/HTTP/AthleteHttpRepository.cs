using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.HTTP;

public class AthleteHttpRepository : HttpRepository<Athlete>
{
    public AthleteHttpRepository(NHttpClient client)
        : base("athletes", client) { }
}
