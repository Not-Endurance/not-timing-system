using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.Documents.Horses;

public class HorseDocument : Document
{
    public static HorseDocument Create(IHorse horse)
    {
        return new HorseDocument
        {
            Id = horse.Id,
            FeiId = horse.FeiId,
            Name = horse.Name,
        };
    }

    public string? FeiId { get; init; }
    public string Name { get; init; } = default!;

    public Horse ToSetupDomain()
    {
        return new Horse(Id, Name, FeiId);
    }
}
