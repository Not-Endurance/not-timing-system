using Not.Collections;
using NTS.Domain.Core.Aggregates;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessClientProcedures
{
    Task ReceiveParticipation(Participation participation);
}
