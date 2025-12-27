using Not.Random;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using static NTS.Application.DataTransferObjects.Models.CommonModel;

namespace NTS.Application.DataTransferObjects.Models;

public class CoreModel
{
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

        public string Name { get; init; } = default!;
        public CompetitionRuleset Ruleset { get; init; }
        public CompetitionType Type { get; init; }

        public Competition ToDomain()
        {
            return new Competition(Name, Ruleset, Type);
        }
    }

    public class PhaseModel
    {
        public static PhaseModel Create(Phase phase)
        {
            return new PhaseModel
            {
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

        public string Gate { get; init; } = default!;
        public double Length { get; init; }
        public int MaxRecovery { get; init; }
        public int? Rest { get; init; }
        public CompetitionRuleset Ruleset { get; init; }
        public bool IsFinal { get; init; }
        public DateTimeOffset? StartTime { get; init; }
        public DateTimeOffset? ArriveTime { get; init; }
        public DateTimeOffset? PresentTime { get; init; }
        public DateTimeOffset? RepresentTime { get; init; }
        public bool IsReinspectionRequested { get; init; }
        public bool IsRequiredInspectionRequested { get; init; }
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

        public Phase ToDomain()
        {
            return new Phase(
                RandomHelper.GenerateUniqueInteger(),
                Gate,
                Length,
                MaxRecovery,
                Rest,
                Ruleset,
                IsFinal,
                CompulsoryThresholdInterval,
                StartTime,
                ArriveTime,
                PresentTime,
                RepresentTime,
                IsReinspectionRequested,
                IsRequiredInspectionRequested,
                false
            );
        }
    }

    public class TotalModel
    {
        public static TotalModel Create(Total total)
        {
            return new TotalModel
            {
                LastArriveTime = total.FinishTime?.ToDateTimeOffset(),
                AverageSpeed = total.AverageSpeed.ToDouble(),
                Interval = total.Interval.ToTimeSpan(),
                RideInterval = total.RideInterval.ToTimeSpan(),
                RecoveryInterval = total.RecoveryInterval.ToTimeSpan(),
                RecoveryIntervalWithoutFinal = total.RecoveryIntervalWithoutFinal.ToTimeSpan(),
            };
        }

        public DateTimeOffset? LastArriveTime { get; init; }
        public double AverageSpeed { get; init; }
        public TimeSpan Interval { get; init; }
        public TimeSpan RideInterval { get; init; }
        public TimeSpan RecoveryInterval { get; init; }
        public TimeSpan RecoveryIntervalWithoutFinal { get; init; }
    }

    public class EliminatedModel
    {
        public static EliminatedModel Create(Eliminated eliminated)
        {
            if (eliminated is FailedToQualify failedToQualify)
            {
                return new EliminatedModel
                {
                    Code = eliminated.Code,
                    Reason = eliminated.Complement,
                    FtqCodes = failedToQualify.FtqCodes.ToArray(),
                };
            }
            else if (eliminated is Disqualified disqualified)
            {
                return new EliminatedModel
                {
                    Code = eliminated.Code,
                    Reason = eliminated.Complement,
                    DqCodes = disqualified.DqCodes.ToArray(),
                };
            }
            return new EliminatedModel { Code = eliminated.Code, Reason = eliminated.Complement };
        }

        public string Code { get; init; } = default!;
        public string? Reason { get; init; }
        public FailToQualifyCode[]? FtqCodes { get; init; }
        public DisqualifyCode[] DqCodes { get; init; } = default!;

        public Eliminated ToDomain()
        {
            return Code switch // TODO refactor Eliminated to non-abstract and only FTQ as separate class
            {
                Eliminated.FAILED_TO_QUALIFY => new FailedToQualify(FtqCodes!, Reason),
                Eliminated.WITHDRAWN => new Withdrawn(),
                Eliminated.DISQUALIFIED => new Disqualified(DqCodes, Reason!),
                Eliminated.FINISHED_NOT_RANKED => new FinishedNotRanked(Reason!),
                Eliminated.RETIRED => new Retired(),
                _ => throw new NotImplementedException(),
            };
        }
    }

    public class RankingEntryModel
    {
        public static RankingEntryModel Create(RankingEntry rankingEntry)
        {
            return new RankingEntryModel
            {
                Participation = ParticipationModel.Create(rankingEntry.Participation),
                Rank = rankingEntry.Rank,
                IsNotRanked = rankingEntry.IsNotRanked,
            };
        }

        public ParticipationModel Participation { get; init; } = default!;
        public int? Rank { get; init; }
        public bool IsNotRanked { get; init; }

        public RankingEntry ToDomain()
        {
            var participation = Participation.ToCoreDomain();
            return new RankingEntry(RandomHelper.GenerateUniqueInteger(), participation, Rank, IsNotRanked);
        }
    }

    public class RanklistModel
    {
        public static RanklistModel Create(Ranklist ranklist)
        {
            return new RanklistModel
            {
                Name = ranklist.Name,
                Ruleset = ranklist.Ruleset,
                Type = ranklist.Type,
                Category = ranklist.Category,
                CompetitionFeiId = ranklist.Ranking.CompetitionFeiId,
                FeiRule = ranklist.Ranking.FeiRule,
                FeiScheduleNumber = ranklist.Ranking.FeiScheduleNumber,
                Entries = ranklist.Entries.Select(RankingEntryModel.Create).ToArray(),
            };
        }

        public string Name { get; init; } = default!;
        public CompetitionRuleset Ruleset { get; init; }
        public CompetitionType Type { get; init; }
        public ParticipationCategory Category { get; init; }
        public string? CompetitionFeiId { get; init; }
        public string? FeiRule { get; init; }
        public string? FeiScheduleNumber { get; init; }
        public RankingEntryModel[] Entries { get; init; } = [];

        public Ranklist ToDomain()
        {
            var entries = Entries.Select(x => x.ToDomain());
            var competition = new Competition(Name, Ruleset, Type);
            var ranking = new Ranking(competition, Category, CompetitionFeiId, FeiRule, FeiScheduleNumber, entries);
            return new Ranklist(ranking, entries);
        }
    }

    public class ArchiveModel : Identity
    {
        public static ArchiveModel Create(
            EnduranceEvent enduranceEvent,
            IEnumerable<Official> officials,
            IEnumerable<Ranklist> ranklists
        )
        {
            return new ArchiveModel
            {
                Id = enduranceEvent.Id,
                Country = CountryModel.Create(enduranceEvent.PopulatedPlace.Country),
                City = enduranceEvent.PopulatedPlace.City,
                Location = enduranceEvent.PopulatedPlace.Location,
                FeiShowId = enduranceEvent.FeiShowId,
                FeiId = enduranceEvent.FeiId,
                FeiEventCode = enduranceEvent.FeiEventCode,
                StartDay = enduranceEvent.EventSpan.StartDay,
                EndDay = enduranceEvent.EventSpan.EndDay,
                Officials = officials.Select(OfficialModel.Create).ToArray(),
                Ranklists = ranklists.Select(RanklistModel.Create).ToArray(),
            };
        }

        public CountryModel Country { get; init; } = default!;
        public string City { get; init; } = default!;
        public string? Location { get; init; }
        public string? FeiShowId { get; init; }
        public string? FeiId { get; init; }
        public string? FeiEventCode { get; init; }
        public DateTimeOffset StartDay { get; init; }
        public DateTimeOffset EndDay { get; init; }
        public OfficialModel[] Officials { get; init; } = default!;
        public RanklistModel[] Ranklists { get; init; } = default!;

        public ArchiveEntry ToDomain()
        {
            var enduranceEvent = new EnduranceEvent(
                Id,
                Country.ToDomain(),
                City,
                Location ?? "",
                StartDay,
                EndDay,
                FeiShowId,
                FeiId,
                FeiEventCode
            );
            var officials = Officials.Select(x => x.ToCoreDomain());
            var ranklists = Ranklists.Select(x => x.ToDomain());
            return new ArchiveEntry(enduranceEvent, officials, ranklists);
        }
    }
}
