using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NC.Blazor.Client.Authentication.Services;
using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class LoginContentBehind : NComponent
{
    [Inject]
    IUiPasskeyService Passkeys { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    SessionAuthenticationStateProvider Session { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected string? Message { get; private set; }

    protected void OpenEmailLogin()
    {
        Navigator.NavigateTo(Routes.LOGIN_WITH_EMAIL_PAGE);
    }

    protected void OpenRegister()
    {
        Navigator.NavigateTo(Routes.REGISTER_PAGE);
    }

    protected async Task LoginWithPasskeySafe()
    {
        Message = null;
        try
        {
            if (!await PasskeyCeremony.IsSupportedAsync(JavaScript))
            {
                Message = Browser_cannot_use_passkeys_sign_in_with_code_string;
                return;
            }

            var options = await Passkeys.SignInOptionsAsync();
            var credential = await PasskeyCeremony.GetAsync(JavaScript, options);
            await Passkeys.SignInAsync(credential);
            Session.Refresh();
            Navigator.NavigateTo("/");
        }
        catch (JSException)
        {
            Message = Passkey_not_used_string;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Message = Passkey_not_accepted_string;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
