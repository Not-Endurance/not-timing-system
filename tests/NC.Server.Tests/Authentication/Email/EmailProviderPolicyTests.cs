using NC.Server.Authentication.Email;
using Xunit;

namespace NC.Server.Tests.Authentication.Email;

public sealed class EmailProviderPolicyTests
{
    [Fact]
    public void Problem_WhenProductionUsesConsole_ShouldReturnAProblem()
    {
        Assert.NotNull(EmailProviderPolicy.Problem(EmailProviderKind.Console, production: true));
    }

    [Fact]
    public void Problem_WhenProductionUsesOutbox_ShouldReturnAProblem()
    {
        Assert.NotNull(EmailProviderPolicy.Problem(EmailProviderKind.Outbox, production: true));
    }

    [Fact]
    public void Problem_WhenProductionUsesBrevo_ShouldReturnNull()
    {
        Assert.Null(EmailProviderPolicy.Problem(EmailProviderKind.Brevo, production: true));
    }

    [Fact]
    public void Problem_WhenDevelopmentUsesConsole_ShouldReturnNull()
    {
        Assert.Null(EmailProviderPolicy.Problem(EmailProviderKind.Console, production: false));
    }
}
