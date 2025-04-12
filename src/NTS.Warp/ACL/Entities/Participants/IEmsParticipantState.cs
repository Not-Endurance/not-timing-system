using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Participants;

public interface IEmsParticipantState : IEmsIdentifiable
{
    public bool Unranked { get; }
    public string Number { get; }
    int? MaxAverageSpeedInKmPh { get; }
}
