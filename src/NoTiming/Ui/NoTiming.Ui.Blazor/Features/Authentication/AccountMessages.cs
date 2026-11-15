using System.Net;
using System.Net.Http;

namespace NoTiming.Ui.Blazor.Features.Authentication;

/// <summary>The words the sign-in pages show for a refused request. The reason is never more specific than the server's.</summary>
public static class AccountMessages
{
    public static string? For(Exception exception)
    {
        if (exception is not HttpRequestException { StatusCode: { } status })
        {
            return null;
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => Code_not_right_or_expired_string,
            HttpStatusCode.TooManyRequests => Too_many_codes_requested_string,
            HttpStatusCode.Forbidden => Request_could_not_be_checked_string,
            _ => null,
        };
    }
}
