using Core.Domain.Common.Exceptions;
using Core.Domain.Common.Models;
using Core.Domain.State;
using Core.Domain.State.Athletes;
using Core.Domain.State.Horses;
using Core.Domain.State.Participants;
using Core.Domain.State.Participations;
using Core.Domain.AggregateRoots.Configuration.Extensions;
using Core.Domain.AggregateRoots.Manager.Aggregates;
using Core.Domain.Common.Extensions;
using System.Linq;
using static Core.Localization.Strings;

namespace Core.Domain.AggregateRoots.Configuration.Aggregates;

public class ParticipantsAggregate : IAggregate
{
    private readonly IState state;

    internal ParticipantsAggregate(IState state)
    {
        this.state = state;
    }

    public Participant Save(IParticipantState participantState, int athleteId, int horseId)
    {
        this.state.ValidateThatEventHasNotStarted();

        var athlete = this.state.Athletes.FindDomain(athleteId);
        var horse = this.state.Horses.FindDomain(horseId);
        if (this.IsPartOfAnotherParticipant(athlete, participantState.Id))
        {
            throw Helper.Create<ParticipantException>(ALREADY_PARTICIPATING_MESSAGE, athlete.Name);
        }
        if (this.IsPartOfAnotherParticipant(horse, participantState.Id))
        {
            throw Helper.Create<ParticipantException>(ALREADY_PARTICIPATING_MESSAGE, horse.Name);
        }

        var participant = this.state.Participants.FindDomain(participantState.Id);
        if (participant == null)
        {
            participant = new Participant(athlete, horse, participantState);
            this.state.Participants.AddOrUpdate(participant);
        }
        else
        {
            participant.Athlete = athlete;
            participant.Horse = horse;
            participant.MaxAverageSpeedInKmPh = participantState.MaxAverageSpeedInKmPh;
            participant.Number = participantState.Number;
            participant.Unranked = participantState.Unranked;
        }

        return participant;
    }

    public void Remove(int id)
    {
        this.state.ValidateThatEventHasNotStarted();
        var participant = this.state.Participants.FindDomain(id);
        var participation = this.state.Participations.FirstOrDefault(p => p.Participant.Id == id);
        this.state.Participations.Remove(participation);
        this.state.Participants.Remove(participant);
    }
    public void AddParticipation(int competitionId, int participantId)
    {
        this.state.ValidateThatEventHasNotStarted();

        var competition = this.state.Event.Competitions.FindDomain(competitionId);
        var participation = this.state.Participations.FirstOrDefault(x => x.Participant.Id == participantId);
        if (participation == null)
        {
            var participant = this.state.Participants.FindDomain(participantId);
            participation = new Participation(participant, competition);
            this.state.Participations.Add(participation);
            return;
        }
        participation.Aggregate().Add(competition);
    }
    private bool IsPartOfAnotherParticipant(Athlete athlete, int participantId)
        => this.state.Participants.Any(x => x.Athlete.Equals(athlete) && x.Id != participantId);

    private bool IsPartOfAnotherParticipant(Horse horse, int participantId)
        => this.state.Participants.Any(x => x.Horse.Equals(horse) && x.Id != participantId);

    public void __REVERT_START_PARTICIPATIONS__()
    {
        foreach (var participation in this.state.Participations)
        {
            participation.__REMOVE_PERFORMANCES__();
        }
    }
}
