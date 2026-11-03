using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// What the services of the Console tell a person of a request the Api refused (#646, ADR-0017): a refusal is a
/// <see cref="RepositoryException"/> carrying the Api's words, and the form that asked shows those words as a domain error,
/// and keeps what was typed. A request that got no answer is one too, with the words of the repository (that the server
/// cannot be reached), so that the form says what happened rather than failing with a transport error.
/// </summary>
public static class ApiRefusals
{
    public static async Task<T> Answered<T>(Func<Task<T>> request)
    {
        try
        {
            return await request();
        }
        catch (RepositoryException refused)
        {
            throw new DomainException(refused.Message);
        }
    }

    public static async Task Answered(Func<Task> request)
    {
        await Answered(async () =>
        {
            await request();
            return true;
        });
    }
}
