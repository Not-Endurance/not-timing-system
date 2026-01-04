namespace NTS.Warp;

public class WarpRequest
{
    public static WarpRequest Crate(string enduranceEventId)
    {
        return new() { EnduranceEventId = enduranceEventId };
    }

    public static WarpRequest<T> Create<T>(string enduranceEventId, T payload)
    {
        return new WarpRequest<T> { EnduranceEventId = enduranceEventId, Payload = payload };
    }

    public string EnduranceEventId { get; init; } = default!;
}

public class WarpRequest<T> : WarpRequest
{
    public T Payload { get; init; } = default!;
}
