using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Console;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Console;

/// <summary>What the Console tells a person of a request the Api refused: the Api's words, as a domain error; an answer is kept as it is.</summary>
public sealed class ApiRefusalsTests
{
    [Fact]
    public async Task A_refusal_is_said_in_the_words_of_the_Api_as_a_domain_error()
    {
        var error = await Assert.ThrowsAsync<DomainException>(
            () =>
                ApiRefusals.Answered(
                    () => throw new RepositoryException(RepositoryErrorKind.Refused, "The Event has ended.")
                )
        );

        Assert.Equal("The Event has ended.", error.Message);
    }

    [Fact]
    public async Task An_answer_that_was_given_is_returned_as_it_is()
    {
        var answer = await ApiRefusals.Answered(() => Task.FromResult(42));

        Assert.Equal(42, answer);
    }

    [Fact]
    public async Task A_request_that_got_no_answer_is_said_as_a_domain_error_too()
    {
        var error = await Assert.ThrowsAsync<DomainException>(
            () =>
                ApiRefusals.Answered(() => throw new RepositoryException(RepositoryErrorKind.Unavailable, "No answer."))
        );

        Assert.Equal("No answer.", error.Message);
    }
}
