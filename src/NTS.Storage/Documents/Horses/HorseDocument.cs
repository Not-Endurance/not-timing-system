using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.Documents.Horses;

public class HorseDocument : Document
{
    public HorseDocument(IHorse horse)
        : base(horse.Id)
    {
        FeiId = horse.FeiId;
        Name = horse.Name;
    }

    public string? FeiId { get; init; }
    public string Name { get; init; }

    public Horse ToDomain()
    {
        return new Horse(Id, Name, FeiId);
    }
}
