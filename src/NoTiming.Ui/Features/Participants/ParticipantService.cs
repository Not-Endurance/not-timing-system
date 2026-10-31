using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using Not.Blazor.Client.REST;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;
using NoTiming.Ui.Storage.REST;
using NoTiming.Ui.Storage.Core.Repositories;

namespace NoTiming.Ui.Features.Participants;

/// <summary>Finds an Athlete or a Horse by name (#639, ADR-0010).</summary>
public interface IParticipantSearch
{
    /// <summary>
    /// A page of the Athletes or of the Horses whose names have every word of the text in them, in any order and in any case,
    /// in the order of their names and <see cref="ParticipantService.SEARCH_PAGE_SIZE"/> to a page; the first page is number 1.
    /// A name is searched in the script it is registered in, and in the other if the row has its name in English. A text with
    /// no word of <see cref="ParticipantService.MIN_CHARACTERS"/> characters finds none, without asking, and what was answered
    /// before is not asked again (<see cref="ParticipantSearchMemory"/>).
    /// </summary>
    Task<ParticipantSearchPage> Search(ParticipantKind kind, string? text, int number, CancellationToken ct);
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
    /// A Participation with its Phases, as the Results show it: none when it is not there. A read that did not come is a
    /// <c>RepositoryException</c>, and the Participation may still be there.
    /// </summary>
    Task<Participation?> ParticipationOf(Guid id);
}

public class ParticipantService : IParticipantSearch, IParticipationHistory, IScoped
{
    const int MAX_PAGE_SIZE = 500; // the most the Api puts in a page of a history
    const int MAX_WORDS = 6; // the most words the Api takes of a text
    public const int MIN_CHARACTERS = 2; // the fewest characters of the longest word the Api searches for
    public const int SEARCH_PAGE_SIZE = 10; // the size of a page of a search: the Api's own, asked for all the same
    public const int SEARCH_PAUSE_MILLISECONDS = 700; // how long a person has to stop typing before a search is made, each of which draws on the budget of the account

    /// <summary>The words of a text that the Api is asked for: the first six, because it takes no more.</summary>
    public static IReadOnlyList<string> WordsOf(string? text)
    {
        return [.. (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(MAX_WORDS)];
    }

    /// <summary>Whether a text is one that the Api searches for: one of its words has <see cref="MIN_CHARACTERS"/> characters at least.</summary>
    public static bool IsSearchable(string? text)
    {
        return WordsOf(text).Any(x => x.Length >= MIN_CHARACTERS);
    }

    readonly ParticipationApiRepository _participations;
    readonly AthleteApiRepository _athletes;
    readonly HorseApiRepository _horses;
    readonly ParticipantSearchMemory _searched;

    public ParticipantService(
        ParticipationApiRepository participations,
        AthleteApiRepository athletes,
        HorseApiRepository horses,
        TimeProvider time
    )
    {
        _participations = participations;
        _athletes = athletes;
        _horses = horses;
        _searched = new ParticipantSearchMemory(time);
    }

    public async Task<ParticipantSearchPage> Search(
        ParticipantKind kind,
        string? text,
        int number,
        CancellationToken ct
    )
    {
        var words = WordsOf(text);
        if (!words.Any(x => x.Length >= MIN_CHARACTERS))
        {
            return new ParticipantSearchPage([], hasMore: false); // the Api would refuse it with a 400, so it is not asked
        }

        var answer = _searched.Recall(kind, words, number) ?? await Ask(kind, words, number, ct);
        return new ParticipantSearchPage([.. answer.Rows.Select(x => x.Named())], answer.HasMore);
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
        var (rows, hasNext) = await _participations.HistoryOf(member, id, size, number, ct);
        return new ParticipationHistoryPage([.. rows.Select(ParticipantJson.ParticipationOf)], hasNext);
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

    public async Task<Participation?> ParticipationOf(Guid id)
    {
        return await _participations.Read(id);
    }

    async Task<ParticipantSearchAnswer> Ask(
        ParticipantKind kind,
        IReadOnlyList<string> words,
        int number,
        CancellationToken ct
    )
    {
        var query = new ListQuery(
            filter: $"contains(name,'{string.Join(' ', words).Replace("'", "''")}')",
            sort: "name",
            pageSize: SEARCH_PAGE_SIZE,
            pageNumber: number
        );
        var (rows, hasNext) =
            kind == ParticipantKind.Athlete
                ? await _athletes.SearchAllTenants(query, ct)
                : await _horses.SearchAllTenants(query, ct);
        var answer = new ParticipantSearchAnswer([.. rows.Select(x => ParticipantJson.FoundOf(kind, x))], hasNext);
        _searched.Remember(kind, words, number, answer);
        return answer;
    }
}
