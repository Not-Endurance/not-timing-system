using NoTiming.Application.Contracts.Core.Models;

namespace NoTiming.Api.Application.Features.Participations;

public interface IParticipationChangeRepository
{
    Task UpdateAndAnnounceChange(ParticipationModel participation);
}
