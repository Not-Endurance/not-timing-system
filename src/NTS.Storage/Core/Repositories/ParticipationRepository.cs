using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class ParticipationRepository : EventScopedApiRepository<Participation, ParticipationModel>, ITransient
{
    public ParticipationRepository(NHttpClient client, IServiceProvider serviceProvider)
        : base("participations", client, serviceProvider) { }
}
