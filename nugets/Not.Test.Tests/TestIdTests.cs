namespace Not.Test.Tests;

public class TestIdTests
{
    [Fact]
    public void Of_gives_the_guid_that_reads_as_the_number()
    {
        Assert.Equal(Guid.Parse("00000007-0000-0000-0000-000000000000"), TestId.Of(7));
    }

    [Fact]
    public void Of_gives_the_same_guid_for_the_same_number()
    {
        Assert.Equal(TestId.Of(3), TestId.Of(3));
    }

    [Fact]
    public void Of_gives_a_different_guid_for_each_number()
    {
        Assert.NotEqual(TestId.Of(1), TestId.Of(2));
    }
}
