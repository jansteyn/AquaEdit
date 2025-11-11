using AquaEdit.Core.Plugins;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using AquaEdit.UI.Avalonia.Views.Dialogs;
using Avalonia.Controls;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// Main application ViewModel managing documents and application state
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private EditorViewModel? _activeEditor;
    private SearchViewModel? _searchViewModel;
    private bool _isSearchVisible;
    private string _title = "AquaEdit";
    private string _statusMessage = "Ready";

    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public EditorViewModel? ActiveEditor
    {
        get => _activeEditor;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _activeEditor, value) != null)
            {
                UpdateTitle();
                
                if (_searchViewModel != null && value != null)
                {
                    _searchViewModel = new SearchViewModel(value);
                }
            }
        }
    }

    public SearchViewModel? SearchViewModel
    {
        get => _searchViewModel;
        private set => this.RaiseAndSetIfChanged(ref _searchViewModel, value);
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => this.RaiseAndSetIfChanged(ref _isSearchVisible, value);
    }

    public ObservableCollection<EditorViewModel> OpenDocuments { get; }
    public ObservableCollection<string> RecentFiles { get; }

    // Application Commands
    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    
    // Edit Commands
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }
    
    // View Commands
    public ReactiveCommand<Unit, Unit> ToggleSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToLineCommand { get; }
    
    // Help Commands
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }
    public ReactiveCommand<Unit, Unit> SettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> PluginManagerCommand { get; }

    public MainViewModel()
    {
        _pluginManager = new PluginManager();
        OpenDocuments = new ObservableCollection<EditorViewModel>();
        RecentFiles = new ObservableCollection<string>();

        // Initialize commands
        NewFileCommand = ReactiveCommand.CreateFromTask(
            NewFileAsync,
            outputScheduler: RxApp.MainThreadScheduler);

        OpenFileCommand = ReactiveCommand.CreateFromTask(
            OpenFileAsync,
            outputScheduler: RxApp.MainThreadScheduler);

        SaveCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                if (ActiveEditor != null)
                {
                    await ActiveEditor.SaveCommand.Execute();
                }
            },
            this.WhenAnyValue(x => x.ActiveEditor, x => x.ActiveEditor!.IsDirty, 
                (editor, dirty) => editor != null && dirty),
            RxApp.MainThreadScheduler);

        SaveAsCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                if (ActiveEditor != null)
                {
                    await ActiveEditor.SaveAsCommand.Execute().ToTask();
                }
            },
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        SaveAllCommand = ReactiveCommand.CreateFromTask(
            SaveAllAsync,
            this.WhenAnyValue(x => x.OpenDocuments.Count).Select(count => count > 0),
            RxApp.MainThreadScheduler);

        CloseFileCommand = ReactiveCommand.Create(
            CloseFile,
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        ExitCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        // Edit commands delegate to active editor
        UndoCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                if (ActiveEditor != null)
                {
                    await ActiveEditor.UndoCommand.Execute().ToTask();
                }
            },
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        RedoCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                if (ActiveEditor != null)
                {
                    await ActiveEditor.RedoCommand.Execute().ToTask();
                }
            },
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        CutCommand = ReactiveCommand.Create(() => { }, outputScheduler: RxApp.MainThreadScheduler);
        CopyCommand = ReactiveCommand.Create(() => { }, outputScheduler: RxApp.MainThreadScheduler);
        PasteCommand = ReactiveCommand.Create(() => { }, outputScheduler: RxApp.MainThreadScheduler);

        // View commands
        ToggleSearchCommand = ReactiveCommand.Create(
            ToggleSearch,
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        GoToLineCommand = ReactiveCommand.CreateFromTask(
            ShowGoToLineDialog,
            this.WhenAnyValue(x => x.ActiveEditor).Select(editor => editor != null),
            RxApp.MainThreadScheduler);

        AboutCommand = ReactiveCommand.CreateFromTask(
            ShowAboutDialog,
            outputScheduler: RxApp.MainThreadScheduler);

        SettingsCommand = ReactiveCommand.CreateFromTask(
            ShowSettingsDialog,
            outputScheduler: RxApp.MainThreadScheduler);

        PluginManagerCommand = ReactiveCommand.CreateFromTask(
            ShowPluginManagerDialog,
            outputScheduler: RxApp.MainThreadScheduler);

        // Subscribe to active editor status updates
        this.WhenAnyValue(x => x.ActiveEditor!.StatusText)
            .Where(x => ActiveEditor != null)
            .Subscribe(status => StatusMessage = status ?? "Ready")
            .DisposeWith(Disposables);

        // Load plugins
        LoadPlugins();
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        
        // Load recent files
        LoadRecentFiles();
    }

    /// <summary>
    /// Creates a new empty document
    /// </summary>
    private async Task NewFileAsync()
    {
        var editor = new EditorViewModel();
        OpenDocuments.Add(editor);
        ActiveEditor = editor;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Opens a file dialog and loads the selected file
    /// </summary>
    private async Task OpenFileAsync()
    {
        // This would be triggered by the UI layer providing a file path
        // For demonstration, we'll create a placeholder
        await Task.CompletedTask;
    }

    /// <summary>
    /// Opens a specific file by path
    /// </summary>
    public async Task OpenFileAsync(string filePath)
    {
        try
        {
            // Check if file is already open
            foreach (var doc in OpenDocuments)
            {
                if (doc.FilePath == filePath)
                {
                    ActiveEditor = doc;
                    return;
                }
            }

            var editor = new EditorViewModel();
            await editor.OpenFileAsync(filePath);
            
            OpenDocuments.Add(editor);
            ActiveEditor = editor;

            AddRecentFile(filePath);
            
            // Notify plugins
            var textBuffer = typeof(EditorViewModel)
                .GetField("_textBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(editor) as AquaEdit.Core.TextBuffer;
            
            if (textBuffer != null)
            {
                _pluginManager.NotifyDocumentOpened(textBuffer);
            }

            StatusMessage = $"Opened {editor.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open file: {ex.Message}";
        }
    }

    /// <summary>
    /// Saves all open documents
    /// </summary>
    private async Task SaveAllAsync()
    {
        foreach (var doc in OpenDocuments)
        {
            if (doc.IsDirty)
            {
                await doc.SaveCommand.Execute();
            }
        }
        StatusMessage = "All files saved";
    }

    /// <summary>
    /// Closes the active document
    /// </summary>
    private void CloseFile()
    {
        if (ActiveEditor == null)
            return;

        if (ActiveEditor.IsDirty)
        {
            // Would show save dialog in real implementation
        }

        OpenDocuments.Remove(ActiveEditor);
        ActiveEditor.Dispose();
        _pluginManager.NotifyDocumentClosed();

        ActiveEditor = OpenDocuments.Count > 0 ? OpenDocuments[^1] : null;
        StatusMessage = "File closed";
    }

    /// <summary>
    /// Toggles search panel visibility
    /// </summary>
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;

        if (IsSearchVisible && ActiveEditor != null && SearchViewModel == null)
        {
            SearchViewModel = new SearchViewModel(ActiveEditor);
        }
    }

    /// <summary>
    /// Updates the window title based on active document
    /// </summary>
    private void UpdateTitle()
    {
        Title = ActiveEditor != null
            ? $"{ActiveEditor.FileName}{(ActiveEditor.IsDirty ? "*" : "")} - AquaEdit"
            : "AquaEdit";
    }

    /// <summary>
    /// Loads plugins from the plugins directory
    /// </summary>
    private void LoadPlugins()
    {
        var pluginsPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Plugins");

        _pluginManager.DiscoverPlugins(pluginsPath);
        StatusMessage = $"Loaded {_pluginManager.LoadedPlugins.Count} plugin(s)";
    }

    /// <summary>
    /// Loads recent files list
    /// </summary>
    private void LoadRecentFiles()
    {
        // Would load from config file
        // For now, placeholder
    }

    /// <summary>
    /// Adds a file to the recent files list
    /// </summary>
    private void AddRecentFile(string filePath)
    {
        RecentFiles.Remove(filePath); // Remove if exists
        RecentFiles.Insert(0, filePath); // Add to top

        // Keep only last 10
        while (RecentFiles.Count > 10)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }

    /// <summary>
    /// Shows the settings dialog
    /// </summary>
    private async Task ShowSettingsDialog()
    {
        var dialog = new SettingsDialog
        {
            DataContext = new SettingsViewModel()
        };

        await dialog.ShowDialog((Window)null!); // Would pass actual parent window
    }

    /// <summary>
    /// Shows the about dialog
    /// </summary>
    private async Task ShowAboutDialog()
    {
        var dialog = new AboutDialog
        {
            DataContext = new AboutViewModel()
        };

        await dialog.ShowDialog((Window)null!); // Would pass actual parent window
    }

    /// <summary>
    /// Shows the go to line dialog
    /// </summary>
    private async Task ShowGoToLineDialog()
    {
        if (ActiveEditor == null)
            return;

        var dialog = new GoToLineDialog
        {
            DataContext = new GoToLineViewModel(ActiveEditor.LineCount)
        };

        var result = await dialog.ShowDialog<int?>((Window)null!); // Would pass actual parent window
        
        if (result.HasValue)
        {
            await ActiveEditor.GoToLineCommand.Execute(result.Value);
        }
    }

    /// <summary>
    /// Shows the plugin manager dialog
    /// </summary>
    private async Task ShowPluginManagerDialog()
    {
        var dialog = new PluginManagerDialog
        {
            DataContext = new PluginManagerViewModel(_pluginManager)
        };

        await dialog.ShowDialog((Window)null!); // Would pass actual parent window
    }
}
