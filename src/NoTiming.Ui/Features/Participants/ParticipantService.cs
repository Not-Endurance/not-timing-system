using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Participants;

/// <summary>Finds an Athlete or a Horse by name (#639, ADR-0010).</summary>
public interface IParticipantSearch
{
    /// <summary>
    /// The Participants whose names have the text in them, in either script, at most ten. A text of fewer than
    /// <see cref="ParticipantService.MIN_CHARACTERS"/> characters finds none, without asking.
    /// </summary>
    Task<IReadOnlyList<ParticipantName>> Search(string? text, CancellationToken ct);
}

/// <summary>What an Athlete or a Horse took part in (#639, ADR-0010): their history, and each Participation of it.</summary>
public interface IParticipationHistory
{
    /// <summary>
    /// A page of the history of an Athlete or a Horse, newest first: the Participations of the Events that have ended, in any
    /// Tenant, with the other half of each and what it placed as. A page is numbered from 1 and holds at most <paramref name="size"/>.
    /// </summary>
    Task<ParticipationHistoryPage> HistoryOf(ParticipantKind kind, Guid id, int number, int size, CancellationToken ct);

    /// <summary>The Horses an Athlete has ridden, as their history shows them: once each, the latest ridden first.</summary>
    Task<IReadOnlyList<ParticipantName>> HorsesRiddenBy(Guid athleteId, CancellationToken ct);

    /// <summary>
    /// A Participation with its Phases, as the Results show it: none when it is not there, and a read that failed says so, which
    /// is not the same: the person has been told by then, and the Participation may still be there.
    /// </summary>
    Task<RowRead<Participation>> ParticipationOf(Guid id);
}

public class ParticipantService : IParticipantSearch, IParticipationHistory, IScoped
{
    const int MAX_PAGE_SIZE = 500; // the most the Api puts in a page
    public const int MIN_CHARACTERS = 3; // the fewest characters the Api searches for

    readonly JsonApiClient _api;
    readonly IRepository<Participation> _participations;

    public ParticipantService(JsonApiClient api, IRepository<Participation> participations)
    {
        _api = api;
        _participations = participations;
    }

    public async Task<IReadOnlyList<ParticipantName>> Search(string? text, CancellationToken ct)
    {
        var term = text?.Trim() ?? string.Empty;
        if (term.Length < MIN_CHARACTERS)
        {
            return []; // the Api would refuse it, and a search counts against the budget of the account
        }

        var filter = Uri.EscapeDataString($"contains(name,'{term.Replace("'", "''")}')");
        var response = await _api.Send(HttpMethod.Get, $"participants?filter={filter}", cancellationToken: ct);
        if (!response.IsSuccess)
        {
            throw response.ToException();
        }

        return [.. response.Document!.Value.GetProperty("data").EnumerateArray().Select(ParticipantJson.NameOf)];
    }

    public async Task<ParticipationHistoryPage> HistoryOf(
        ParticipantKind kind,
        Guid id,
        int number,
        int size,
        CancellationToken ct
    )
    {
        var member = kind == ParticipantKind.Athlete ? "athleteId" : "horseId";
        var query = string.Join(
            '&',
            $"filter={Uri.EscapeDataString($"{member} eq {id}")}",
            $"{Uri.EscapeDataString("page[size]")}={size}",
            $"{Uri.EscapeDataString("page[number]")}={number}"
        );
        var response = await _api.Send(HttpMethod.Get, $"participations/history?{query}", cancellationToken: ct);
        if (!response.IsSuccess)
        {
            throw response.ToException();
        }

        var document = response.Document!.Value;
        return new ParticipationHistoryPage(
            [.. document.GetProperty("data").EnumerateArray().Select(ParticipantJson.ParticipationOf)],
            ParticipantJson.HasNext(document)
        );
    }

    public async Task<IReadOnlyList<ParticipantName>> HorsesRiddenBy(Guid athleteId, CancellationToken ct)
    {
        var horses = new List<ParticipantName>();
        for (var number = 1; ; number++)
        {
            var page = await HistoryOf(ParticipantKind.Athlete, athleteId, number, MAX_PAGE_SIZE, ct);
            foreach (var other in page.Items.Select(x => x.Other))
            {
                if (horses.All(known => known.Id != other.Id))
                {
                    horses.Add(other);
                }
            }

            if (!page.HasMore)
            {
                return horses;
            }
        }
    }

    public async Task<RowRead<Participation>> ParticipationOf(Guid id)
    {
        return _participations is IRowReader<Participation> reader
            ? await reader.Find(id)
            : new RowRead<Participation>(await _participations.Read(id), failed: false);
    }
}
