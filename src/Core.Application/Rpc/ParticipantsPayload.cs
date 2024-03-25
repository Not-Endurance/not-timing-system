using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Domain.State.Participants;
using System.Collections.Generic;

namespace Core.Application.Rpc;

public class ParticipantsPayload
{
	public List<ParticipantEntry> Participants { get; set; } = new();
	public int EventId { get; set; }
}
