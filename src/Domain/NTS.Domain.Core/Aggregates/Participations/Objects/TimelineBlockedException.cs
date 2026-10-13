using Not.Domain.Exceptions;

namespace NTS.Domain.Core.Aggregates.Participations.Objects;

/// <summary>
/// A change of the time events that the Participation refuses (ADR-0005, #619): it would leave a Phase without all its times
/// while a later Phase has times, so that the Start that the later Phase was given when the earlier one was out would no
/// longer be true. It names the events that were chosen to be disabled in the Phase that would be left incomplete.
/// </summary>
public sealed class TimelineBlockedException : DomainException
{
    public TimelineBlockedException(IReadOnlyList<Guid> blocking)
        : base(Disabling_these_would_leave_an_earlier_Phase_incomplete_string)
    {
        Blocking = blocking;
    }

    /// <summary>The events that were chosen to be disabled and that would leave an earlier Phase incomplete.</summary>
    public IReadOnlyList<Guid> Blocking { get; }
}
