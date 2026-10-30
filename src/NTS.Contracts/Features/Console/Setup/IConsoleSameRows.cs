namespace NTS.Contracts.Features.Console.Setup;

/// <summary>
/// The registry's guard against a second copy (#646, ADR-0010): the rows, of this Tenant and of the others, that an Athlete or a
/// Horse about to be registered or renamed is the same as, by its name in either script or by its FEI ID. It warns and names the
/// rows, and decides nothing: the person may still register another. Only the rows the Api tells of are found, which for another
/// Tenant's are those whose name has the typed name in it.
/// </summary>
public interface IConsoleSameRows
{
    Task<IReadOnlyList<SameRow>> OfAthlete(AthleteFormModel model);

    Task<IReadOnlyList<SameRow>> OfHorse(HorseFormModel model);
}
