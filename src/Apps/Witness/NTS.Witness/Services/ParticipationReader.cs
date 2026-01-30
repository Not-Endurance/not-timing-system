using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Warp;
using NTS.Domain.Core.Aggregates;
using static NTS.Application.Factories.ParticipationAndRankingFactory;

namespace NTS.Witness.Services;

public class ParticipationReader : IReadMany<Participation>, ITransient
{
    readonly ParticipationService _participationService;
    readonly ISelectedEventContext _eventContext;

    public ParticipationReader(ParticipationService participationService, ISelectedEventContext selectedEventContext)
    {
        _participationService = participationService;
        _eventContext = selectedEventContext;
    }

    public Task<IEnumerable<Participation>> ReadMany()
    {
        if (_participationService.Active.Any())
        {
            return Task.FromResult(_participationService.Active);
        }
        var participations = new List<Participation>();
        var setupCompetitions = _eventContext.Event!.Competitions.Where(competition => competition.Phases.Count > 0);
        foreach (var setupCompetition in setupCompetitions)
        {
            var setupParticipations = setupCompetition.Participations;
            foreach (var setupParticipation in setupParticipations)
            {
                participations.Add(CreateParticipation(setupCompetition, setupParticipation));
            }
        }
        return Task.FromResult(participations.AsEnumerable());
    }

    public Task<IEnumerable<Participation>> ReadMany(Expression<Func<Participation, bool>> filter)
    {
        throw new NotImplementedException();
    }
}
