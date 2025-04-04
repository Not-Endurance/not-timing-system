using Not.Random;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
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

    public Combination ToDomain()
    {
        var athlete = new Athlete(Athlete.ToSetupDomain());
        var horse = new Horse(Horse.ToSetupDomain());
        var minSpeed = Speed.Create(MinAverageSpeed);
        var maxSpeed = Speed.Create(MaxAverageSpeed);
        return new Combination(RandomHelper.GenerateUniqueInteger(), Number, athlete, horse, Distance, minSpeed, maxSpeed);
    }
}
