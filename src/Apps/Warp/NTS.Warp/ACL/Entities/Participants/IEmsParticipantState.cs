using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Participants;

public interface IEmsParticipantState : IEmsIdentifiable
{
    public bool Unranked { get; }
    public string Number { get; }
    int? MaxAverageSpeedInKmPh { get; }
}
