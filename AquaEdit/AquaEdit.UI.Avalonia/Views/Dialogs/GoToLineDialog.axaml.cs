using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace AquaEdit.UI.Avalonia.Views.Dialogs;

public partial class GoToLineDialog : ReactiveWindow<GoToLineViewModel>
{
    public int? SelectedLineNumber { get; private set; }

    public GoToLineDialog()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Handle Go To command
            ViewModel?.GoToCommand.Subscribe(_ =>
            {
                SelectedLineNumber = ViewModel.LineNumber;
                Close(SelectedLineNumber);
            }).DisposeWith(disposables);

            // Handle Cancel command
            ViewModel?.CancelCommand.Subscribe(_ =>
            {
                SelectedLineNumber = null;
                Close();
            }).DisposeWith(disposables);
        });
    }
}