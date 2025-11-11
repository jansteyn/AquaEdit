using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace AquaEdit.UI.Avalonia.Views.Dialogs;

public partial class AboutDialog : ReactiveWindow<AboutViewModel>
{
    public AboutDialog()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            ViewModel?.CloseCommand.Subscribe(_ => Close())
                .DisposeWith(disposables);
        });
    }
}