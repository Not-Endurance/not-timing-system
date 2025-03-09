using System.Data.Common;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Storage.Documents.Athletes;
using NTS.Storage.Documents.Horses;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class CombinationModel
{
    public CombinationModel(Combination domainModel)
    {
        Number = domainModel.Number;
        Distance = domainModel.Distance;
        MinAverageSpeed = domainModel.MinAverageSpeed;
        MaxAverageSpeed = domainModel.MaxAverageSpeed;
        Athlete = new AthleteDocument(domainModel.Athlete);
        Horse = new HorseDocument(domainModel.Horse);
    }

    public int Number { get; init; }
    public string Distance { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }
    public AthleteDocument Athlete { get; init; }
    public HorseDocument Horse { get; init; }
}
