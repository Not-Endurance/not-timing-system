using NoTiming.Ui;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The pages of an Event are addressed by the Event's id (#630, ADR-0007), and the address of old that names none stays,
/// for the Live Event the app follows: a bookmark of the viewers must keep working.
/// </summary>
public sealed class UiRoutesTests
{
    [Fact]
    public void The_address_of_a_page_of_an_Event_has_the_id_of_the_Event_in_it()
    {
        var id = TestId.Of(7);

        Assert.Equal(
            "/events/00000007-0000-0000-0000-000000000000/startlist",
            Routes.Of(Routes.EVENT_STARTLIST_PAGE, id)
        );
        Assert.Equal(
            "/historic-events/00000007-0000-0000-0000-000000000000",
            Routes.Of(Routes.HISTORIC_EVENT_DETAILS_PAGE, id)
        );
    }

    [Theory]
    [InlineData(Routes.SNAPSHOT_PAGE, Routes.EVENT_SNAPSHOT_PAGE)]
    [InlineData(Routes.STARTLIST_PAGE, Routes.EVENT_STARTLIST_PAGE)]
    [InlineData(Routes.ARRIVELIST_PAGE, Routes.EVENT_ARRIVELIST_PAGE)]
    [InlineData(Routes.PRESENTLIST_PAGE, Routes.EVENT_PRESENTLIST_PAGE)]
    [InlineData(Routes.PERFORMANCE_PAGE, Routes.EVENT_PERFORMANCE_PAGE)]
    public void A_page_of_the_Live_Event_the_app_follows_is_the_same_page_of_an_Event_under_its_id(
        string followed,
        string ofAnEvent
    )
    {
        var id = TestId.Of(7);

        Assert.Equal($"/events/{id}{followed}", Routes.Of(ofAnEvent, id));
    }
}
