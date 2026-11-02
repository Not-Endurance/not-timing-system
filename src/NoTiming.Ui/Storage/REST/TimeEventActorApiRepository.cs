using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using static Not.Blazor.Client.REST.JsonAttributes;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// The people an Event's time keeping names, as the Api serves them at <c>/api/time-event-actors</c> (#618): their names, by
/// their ids. Every page of the list is read, as the Api cuts a long list into pages.
/// </summary>
public class TimeEventActorApiRepository
{
    const int PAGE_SIZE = 500;
    const int MAX_PAGES = 20;

    readonly JsonApiClient _client;

    public TimeEventActorApiRepository(JsonApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesOf(Guid eventId)
    {
        var actors = await JsonApiAnswers.Reached(() =>
            _client.GetAll(
                "time-event-actors",
                new ListQuery(filter: $"eventId eq {eventId}", pageSize: PAGE_SIZE),
                MAX_PAGES
            )
        );
        return actors
            .Select(x => (
                Id: Guid.TryParse(x.GetProperty("id").GetString(), out var id) ? id : (Guid?)null,
                Name: Text(x.GetProperty("attributes"), "displayName") ?? string.Empty
            ))
            .Where(x => x.Id != null)
            .ToDictionary(x => x.Id!.Value, x => x.Name);
    }
}
