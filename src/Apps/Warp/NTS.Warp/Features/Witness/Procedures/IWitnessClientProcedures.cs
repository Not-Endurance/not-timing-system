using Not.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessClientProcedures
{
    Task Receive(Participation participation);
}
