using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteHttpRepository : HttpRepository<Athlete>
{
    public AthleteHttpRepository(NHttpClient client)
        : base("athletes", client) { }
}
