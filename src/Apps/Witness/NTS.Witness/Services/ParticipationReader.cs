using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Application.Factories;
using NTS.Application.Socket;
using NTS.Application.Core;

namespace NTS.Witness.Services;

public class ParticipationReader : IReadMany<Participation>, ITransient
{
    readonly IParticipationContext _participationContext;
    readonly INtsSocketService _eventContext;

    public ParticipationReader(IParticipationContext participationContext, INtsSocketService selectedEventContext)
    {
        _participationContext = participationContext;
        _eventContext = selectedEventContext;
    }

    public Task<IEnumerable<Participation>> ReadMany()
    {
        if (_participationContext.Participations.Any())
        {
            return Task.FromResult(_participationContext.Participations.AsEnumerable());
        }
        var participations = new List<Participation>();
        var setupCompetitions = _eventContext.Event!.Competitions.Where(competition => competition.Phases.Count > 0);
        foreach (var setupCompetition in setupCompetitions)
        {
            var setupParticipations = setupCompetition.Participations;
            foreach (var setupParticipation in setupParticipations)
            {
                var participation = ParticipationAndRankingFactory.CreateParticipation(setupCompetition, setupParticipation);
                participations.Add(participation);
            }
        }
        return Task.FromResult(participations.AsEnumerable());
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
