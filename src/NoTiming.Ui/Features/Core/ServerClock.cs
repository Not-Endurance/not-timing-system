using Not.Application.CRUD.Ports;
using Not.Injection;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;

namespace NoTiming.Ui.Features.Core;

/// <summary>
/// The time of the server as the Ui knows it (#646): the <c>Date</c> that the Api puts on every answer, to the second, which
/// is as close as a request for a Representation or an Inspection needs (the clocks are to agree within about a minute). The
/// difference to the clock of the device is measured at the middle of a request, so that the time it took does not count.
/// </summary>
public class ServerClock : IServerClock, IScoped
{
    readonly AccountApiRepository _account;
    readonly TimeProvider _time;
    TimeSpan _difference = TimeSpan.Zero;

    public ServerClock(AccountApiRepository account, TimeProvider time)
    {
        _account = account;
        _time = time;
    }

    public DateTimeOffset Now => _time.GetUtcNow() + _difference;

    public async Task Synchronise()
    {
        try
        {
            var asked = _time.GetUtcNow();
            var said = await _account.ServerTime(); // any answer carries the time, a refusal too
            var answered = _time.GetUtcNow();
            if (said is { } instant)
            {
                _difference = instant - (asked + (answered - asked) / 2);
            }
        }
        catch (RepositoryException)
        {
            // The server could not be reached: the clock of the device is what there is.
        }
    }
}
