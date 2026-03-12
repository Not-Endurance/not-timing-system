using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.CRUD.Ports;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Features.Core.State;
using NTS.Witness.Features.Setup.UpcomingEvents;
using NTS.Witness.Features.Socket;

namespace NTS.Witness.Blazor.Features;

public class HomeContentBehind : NComponent
{
    [Inject]
    AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    IUpcomingEventService UpcomingEventService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IConnectionStatus ConnectionStatus { get; set; } = default!;

    [Inject]
    IReadMany<Participation> Participations { get; set; } = default!;

    [Inject]
    IParticipationService ParticipationService { get; set; } = default!;

    protected IEnumerable<UpcomingEvent> Events { get; set; } = [];
    protected string[] EventsTableHeaders { get; set; } = [Event_string, Place_string, Country_string, ""];
    protected string UserName { get; set; } = string.Empty;
    protected string UserRoles { get; set; } = string.Empty;
    protected bool IsConnected => ConnectionStatus.IsConnected();
    protected UpcomingEvent? SelectedEvent => SocketService.Event;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var roles = user.FindAll(ClaimTypes.Role);

            UserName = user.Identity?.Name ?? string.Empty;
            UserRoles = roles.Any() ? string.Join(", ", roles.Select(r => r.Value)) : No_roles_assigned_string;
            Events = await UpcomingEventService.GetEvents();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ConnectTo(UpcomingEvent upcomingEvent)
    {
        try
        {
            if (IsConnected)
            {
                await SocketService.Disconnect();
            }

            await SocketService.Connect(upcomingEvent);
            var activeParticipations = await Participations.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
            ParticipationService.Set(activeParticipations);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task Disconnect()
    {
        try
        {
            await SocketService.Disconnect();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
