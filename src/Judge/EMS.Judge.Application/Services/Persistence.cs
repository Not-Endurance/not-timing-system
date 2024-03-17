using Core.ConventionalServices;
using Core.Events;
using Core.Services;
using Core.Domain.AggregateRoots.Manager.WitnessEvents;
using Core.Domain.State;
using EMS.Judge.Application.Common.Services;
using EMS.Judge.Application.Models;
using EMS.Judge.Application.State;
using System;
using System.Collections.Generic;

namespace EMS.Judge.Application.Services;

public class Persistence : IPersistence
{
    private const string STORAGE_FILE_NAME = "judge.json";
    private const string SANDBOX_STORAGE_FILE_NAME = "judge-sandbox.json";
    public string StateDirectoryPath { get; private set; }

    private readonly ISettings settings;
    private readonly IStateSetter stateSetter;
    private readonly IState state;
    private readonly IFileService file;
    private readonly IJsonSerializationService serialization;

    public Persistence(
        ISettings settings,
        IStateContext context,
        IStateSetter stateSetter,
        IFileService file,
        IJsonSerializationService serialization)
    {
        this.settings = settings;
        this.stateSetter = stateSetter;
        this.state = context.State;
        this.file = file;
        this.serialization = serialization;
        Witness.StateChanged += (_, _) => this.SaveState();
    }

    public void LogStatistics(string text)
    {
        var path = $"{this.StateDirectoryPath}/statistics.txt";
        this.file.Create(path, text);
    }

    public string LogError(string message, string stackTrace)
    {
        var timestamp = DateTime.Now.ToString("yyyy-mm-ddTHH-mm-ss");
        var log = new Dictionary<string, object>
        {
            { "error-message", message },
            { "error-stack-trance", stackTrace },
            { "state", this.state },
        };
        var serialized = this.serialization.Serialize(log);
        var filename = $"{timestamp}_error.json";
        var path = $"{this.StateDirectoryPath}/{filename}";
        this.file.Create(path, serialized);
        return path;
    }

    public PersistenceResult Configure(string directoryPath)
    {
        if (this.stateSetter.Event != null) 
        {
            return PersistenceResult.Existing;
        }

        Console.WriteLine("Restoring state");
        this.StateDirectoryPath = directoryPath;
        var database = this.GetFilePath();
        var sandboxDatabase = this.GetSandBoxFilePath();
        if (this.settings.IsSandboxMode && this.file.Exists(sandboxDatabase))
        {
            this.LoadState(sandboxDatabase);
            return PersistenceResult.Existing;
        }
        if (this.file.Exists(database))
        {
            this.LoadState(database);
            Console.WriteLine($"Event in state: {this.stateSetter.Event?.Id}, {this.stateSetter.Event?.Name}");
			Console.WriteLine("Restored state!");
			return PersistenceResult.Existing;
        }

        this.SaveState();
        Console.WriteLine("No existing state to restore");
        return PersistenceResult.New;
    }

    public void SaveState()
    {
        var serialized = this.serialization.Serialize(this.state);
        var databasePath = BuildStorageFilePath();
        this.file.Create(databasePath, serialized);
    }

    private void LoadState(string path)
    {
        var contents = this.file.Read(path);
        var state = this.serialization.Deserialize<StateModel>(contents);
        this.stateSetter.Set(state);
        CoreEvents.RaiseStateLoaded();
    }

    private string BuildStorageFilePath()
        => this.settings.IsSandboxMode
            ? this.GetSandBoxFilePath()
            : this.GetFilePath();

    private string GetSandBoxFilePath()
        => $"{this.StateDirectoryPath}\\{SANDBOX_STORAGE_FILE_NAME}";

    private string GetFilePath()
        => $"{this.StateDirectoryPath}\\{STORAGE_FILE_NAME}";
}


public interface IPersistence : ISingletonService
{
	string StateDirectoryPath { get; }
	void SaveState();
    void LogStatistics(string text);
    string LogError(string message, string stackTrace); 
    PersistenceResult Configure(string directoryPath);
}
