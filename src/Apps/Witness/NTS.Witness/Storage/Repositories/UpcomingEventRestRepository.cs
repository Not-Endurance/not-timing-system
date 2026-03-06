using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Storage.Repositories;

public class UpcomingEventRestRepository : RestApiRepository<UpcomingEvent>, ITransient
{
    public UpcomingEventRestRepository(NHttpClient httpClient)
        : base("upcoming-event", httpClient) { }
}
