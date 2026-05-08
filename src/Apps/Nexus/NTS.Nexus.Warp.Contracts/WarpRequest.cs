namespace NTS.Nexus.Warp.Contracts;

public class WarpRequest
{
    public static WarpRequest Create(string eventId)
    {
        return new() { EventId = eventId };
    }

    public static WarpRequest<T> Create<T>(string eventId, T payload)
    {
        return new WarpRequest<T> { EventId = eventId, Payload = payload };
    }

    public string EventId { get; init; } = default!;
}

public class WarpRequest<T> : WarpRequest
{
    public T Payload { get; init; } = default!;
}
