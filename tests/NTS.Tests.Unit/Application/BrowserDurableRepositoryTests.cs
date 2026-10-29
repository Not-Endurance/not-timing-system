using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.JSInterop;
using Not.Application.CRUD.Ports;
using Not.Blazor.Client.Browser;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The durable repository over the local storage of the browser (ADR-0013, #645): a change is backed up before the remote is
/// asked, the backup goes once the remote has answered for good, and a read returns the backup while there is one. A browser that
/// refuses its storage throws, and nothing is sent without its backup.
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
        var repository = Repository(browser, new RemoteNotes());
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
        var repository = Repository(browser, new RemoteNotes());

        var final = await repository.StoreDurably(ID, new Note { Id = ID, Text = "Ana" }, _ => Task.FromResult(true));

        Assert.True(final);
        Assert.False(browser.Items.ContainsKey(KeyOf()));
    }

    [Fact]
    public async Task An_answer_that_is_only_for_now_keeps_the_backup()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser, new RemoteNotes());

        var final = await repository.StoreDurably(ID, new Note { Id = ID, Text = "Ana" }, _ => Task.FromResult(false));

        Assert.False(final);
        Assert.Contains("Ana", browser.Items[KeyOf()]);
    }

    [Fact]
    public async Task A_remote_that_fails_keeps_the_backup_and_the_failure_reaches_the_caller()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser, new RemoteNotes());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            repository.StoreDurably(ID, new Note { Id = ID, Text = "Ana" }, _ => throw new HttpRequestException("The Api did not answer."))
        );

        Assert.Contains("Ana", browser.Items[KeyOf()]);
    }

    [Fact]
    public async Task A_backup_that_cannot_be_written_throws_and_the_remote_is_not_asked()
    {
        var browser = new FakeLocalStorage { Fails = new JSException("The browser refuses its storage.") };
        var repository = Repository(browser, new RemoteNotes());
        var asked = false;

        await Assert.ThrowsAsync<JSException>(() =>
            repository.StoreDurably(
                ID,
                new Note { Id = ID, Text = "Ana" },
                _ =>
                {
                    asked = true;
                    return Task.FromResult(true);
                }
            )
        );

        Assert.False(asked);
    }

    [Fact]
    public async Task A_read_returns_the_backup_while_there_is_one_and_does_not_ask_the_remote()
    {
        var browser = new FakeLocalStorage();
        var remote = new RemoteNotes();
        remote.Put(new Note { Id = ID, Text = "Remote" });
        browser.Put(KeyOf(), JsonSerializer.Serialize(new Note { Id = ID, Text = "Kept" }, OPTIONS));

        var read = await Repository(browser, remote).ReadDurably(ID);

        Assert.Equal("Kept", read!.Text);
        Assert.Equal(0, remote.Reads);
    }

    [Fact]
    public async Task Without_a_backup_a_read_returns_what_the_remote_has()
    {
        var remote = new RemoteNotes();
        remote.Put(new Note { Id = ID, Text = "Remote" });

        var read = await Repository(new FakeLocalStorage(), remote).ReadDurably(ID);

        Assert.Equal("Remote", read!.Text);
    }

    [Fact]
    public async Task A_browser_that_refuses_to_read_makes_the_read_throw()
    {
        var browser = new FakeLocalStorage { Fails = new JSException("The browser refuses its storage.") };

        await Assert.ThrowsAsync<JSException>(() => Repository(browser, new RemoteNotes()).ReadDurably(ID));
    }

    [Fact]
    public async Task A_backup_that_is_not_an_item_is_removed_and_the_remote_is_read()
    {
        var browser = new FakeLocalStorage();
        var remote = new RemoteNotes();
        remote.Put(new Note { Id = ID, Text = "Remote" });
        browser.Put(KeyOf(), "not json");

        var read = await Repository(browser, remote).ReadDurably(ID);

        Assert.Equal("Remote", read!.Text);
        Assert.False(browser.Items.ContainsKey(KeyOf()));
    }

    [Fact]
    public async Task A_removal_deletes_at_the_remote_first_and_then_removes_the_backup()
    {
        var browser = new FakeLocalStorage();
        var remote = new RemoteNotes();
        var note = new Note { Id = ID, Text = "Ana" };
        remote.Put(note);
        browser.Put(KeyOf(), JsonSerializer.Serialize(note, OPTIONS));

        await Repository(browser, remote).RemoveDurably(ID, note);

        Assert.False(remote.Has(ID));
        Assert.False(browser.Items.ContainsKey(KeyOf()));
    }

    [Fact]
    public async Task A_removal_that_the_remote_refuses_keeps_the_backup()
    {
        var browser = new FakeLocalStorage();
        var remote = new RemoteNotes { DeleteFails = new HttpRequestException("The Api did not answer.") };
        var note = new Note { Id = ID, Text = "Ana" };
        browser.Put(KeyOf(), JsonSerializer.Serialize(note, OPTIONS));

        await Assert.ThrowsAsync<HttpRequestException>(() => Repository(browser, remote).RemoveDurably(ID, note));

        Assert.True(browser.Items.ContainsKey(KeyOf()));
    }

    [Fact]
    public async Task A_backup_that_cannot_be_removed_after_a_final_answer_does_not_fail_the_store()
    {
        var browser = new FakeLocalStorage();
        var repository = Repository(browser, new RemoteNotes());

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
    public async Task A_repository_without_a_remote_keeps_only_the_backup_and_throws_for_its_plain_operations()
    {
        var repository = Repository(new FakeLocalStorage(), remote: null);

        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Create(new Note { Id = ID }));
        Assert.Null(await repository.ReadDurably(ID));
    }

    [Fact]
    public async Task A_pending_read_returns_the_backups_under_the_prefix_that_match_and_no_others()
    {
        var browser = new FakeLocalStorage();
        browser.Put(KeyOf(ID), JsonSerializer.Serialize(new Note { Id = ID, Text = "ana" }, OPTIONS));
        browser.Put(KeyOf(OTHER_ID), JsonSerializer.Serialize(new Note { Id = OTHER_ID, Text = "boris" }, OPTIONS));
        browser.Put("nts.language", "bg");

        var pending = await Repository(browser, new RemoteNotes()).ReadPendingDurably(x => x.Text == "ana");

        Assert.Equal(["ana"], pending.Select(x => x.Text));
    }

    [Fact]
    public async Task A_pending_read_removes_a_backup_that_is_not_an_item_and_returns_the_others()
    {
        var browser = new FakeLocalStorage();
        browser.Put(KeyOf(ID), "not json");
        browser.Put(KeyOf(OTHER_ID), JsonSerializer.Serialize(new Note { Id = OTHER_ID, Text = "boris" }, OPTIONS));

        var pending = await Repository(browser, new RemoteNotes()).ReadPendingDurably(_ => true);

        Assert.Equal(["boris"], pending.Select(x => x.Text));
        Assert.False(browser.Items.ContainsKey(KeyOf(ID)));
    }

    static BrowserDurableRepository<Note> Repository(FakeLocalStorage browser, IRepository<Note>? remote)
    {
        return new BrowserDurableRepository<Note>(remote, new BrowserLocalStorage(browser), PREFIX, OPTIONS);
    }

    static string KeyOf()
    {
        return KeyOf(ID);
    }

    static string KeyOf(Guid id)
    {
        return PREFIX + id.ToString("D");
    }

    internal sealed class Note
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = "";
    }

    /// <summary>A remote repository in memory, which answers the operations the durable repository asks of it.</summary>
    sealed class RemoteNotes : IRepository<Note>
    {
        readonly Dictionary<Guid, Note> _notes = [];

        public int Reads { get; private set; }
        public Exception? DeleteFails { get; init; }

        public void Put(Note note)
        {
            _notes[note.Id] = note;
        }

        public bool Has(Guid id)
        {
            return _notes.ContainsKey(id);
        }

        public Task<Note?> Read(Guid id)
        {
            Reads++;
            return Task.FromResult(_notes.GetValueOrDefault(id));
        }

        public Task Delete(Note item)
        {
            if (DeleteFails != null)
            {
                throw DeleteFails;
            }

            _notes.Remove(item.Id);
            return Task.CompletedTask;
        }

        public Task Create(Note item)
        {
            throw new NotSupportedException("The test does not create.");
        }

        public Task<Note?> Read(Expression<Func<Note, bool>> filter)
        {
            throw new NotSupportedException("The test does not filter.");
        }

        public Task<IEnumerable<Note>> ReadMany()
        {
            throw new NotSupportedException("The test does not list.");
        }

        public Task<IEnumerable<Note>> ReadMany(Expression<Func<Note, bool>> filter)
        {
            throw new NotSupportedException("The test does not list.");
        }

        public Task Update(Note item)
        {
            throw new NotSupportedException("The test does not update.");
        }

        public Task DeleteMany(IEnumerable<Note> items)
        {
            throw new NotSupportedException("The test does not delete many.");
        }

        public Task DeleteMany(Expression<Func<Note, bool>> filter)
        {
            throw new NotSupportedException("The test does not delete many.");
        }
    }
}
