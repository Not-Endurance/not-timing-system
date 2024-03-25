using EMS.Judge.Controls.Manager;
using EMS.Judge.Common;
using Core.Domain.AggregateRoots.Common.Performances;
using Core.Domain.State.LapRecords;
using Core.Domain.State.Participations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static Core.Localization.Strings;

namespace EMS.Judge.Controls.Ranking;

public class ParticipationResultModel : ParticipationGridModel
{
    public ParticipationResultModel(int rank, Participation participation)
        : base(participation, true)
    {
        this.Rank = rank;
        this.HorseGenderString = participation.Participant.Horse.IsStallion ? STALLION : MARE; // TODO: move in domain

        if (participation.Participant.LapRecords.Any())
        {
            var (ride, rec, total, speed) = this.CalculateTotalValues(participation);
            this.TotalAverageSpeed = ValueSerializer.FormatDouble(speed);
            this.TotalTime = ValueSerializer.FormatSpan(total);
            this.RideTime = ValueSerializer.FormatSpan(ride);
            this.RecTime = ValueSerializer.FormatSpan(rec);
        }

        var (notQualified, visibility) = this.HandleNotQualified(participation.Participant.LapRecords.ToList());
        this.DisqualifiedVisibility = visibility;
        this.NotQualifiedText = notQualified;
    }

    private (TimeSpan ride, TimeSpan rec, TimeSpan total, double? speed) CalculateTotalValues(Participation participation)
    {
        var performances = Performance.GetAll(participation).ToArray();
        var totalLenght = participation.Participant.LapRecords.Sum(x => x.Lap.LengthInKm);

        var rideTime = performances.Aggregate(TimeSpan.Zero, (result, x) => result + (x.ArrivalTime.Value - x.StartTime));
        var recTime = performances
            .Where(x => !x.Record.Lap.IsFinal)
            .Aggregate(TimeSpan.Zero, (result, x) => result + x.RecoverySpan.Value);
        var totalPhaseTime = rideTime + recTime;
        var avrageTotalPhaseSpeed = totalLenght / totalPhaseTime.TotalHours;

        return (rideTime, recTime, totalPhaseTime, avrageTotalPhaseSpeed);
    }

    private (string notQualified, Visibility) HandleNotQualified(List<LapRecord> records)
    {
        var disqualifiedRecord = records.FirstOrDefault(x => x.Result?.IsNotQualified ?? false);
        var visibility = disqualifiedRecord != null
            ? Visibility.Visible
            : Visibility.Collapsed;
        var text = disqualifiedRecord?.Result.ToString();

        return (text, visibility);
    }

    public int Rank { get; }
    public string HorseGenderString { get; }
    public string TotalAverageSpeed { get; }
    public string TotalTime { get; }
    public string RideTime { get; }
    public string RecTime { get; }
    public Visibility DisqualifiedVisibility { get; }
    public string NotQualifiedText { get; }
}
