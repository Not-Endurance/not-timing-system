using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NC.Blazor.Client.Authentication.Services;
using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class SignInContentBehind : NComponent
{
    [Inject]
    IUiAccountService Account { get; set; } = default!;

    [Inject]
    IUiPasskeyService Passkeys { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    SessionAuthenticationStateProvider Session { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected string Email { get; set; } = string.Empty;

    protected string Code { get; set; } = string.Empty;

    protected bool CodeSent { get; private set; }

    protected string? Message { get; private set; }

    protected async Task SendCodeSafe()
    {
        Message = null;
        try
        {
            await Account.RequestSignInCodeAsync(Email);
            CodeSent = true;
        }
        catch (Exception ex) when (AccountMessages.For(ex) is { } message)
        {
            Message = message;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task VerifySafe()
    {
        Message = null;
        try
        {
            await Account.VerifyCodeAsync(Email, Code);
            Session.Refresh();
            Navigator.NavigateTo("/");
        }
        catch (Exception ex) when (AccountMessages.For(ex) is { } message)
        {
            Message = message;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task SignInWithPasskeySafe()
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
