using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace AquaEdit.UI.Avalonia.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; }

    protected CompositeDisposable Disposables { get; private set; }

    public ViewModelBase()
    {
        Activator = new ViewModelActivator();
        Disposables = new CompositeDisposable();

        this.WhenActivated(disposables =>
        {
            Disposables = disposables;
            OnActivated();
        });
    }

    /// <summary>
    /// Override this method to handle activation logic
    /// </summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>
    /// Override this method to handle deactivation logic
    /// </summary>
    protected virtual void OnDeactivated()
    {
    }
}
