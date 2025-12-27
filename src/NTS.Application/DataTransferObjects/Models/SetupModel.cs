using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using static NTS.Application.DataTransferObjects.Models.CommonModel;

namespace NTS.Application.DataTransferObjects.Models;

public class SetupModel
{
    public class AthleteModel : Identity
    {
        public static AthleteModel Create(IAthlete athlete)
        {
            return new AthleteModel
            {
                Id = athlete.Id,
                FeiId = athlete.FeiId,
                Names = athlete.Names,
                Country = CountryModel.Create(athlete.Country),
                Club = athlete.Club == null ? null : ClubModel.Create(athlete.Club),
            };
        }

        public string[] Names { get; init; } = default!;
        public CountryModel? Country { get; init; } // TODO: should be required
        public ClubModel? Club { get; init; }
        public string? FeiId { get; init; }

        public Athlete ToSetupDomain()
        {
            return new Athlete(Id, Names, FeiId, Country?.ToDomain(), Club?.ToDomain());
        }
    }

    public class HorseModel : Identity
    {
        public static HorseModel Create(IHorse horse)
        {
            return new HorseModel
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

    public class ClubModel : Identity
    {
        public static ClubModel Create(IClub club)
        {
            return new ClubModel { Id = club.Id, Name = club.Name };
        }

        public string Name { get; init; } = default!;

        public Club ToDomain()
        {
            return new Club(Id, Name);
        }
    }
}
