using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class UpcomingEventRestApiRepository : RestApiRepository<UpcomingEvent>
{
    public UpcomingEventRestApiRepository(NHttpClient httpClient)
        : base("upcoming-event", httpClient) { }
}
