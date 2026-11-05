using NC.Application.HTTP;
using NC.Injection;
using NC.Serialization.JSON;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Application.Features.Account;

public sealed class UiAccountService : IUiAccountService, IScoped
{
    const string SIGN_IN_CODE = "sign-in/code";
    const string SIGN_IN_VERIFY = "sign-in/verify";
    const string REGISTER = "register";
    const string SIGN_OUT = "sign-out";

    readonly NHttpClient _client;

    public UiAccountService(NHttpClient client)
    {
        _client = client;
    }

    public async Task RequestSignInCodeAsync(string email)
    {
        await SendAsync(SIGN_IN_CODE, new EmailPayload { Email = email });
    }

    public async Task RequestRegistrationCodeAsync(RegistrationRequest request)
    {
        await SendAsync(REGISTER, request);
    }

    public async Task VerifyCodeAsync(string email, string code)
    {
        await SendAsync(SIGN_IN_VERIFY, new VerifyPayload { Email = email, Code = code });
    }

    public async Task SignOutAsync()
    {
        await SendAsync(SIGN_OUT, payload: null);
    }

    /// <summary>The account routes answer with their own JSON, or none, so the body is sent and read as it is.</summary>
    async Task SendAsync(string endpoint, object? payload)
    {
        await _client.SendRawJson(HttpMethod.Post, endpoint, payload?.ToJson());
    }

    sealed class EmailPayload
    {
        public string Email { get; set; } = string.Empty;
    }

    sealed class VerifyPayload
    {
        public string Email { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }
}
