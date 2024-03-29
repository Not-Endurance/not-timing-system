﻿using EMS.Judge.Common;
using EMS.Judge.Common.Services;
using System.Windows.Controls;
using System.Windows.Input;

namespace EMS.Judge.Views.Content.Import;

public partial class ImportView : UserControl, IView
{
    private readonly IInputHandler inputHandler;
    public ImportView(IInputHandler inputHandler) : this()
    {
        this.inputHandler = inputHandler;
        this.inputHandler = inputHandler;
    }
    public ImportView()
    {
        InitializeComponent();
    }

    public string RegionName { get; } = Regions.CONTENT_LEFT;

    public void HandleScroll(object sender, MouseWheelEventArgs args)
    {
        this.inputHandler.HandleScroll(sender, args);
    }
}
