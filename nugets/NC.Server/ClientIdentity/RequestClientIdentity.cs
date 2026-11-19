using Microsoft.AspNetCore.Http;
using NC.Application.Identity;

namespace NC.Server.ClientIdentity;

public sealed class RequestClientIdentity : IRequestClientIdentity
{
    readonly IHttpContextAccessor _httpContextAccessor;

    public RequestClientIdentity(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ClientId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.Request.Headers[ClientIdentityHeaders.CLIENT_ID].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
