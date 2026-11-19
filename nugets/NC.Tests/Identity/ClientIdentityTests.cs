using NC.Application.Identity;
using Xunit;

namespace NC.Tests.Identity;

public sealed class ClientIdentityTests
{
    [Fact]
    public void Constructor_WhenCreated_ShouldHaveAnId()
    {
        var identity = new ClientIdentity();

        Assert.False(string.IsNullOrWhiteSpace(identity.Id));
    }

    [Fact]
    public void Constructor_WhenCreatedTwice_ShouldGiveDifferentIds()
    {
        var first = new ClientIdentity();
        var second = new ClientIdentity();

        Assert.NotEqual(first.Id, second.Id);
    }
}
