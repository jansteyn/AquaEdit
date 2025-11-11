using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AquaEdit.UI.Avalonia.ViewModels;
using ReactiveUI;
using System;

namespace AquaEdit.UI.Avalonia.Views;

public partial class SearchView : ReactiveUserControl<SearchViewModel>
{
    public SearchView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Focus search box when activated
            this.FindControl<TextBox>("SearchTextBox")?.Focus();
        });
    }
}