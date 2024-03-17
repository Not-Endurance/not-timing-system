using Core.Application.Rpc;
using Core.Application.Rpc.Procedures;
using Core.ConventionalServices;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Enums;
using Core.Models;
using EMS.Witness.Rpc;

namespace EMS.Witness.Services;
public class StartlistService : IStartlistService
{
	private readonly IWitnessContext context;
    private readonly IStartlistClient startlistClient;

    public StartlistService(IWitnessContext context, IStartlistClient startlistClient)
    {
        this.context = context;
        this.startlistClient = startlistClient;
    }

    public ObservableCollection<StartlistEntry> Startlist { get;} = new();
    public int? SelectedStage { get; private set; }
    public Dictionary<int, Startlist> StartlistsByStage => this.context.Startlists;

    public async Task Load()
    {
        var result = await this.startlistClient.Load();
        if (result.IsSuccessful)
        {
            this.context.Startlists = result.Data!;
            this.SelectList();
        }
    }

    public void Update(StartlistEntry entry, CollectionAction action)
    {
        if (!this.StartlistsByStage.ContainsKey(entry.Stage))
        {
            this.StartlistsByStage[entry.Stage] = new();
        }
		this.StartlistsByStage[entry.Stage].Update(entry, action);
        if (this.SelectedStage == entry.Stage || this.SelectedStage == null)
        {
            this.Startlist.Update(entry, action);
        }
    }

    public void SelectList(int? stage = null)
    {
        var list = stage == null
            ? new Startlist(this.context.Startlists.SelectMany(x => x.Value))
            : this.context.Startlists[stage.Value];
        this.SelectedStage = stage;
        this.Startlist.Clear();
        this.Startlist.AddRange(list);
    }
}

public interface IStartlistService : ISingletonService
{
    ObservableCollection<StartlistEntry> Startlist { get; }
    Dictionary<int, Startlist> StartlistsByStage { get; }
    Task Load();
    void Update(StartlistEntry entry, CollectionAction action);
    void SelectList(int? stage = null);
}
