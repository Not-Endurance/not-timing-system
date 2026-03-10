using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Storage.Repositories;

public class UpcomingEventRestRepository : RestApiRepository<UpcomingEvent, UpcomingEventModel>, ITransient
{
    public UpcomingEventRestRepository(NHttpClient httpClient)
        : base("upcoming-event", httpClient) { }
}
