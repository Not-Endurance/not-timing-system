using NTS.Domain.Core.Aggregates.Participations;
using NTS.Storage.Documents.Athletes;
using NTS.Storage.Documents.Horses;

namespace NTS.Storage.Documents.Archive.Models;

public class CombinationDocumentModel
{
    public static CombinationDocumentModel Create(Combination combination)
    {
        return new CombinationDocumentModel
        {
            Number = combination.Number,
            Distance = combination.Distance,
            MinAverageSpeed = combination.MinAverageSpeed,
            MaxAverageSpeed = combination.MaxAverageSpeed,
            Athlete = AthleteDocument.Create(combination.Athlete),
            Horse = HorseDocument.Create(combination.Horse),
        };
    }

    public int Number { get; init; }
    public string Distance { get; init; } = default!;
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }
    public AthleteDocument Athlete { get; init; } = default!;
    public HorseDocument Horse { get; init; } = default!;
}
