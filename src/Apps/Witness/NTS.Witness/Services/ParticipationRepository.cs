using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Witness.RPC;

namespace NTS.Witness.Services;

public class ParticipationReader : IReadMany<Participation>, ITransient
{
    readonly ParticipationService _participationService;

    public ParticipationReader(ParticipationService participationService)
    {
        _participationService = participationService;
    }

    public Task<IEnumerable<Participation>> ReadMany()
    {
        return Task.FromResult(_participationService.Active);
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
