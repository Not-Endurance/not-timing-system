using Not.Application.HTTP;
using NoTiming.Ui.Storage.Core.Repositories;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Participations as the dashboard writes them (#646): the repository of the Api, whose refusal is not told to the person
/// by the repository but by the service that asked, in the words it chooses, so that a refusal is said once and the form that
/// sent the change can keep what was typed. What the Api answered stays in <c>LastError</c>, with its code.
/// </summary>
public sealed class ConsoleParticipationWrites : ParticipationApiRepository
{
    public ConsoleParticipationWrites(JsonApiClient client)
        : base(client) { }

    protected override void OnError(JsonApiError error) { }
}
