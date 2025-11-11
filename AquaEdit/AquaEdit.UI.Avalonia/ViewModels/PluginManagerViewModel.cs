using AquaEdit.Core.Plugins;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for managing plugins
/// </summary>
public class PluginManagerViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private PluginItemViewModel? _selectedPlugin;
    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            FilterPlugins();
        }
    }

    public PluginItemViewModel? SelectedPlugin
    {
        get => _selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
    }

    public ObservableCollection<PluginItemViewModel> Plugins { get; }
    public ObservableCollection<PluginItemViewModel> FilteredPlugins { get; }

    // Commands
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> EnablePluginCommand { get; }
    public ReactiveCommand<Unit, Unit> DisablePluginCommand { get; }
    public ReactiveCommand<Unit, Unit> UninstallPluginCommand { get; }
    public ReactiveCommand<Unit, Unit> InstallPluginCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public IObservable<bool> HasSelectedPlugin { get; }

    public PluginManagerViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        Plugins = new ObservableCollection<PluginItemViewModel>();
        FilteredPlugins = new ObservableCollection<PluginItemViewModel>();

        HasSelectedPlugin = this.WhenAnyValue(x => x.SelectedPlugin)
            .Select(p => p != null)
            .ObserveOn(RxApp.MainThreadScheduler);

        RefreshCommand = ReactiveCommand.Create(
            LoadPlugins,
            outputScheduler: RxApp.MainThreadScheduler);

        EnablePluginCommand = ReactiveCommand.Create(
            EnablePlugin,
            HasSelectedPlugin,
            RxApp.MainThreadScheduler);

        DisablePluginCommand = ReactiveCommand.Create(
            DisablePlugin,
            HasSelectedPlugin,
            RxApp.MainThreadScheduler);

        UninstallPluginCommand = ReactiveCommand.Create(
            UninstallPlugin,
            HasSelectedPlugin,
            RxApp.MainThreadScheduler);

        InstallPluginCommand = ReactiveCommand.Create(
            InstallPlugin,
            outputScheduler: RxApp.MainThreadScheduler);

        CloseCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        LoadPlugins();
    }

    private void LoadPlugins()
    {
        Plugins.Clear();

        foreach (var plugin in _pluginManager.LoadedPlugins)
        {
            var viewModel = new PluginItemViewModel
            {
                Name = plugin.Name,
                Version = plugin.Version,
                Description = plugin.Description,
                IsEnabled = true,
                Plugin = plugin
            };

            Plugins.Add(viewModel);
        }

        FilterPlugins();
    }

    private void FilterPlugins()
    {
        FilteredPlugins.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Plugins
            : Plugins.Where(p => 
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var plugin in filtered)
        {
            FilteredPlugins.Add(plugin);
        }
    }

    private void EnablePlugin()
    {
        if (SelectedPlugin != null)
        {
            SelectedPlugin.IsEnabled = true;
        }
    }

    private void DisablePlugin()
    {
        if (SelectedPlugin != null)
        {
            SelectedPlugin.IsEnabled = false;
        }
    }

    private void UninstallPlugin()
    {
        if (SelectedPlugin != null)
        {
            Plugins.Remove(SelectedPlugin);
            FilterPlugins();
        }
    }

    private void InstallPlugin()
    {
        // Open file picker to install new plugin
    }
}

/// <summary>
/// ViewModel representing a single plugin
/// </summary>
public class PluginItemViewModel : ViewModelBase
{
    private bool _isEnabled;

    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IEditorPlugin? Plugin { get; set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public string StatusText => IsEnabled ? "Enabled" : "Disabled";
}