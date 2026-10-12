using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The History of the Snapshot page in a real browser (#618, ADR-0013): the latest entry of a start number and kind has a time
/// field and an Update button, an older one has none; a correction that the server takes shows the new time, which is still
/// there when the page is loaded again; one that it does not take says why and leaves the time; while it is asked a spinner
/// shows.
/// </summary>
public sealed class SnapshotHistoryBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public SnapshotHistoryBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Official_corrects_the_time_of_the_latest_entry_which_shows_the_new_time_with_a_spinner_while_it_is_asked_and_after_a_reload()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await SendSnapshotOfAsync(page, 1);
        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        var entry = await OpenHistoryEntryAsync(page, 1);
        var field = entry.GetByLabel("Timestamp");
        await Expect(field).ToHaveValueAsync(new Regex(@"^\d\d:\d\d:\d\d$"), new() { Timeout = PATIENCE });
        var sent = TimeSpan.ParseExact(await field.InputValueAsync(), @"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var corrected = sent.Add(TimeSpan.FromSeconds(-90));
        var correctedText = corrected.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var release = new TaskCompletionSource();
        await page.RouteAsync(
            "**/api/snapshots/*",
            async route =>
            {
                if (route.Request.Method == "PATCH")
                {
                    await release.Task; // the server is slow to answer
                }

                await route.ContinueAsync();
            }
        );

        await field.ClickAsync();
        await field.PressAsync("Control+A");
        await field.PressSequentiallyAsync(correctedText.Replace(":", string.Empty, StringComparison.Ordinal));
        await Expect(field).ToHaveValueAsync(correctedText);
        await entry.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(page.GetByTestId("history-updating")).ToBeVisibleAsync(); // it is being asked
        release.SetResult();
        await Expect(page.GetByText("The time was updated.")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("history-updating")).ToHaveCountAsync(0, Counted());
        await Expect(field).ToHaveValueAsync(correctedText);
        await Eventually(async () => (await scene.EventsOfAsync(1)).Count == 2);
        Assert.Equal(["Arrived", "ArriveUpdated"], (await scene.EventsOfAsync(1)).Select(x => x["Kind"].AsString));

        // the history that is kept has it: after a reload the entry shows the corrected time and can be corrected again
        await page.ReloadAsync();
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        var again = await OpenHistoryEntryAsync(page, 1);
        await Expect(again.GetByLabel("Timestamp")).ToHaveValueAsync(correctedText, Filled());
        await Expect(again.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_time_the_server_records_and_does_not_take_says_why_and_the_entry_keeps_its_time()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await SendSnapshotOfAsync(page, 1);
        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        await Task.Delay(1500);
        await scene.SendAsync(SnapshotType.Present, 1); // somewhere else the rider was presented, a moment after it arrived
        var entry = await OpenHistoryEntryAsync(page, 1);
        var field = entry.GetByLabel("Timestamp");
        await Expect(field).ToHaveValueAsync(new Regex(@"^\d\d:\d\d:\d\d$"), new() { Timeout = PATIENCE });
        var sentText = await field.InputValueAsync();
        var later = TimeSpan
            .ParseExact(sentText, @"hh\:mm\:ss", CultureInfo.InvariantCulture)
            .Add(TimeSpan.FromMinutes(10))
            .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture); // an arrival after the presentation

        await field.ClickAsync();
        await field.PressAsync("Control+A");
        await field.PressSequentiallyAsync(later.Replace(":", string.Empty, StringComparison.Ordinal));
        await entry.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(page.GetByText("The time was recorded but not taken: Rejected Invalid Time."))
            .ToBeVisibleAsync(Loaded());
        await Expect(field).ToHaveValueAsync(sentText); // the entry is as it was
        await Eventually(async () => (await scene.EventsOfAsync(1)).Any(x => x["Kind"].AsString == "ArriveUpdated"));
        Assert.Equal(
            ["Arrived", "Presented", "ArriveUpdated"],
            (await scene.EventsOfAsync(1)).Select(x => x["Kind"].AsString)
        ); // it is an event of its own, which was rejected
    }

    [Fact]
    public async Task Only_the_latest_entry_of_a_start_number_and_kind_has_a_time_field_and_an_older_one_is_read_only()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await SendSnapshotOfAsync(page, 1);
        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("history-group")).ToHaveCountAsync(1, Counted());
        await SendSnapshotOfAsync(page, 1); // the same rider, as the same kind of time, again
        await Expect(page.GetByTestId("history-group")).ToHaveCountAsync(2, Counted());

        var groups = page.GetByTestId("history-group");
        await groups.Nth(0).GetByRole(AriaRole.Button).First.ClickAsync(); // the latest group is the first of the list
        await Expect(groups.Nth(0).GetByLabel("Timestamp")).ToBeVisibleAsync(Loaded());
        await groups.Nth(1).GetByRole(AriaRole.Button).First.ClickAsync(); // one group is open at a time

        await Expect(groups.Nth(1).GetByTestId("history-entry-1")).ToBeVisibleAsync(Loaded()); // the older entry is still shown
        await Expect(groups.Nth(1).GetByLabel("Timestamp")).ToHaveCountAsync(0, Counted());
        await Expect(groups.Nth(1).GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }))
            .ToHaveCountAsync(0, Counted());
    }

    [Fact]
    public async Task A_correction_that_no_answer_came_to_keeps_what_was_typed_and_is_sent_again_by_the_button()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await SendSnapshotOfAsync(page, 1);
        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        var entry = await OpenHistoryEntryAsync(page, 1);
        var field = entry.GetByLabel("Timestamp");
        await Expect(field).ToHaveValueAsync(new Regex(@"^\d\d:\d\d:\d\d$"), new() { Timeout = PATIENCE });
        var corrected = TimeSpan
            .ParseExact(await field.InputValueAsync(), @"hh\:mm\:ss", CultureInfo.InvariantCulture)
            .Add(TimeSpan.FromSeconds(-45))
            .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var answered = false;
        await page.RouteAsync(
            "**/api/snapshots/*",
            async route =>
            {
                if (route.Request.Method == "PATCH" && !answered)
                {
                    answered = true;
                    await route.AbortAsync(); // the connection is lost, and no answer comes
                }
                else
                {
                    await route.ContinueAsync();
                }
            }
        );
        await field.ClickAsync();
        await field.PressAsync("Control+A");
        await field.PressSequentiallyAsync(corrected.Replace(":", string.Empty, StringComparison.Ordinal));

        await entry.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(page.GetByText("The server did not answer. Press Update to send the time again."))
            .ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("history-updating")).ToHaveCountAsync(0, Counted());
        await Expect(field).ToHaveValueAsync(corrected); // what was typed stays
        Assert.Single(await scene.EventsOfAsync(1)); // and nothing was recorded
        await entry.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
        await Expect(page.GetByText("The time was updated.")).ToBeVisibleAsync(Loaded());
        await Expect(field).ToHaveValueAsync(corrected);
        Assert.Equal(["Arrived", "ArriveUpdated"], (await scene.EventsOfAsync(1)).Select(x => x["Kind"].AsString));
    }

    [Fact]
    public async Task The_fields_of_the_entries_of_one_group_are_each_the_ones_of_their_entry()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await ChipOf(page, 1).ClickAsync();
        await ChipOf(page, 2).ClickAsync();
        var capture = page.GetByRole(AriaRole.Button, new() { Name = "Snapshot", Exact = true });
        await capture.First.ClickAsync();
        await capture.First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Arrive", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Snapshots sent as Arrive")).ToBeVisibleAsync();
        var group = page.GetByTestId("history-group").First;
        await group.GetByRole(AriaRole.Button).First.ClickAsync();
        var (first, second) = (group.GetByTestId("history-entry-1"), group.GetByTestId("history-entry-2"));
        await Expect(first.GetByLabel("Timestamp")).ToBeVisibleAsync(Loaded());
        await Expect(second.GetByLabel("Timestamp")).ToBeVisibleAsync(Loaded());

        foreach (
            var (field, digits) in new[]
            {
                (first.GetByLabel("Timestamp"), "010203"),
                (second.GetByLabel("Timestamp"), "040506"),
            }
        )
        {
            await field.ClickAsync();
            await field.PressAsync("Control+A");
            await field.PressSequentiallyAsync(digits);
        }

        await Expect(first.GetByLabel("Timestamp")).ToHaveValueAsync("01:02:03");
        await Expect(second.GetByLabel("Timestamp")).ToHaveValueAsync("04:05:06");
    }

    /// <summary>The entry of the start number in the latest group of the History, which is opened.</summary>
    static async Task<ILocator> OpenHistoryEntryAsync(IPage page, int number)
    {
        var group = page.GetByTestId("history-group").First;
        await group.GetByRole(AriaRole.Button).First.ClickAsync();
        var entry = group.GetByTestId($"history-entry-{number}");
        await Expect(entry).ToBeVisibleAsync(Loaded());
        return entry;
    }
}
