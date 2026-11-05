using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Tests.Authentication.Store;

public sealed class AuthenticationSettingsValidatorTests
{
    [Fact]
    public void Validate_WhenProductionOriginIsHttp_ShouldFail()
    {
        var validator = new AuthenticationSettingsValidator(new FakeEnvironment("Production"));
        var settings = ProductionSettings(appOrigin: "http://app.no-company.eu");

        var result = validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("https", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenProductionOriginIsHttps_ShouldSucceed()
    {
        var validator = new AuthenticationSettingsValidator(new FakeEnvironment("Production"));
        var settings = ProductionSettings(appOrigin: "https://app.no-company.eu");

        var result = validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    static AuthenticationSettings ProductionSettings(string appOrigin)
    {
        return new AuthenticationSettings
        {
            RelyingPartyId = "no-company.eu",
            AppOrigin = appOrigin,
            CodeSecret = "production-secret",
            EmailProvider = "brevo",
            BrevoApiKey = "api-key",
        };
    }

    sealed class FakeEnvironment : IHostEnvironment
    {
        public FakeEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "NoTiming";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
