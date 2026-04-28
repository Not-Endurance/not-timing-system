using Not.Injection;
using Not.Safe;

namespace NTS.Witness.Blazor.Features.Socket;

public class BlazorSocketService : IScoped
{
    readonly IEventConnectionCoordinator _connectionCoordinator;

    public BlazorSocketService(IEventConnectionCoordinator connectionCoordinator)
    {
        _connectionCoordinator = connectionCoordinator;
    }

    public async Task EnsureConnected()
    {
        try
        {
            await _connectionCoordinator.EnsureConnected();
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
    }
}
