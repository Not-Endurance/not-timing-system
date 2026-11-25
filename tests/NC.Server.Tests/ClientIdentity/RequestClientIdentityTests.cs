using Microsoft.AspNetCore.Http;
using NC.Application.Identity;
using NC.Server.ClientIdentity;
using Xunit;

namespace NC.Server.Tests.ClientIdentity;

public sealed class RequestClientIdentityTests
{
    [Fact]
    public void ClientId_WhenRequestCarriesTheHeader_ShouldReturnItsValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ClientIdentityHeaders.CLIENT_ID] = "client-7";
        var identity = new RequestClientIdentity(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("client-7", identity.ClientId);
    }

    [Fact]
    public void ClientId_WhenRequestHasNoHeader_ShouldBeNull()
    {
        var identity = new RequestClientIdentity(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.Null(identity.ClientId);
    }

    [Fact]
    public void ClientId_WhenThereIsNoRequest_ShouldBeNull()
    {
        var identity = new RequestClientIdentity(new HttpContextAccessor());

        Assert.Null(identity.ClientId);
    }
}
