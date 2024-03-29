﻿using EMS.Judge.Common;
using EMS.Judge.Common.Services;
using EMS.Judge.Services;
using EMS.Judge.Application.Aggregates.Import;
using EMS.Judge.Application.Services;
using Core.Domain.AggregateRoots.Manager;
using Prism.Commands;
using Prism.Regions;
using System.Windows;
using System;

namespace EMS.Judge.Views.Content.Import;

public class ImportViewModel : ViewModelBase
{
    private readonly JudgeSettings judgeSettings;
    private readonly IExecutor<IImportService> importExecutor;
    private readonly IApplicationContext context;
    private readonly IPersistence persistence;
    private readonly IExplorerService explorer;
    private readonly INavigationService navigation;

    public ImportViewModel(
        JudgeSettings judgeSettings,
        IExecutor<IImportService> importExecutor,
        IApplicationContext context,
        IPersistence persistence,
        IExplorerService explorer,
        INavigationService navigation)
    {
        this.judgeSettings = judgeSettings;
        this.importExecutor = importExecutor;
        this.context = context;
        this.persistence = persistence;
        this.explorer = explorer;
        this.navigation = navigation;
        this.OpenFolderDialog = new DelegateCommand(this.OpenFolderDialogAction);
        this.OpenImportFileDialog = new DelegateCommand(this.OpenImportFileDialogAction);
    }

    public DelegateCommand OpenFolderDialog { get; }
    public DelegateCommand OpenImportFileDialog { get; }

    private string workDirectoryPath;
    private string importFilePath;
    private Visibility workDirectoryVisibility = Visibility.Visible;
    private Visibility importFilePathVisibility = Visibility.Hidden;

    public override void OnNavigatedTo(NavigationContext context)
    {
        base.OnNavigatedTo(context);
        if (this.context.IsInitialized)
        {
            this.WorkDirectoryVisibility = Visibility.Collapsed;
            this.ImportFilePathVisibility = Visibility.Visible;
        }
    }

    public string WorkDirectoryPath
    {
        get => this.workDirectoryPath;
        private set => this.SetProperty(ref this.workDirectoryPath, value);
    }
    public Visibility WorkDirectoryVisibility
    {
        get => this.workDirectoryVisibility;
        set => this.SetProperty(ref this.workDirectoryVisibility, value);
    }
    public string ImportFilePath
    {
        get => this.importFilePath;
        set => this.SetProperty(ref this.importFilePath, value);
    }
    public Visibility ImportFilePathVisibility
    {
        get => this.importFilePathVisibility;
        set => this.SetProperty(ref this.importFilePathVisibility, value);
    }

    private void OpenFolderDialogAction()
    {
        try
        {
            var selectedPath = this.explorer.SelectDirectory();
            if (selectedPath == null)
            {
                return;
            }
            this.WorkDirectoryPath = selectedPath;

            this.WorkDirectoryVisibility = Visibility.Collapsed;
            this.ImportFilePathVisibility = Visibility.Visible;

            var result = this.persistence.Configure(selectedPath);
            ManagerRoot.dataDirectoryPath = selectedPath;
            this.context.Initialize();
            this.judgeSettings.IsConfigured = true;
            if (result.IsExistingFile)
            {
                this.Redirect();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    private void OpenImportFileDialogAction()
    {
        var path = this.explorer.SelectFile();
        if (path == null)
        {
            return;
        }

        this.ImportFilePath = path;
        var isSuccessful = this.importExecutor.Execute(x => x.Import(path), true);
        if (isSuccessful)
        {
            this.context.Initialize();
            this.Redirect();
        }
    }

    private void Redirect()
    {
        this.navigation.NavigateToEvent();
    }
}
