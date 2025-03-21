using NTS.Relay.ACL.Entities.Competitions;
using NTS.Relay.ACL.Entities.LapRecords;
using NTS.Relay.ACL.Entities.Participants;
using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Participations;

public class EmsParticipation : EmsDomainBase<EmsParticipationException>
{
    List<int> competitionsIds = [];

    [Newtonsoft.Json.JsonConstructor]
    internal EmsParticipation() { }

    public EmsParticipation(EmsParticipant participant, EmsCompetition competition)
        : base(GENERATE_ID)
    {
        Participant = participant;
        CompetitionConstraint = competition;
        competitionsIds.Add(competition.Id);
    }

    public EmsParticipant Participant { get; private set; }
    public EmsCompetition CompetitionConstraint { get; internal set; }
    public WitnessEventType UpdateType { get; internal set; }
    public bool IsNotComplete =>
        !Participant.LapRecords.Any(x => x.Result?.IsNotQualified ?? false)
        && (
            Participant.LapRecords.Count != CompetitionConstraint?.Laps.Count
            || Participant.LapRecords.Last().Result == null
        );
    public double? Distance => CompetitionConstraint?.Laps.Select(x => x.LengthInKm).Sum();
    public IReadOnlyList<int> CompetitionsIds
    {
        get => competitionsIds.AsReadOnly();
        private set => competitionsIds = value.ToList();
    }

    internal void Add(EmsCompetition competition)
    {
        if (CompetitionConstraint == null)
        {
            CompetitionConstraint = competition;
        }
        competitionsIds.Add(competition.Id);
    }

    internal void Remove(int competitionId)
    {
        competitionsIds.Remove(competitionId);
        if (!CompetitionsIds.Any())
        {
            CompetitionConstraint = null;
        }
    }

    public void __REMOVE_PERFORMANCES__()
    {
        Participant.__REMOVE_RECORDS__();
    }
}
