using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface IParticipantstHubProcedures
{
	ParticipantsPayload SendParticipants();
    Task ReceiveWitnessEvent(IEnumerable<ParticipantEntry> entries, WitnessEventType type);
}

public interface IParticipantsClientProcedures
{
    Task ReceiveEntryUpdate(ParticipantEntry entry, CollectionAction action);
}
