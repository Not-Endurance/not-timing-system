﻿using EnduranceJudge.Application.Hardware;
using EnduranceJudge.Gateways.Desktop.Core;
using EnduranceJudge.Gateways.Desktop.Views.Content.Hardware.Tags;
using EnduranceJudge.Gateways.Desktop.Views.Content.Manager;
using Prism.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Media;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EnduranceJudge.Gateways.Desktop.Views.Content.Hardware;

public class HardwareViewModel : ViewModelBase
{
    private readonly VupRfidController controller;
    private string message = "";
    private int power = 27;
    private bool isListing;

    public HardwareViewModel()
    {
        this.controller = new VupRfidController(FinishWitness.FINISH_DEVICE_IP);
        this.controller.MessageEvent += (_, message) => this.Message = message;
        this.controller.ReadEvent += this.HandleReadEventTag;
        this.isListing = this.controller.IsPolling;

        this.Connect = new DelegateCommand(this.ConnectAction);
        this.Start = new DelegateCommand(this.StartAction);
        this.Stop = new DelegateCommand(this.StopAction);
        this.SetPower = new DelegateCommand(this.SetPowerAction);
        this.Reset = new DelegateCommand(this.ResetAction);
        this.Disconnect = new DelegateCommand(this.DisconnectAction);
    }

    public ObservableCollection<TagViewModel> Tags { get; } = new();
    public DelegateCommand Connect { get; }
    public DelegateCommand Start { get; }
    public DelegateCommand Stop { get; }
    public DelegateCommand SetPower { get; }
    public DelegateCommand Reset { get; }
    public DelegateCommand Disconnect { get; }

    public string Message
    {
        get => this.message;
        set => this.SetProperty(ref this.message, value);
    }
    public string Power
    {
        get => this.power.ToString();
        set => this.SetProperty(ref this.power, int.Parse(value));
    }
    public bool IsListing
    {
        get => this.isListing;
        set
        {
            this.SetProperty(ref this.isListing, value);
            this.RaisePropertyChanged(nameof (this.IsNotListing));
        }
    }
    public bool IsNotListing
    {
        get => !isListing;
    }

    public void ConnectAction()
    {
        this.controller.Connect();
    }

    public void StartAction()
    {
        this.IsListing = true;
        Task.Run(() => this.controller.StartPolling());
    }

    public void StopAction()
    {
        this.IsListing = false;
        this.controller.StopPolling();
    }

    public void SetPowerAction()
    {
        this.controller.SetPower(this.power);
    }

    public void ResetAction()
    {
        foreach (var tag in this.Tags)
        {
            tag.DetectedCount = 0;
        }
    }

    public void DisconnectAction()
    {
        Task.Run(() => this.controller.Disconnect());
    }

    private void HandleReadEventTag(object sender, IEnumerable<string> tagIds)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            SynchronizationContext.SetSynchronizationContext(new
                DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));

            SynchronizationContext.Current!.Post(_ =>
            {
                SystemSounds.Beep.Play();
                foreach (var tagId in tagIds)
                {
                    var existingTag = this.Tags.FirstOrDefault(x => x.Id == tagId);
                    if (existingTag != null)
                    {
                        existingTag.DetectedCount++;
                    }
                    else
                    {
                        var tag = new TagViewModel { DetectedCount = 1, Id = tagId };
                        this.Tags.Add(tag);
                    }
                }
            }, null);
        });
    }
}