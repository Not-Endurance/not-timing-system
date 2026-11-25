using NC.Application.RPC.Changes;
using Xunit;

namespace NC.Tests.RPC.Changes;

public sealed class ChangeNoticeTests
{
    [Fact]
    public void IsFrom_WhenWriterIsTheSameClient_ShouldBeTrue()
    {
        var notice = new ChangeNotice { WriterId = "client-1" };

        Assert.True(notice.IsFrom("client-1"));
    }

    [Fact]
    public void IsFrom_WhenWriterIsAnotherClient_ShouldBeFalse()
    {
        var notice = new ChangeNotice { WriterId = "client-2" };

        Assert.False(notice.IsFrom("client-1"));
    }
}
