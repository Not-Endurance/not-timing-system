using Not.Application.HTTP;
using Not.Injection;
using NTS.Contracts.Core;

namespace NoTiming.Ui.Features.Core;

/// <summary>
/// The time of the server as the Ui knows it (#646): the <c>Date</c> that the Api puts on every answer, to the second, which
/// is as close as a request for a Representation or an Inspection needs (the clocks are to agree within about a minute). The
/// difference to the clock of the device is measured at the middle of a request, so that the time it took does not count.
/// </summary>
public class ServerClock : IServerClock, IScoped
{
    readonly JsonApiClient _api;
    readonly TimeProvider _time;
    TimeSpan _difference = TimeSpan.Zero;

    public ServerClock(JsonApiClient api, TimeProvider time)
    {
        _api = api;
        _time = time;
    }

    public DateTimeOffset Now => _time.GetUtcNow() + _difference;

    public async Task Synchronise()
    {
        try
        {
            var asked = _time.GetUtcNow();
            var response = await _api.Send(HttpMethod.Get, "me"); // any answer carries the time, a refusal too
            var answered = _time.GetUtcNow();
            if (response.ServerTime is { } said)
            {
                _difference = said - (asked + (answered - asked) / 2);
            }
        }
        catch (HttpRequestException)
        {
            // The server could not be reached: the clock of the device is what there is.
        }
    }
}
