using System.Net.Http;
using Not.Application.CRUD.Ports;
using Not.Blazor.Client.REST;

namespace NTS.Tests.Unit.Not.Blazor.Client.REST;

/// <summary>What a request that got no answer is to a repository: an unavailable failure, with the transport exception inside it (ADR-0017).</summary>
public sealed class JsonApiAnswersTests
{
    [Fact]
    public async Task A_request_that_got_no_answer_is_an_unavailable_failure_with_the_transport_exception_inside_it()
    {
        var transport = new HttpRequestException("The server did not answer.");

        var failure = await Assert.ThrowsAsync<RepositoryException>(
            () => JsonApiAnswers.Reached(() => Task.FromException<int>(transport))
        );

        Assert.Equal(RepositoryErrorKind.Unavailable, failure.Kind);
        Assert.Same(transport, failure.InnerException);
    }
}
