namespace NoTiming.Ui;

public static class Routes
{
    const string EVENT_ID_SEGMENT = "{eventId:guid}";
    public const string HOME = "/";
    public const string SNAPSHOT_PAGE = "/snapshot";
    public const string STARTLIST_PAGE = "/startlist";
    public const string ARRIVELIST_PAGE = "/arrivelist";
    public const string PRESENTLIST_PAGE = "/presentlist";
    public const string PERFORMANCE_PAGE = "/performance";
    public const string EVENT_SNAPSHOT_PAGE = $"/events/{EVENT_ID_SEGMENT}/snapshot";
    public const string EVENT_STARTLIST_PAGE = $"/events/{EVENT_ID_SEGMENT}/startlist";
    public const string EVENT_ARRIVELIST_PAGE = $"/events/{EVENT_ID_SEGMENT}/arrivelist";
    public const string EVENT_PRESENTLIST_PAGE = $"/events/{EVENT_ID_SEGMENT}/presentlist";
    public const string EVENT_PERFORMANCE_PAGE = $"/events/{EVENT_ID_SEGMENT}/performance";
    public const string PROFILE_PAGE = "/profile";
    public const string PASSKEYS_PAGE = "/account/passkeys"; // a page of the host (ADR-0002), which the app leaves for
    public const string HISTORIC_EVENTS_PAGE = "/historic-events";
    public const string HISTORIC_EVENT_DETAILS_PAGE = $"{HISTORIC_EVENTS_PAGE}/{EVENT_ID_SEGMENT}";

    /// <summary>
    /// The address of a page of an Event, which the route names (#630, ADR-0007): the route of the page with the Event's id
    /// in it. The pages with no Event in their address are the same pages for the Live Event the app follows, which the
    /// bookmarks of the viewers of old point to.
    /// </summary>
    public static string Of(string eventPage, Guid eventId)
    {
        return eventPage.Replace(EVENT_ID_SEGMENT, eventId.ToString());
    }
}
