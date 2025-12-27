using Not.Exceptions;
using Not.Random;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Settings;
using static NTS.Application.DataTransferObjects.Models.CoreModel;
using static NTS.Application.DataTransferObjects.Models.SetupModel;

namespace NTS.Application.DataTransferObjects.Models;

public class CommonModel
{
    public class OfficialModel : Identity
    {
        public static OfficialModel Create(Official official)
        {
            return new OfficialModel
            {
                Id = official.Id,
                Names = official.Person.Names,
                Role = official.Role,
            };
        }

        public static OfficialModel Create(Domain.Setup.Aggregates.Official official)
        {
            return new OfficialModel
            {
                Id = official.Id,
                Names = official.Person.Names,
                Role = official.Role,
            };
        }

        public string[] Names { get; init; } = [];
        public OfficialRole Role { get; init; } = default!;

        public Official ToCoreDomain()
        {
            return new Official(Id, Names, Role);
        }

        public Domain.Setup.Aggregates.Official ToSetupDomain()
        {
            return new Domain.Setup.Aggregates.Official(Id, Names, Role);
        }
    }

    public class CountryModel : Identity
    {
        public static CountryModel Create(Country country)
        {
            return new CountryModel
            {
                Id = country.Id,
                Name = country.Name,
                IsoCode = country.IsoCode,
                NfCode = country.NfCode,
                Locale = country.Locale,
            };
        }

        public string Name { get; init; } = default!;
        public string? IsoCode { get; init; }
        public string? NfCode { get; init; }
        public string? Locale { get; init; }

        public Country ToDomain()
        {
            return new Country(Id, Name, IsoCode, NfCode, Locale);
        }
    }

    public class SettingModel : Identity
    {
        public static SettingModel Create(Setting setting)
        {
            return new SettingModel
            {
                Id = setting.Id,
                Country = CountryModel.Create(setting.Country),
                DetectionMode = setting.DetectionMode,
                AccountId = setting.AccountId.ToString(),
            };
        }

        public string AccountId { get; set; } = default!;
        public CountryModel Country { get; set; } = default!;
        public DetectionMode? DetectionMode { get; set; }

        public Setting ToDomain()
        {
            return new Setting(Id, Guid.Parse(AccountId), Country.ToDomain(), DetectionMode);
        }
    }

    public class CombinationModel
    {
        public static CombinationModel Create(Combination combination)
        {
            return new CombinationModel
            {
                Number = combination.Number,
                Distance = combination.Distance,
                MinAverageSpeed = combination.MinAverageSpeed,
                MaxAverageSpeed = combination.MaxAverageSpeed,
                Athlete = AthleteModel.Create(combination.Athlete),
                Horse = HorseModel.Create(combination.Horse),
            };
        }

        public static CombinationModel Create(Domain.Setup.Aggregates.Combination combination)
        {
            return new CombinationModel
            {
                Number = combination.Number,
                Athlete = AthleteModel.Create(combination.Athlete),
                Horse = HorseModel.Create(combination.Horse),
            };
        }

        public int Number { get; init; }
        public string? Distance { get; init; }
        public double? MinAverageSpeed { get; init; }
        public double? MaxAverageSpeed { get; init; }
        public AthleteModel Athlete { get; init; } = default!;
        public HorseModel Horse { get; init; } = default!;

        public Combination ToCoreDomain()
        {
            GuardHelper.ThrowIfDefault(Distance, "Distance cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(
                MinAverageSpeed,
                "MinAverageSpeed cannot be null when converting to Core Domain"
            );
            GuardHelper.ThrowIfDefault(
                MaxAverageSpeed,
                "MaxAverageSpeed cannot be null when converting to Core Domain"
            );

            var athlete = new Athlete(Athlete.ToSetupDomain());
            var horse = new Horse(Horse.ToSetupDomain());
            var minSpeed = Speed.Create(MinAverageSpeed);
            var maxSpeed = Speed.Create(MaxAverageSpeed);
            return new Combination(
                RandomHelper.GenerateUniqueInteger(),
                Number,
                athlete,
                horse,
                athlete.Club,
                Distance!,
                minSpeed,
                maxSpeed
            );
        }

        public Domain.Setup.Aggregates.Combination ToSetupDomain()
        {
            var athlete = Athlete.ToSetupDomain();
            var horse = Horse.ToSetupDomain();
            return new Domain.Setup.Aggregates.Combination(
                RandomHelper.GenerateUniqueInteger(),
                Number,
                athlete,
                horse,
                null // Tag prop is not used currently
            );
        }
    }

    public class ParticipationModel
    {
        public static ParticipationModel Create(Participation participation)
        {
            var total = participation.GetTotal();

            return new ParticipationModel
            {
                Category = participation.Category,
                Competition = CompetitionModel.Create(participation.Competition),
                Combination = CombinationModel.Create(participation.Combination),
                Phases = participation.Phases.Select(PhaseModel.Create).ToArray(),
                Total = total == null ? null : TotalModel.Create(total),
                Eliminated = participation.Eliminated == null ? null : EliminatedModel.Create(participation.Eliminated),
            };
        }

        public static ParticipationModel Create(Domain.Setup.Aggregates.Participation participation)
        {
            return new ParticipationModel
            {
                IsNotRanked = participation.IsNotRanked,
                Category = participation.Category,
                Combination = CombinationModel.Create(participation.Combination),
                StartTimeOverride = participation.StartTimeOverride,
                MaxSpeedOverride = participation.MaxSpeedOverride,
                MinSpeedOverride = participation.MinSpeedOverride,
                MinAverageSpeed = participation.MinAverageSpeed,
                MaxAverageSpeed = participation.MaxAverageSpeed,
            };
        }

        public ParticipationCategory Category { get; init; } = default!;
        public CompetitionModel? Competition { get; init; }
        public CombinationModel Combination { get; init; } = default!;
        public PhaseModel[] Phases { get; init; } = default!;
        public TotalModel? Total { get; init; }
        public EliminatedModel? Eliminated { get; init; }
        public bool? IsNotRanked { get; init; }
        public DateTimeOffset? StartTimeOverride { get; init; }
        public double? MaxSpeedOverride { get; init; }
        public double? MinSpeedOverride { get; init; }
        public double? MinAverageSpeed { get; init; }

        public double? MaxAverageSpeed { get; init; }

        public Participation ToCoreDomain()
        {
            GuardHelper.ThrowIfDefault(Competition, "Competition cannot be null when converting to Core Domain");
            if (Phases == null || Phases.Length == 0)
            {
                GuardHelper.Exception("Phases cannot be null or empty when converting to Core Domain");
            }
            var competition = Competition!.ToDomain();
            var combination = Combination.ToCoreDomain();
            var phases = Phases!.Select(x => x.ToDomain());
            var eliminated = Eliminated?.ToDomain();
            return new Participation(
                RandomHelper.GenerateUniqueInteger(),
                Category,
                competition,
                combination,
                new(phases),
                eliminated
            );
        }

        public Domain.Setup.Aggregates.Participation ToSetupDomain()
        {
            GuardHelper.ThrowIfDefault(IsNotRanked, "IsNotRanked cannot be null when converting to Setup Domain");
            var combination = Combination.ToSetupDomain();
            return new Domain.Setup.Aggregates.Participation(
                RandomHelper.GenerateUniqueInteger(),
                (bool)IsNotRanked,
                combination,
                Category,
                StartTimeOverride,
                MaxSpeedOverride,
                MinSpeedOverride,
                MinAverageSpeed,
                MaxAverageSpeed
            );
        }
    }
}
