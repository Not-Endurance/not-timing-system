using System.Text;
using NoTiming.Api.Application.Authentication.Codes;

namespace NoTiming.Api.Application.Tests.Authentication.Codes;

public sealed class CodeMacTests
{
    static readonly byte[] Secret = Encoding.UTF8.GetBytes("server-secret-for-tests");

    [Fact]
    public void Compute_WhenInputsAreTheSame_ShouldReturnTheSameValue()
    {
        var first = CodeMac.Compute(Secret, "ana@example.test", "123456");
        var second = CodeMac.Compute(Secret, "ana@example.test", "123456");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_WhenCodeDiffers_ShouldReturnAnotherValue()
    {
        var first = CodeMac.Compute(Secret, "ana@example.test", "123456");
        var second = CodeMac.Compute(Secret, "ana@example.test", "123457");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_WhenEmailDiffers_ShouldReturnAnotherValue()
    {
        var first = CodeMac.Compute(Secret, "ana@example.test", "123456");
        var second = CodeMac.Compute(Secret, "ivan@example.test", "123456");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Compute_WhenSecretDiffers_ShouldReturnAnotherValue()
    {
        var first = CodeMac.Compute(Secret, "ana@example.test", "123456");
        var second = CodeMac.Compute(Encoding.UTF8.GetBytes("another-secret"), "ana@example.test", "123456");

        Assert.NotEqual(first, second);
    }
}
