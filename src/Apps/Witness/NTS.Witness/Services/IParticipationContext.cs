using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Services;

public interface IParticipationContext : IStatefulService
{
    IEnumerable<Participation> Active { get; }
}
