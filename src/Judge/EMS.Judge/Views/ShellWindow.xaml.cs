﻿using EMS.Judge.Application.Services;
using System.Windows;

namespace EMS.Judge.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ISettings settings) : this()
    {
        this.Version.Text = settings.Version;
    }
    
    public ShellWindow()
    {
        InitializeComponent();
    }
}
