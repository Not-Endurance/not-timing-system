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
    public static int EnsureId(int id)
    {
        return id == default ? RandomHelper.GenerateUniqueInteger() : id;
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
            return new Setting(EnsureId(Id), Guid.Parse(AccountId), Country.ToDomain(), DetectionMode);
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
            return new Country(EnsureId(Id), Name, IsoCode, NfCode, Locale);
        }
    }

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
            return new Official(EnsureId(Id), Names, Role);
        }

        public Domain.Setup.Aggregates.Official ToSetupDomain()
        {
            return new Domain.Setup.Aggregates.Official(EnsureId(Id), Names, Role);
        }
    }

    public class CompetitionModel
    {
        public static CompetitionModel Create(Competition competition)
        {
            return new CompetitionModel
            {
                Name = competition.Name,
                Ruleset = competition.Ruleset,
                Type = competition.Type,
            };
        }

        public static CompetitionModel Create(Domain.Setup.Aggregates.Competition competition)
        {
            return new CompetitionModel
            {
                Id = competition.Id,
                Name = competition.Name,
                Type = competition.Type,
                Ruleset = competition.Ruleset,
                Start = competition.Start,
                CompulsoryThreshold = competition.CompulsoryThresholdSpan,
                FeiId = competition.FeiId,
                FeiRule = competition.FeiRule,
                FeiScheduleNumber = competition.FeiScheduleNumber,
                Phases = competition.Phases.Select(PhaseModel.Create).ToArray(),
                Participations = competition.Participations.Select(ParticipationModel.Create).ToArray(),
            };
        }

        public int Id { get; init; }
        public string Name { get; init; } = default!;
        public CompetitionType Type { get; init; }
        public CompetitionRuleset Ruleset { get; init; }
        public DateTimeOffset? Start { get; init; }
        public TimeSpan? CompulsoryThreshold { get; init; }
        public string? FeiId { get; init; }
        public string? FeiRule { get; init; }
        public string? FeiScheduleNumber { get; init; }
        public PhaseModel[] Phases { get; init; } = default!;
        public ParticipationModel[] Participations { get; init; } = default!;

        public Competition ToCoreDomain()
        {
            return new Competition(Name, Ruleset, Type);
        }

        public Domain.Setup.Aggregates.Competition ToSetupDomain()
        {
            return new Domain.Setup.Aggregates.Competition(
                EnsureId(Id),
                Name,
                Type,
                Ruleset,
                Start,
                CompulsoryThreshold,
                FeiId,
                FeiRule,
                FeiScheduleNumber,
                Phases.Select(x => x.ToSetupDomain()),
                Participations.Select(x => x.ToSetupDomain())
            );
        }
    } 

    public class PhaseModel
    {
        public static PhaseModel Create(Phase phase)
        {
            return new PhaseModel
            {
                Id = phase.Id,
                Gate = phase.Gate,
                Length = phase.Length,
                MaxRecovery = phase.MaxRecovery,
                Rest = phase.Rest,
                Ruleset = phase.Ruleset,
                IsFinal = phase.IsFinal,
                StartTime = phase.StartTime,
                ArriveTime = phase.ArriveTime,
                PresentTime = phase.PresentTime,
                RepresentTime = phase.RepresentTime,
                IsReinspectionRequested = phase.IsReinspectionRequested,
                IsRequiredInspectionRequested =
                    phase.IsRequiredInspectionRequested || phase.IsRequiredInspectionCompulsory, // TODO: probably remove compulsory altogether
                IsRequiredInspectionCompulsory = phase.IsRequiredInspectionCompulsory,
                CompulsoryThresholdInterval = phase.CompulsoryThresholdSpan,
                RequiredInspectionTime = phase.GetRequiredInspectionTime(),
                OutTime = phase.GetOutTime(),
                LoopInterval = phase.GetLoopInterval(),
                PhaseInterval = phase.GetPhaseInterval(),
                RecoveryInterval = phase.GetRecoveryInterval(),
                AverageLoopSpeed = phase.GetAverageLoopSpeed(),
                AveragePhaseSpeed = phase.GetAveragePhaseSpeed(),
                AverageSpeed = phase.GetAverageSpeed(),
                IsComplete = phase.IsComplete(),
            };
        }

        public static PhaseModel Create(Domain.Setup.Aggregates.Phase phase)
        {
            return new PhaseModel
            {
                Id = phase.Id,
                Loop = LoopModel.Create(phase.Loop!),
                Recovery = phase.Recovery,
                Rest = phase.Rest,
            };
        }

        public int Id { get; init; }
        public string? Gate { get; init; } = default!;
        public double? Length { get; init; }
        public int? MaxRecovery { get; init; }
        public int? Rest { get; init; }
        public CompetitionRuleset? Ruleset { get; init; }
        public bool? IsFinal { get; init; }
        public DateTimeOffset? StartTime { get; init; }
        public DateTimeOffset? ArriveTime { get; init; }
        public DateTimeOffset? PresentTime { get; init; }
        public DateTimeOffset? RepresentTime { get; init; }
        public bool? IsReinspectionRequested { get; init; }
        public bool? IsRequiredInspectionRequested { get; init; }
        public bool? IsRequiredInspectionCompulsory { get; init; }
        public DateTimeOffset? RequiredInspectionTime { get; init; }
        public DateTimeOffset? OutTime { get; init; }
        public TimeSpan? LoopInterval { get; init; }
        public TimeSpan? PhaseInterval { get; init; }
        public TimeSpan? RecoveryInterval { get; init; }
        public TimeSpan? CompulsoryThresholdInterval { get; init; } = TimeSpan.FromMinutes(10);
        public double? AverageLoopSpeed { get; init; }
        public double? AveragePhaseSpeed { get; init; }
        public double? AverageSpeed { get; init; }
        public bool IsComplete { get; init; }
        // Setup properties
        public int? Recovery {get; init;}
        public LoopModel? Loop { get; init; }

        public bool CheckCompulsoryTreshold()
        {
            GuardHelper.ThrowIfDefault(IsFinal);
            if (CompulsoryThresholdInterval == null || (bool)IsFinal)
            {
                return false;
            }
            return RecoveryInterval >= CompulsoryThresholdInterval;
        }

        public Phase ToCoreDomain()
        {
            GuardHelper.ThrowIfDefault(Gate, " Gate cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(Length, "Length cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(MaxRecovery, "MaxRecovery cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(Ruleset, "Ruleset cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(IsFinal, "IsFinal cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(IsReinspectionRequested, "IsReinspectionRequested cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(IsRequiredInspectionRequested, "IsRequiredInspectionRequested cannot be null when converting to Core Domain");
            GuardHelper.ThrowIfDefault(IsRequiredInspectionCompulsory, "IsRequiredInspectionCompulsory cannot be null when converting to Core Domain");
            return new Phase(
                EnsureId(Id),
                Gate,
                (double)Length,
                (int)MaxRecovery,
                Rest,
                (CompetitionRuleset)Ruleset,
                (bool)IsFinal,
                CompulsoryThresholdInterval,
                StartTime,
                ArriveTime,
                PresentTime,
                RepresentTime,
                (bool)IsReinspectionRequested,
                (bool)IsRequiredInspectionRequested,
                (bool)IsRequiredInspectionCompulsory
            );
        }

        public Domain.Setup.Aggregates.Phase ToSetupDomain()
        {
            GuardHelper.ThrowIfDefault(Loop, "Loop cannot be null when converting to Setup Domain");
            var loop = Loop.ToSetupDomain();
            return new Domain.Setup.Aggregates.Phase(EnsureId(Id), loop, Recovery, Rest);
        }
    }

    public class CombinationModel
    {
        public static CombinationModel Create(Combination combination)
        {
            return new CombinationModel
            {
                Id = combination.Id,
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
                Id = combination.Id,
                Number = combination.Number,
                Athlete = AthleteModel.Create(combination.Athlete),
                Horse = HorseModel.Create(combination.Horse),
            };
        }

        public int Id { get; init; }
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
                EnsureId(Id),
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
                EnsureId(Id),
                Number,
                athlete,
                horse,
                null // Tag prop is not used currently
            );
        }
        //    public Combination ToSetupDomain()
        //    {
        //        return Combination.Update(EnsureId(Id), Number, Athlete.ToSetupDomain(), Horse.ToSetupDomain(), null);
        //    }
    }

    public class ParticipationModel
    {
        public static ParticipationModel Create(Participation participation)
        {
            var total = participation.GetTotal();

            return new ParticipationModel
            {
                Id = participation.Id,
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
                Id = participation.Id,
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

        public int Id { get; init; }
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
            var competition = Competition!.ToCoreDomain();
            var combination = Combination.ToCoreDomain();
            var phases = Phases!.Select(x => x.ToCoreDomain());
            var eliminated = Eliminated?.ToDomain();
            return new Participation(
                EnsureId(Id),
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
                EnsureId(Id),
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
