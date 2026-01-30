using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Storage.Repositories;

public class UpcomingEventRepository : RestApiRepository<UpcomingEvent>
{
    public UpcomingEventRepository(NHttpClient httpClient)
        : base("upcoming-event", httpClient) { }
}
