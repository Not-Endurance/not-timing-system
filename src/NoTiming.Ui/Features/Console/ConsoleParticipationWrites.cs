using Not.Application.HTTP;
using NoTiming.Ui.Storage.Core.Repositories;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Participations as the dashboard writes them (#646): the repository of the Api. A refusal is thrown to the service that
/// asked, which says it in the words it chooses, so that a refusal is said once and the form that sent the change can keep
/// what was typed. What the Api answered is in the exception, with its code.
/// </summary>
public sealed class ConsoleParticipationWrites : ParticipationApiRepository
{
    public ConsoleParticipationWrites(JsonApiClient client)
        : base(client) { }
}
