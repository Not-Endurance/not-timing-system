using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Warp.Dummies;

// TODO: Remove ITransient, IScoped, ISingleton interfaces from shared stuff in order to avoid having to define dummies
// instead the shared projects should provide options to opt-in or opt-out of those services
public class ParticipationReaderDummy : IReadMany<Participation>
{
    public Task<IEnumerable<Participation>> ReadAll()
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Participation>> ReadAll(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
