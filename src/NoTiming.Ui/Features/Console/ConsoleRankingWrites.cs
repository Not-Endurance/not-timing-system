using Not.Application.HTTP;
using NoTiming.Ui.Storage.Core.Repositories;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Rankings as the Console writes them (#646): the repository of the Api. A refusal is thrown to the service that asked,
/// which says it, so that it is said once. What the Api answered is in the exception, with its code.
/// </summary>
public sealed class ConsoleRankingWrites : RankingApiRepository
{
    public ConsoleRankingWrites(JsonApiClient client)
        : base(client) { }
}
