using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Harness;

internal sealed class PhaseState : IPhaseState
{
    public PhaseState(
        Guid id,
        DateTimeOffset? startTime,
        DateTimeOffset? arriveTime,
        DateTimeOffset? presentTime,
        DateTimeOffset? representTime
    )
    {
        Id = id;
        StartTime = startTime;
        ArriveTime = arriveTime;
        PresentTime = presentTime;
        RepresentTime = representTime;
    }

    public Guid Id { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? ArriveTime { get; }
    public DateTimeOffset? PresentTime { get; }
    public DateTimeOffset? RepresentTime { get; }
}
