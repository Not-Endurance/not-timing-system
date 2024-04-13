using EMS.Judge.Common.Components.Templates.SimpleListItem;
using EMS.Judge.Services;
using EMS.Judge.Views.Content.Configuration.Core;
using EMS.Judge.Application.Common;
using Core.Mappings;
using Core.Domain.AggregateRoots.Configuration;
using Core.Domain.Common.Models;
using Core.Domain.State.Athletes;
using Core.Domain.State.Horses;
using Core.Domain.State.Participants;
using Prism.Commands;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Windows;
using EMS.Judge.Application.Services;
using System.Linq;
using EMS.Judge.Application.Common.Exceptions;

namespace EMS.Judge.Views.Content.Configuration.Roots.Participants;

public class ParticipantViewModel : ConfigurationBase<ParticipantView, Participant>,
    IParticipantState,
    IMapFrom<Participant>
{
    private readonly IExecutor<IRfidService> rfidServiceExecutor;
    private readonly IExecutor<ConfigurationRoot> executor;
    private readonly IQueries<Athlete> athletes;
    private readonly IQueries<Horse> horses;

    private ParticipantViewModel() : base(null) {}
    public ParticipantViewModel(
        IExecutor<IRfidService> rfidServiceExecutor,
        IExecutor<ConfigurationRoot> executor,
        IQueries<Athlete> athletes,
        IQueries<Horse> horses,
        IQueries<Participant> participants) : base(participants)
    {
        this.rfidServiceExecutor = rfidServiceExecutor;
        this.executor = executor;
        this.athletes = athletes;
        this.horses = horses;
        this.WriteTag = new DelegateCommand(this.WriteTagAction);
        this.ToggleIsAverageSpeedInKmPhVisibility = new DelegateCommand(
            this.ToggleIsAverageSpeedInKmPhVisibilityAction);
        this.RemoveTags = new DelegateCommand(this.RemoveTagsAction);
    }

    public DelegateCommand ToggleIsAverageSpeedInKmPhVisibility { get; }
    public DelegateCommand WriteTag { get; }
    public DelegateCommand RemoveTags { get; }

    public ObservableCollection<SimpleListItemViewModel> HorseItems { get; } = new();
    public ObservableCollection<SimpleListItemViewModel> AthleteItems { get; } = new();
    public ObservableCollection<RfidTag> RfidTags { get; } = new();

    public string number;
    public int? maxAverageSpeedInKmPh;
    private Visibility maxAverageSpeedInKmPhVisibility = Visibility.Hidden;
    private int horseId;
    private int athleteId;
    private string name;
    private bool unranked;
    private string horseName;
    private string athleteName;
    private int positionid;
    private bool isWriteTagEnabled = true;

    public bool IsWriteTagEnababled
    {
        get => this.isWriteTagEnabled;
        set => this.SetProperty(ref this.isWriteTagEnabled, value);
    }

    public ObservableCollection<SimpleListItemViewModel> PositionItems { get; } = new() 
    { 
       new(0, "left"),
       new(1, "right")
    };

    public int PositionId
    { 
        get => this.positionid;
        set => this.SetProperty(ref this.positionid, value);
    }

    public override void OnNavigatedTo(NavigationContext context)
    {
        this.LoadAthletes();
        this.LoadHorses();
        base.OnNavigatedTo(context);
        if (this.MaxAverageSpeedInKmPh.HasValue)
        {
            this.ShowMaxAverageSpeedInKmPh();
        }
    }

    protected override IDomain Persist()
    {
        var result = this.executor.Execute(
            config => config.Participants.Save(this, this.AthleteId, this.HorseId),
            true);
        return result;
    }

    private void RemoveTagsAction()
    {
        this.rfidServiceExecutor.Execute(x =>
        {
            var participant = this.Queries.GetOne(y => y.Number == this.Number);
            participant.RfidTags.Clear();
            this.RfidTags.Clear();
        },
        true);
    }

    private void WriteTagAction()
    {
        this.IsWriteTagEnababled = false;
        this.rfidServiceExecutor.Execute(x =>
        {
            var position = this.PositionItems.First(x => x.Id == this.PositionId).Name;
            var tag = x.Write(position, this.Number);
            var existingParticipant = this.Queries.GetOne(x => x.RfidTags.Contains(tag));
            if (existingParticipant != null)
            {
                throw new AppException($"Tag '{tag.Id}' is assigned to participant '{existingParticipant.Number}'");
            }
            var participant = this.Queries.GetOne(y => y.Number == this.Number);
            var existing = participant.RfidTags.FirstOrDefault(x => x.Id == tag.Id);
            if (existing != null)
            {
                participant.RfidTags.Remove(existing);
                this.RfidTags.Remove(existing);
            }
            if (participant.RfidTags.Count == 2)
            {
                participant.RfidTags.RemoveAt(0);
                this.RfidTags.RemoveAt(0);
            }
            participant.RfidTags.Add(tag);
            this.RfidTags.Add(tag);
        },
        true);
        this.IsWriteTagEnababled = true;
    }

    private void LoadAthletes()
    {
        var athletes = this.athletes.GetAll();
        var viewModels = athletes.MapEnumerable<SimpleListItemViewModel>();
        this.AthleteItems.Clear();
        this.AthleteItems.AddRange(viewModels);
    }
    private void LoadHorses()
    {
        var horses = this.horses.GetAll();
        var viewModels = horses.MapEnumerable<SimpleListItemViewModel>();
        this.HorseItems.Clear();
        this.HorseItems.AddRange(viewModels);
    }

    public void ToggleIsAverageSpeedInKmPhVisibilityAction()
    {
        if (this.MaxAverageSpeedInKmPhVisibility == Visibility.Hidden)
        {
            this.ShowMaxAverageSpeedInKmPh();
        }
        else
        {
            this.RemoveMaxAverageSpeedInKmPh();
        }
    }
    private void ShowMaxAverageSpeedInKmPh()
    {
        this.MaxAverageSpeedInKmPhVisibility = Visibility.Visible;
    }
    private void RemoveMaxAverageSpeedInKmPh()
    {
        this.MaxAverageSpeedInKmPhVisibility = Visibility.Hidden;
        this.MaxAverageSpeedInKmPh = null;
    }
    public Visibility MaxAverageSpeedInKmPhVisibility
    {
        get => this.maxAverageSpeedInKmPhVisibility;
        set => this.SetProperty(ref this.maxAverageSpeedInKmPhVisibility, value);
    }

    private string FormatName()
    {
        return Participant.FormatName(this.Number, this.AthleteName, this.horseName);
    }

    public string Number
    {
        get => this.number;
        set => this.SetProperty(ref this.number, value);
    }
    public int? MaxAverageSpeedInKmPh
    {
        get => this.maxAverageSpeedInKmPh;
        set => this.SetProperty(ref this.maxAverageSpeedInKmPh, value);
    }
    public int HorseId
    {
        get => this.horseId;
        set => this.SetProperty(ref this.horseId, value);
    }
    public int AthleteId
    {
        get => this.athleteId;
        set => this.SetProperty(ref this.athleteId, value);
    }
    public string HorseName
    {
        get => this.horseName;
        set
        {
            this.SetProperty(ref this.horseName, value);
            this.Name = this.FormatName();
        }
    }
    public string AthleteName
    {
        get => this.athleteName;
        set
        {
            this.SetProperty(ref this.athleteName, value);
            this.Name = this.FormatName();
        }
    }
    public string Name
    {
        get => this.name;
        private set => this.SetProperty(ref this.name, value);
    }
    public bool Unranked
    {
        get => this.unranked;
        set => this.SetProperty(ref this.unranked, value);
    }
}
