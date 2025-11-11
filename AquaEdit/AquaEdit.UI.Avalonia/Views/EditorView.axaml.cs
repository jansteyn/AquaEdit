using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;
using ReactiveUI;

namespace AquaEdit.UI.Avalonia.Views;

public partial class EditorView : ReactiveUserControl<EditorViewModel>
{
    public EditorView()
    {
        InitializeComponent();
        
        this.WhenActivated(disposables =>
        {
            // Handle scroll events for virtualization
            // This would sync with FirstVisibleLine property
        });
    }
}