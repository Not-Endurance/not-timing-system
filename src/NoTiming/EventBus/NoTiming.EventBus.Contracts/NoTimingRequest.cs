namespace NoTiming.EventBus.Contracts;

public class NoTimingRequest
{
    public static NoTimingRequest Create(string eventId)
    {
        return new() { EventId = eventId };
    }

    public static NoTimingRequest<T> Create<T>(string eventId, T payload)
    {
        return new NoTimingRequest<T> { EventId = eventId, Payload = payload };
    }

    public string EventId { get; init; } = default!;
}

public class NoTimingRequest<T> : NoTimingRequest
{
    public T Payload { get; init; } = default!;
}
