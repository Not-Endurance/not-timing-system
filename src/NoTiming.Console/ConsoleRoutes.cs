namespace NoTiming.Console;

/// <summary>
/// The addresses of the Console (#646). Every one starts with the segment <c>console</c>, which is what names the assembly
/// that the router fetches before it looks for a page (<c>LazyRoutes</c>): a phone that follows an Event never has it. The
/// pages that edit a child of the Setup are handed their form by the list that opens them; one that is opened by its address
/// (a reload, a bookmark) has none, and takes the person back to the Setup of the Event, which its address names. The
/// registry of the Tenant (ADR-0010) is not the Setup of any Event.
/// </summary>
public static class ConsoleRoutes
{
    const string EVENT_ID_SEGMENT = "{eventId:guid}";
    const string SETUP = $"/console/events/{EVENT_ID_SEGMENT}/setup";
    public const string FIRST_SEGMENT = "console";
    public const string HOME = "/console";
    public const string EVENT_SETUP_PAGE = SETUP;
    public const string EVENT_DASHBOARD_PAGE = $"/console/events/{EVENT_ID_SEGMENT}/dashboard";
    public const string EVENT_RANKINGS_PAGE = $"/console/events/{EVENT_ID_SEGMENT}/rankings";
    public const string EVENT_HANDOUTS_PAGE = $"/console/events/{EVENT_ID_SEGMENT}/handouts";
    public const string COMPETITION_PAGE = $"{SETUP}/competition";
    public const string PARTICIPATION_PAGE = $"{SETUP}/competition/participation";
    public const string PHASE_PAGE = $"{SETUP}/competition/phase";
    public const string OFFICIAL_PAGE = $"{SETUP}/official";
    public const string LOOP_PAGE = $"{SETUP}/loop";
    public const string COMBINATION_PAGE = $"{SETUP}/combination";
    public const string ATHLETES_HORSES_PAGE = "/console/registry/athletes-horses";
    public const string CLUBS_PAGE = "/console/registry/clubs";
    public const string ATHLETE_PAGE = "/console/registry/athlete";
    public const string HORSE_PAGE = "/console/registry/horse";
    public const string CLUB_PAGE = "/console/registry/club";
}
