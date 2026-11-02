using System.Net;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;

namespace NTS.Tests.Unit.Not.Application.HTTP;

/// <summary>What a refused request is told as: the kind of failure a caller branches on, the code, and the answer itself (ADR-0017).</summary>
public sealed class JsonApiExceptionTests
{
    static readonly HashSet<string> TRY_LATER = ["participation-busy"];

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, RepositoryErrorKind.NotAllowed)]
    [InlineData(HttpStatusCode.Forbidden, RepositoryErrorKind.NotAllowed)]
    [InlineData(HttpStatusCode.RequestTimeout, RepositoryErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, RepositoryErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, RepositoryErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, RepositoryErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, RepositoryErrorKind.Refused)]
    [InlineData(HttpStatusCode.NotFound, RepositoryErrorKind.Refused)]
    [InlineData(HttpStatusCode.Conflict, RepositoryErrorKind.Refused)]
    [InlineData(HttpStatusCode.UnprocessableEntity, RepositoryErrorKind.Refused)]
    public void A_status_says_what_kind_of_failure_it_is(HttpStatusCode status, RepositoryErrorKind kind)
    {
        var exception = new JsonApiException(Answer(status, "some-code"), TRY_LATER);

        Assert.Equal(kind, exception.Kind);
    }

    [Fact]
    public void A_conflict_the_Api_says_to_try_later_is_unavailable_and_any_other_conflict_is_refused()
    {
        var busy = new JsonApiException(Answer(HttpStatusCode.Conflict, "participation-busy"), TRY_LATER);
        var taken = new JsonApiException(Answer(HttpStatusCode.Conflict, "id-taken"), TRY_LATER);

        Assert.Equal(RepositoryErrorKind.Unavailable, busy.Kind);
        Assert.Equal(RepositoryErrorKind.Refused, taken.Kind);
    }

    [Fact]
    public void The_exception_carries_the_code_the_Api_gave_and_the_answer_itself()
    {
        var answer = Answer(HttpStatusCode.Conflict, "id-taken");

        var exception = new JsonApiException(answer, TRY_LATER);

        Assert.Equal("id-taken", exception.Code);
        Assert.Same(answer, exception.Response);
    }

    [Fact]
    public void The_message_is_the_words_the_Api_said_to_tell_a_person()
    {
        var exception = new JsonApiException(Answer(HttpStatusCode.Conflict, "id-taken"), TRY_LATER);

        Assert.Equal("The row is taken.", exception.Message);
    }

    static JsonApiResponse Answer(HttpStatusCode status, string code)
    {
        var error = new JsonApiError((int)status, code, "The row is taken.", null);
        return new JsonApiResponse(status, null, null, error);
    }
}
