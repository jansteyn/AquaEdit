using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace AquaEdit.UI.Avalonia.Views.Dialogs;

public partial class SettingsDialog : ReactiveWindow<SettingsViewModel>
{
    public SettingsDialog()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Handle OK command
            ViewModel?.OkCommand.Subscribe(_ => Close())
                .DisposeWith(disposables);

            // Handle Cancel command
            ViewModel?.CancelCommand.Subscribe(_ => Close())
                .DisposeWith(disposables);
        });
    }
}