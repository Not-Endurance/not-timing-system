using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Core;
using NTS.Application.Factories;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;

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

        var @event = _eventContext.Event;
        if (@event == null)
        {
            return Task.FromResult(Enumerable.Empty<Participation>());
        }

        var participations = new List<Participation>();
        var setupCompetitions = @event.Competitions.Where(competition => competition.Phases.Count > 0);
        foreach (var setupCompetition in setupCompetitions)
        {
            var setupParticipations = setupCompetition.Participations;
            foreach (var setupParticipation in setupParticipations)
            {
                var participation = ParticipationAndRankingFactory.CreateParticipation(
                    setupCompetition,
                    setupParticipation
                );
                participations.Add(participation);
            }
        }
        return Task.FromResult(participations.AsEnumerable());
    }

    public async Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        var participations = await ReadMany();
        return participations.AsQueryable().Where(filter).ToList();
    }
}
