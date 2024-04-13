using Core.Models;

namespace Core.Domain.State.Participants;

public interface IParticipantState : IIdentifiable
{
    public bool Unranked { get; }
    public string Number { get; }
    int? MaxAverageSpeedInKmPh { get; }
}
