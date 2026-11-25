using NC.Application.RPC.Changes;

namespace NoTiming.Application.Contracts.Shared;

/// <summary>
/// A document whose changes are announced to clients. A write that should be announced sets <see cref="Emitted"/> to false.
/// The event bus sends the change and sets it back to true, so the document itself does not need to know about the bus.
/// </summary>
public interface IOutbox<TEvent>
    where TEvent : ChangeNotice
{
    int Id { get; }

    int EventId { get; }

    bool Emitted { get; set; }

    string? WriterId { get; set; }

    TEvent ToEvent();
}
