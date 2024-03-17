using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface IParticipantstHubProcedures
{
    (int eventId, IEnumerable<ParticipantEntry> participants) Get();
    Task Witness(IEnumerable<ParticipantEntry> entries, WitnessEventType type);
}

public interface IParticipantsClientProcedures
{
    Task Update(ParticipantEntry entry, CollectionAction action);
}
