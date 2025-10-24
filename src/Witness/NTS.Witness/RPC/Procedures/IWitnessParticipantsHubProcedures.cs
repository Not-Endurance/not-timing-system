using System.Collections.Generic;
using System.Threading.Tasks;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessParticipantsHubProcedures
{
    Task<IEnumerable<Participation>> SendParticipants();
}
