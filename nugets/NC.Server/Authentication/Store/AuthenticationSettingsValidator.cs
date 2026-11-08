using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Store;

/// <summary>Refuses to start with a configuration that would make sign-in unsafe in this environment.</summary>
public sealed class AuthenticationSettingsValidator : IValidateOptions<AuthenticationSettings>
{
    readonly IHostEnvironment _environment;

    public AuthenticationSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, AuthenticationSettings options)
    {
        var production = _environment.IsProduction();
        var failures = new List<string>();

        if (production && !options.AppOrigin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Production must serve the app over https, because the session cookie and passkeys need it.");
        }

        var relyingPartyProblem = RelyingPartyPolicy.Problem(options.RelyingPartyId, options.AppOrigin, production);
        if (relyingPartyProblem != null)
        {
            failures.Add(relyingPartyProblem);
        }

        if (!_environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.CodeSecret))
        {
            failures.Add("The code secret is required outside Development.");
        }

        if (!Enum.TryParse<EmailProviderKind>(options.EmailProvider, ignoreCase: true, out var provider))
        {
            failures.Add($"Unknown email provider '{options.EmailProvider}'.");
        }
        else
        {
            var providerProblem = EmailProviderPolicy.Problem(provider, production);
            if (providerProblem != null)
            {
                failures.Add(providerProblem);
            }

            if (provider == EmailProviderKind.Brevo && string.IsNullOrWhiteSpace(options.BrevoApiKey))
            {
                failures.Add("The Brevo API key is required when Brevo sends the email.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
