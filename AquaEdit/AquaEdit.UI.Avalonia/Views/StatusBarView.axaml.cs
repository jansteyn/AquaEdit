using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;

namespace AquaEdit.UI.Avalonia.Views;

public partial class StatusBarView : ReactiveUserControl<StatusBarViewModel>
{
    public StatusBarView()
    {
        InitializeComponent();
    }
}