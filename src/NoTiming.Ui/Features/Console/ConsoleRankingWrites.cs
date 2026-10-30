using Not.Application.HTTP;
using NoTiming.Ui.Storage.Core.Repositories;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Rankings as the Console writes them (#646): the repository of the Api, whose refusal is told to the person by the
/// service that asked and not by the repository, so that it is said once. What the Api answered stays in <c>LastError</c>.
/// </summary>
public sealed class ConsoleRankingWrites : RankingApiRepository
{
    public ConsoleRankingWrites(JsonApiClient client)
        : base(client) { }

    protected override void OnError(JsonApiError error) { }
}
