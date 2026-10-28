using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Participants;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Participants;

/// <summary>
/// What the search remembers of its searches (#639, ADR-0010): each search is a request against the budget of the account, so
/// what has been answered is not asked again. The same words in any order and case are one search, a page that was read is
/// remembered by its number, and a text that only grows from a complete answer to a shorter one (a word that gets longer, a
/// word that comes with it) is answered from those rows, narrowed by the words, with no request. A minute is as long as an
/// answer is kept, because a row can be added in the same session, and fifty are all that is kept.
/// </summary>
public sealed class ParticipantSearchMemoryTests
{
    static readonly DateTimeOffset NOW = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);

    readonly Clock _clock = new() { Now = NOW };
    readonly ParticipantSearchMemory _memory;

    public ParticipantSearchMemoryTests()
    {
        _memory = new ParticipantSearchMemory(_clock);
    }

    [Fact]
    public void The_same_words_in_any_order_and_case_are_one_search_and_each_page_and_each_kind_is_its_own()
    {
        var ivan = Athlete("Иван Иванов", "Ivan Ivanov");
        _memory.Remember(ParticipantKind.Athlete, ["Ivan", "ivanov"], 1, Answer(hasMore: true, ivan));

        var again = _memory.Recall(ParticipantKind.Athlete, ["IVANOV", "ivan"], 1);
        var written = _memory.Recall(ParticipantKind.Athlete, ["ivan", "IVAN", "ivanov"], 1); // a word that is written again is the same word
        var secondPage = _memory.Recall(ParticipantKind.Athlete, ["ivan", "ivanov"], 2);
        var horses = _memory.Recall(ParticipantKind.Horse, ["ivan", "ivanov"], 1);

        Assert.Equal([ivan], again!.Rows);
        Assert.True(again.HasMore);
        Assert.Equal([ivan], written!.Rows);
        Assert.Null(secondPage);
        Assert.Null(horses);
    }

    [Fact]
    public void A_text_that_only_grows_is_answered_from_a_complete_answer_to_the_shorter_one_in_the_order_of_the_Api()
    {
        var one = Athlete("Иван Иванов", "Ivan Ivanov", feiId: "10001234");
        var two = Athlete("Иван Петров", "Ivan Petrov");
        var three = Athlete("Мария Иванова", "Maria Ivanova");
        _memory.Remember(ParticipantKind.Athlete, ["iva"], 1, Answer(hasMore: false, one, two, three));

        var word = _memory.Recall(ParticipantKind.Athlete, ["ivan"], 1); // the word gets longer
        var longer = _memory.Recall(ParticipantKind.Athlete, ["ivanov"], 1);
        var longest = _memory.Recall(ParticipantKind.Athlete, ["ivanova"], 1);
        var another = _memory.Recall(ParticipantKind.Athlete, ["ivan", "petrov"], 1); // a word comes with it
        var byFeiId = _memory.Recall(ParticipantKind.Athlete, ["IVA", "1234"], 1); // the FEI ID is matched as the Api matches it
        var nobody = _memory.Recall(ParticipantKind.Athlete, ["ivan", "zzz"], 1);

        Assert.Equal([one, two, three], word!.Rows);
        Assert.Equal([one, three], longer!.Rows);
        Assert.Equal([three], longest!.Rows);
        Assert.Equal([two], another!.Rows);
        Assert.Equal([one], byFeiId!.Rows);
        Assert.Empty(nobody!.Rows);
        Assert.All([word, longer, longest, another, byFeiId, nobody], x => Assert.False(x!.HasMore));
    }

    [Theory]
    [InlineData("iva")] // shorter
    [InlineData("van")] // the word of the answer is not part of it
    [InlineData("petrov")] // another word
    [InlineData("иван")] // the same name in the other script is another search
    [InlineData("ivn")]
    public void A_text_that_does_not_grow_from_the_answer_is_not_answered_from_it(string word)
    {
        _memory.Remember(
            ParticipantKind.Athlete,
            ["ivan"],
            1,
            Answer(hasMore: false, Athlete("Иван Петров", "Ivan Petrov"))
        );

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, [word], 1));
    }

    [Fact]
    public void Every_word_of_the_older_text_has_to_be_part_of_a_word_of_the_newer_one()
    {
        _memory.Remember(
            ParticipantKind.Athlete,
            ["ivan", "petrov"],
            1,
            Answer(hasMore: false, Athlete("Иван Петров", "Ivan Petrov"))
        );

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["ivanov", "smith"], 1)); // petrov is not part of either
        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["ivan"], 1)); // petrov is part of none
        Assert.NotNull(_memory.Recall(ParticipantKind.Athlete, ["ivanov", "petrovich"], 1));
        Assert.NotNull(_memory.Recall(ParticipantKind.Athlete, ["ivanpetrov"], 1)); // the space is gone, and both words are part of the one
    }

    [Fact]
    public void An_answer_with_a_next_page_does_not_answer_another_text_because_it_does_not_hold_every_row()
    {
        _memory.Remember(
            ParticipantKind.Athlete,
            ["iva"],
            1,
            Answer(hasMore: true, Athlete("Иван Иванов", "Ivan Ivanov"))
        );

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["ivan"], 1));
    }

    [Fact]
    public void Only_the_first_page_of_a_text_is_answered_from_a_shorter_text_and_only_of_the_same_kind()
    {
        _memory.Remember(
            ParticipantKind.Athlete,
            ["iva"],
            1,
            Answer(hasMore: false, Athlete("Иван Иванов", "Ivan Ivanov"))
        );

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["ivan"], 2));
        Assert.Null(_memory.Recall(ParticipantKind.Horse, ["ivan"], 1));
    }

    [Fact]
    public void A_later_page_is_not_a_source_of_a_narrowed_answer_because_it_holds_only_some_of_the_rows()
    {
        _memory.Remember(
            ParticipantKind.Athlete,
            ["iva"],
            2,
            Answer(hasMore: false, Athlete("Иван Иванов", "Ivan Ivanov"))
        );

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["ivan"], 1));
    }

    [Fact]
    public void Asking_again_replaces_that_page_of_that_kind_and_no_other()
    {
        var first = Athlete("First");
        var second = Athlete("Second");
        var again = Athlete("Again");
        var horse = Horse("Horse");
        _memory.Remember(ParticipantKind.Athlete, ["wor"], 1, Answer(hasMore: true, first));
        _memory.Remember(ParticipantKind.Athlete, ["wor"], 2, Answer(hasMore: true, second));
        _memory.Remember(ParticipantKind.Horse, ["wor"], 1, Answer(hasMore: true, horse));

        _memory.Remember(ParticipantKind.Athlete, ["wor"], 1, Answer(hasMore: true, again));

        Assert.Equal([again], _memory.Recall(ParticipantKind.Athlete, ["wor"], 1)!.Rows);
        Assert.Equal([second], _memory.Recall(ParticipantKind.Athlete, ["wor"], 2)!.Rows);
        Assert.Equal([horse], _memory.Recall(ParticipantKind.Horse, ["wor"], 1)!.Rows);
    }

    [Fact]
    public void A_complete_answer_with_no_rows_answers_every_text_that_grows_from_it()
    {
        _memory.Remember(ParticipantKind.Horse, ["zzzz"], 1, Answer(hasMore: false));

        var typo = _memory.Recall(ParticipantKind.Horse, ["zzzzy", "x"], 1);

        Assert.Empty(typo!.Rows);
        Assert.False(typo.HasMore);
    }

    [Fact]
    public void A_narrowed_answer_is_remembered_and_narrows_further_when_the_answer_it_came_from_is_gone()
    {
        var one = Athlete("Иван Иванов", "Ivan Ivanov");
        var three = Athlete("Мария Иванка", "Maria Ivanka");
        _memory.Remember(ParticipantKind.Athlete, ["iva"], 1, Answer(hasMore: false, one, three));
        Assert.Equal([one, three], _memory.Recall(ParticipantKind.Athlete, ["ivan"], 1)!.Rows); // remembers the narrowed one
        for (var i = 0; i < 49; i++)
        {
            _memory.Remember(ParticipantKind.Horse, [$"w{i:00}x"], 1, Answer(hasMore: false)); // the answer to "iva" is the oldest of fifty-one
        }

        Assert.Null(_memory.Recall(ParticipantKind.Athlete, ["iva"], 1));
        Assert.Equal([one, three], _memory.Recall(ParticipantKind.Athlete, ["ivan"], 1)!.Rows);
        Assert.Equal([one], _memory.Recall(ParticipantKind.Athlete, ["ivanov"], 1)!.Rows);
    }

    [Fact]
    public void An_answer_is_kept_for_a_minute_and_a_narrowed_one_is_kept_from_the_time_it_was_made()
    {
        var one = Athlete("Иван Иванов", "Ivan Ivanov");
        _memory.Remember(ParticipantKind.Athlete, ["iva"], 1, Answer(hasMore: false, one));

        _clock.Now = NOW.AddSeconds(59);
        var kept = _memory.Recall(ParticipantKind.Athlete, ["iva"], 1);
        var narrowed = _memory.Recall(ParticipantKind.Athlete, ["ivan"], 1); // made now, so it is kept from now
        _clock.Now = NOW.AddSeconds(60);
        var forgotten = _memory.Recall(ParticipantKind.Athlete, ["iva"], 1); // a minute old is old
        var narrowedLater = _memory.Recall(ParticipantKind.Athlete, ["ivanov"], 1);

        Assert.Equal([one], kept!.Rows);
        Assert.Equal([one], narrowed!.Rows);
        Assert.Null(forgotten);
        Assert.Equal([one], narrowedLater!.Rows); // from the answer made at the time of the narrowing, which is a second old
    }

    [Fact]
    public void Fifty_answers_are_kept_and_the_oldest_is_forgotten_first()
    {
        for (var i = 0; i <= 50; i++)
        {
            _memory.Remember(ParticipantKind.Horse, [$"w{i:00}x"], 1, Answer(hasMore: true, Horse($"Horse {i}")));
        }

        Assert.Null(_memory.Recall(ParticipantKind.Horse, ["w00x"], 1)); // the first of fifty-one
        Assert.NotNull(_memory.Recall(ParticipantKind.Horse, ["w01x"], 1));
        Assert.NotNull(_memory.Recall(ParticipantKind.Horse, ["w50x"], 1));
    }

    [Fact]
    public void A_search_made_again_replaces_the_answer_and_counts_as_the_newest()
    {
        var old = Horse("Old");
        var fresh = Horse("Fresh");
        _memory.Remember(ParticipantKind.Horse, ["first"], 1, Answer(hasMore: true, old));
        for (var i = 1; i < 50; i++)
        {
            _memory.Remember(ParticipantKind.Horse, [$"w{i:00}x"], 1, Answer(hasMore: true));
        }

        _memory.Remember(ParticipantKind.Horse, ["first"], 1, Answer(hasMore: true, fresh)); // the first is the oldest of fifty, and is asked for again
        _memory.Remember(ParticipantKind.Horse, ["one", "more"], 1, Answer(hasMore: true));

        Assert.Equal([fresh], _memory.Recall(ParticipantKind.Horse, ["first"], 1)!.Rows);
        Assert.Null(_memory.Recall(ParticipantKind.Horse, ["w01x"], 1)); // so the oldest was another
    }

    [Theory]
    [InlineData("Иван", null, null, "иван", true)]
    [InlineData("Иван", null, null, "ИВ", true)] // in any case
    [InlineData("Иван Иванов", "Ivan Ivanov", null, "vanov", true)]
    [InlineData("Иван", "Ivan Ivanov", null, "ivanov", true)] // the name in English
    [InlineData("Иван", "Ivan", "10001234", "1234", true)] // the FEI ID
    [InlineData("Иван", "Ivan", "10001234", "1235", false)]
    [InlineData("Иван", null, null, "ivan", false)] // no name in English: the script it is registered in
    [InlineData("Иван", "Ivan", null, "петров", false)]
    public void A_word_is_part_of_a_name_the_name_in_English_or_the_FEI_ID_in_any_case(
        string name,
        string? english,
        string? feiId,
        string word,
        bool expected
    )
    {
        Assert.Equal(expected, Athlete(name, english, feiId).Has(word));
    }

    static ParticipantSearchAnswer Answer(bool hasMore, params FoundParticipant[] rows)
    {
        return new ParticipantSearchAnswer(rows, hasMore);
    }

    static FoundParticipant Athlete(string name, string? english = null, string? feiId = null)
    {
        return new FoundParticipant(ParticipantKind.Athlete, Guid.NewGuid(), name, english, feiId);
    }

    static FoundParticipant Horse(string name)
    {
        return new FoundParticipant(ParticipantKind.Horse, Guid.NewGuid(), name, null, null);
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
