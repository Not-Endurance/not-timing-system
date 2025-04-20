using Not.Application.HTTP;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Home;

public class EnduranceEventHttpRepository : HttpRepository<UpcomingEvent>
{
    public EnduranceEventHttpRepository(NHttpClient httpClient) : base("upcoming-event", httpClient)
    {
    }
}
