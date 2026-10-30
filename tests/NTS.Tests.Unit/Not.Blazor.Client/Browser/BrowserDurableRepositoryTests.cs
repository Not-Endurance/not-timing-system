using System.Text.Json;
using Microsoft.JSInterop;
using Not.Application.CRUD.Ports;
using Not.Async;
using NTS.Tests.Unit.Harness;
using Not.Blazor.Client.Browser;

namespace NTS.Tests.Unit.Not.Blazor.Client.Browser;

/// <summary>
/// The durable repository over the local storage of the browser (ADR-0013, #645): a change is backed up before the remote is
/// asked, the backup goes once the remote has answered for good, and a backup the browser refuses does not hold the request back.
/// The retries wait five seconds at first, then longer, up to a minute, for as long as they may go on.
/// </summary>
public sealed class BrowserDurableRepositoryTests
{
    const string PREFIX = "nts.durable-test.";
    static readonly Guid ID = TestId.Of(1);
    static readonly Guid OTHER_ID = TestId.Of(2);
    static readonly JsonSerializerOptions OPTIONS = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task The_backup_is_written_before_the_remote_is_asked_and_holds_the_item_under_the_prefix()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);
        var callsWhenAsked = -1;

        await repository.StoreDurably(
            ID,
            new Note { Id = ID, Text = "Ana" },
            _ =>
            {
                callsWhenAsked = browser.Calls.Count;
                return Task.FromResult(false);
            }
        );

        Assert.Equal(1, callsWhenAsked);
        Assert.Equal("localStorage.setItem", browser.Calls[0].Identifier);
        Assert.Contains("Ana", browser.Items[KeyOf()]);
    }

    [Fact]
    public async Task A_final_answer_removes_the_backup_and_is_reported()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);

        var final = await repository.StoreDurably(ID, new Note { Id = ID, Text = "Ana" }, _ => Task.FromResult(true));

        Assert.True(final);
        Assert.False(browser.Items.ContainsKey(KeyOf()));
    }

    [Fact]
    public async Task An_answer_that_is_only_for_now_keeps_the_backup()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);

        var final = await repository.StoreDurably(ID, new Note { Id = ID, Text = "Ana" }, _ => Task.FromResult(false));

        Assert.False(final);
        Assert.Contains("Ana", browser.Items[KeyOf()]);
    }

    [Fact]
    public async Task A_remote_that_fails_keeps_the_backup_and_the_failure_reaches_the_caller()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);

        await Assert.ThrowsAsync<HttpRequestException>(
            () =>
                repository.StoreDurably(
                    ID,
                    new Note { Id = ID, Text = "Ana" },
                    _ => throw new HttpRequestException("The Api did not answer.")
                )
        );

        Assert.Contains("Ana", browser.Items[KeyOf()]);
    }

    [Fact]
    public async Task A_backup_that_cannot_be_written_does_not_hold_the_request_back_and_the_remote_is_asked()
    {
        var browser = new FakeLocalStorage { Fails = new JSException("The browser refuses its storage.") };
        var repository = Repository(browser);
        var asked = false;

        var final = await repository.StoreDurably(
            ID,
            new Note { Id = ID, Text = "Ana" },
            _ =>
            {
                asked = true;
                return Task.FromResult(false);
            }
        );

        Assert.True(asked);
        Assert.False(final);
    }

    [Fact]
    public async Task A_backup_that_cannot_be_removed_after_a_final_answer_does_not_fail_the_store()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);

        var final = await repository.StoreDurably(
            ID,
            new Note { Id = ID, Text = "Ana" },
            _ =>
            {
                browser.Fails = new JSException("The browser refuses its storage.");
                return Task.FromResult(true);
            }
        );

        Assert.True(final);
    }

    [Fact]
    public async Task A_removal_that_fails_in_any_way_after_a_final_answer_does_not_fail_the_store()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser);

        var final = await repository.StoreDurably(
            ID,
            new Note { Id = ID, Text = "Ana" },
            _ =>
            {
                browser.Fails = new TimeoutException("The browser did not answer.");
                return Task.FromResult(true);
            }
        );

        Assert.True(final);
    }

    [Fact]
    public async Task A_pending_read_returns_the_backups_under_the_prefix_that_match_and_no_others()
    {
        var browser = new FakeLocalStorage();
        browser.Put(KeyOf(ID), JsonSerializer.Serialize(new Note { Id = ID, Text = "ana" }, OPTIONS));
        browser.Put(KeyOf(OTHER_ID), JsonSerializer.Serialize(new Note { Id = OTHER_ID, Text = "boris" }, OPTIONS));
        browser.Put("nts.language", "bg");

        var pending = await Repository(browser).ReadPendingDurably(x => x.Text == "ana");

        Assert.Equal(["ana"], pending.Select(x => x.Text));
    }

    [Fact]
    public async Task A_pending_read_removes_a_backup_that_is_not_an_item_and_returns_the_others()
    {
        var browser = new FakeLocalStorage();
        browser.Put(KeyOf(ID), "not json");
        browser.Put(KeyOf(OTHER_ID), JsonSerializer.Serialize(new Note { Id = OTHER_ID, Text = "boris" }, OPTIONS));

        var pending = await Repository(browser).ReadPendingDurably(_ => true);

        Assert.Equal(["boris"], pending.Select(x => x.Text));
        Assert.False(browser.Items.ContainsKey(KeyOf(ID)));
    }

    [Fact]
    public async Task A_retry_waits_five_seconds_then_ten_and_tries_the_round_after_each_wait_until_it_reports_nothing_left()
    {
        var timer = new StepTimer();
        var repository = Repository(new FakeLocalStorage(), timer);
        var tries = 0;

        var run = repository.RetryDurably(
            () => true,
            _ =>
            {
                tries++;
                return Task.FromResult(tries == 2);
            },
            CancellationToken.None
        );
        await Until(() => timer.IsWaiting);
        timer.Elapse();
        await Until(() => tries == 1);
        await Until(() => timer.IsWaiting);
        timer.Elapse();

        await run;
        Assert.Equal(2, tries);
        Assert.Equal([5, 10], timer.Spans.Select(x => (int)x.TotalSeconds));
    }

    [Fact]
    public async Task A_retry_that_may_not_go_on_neither_waits_nor_tries()
    {
        var timer = new StepTimer();
        var repository = Repository(new FakeLocalStorage(), timer);
        var tries = 0;

        await repository.RetryDurably(
            () => false,
            _ =>
            {
                tries++;
                return Task.FromResult(true);
            },
            CancellationToken.None
        );

        Assert.Empty(timer.Spans);
        Assert.Equal(0, tries);
    }

    [Fact]
    public async Task A_stop_during_a_wait_ends_the_retries_without_another_try()
    {
        var timer = new StepTimer();
        var repository = Repository(new FakeLocalStorage(), timer);
        var tries = 0;
        using var stop = new CancellationTokenSource();

        var run = repository.RetryDurably(
            () => true,
            _ =>
            {
                tries++;
                return Task.FromResult(false);
            },
            stop.Token
        );
        await Until(() => timer.IsWaiting);
        stop.Cancel();

        await run;
        Assert.Equal(0, tries);
    }

    static BrowserDurableRepository<Note> Repository(FakeLocalStorage browser, IDelay? delay = null)
    {
        return new BrowserDurableRepository<Note>(
            new BrowserLocalStorage(browser),
            delay ?? new StepTimer(),
            PREFIX,
            OPTIONS
        );
    }

    static string KeyOf()
    {
        return KeyOf(ID);
    }

    static string KeyOf(Guid id)
    {
        return PREFIX + id.ToString("D");
    }

    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The retry did not reach the expected step.");
            }

            await Task.Delay(1);
        }
    }

}
