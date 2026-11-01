using NTS.Contracts.Features.Participants;

namespace NoTiming.Ui.Features.Participants;

/// <summary>
/// What the search remembers of its searches (#639, ADR-0010). Each search is a request against the budget of the account, so
/// what has been answered is not asked again: the same words in any order and case are one search, a page that was read is
/// remembered by its number, and a text that only grows from a complete answer to a shorter one (a word that gets longer, a
/// word that comes with it) is answered from those rows, narrowed by the words, with no request. Every row of such a text is
/// among the rows of the shorter one, because a word that is part of a name has every shorter word that is part of it. An
/// answer is kept for <see cref="REMEMBERED_FOR"/>, because a row can be added in the same session, and
/// <see cref="CAPACITY"/> are all that are kept.
/// </summary>
public sealed class ParticipantSearchMemory
{
    public const int CAPACITY = 50; // the answers that are kept; the oldest is forgotten first
    public static readonly TimeSpan REMEMBERED_FOR = TimeSpan.FromMinutes(1);

    readonly TimeProvider _time;
    readonly List<Remembered> _kept = [];

    public ParticipantSearchMemory(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>
    /// The answer to the search, when it has been answered or can be told from a complete answer to a shorter text, and none when
    /// it has to be asked. An answer that is told from another is remembered as if it had been asked.
    /// </summary>
    /// <param name="words">The words of the text, as they are sent.</param>
    /// <param name="number">The page, from 1.</param>
    public ParticipantSearchAnswer? Recall(ParticipantKind kind, IReadOnlyList<string> words, int number)
    {
        ForgetOld();
        var key = KeyOf(words);
        var asked = _kept.FirstOrDefault(x => x.Kind == kind && x.Key == key && x.Number == number);
        if (asked != null)
        {
            return asked.Answer;
        }

        if (number != 1)
        {
            return null;
        }

        var shorter = _kept.FirstOrDefault(x =>
            x.Kind == kind && x.Number == 1 && !x.Answer.HasMore && Grows(words, x.Words)
        );
        if (shorter == null)
        {
            return null;
        }

        var narrowed = new ParticipantSearchAnswer(
            [.. shorter.Answer.Rows.Where(row => words.All(row.Has))],
            hasMore: false
        );
        Remember(kind, words, number, narrowed);
        return narrowed;
    }

    /// <summary>Keeps the answer to a search that was asked, in place of the one it had before.</summary>
    public void Remember(ParticipantKind kind, IReadOnlyList<string> words, int number, ParticipantSearchAnswer answer)
    {
        ForgetOld();
        var key = KeyOf(words);
        _kept.RemoveAll(x => x.Kind == kind && x.Key == key && x.Number == number);
        _kept.Add(new Remembered(kind, key, words, number, answer, _time.GetUtcNow()));
        if (_kept.Count > CAPACITY)
        {
            _kept.RemoveAt(0);
        }
    }

    void ForgetOld()
    {
        var now = _time.GetUtcNow();
        _kept.RemoveAll(x => now - x.At >= REMEMBERED_FOR);
    }

    /// <summary>The words of a search in one order and case, each once: the Api answers alike for every way of writing them.</summary>
    static string KeyOf(IReadOnlyList<string> words)
    {
        return string.Join(' ', words.Select(x => x.ToLowerInvariant()).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>Whether every word of the older text is part of a word of the newer one.</summary>
    static bool Grows(IReadOnlyList<string> newer, IReadOnlyList<string> older)
    {
        return older.All(old => newer.Any(word => word.Contains(old, StringComparison.OrdinalIgnoreCase)));
    }

    sealed class Remembered
    {
        public Remembered(
            ParticipantKind kind,
            string key,
            IReadOnlyList<string> words,
            int number,
            ParticipantSearchAnswer answer,
            DateTimeOffset at
        )
        {
            Kind = kind;
            Key = key;
            Words = words;
            Number = number;
            Answer = answer;
            At = at;
        }

        public ParticipantKind Kind { get; }
        public string Key { get; }
        public IReadOnlyList<string> Words { get; }
        public int Number { get; }
        public ParticipantSearchAnswer Answer { get; }
        public DateTimeOffset At { get; }
    }
}
