using Core.Domain.AggregateRoots.Common.Performances;
using Core.Domain.AggregateRoots.Manager.WitnessEvents;
using Core.Domain.Common.Exceptions;
using Core.Domain.Common.Models;
using Core.Domain.Enums;
using Core.Domain.State.LapRecords;
using Core.Domain.State.Participations;
using Core.Domain.State.Results;
using Core.Domain.Validation;
using Core.Enums;
using System;
using static Core.Localization.Strings;

namespace Core.Domain.AggregateRoots.Manager.Aggregates;

public class LapRecordsAggregate : IAggregate
{
    private readonly Validator<LapRecordException> validator;

    internal LapRecordsAggregate(LapRecord record)
    {
        this.Record = record;
        this.validator = new Validator<LapRecordException>();
    }

    public LapRecord Record { get; }
    public bool IsComplete =>
        this.Record.ArrivalTime != null && this.Record.InspectionTime != null && !this.Record.IsReinspectionRequired
        || this.Record.ArrivalTime != null && this.Record.ReInspectionTime != null && this.Record.IsReinspectionRequired;

    internal void Vet(DateTime time)
    {
        if (this.Record.InspectionTime == null)
        {
            this.EnterIn(time);
        }
        else if (this.Record.ReInspectionTime == null && this.Record.IsReinspectionRequired)
        {
            this.EnterReIn(time);
        }
    }

    internal void Disqualify(string number, string reason)
    {
        this.Record.Result = new Result(ResultType.Disqualified, reason);
        if (!this.Record.Lap.IsFinal)
        {
            Witness.RaiseStartlistChanged(number, CollectionAction.AddOrUpdate);
        }
        Witness.RaiseParticipantChanged(number, CollectionAction.Remove);
	}
    internal void FailToQualify(string number, string reason)
    {
        this.Record.Result = new Result(ResultType.FailedToQualify, reason);
        if (!this.Record.Lap.IsFinal)
        {
            Witness.RaiseStartlistChanged(number, CollectionAction.AddOrUpdate);
        }
        Witness.RaiseParticipantChanged(number, CollectionAction.Remove);
	}
    internal void Resign(string reason)
    {
        this.Record.Result = new Result(ResultType.Resigned, reason);
    }
    internal void Complete(string number)
    {
		this.Record.Result = new Result(ResultType.Successful);
        if (!this.Record.Lap.IsFinal)
        {
            Witness.RaiseStartlistChanged(number, CollectionAction.AddOrUpdate);
        }
        Witness.RaiseParticipantChanged(number, CollectionAction.AddOrUpdate);
	}
    internal void RequireReInspection(bool isRequired)
    {
        this.Record.IsReinspectionRequired = isRequired;
    }
    internal void RequireCompulsoryInspection(bool isRequired)
    {
        if (this.Record.Lap.IsCompulsoryInspectionRequired)
        {
            throw Helper.Create<LapRecordException>(REQUIRED_INSPECTION_IS_NOT_ALLOWED_ON_FINAL_MESSAGE);
        }
        this.Record.IsRequiredInspectionRequired = isRequired;
    }
    internal void Edit(ILapRecordState state)
    {
        var inTime = state.InspectionTime;
        var reInTime = state.ReInspectionTime;
        this.Record.ArrivalTime = state.ArrivalTime;
        this.Record.InspectionTime = inTime;
        this.Record.ReInspectionTime = reInTime;
    }

    internal void Arrive(DateTime time)
    {
        // time = FixDateForToday(time);
        this.validator.IsLaterThan(time, this.Record.StartTime, ARRIVAL_TERM);
        this.Record.ArrivalTime = time;
    }
    private void EnterIn(DateTime time)
    {
        // time = FixDateForToday(time);
        this.validator.IsLaterThan(time, this.Record.ArrivalTime, INSPECTION_TERM);
        this.Record.InspectionTime = time;
    }
    private void EnterReIn(DateTime time)
    {
        // time = FixDateForToday(time);
        this.validator.IsLaterThan(time, this.Record.InspectionTime, RE_INSPECTION_TERM);
        this.Record.ReInspectionTime = time;
    }

    private bool HasRecovered(DateTime inTime)
    {
        var recoverySpan = TimeSpan.FromMinutes(this.Record.Lap.MaxRecoveryTimeInMins);
        return inTime - this.Record.ArrivalTime <= recoverySpan;
    }

    internal void CheckForResult(Participation participation)
    {
        var averageSpeedLimit = participation.Participant.MaxAverageSpeedInKmPh;
        var type = participation.CompetitionConstraint.Type;
        if (!this.Record.ArrivalTime.HasValue || !this.Record.InspectionTime.HasValue)
        {
            return;
        }
        if (averageSpeedLimit.HasValue && Performance.GetSpeed(this.Record, type) > averageSpeedLimit)
        {
            this.FailToQualify(participation.Participant.Number, "speed");
            return;
        }
        var vetTime = this.Record.InspectionTime;
        if (this.Record.IsReinspectionRequired)
        {
            if (!this.Record.ReInspectionTime.HasValue)
            {
                return;
            }
            vetTime = this.Record.ReInspectionTime;
        }
        if (!this.HasRecovered(vetTime.Value))
        {
            this.FailToQualify(participation.Participant.Number, "MET");
            return;
        }
        else
        {
            this.Complete(participation.Participant.Number);
            return;
        }
    }
}
