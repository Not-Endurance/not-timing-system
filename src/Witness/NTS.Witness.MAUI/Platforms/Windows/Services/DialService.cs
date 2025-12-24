using Not.Notify;

namespace NTS.Witness.Platforms.Services;

public class DialService : IDialService
{
    public void DialNumber(string phoneNumber)
    {
        NotifyHelper.Warn(
            "Dialing is not supported on desktop devices. It seems you are running in a test environment."
        );
    }
}
