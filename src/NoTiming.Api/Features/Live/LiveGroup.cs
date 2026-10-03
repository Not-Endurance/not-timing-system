namespace NoTiming.Api.Features.Live;

/// <summary>
/// The SignalR group of an Event is its Guid in one canonical form, so the spelling a client connects with (braces,
/// no hyphens, upper case) never decides who receives a change notification. Whatever sends to a group uses
/// <see cref="Name"/>.
/// </summary>
public static class LiveGroup
{
    public static string Name(Guid eventId)
    {
        return eventId.ToString("D");
    }

    public static bool TryParse(string? value, out Guid eventId)
    {
        return Guid.TryParse(value, out eventId) && eventId != Guid.Empty;
    }
}
