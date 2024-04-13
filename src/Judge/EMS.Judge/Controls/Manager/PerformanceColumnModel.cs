using EMS.Judge.Common;
using EMS.Judge.Services;
using Core.Utilities;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Common.Performances;
using Core.Domain.State.LapRecords;
using Prism.Commands;
using System;
using System.Windows;
using static Core.Localization.Strings;

namespace EMS.Judge.Controls.Manager;

public class PerformanceColumnModel : ViewModelBase, ILapRecordState
{
    private readonly IExecutor<ManagerRoot> managerExecutor;
    private int index;
    public PerformanceColumnModel(Performance performance, int index, bool isReadonly)
    {
        this.index = index;
        this.IsReadonly = isReadonly;
        this.EditVisibility = Visibility.Visible;
        this.managerExecutor = StaticProvider.GetService<IExecutor<ManagerRoot>>();
        this.Edit = new DelegateCommand(this.EditAction);
        this.Update(performance);
        performance.PropertyChanged += (obj, args) =>
        {
            this.Update(performance);
        };
    }

    public bool IsReadonly { get; }

    public Visibility EditVisibility { get; set; }
    public Visibility ReadonlyVisibility => this.EditVisibility == Visibility.Collapsed
        ? Visibility.Visible
        : Visibility.Collapsed;

    public DelegateCommand Edit { get; }

    public string HeaderValue { get; private set; }
    private DateTime startTime;
    private string arrivalTimeString;
    private string inspectionTimeString;
    private string reInspectionTimeString;
    private string requiredInspectionTimeString;
    private string compulsoryRequiredInspectionTimeString;
    private string recoverySpanString;
    private string timeString;
    public string nextStartTimeString;
    private string averageSpeed;
    private string averageSpeedPhase;

    public void EditAction()
    {
        this.managerExecutor.Execute(manager =>
        {
            var result = manager.EditRecord(this);
            this.Update(result);
        }, true);
    }

    public void Update(Performance performance)
    {
        this.Id = performance.Id;
        this.HeaderValue = this.CreateHeader(performance);
        this.RecoverySpanString = ValueSerializer.FormatSpan(performance.RecoverySpan);
        this.TimeString = ValueSerializer.FormatSpan(performance.Time);
        this.AverageSpeed = performance.AverageSpeed;
        this.AverageSpeedPhaseString = ValueSerializer.FormatDouble(performance.AverageSpeedPhase);
        this.NextStartTimeString = ValueSerializer.FormatTime(performance.NextStartTime);
        this.StartTime = performance.StartTime;
        this.ArrivalTime = performance.ArrivalTime;
        this.InspectionTime = performance.InspectionTime;
        this.IsReinspectionRequired = performance.IsReinspectionRequired;
        this.IsRequiredInspectionRequired = performance.IsRequiredInspectionRequired;
        this.ReInspectionTimeString = ValueSerializer.FormatTime(performance.ReInspectionTime);
        this.RequiredInspectionTimeString = ValueSerializer.FormatTime(performance.RequiredInspectionTime);
        this.CompulsoryRequiredInspectionTimeString = ValueSerializer.FormatTime(performance.CompulsoryRequiredInspectionTime);

        this.RaisePropertyChanged(nameof(this.ReInspectionTimeString));
        this.RaisePropertyChanged(nameof(this.RecoverySpanString));
        this.RaisePropertyChanged(nameof(this.TimeString));
        this.RaisePropertyChanged(nameof(this.AverageSpeed));
        this.RaisePropertyChanged(nameof(this.AverageSpeedPhaseString));
        this.RaisePropertyChanged(nameof(this.NextStartTimeString));
        this.RaisePropertyChanged(nameof(this.ArrivalTimeString));
        this.RaisePropertyChanged(nameof(this.InspectionTimeString));
        this.RaisePropertyChanged(nameof(this.ReInspectionTimeString));
        this.RaisePropertyChanged(nameof(this.RequiredInspectionTimeString));
        this.RaisePropertyChanged(nameof(this.CompulsoryRequiredInspectionTimeString));
    }

    private string CreateHeader(Performance performance)
    {
        var lap = performance.Record.Lap.IsFinal
            ? $"{FINAL}"
            : $"{GATE.ToUpper()}{this.index}";
        var header = $"{lap}/{performance.TotalLength} {KM}";
        return header;
    }

#region IPerformanceState implementation

    public DateTime? ArrivalTime
    {
        get => ValueSerializer.ParseTime(this.ArrivalTimeString);
        private set
        {
            this.ArrivalTimeString = ValueSerializer.FormatTime(value);
        }
    }
    public DateTime? InspectionTime
    {
        get => ValueSerializer.ParseTime(this.InspectionTimeString);
        private set => this.InspectionTimeString = ValueSerializer.FormatTime(value);
    }
    public DateTime? ReInspectionTime
    {
        get => ValueSerializer.ParseTime(this.ReInspectionTimeString);
        private set => this.ReInspectionTimeString = ValueSerializer.FormatTime(value);
    }
    public double? AverageSpeed
    {
        get => ValueSerializer.ParseDouble(this.AverageSpeedString);
        private set => this.AverageSpeedString = ValueSerializer.FormatDouble(value);
    }
    public bool IsReinspectionRequired { get; private set; }
    public bool IsRequiredInspectionRequired { get; private set; }
    public int Id { get; private set; }

#endregion

#region Setters

    public DateTime StartTime
    {
        get => this.startTime;
        private set => this.SetProperty(ref this.startTime, value);
    }
    public string ArrivalTimeString
    {
        get => this.arrivalTimeString;
        set => this.SetProperty(ref this.arrivalTimeString, value);
    }
    public string InspectionTimeString
    {
        get => this.inspectionTimeString;
        set => this.SetProperty(ref this.inspectionTimeString, value);
    }
    public string ReInspectionTimeString
    {
        get => this.reInspectionTimeString;
        set => this.SetProperty(ref this.reInspectionTimeString, value);
    }
    public string RequiredInspectionTimeString
    {
        get => this.requiredInspectionTimeString;
        set => this.SetProperty(ref this.requiredInspectionTimeString, value);
    }
    public string CompulsoryRequiredInspectionTimeString
    {
        get => this.compulsoryRequiredInspectionTimeString;
        set => this.SetProperty(ref this.compulsoryRequiredInspectionTimeString, value);
    }
    public string RecoverySpanString
    {
        get => this.recoverySpanString;
        private set => this.SetProperty(ref this.recoverySpanString, value);
    }
    public string TimeString
    {
        get => this.timeString;
        private set => this.SetProperty(ref this.timeString, value);
    }
    public string AverageSpeedString
    {
        get => this.averageSpeed;
        private set => this.SetProperty(ref this.averageSpeed, AddKmSuffix(value));
    }
    public string AverageSpeedPhaseString
    {
        get => this.averageSpeedPhase;
        private set => this.SetProperty(ref this.averageSpeedPhase, AddKmSuffix(value));
    }
    public string NextStartTimeString
    {
        get => this.nextStartTimeString;
        private set => this.SetProperty(ref this.nextStartTimeString, value);
    }

    private static string AddKmSuffix(string value) => $"{value} {KM_PER_HOUR}";

#endregion Setters;
}
