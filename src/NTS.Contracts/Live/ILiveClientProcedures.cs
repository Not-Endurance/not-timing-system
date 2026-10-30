namespace NTS.Contracts.Live;

/// <summary>
/// The only thing the server sends to the clients connected to an Event (ADR-0013): a Change notification. It names
/// the Participation and never describes it; receivers read it again through REST, where authorization applies.
/// </summary>
public interface ILiveClientProcedures
{
    Task ParticipationChanged(Guid eventId, Guid participationId);
}
