using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.HTTP;

public class HorseHttpRepository : HttpRepository<Horse>
{
    public HorseHttpRepository(NHttpClient client)
        : base("horses", client) { }
}
