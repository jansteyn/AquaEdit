using AquaEdit.Core;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for a single editor document
/// </summary>
public class EditorViewModel : ViewModelBase, IDisposable
{
    private readonly TextBuffer _textBuffer;
    private readonly UndoRedoManager _undoRedoManager;
    private readonly SearchEngine _searchEngine;
    
    private string? _filePath;
    private string? _fileName;
    private bool _isDirty;
    private bool _isLoading;
    private int _loadingProgress;
    private int _lineCount;
    private int _currentLine;
    private int _currentColumn;
    private string _statusText;
    private ObservableCollection<string> _visibleLines;
    private int _firstVisibleLine;
    private int _visibleLineCount = 50;
    private CancellationTokenSource? _searchCancellation;

    public string? FilePath
    {
        get => _filePath;
        private set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public string? FileName
    {
        get => _fileName;
        private set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public int LoadingProgress
    {
        get => _loadingProgress;
        private set => this.RaiseAndSetIfChanged(ref _loadingProgress, value);
    }

    public int LineCount
    {
        get => _lineCount;
        private set => this.RaiseAndSetIfChanged(ref _lineCount, value);
    }

    public int CurrentLine
    {
        get => _currentLine;
        set => this.RaiseAndSetIfChanged(ref _currentLine, value);
    }

    public int CurrentColumn
    {
        get => _currentColumn;
        set => this.RaiseAndSetIfChanged(ref _currentColumn, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ObservableCollection<string> VisibleLines
    {
        get => _visibleLines;
        private set => this.RaiseAndSetIfChanged(ref _visibleLines, value);
    }

    public int FirstVisibleLine
    {
        get => _firstVisibleLine;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _firstVisibleLine, value) == value)
            {
                LoadVisibleLines();
            }
        }
    }

    // Reactive Commands
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<int, Unit> GoToLineCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    // Observables
    public IObservable<bool> CanUndo { get; }
    public IObservable<bool> CanRedo { get; }
    public IObservable<bool> CanSave { get; }

    public EditorViewModel()
    {
        _textBuffer = new TextBuffer();
        _undoRedoManager = new UndoRedoManager(_textBuffer);
        _searchEngine = new SearchEngine(_textBuffer);
        _statusText = "Ready";
        _visibleLines = new ObservableCollection<string>();

        // Set up reactive observables
        CanUndo = this.WhenAnyValue(x => x._undoRedoManager.CanUndo)
            .ObserveOn(RxApp.MainThreadScheduler);

        CanRedo = this.WhenAnyValue(x => x._undoRedoManager.CanRedo)
            .ObserveOn(RxApp.MainThreadScheduler);

        CanSave = this.WhenAnyValue(x => x.IsDirty)
            .ObserveOn(RxApp.MainThreadScheduler);

        // Initialize commands
        SaveCommand = ReactiveCommand.CreateFromTask(
            SaveAsync,
            CanSave,
            RxApp.MainThreadScheduler);

        SaveAsCommand = ReactiveCommand.CreateFromTask(
            SaveAsAsync,
            outputScheduler: RxApp.MainThreadScheduler);

        UndoCommand = ReactiveCommand.Create(
            Undo,
            CanUndo,
            RxApp.MainThreadScheduler);

        RedoCommand = ReactiveCommand.Create(
            Redo,
            CanRedo,
            RxApp.MainThreadScheduler);

        GoToLineCommand = ReactiveCommand.Create<int>(
            GoToLine,
            outputScheduler: RxApp.MainThreadScheduler);

        CloseCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        // Subscribe to property changes for status updates
        this.WhenAnyValue(
                x => x.CurrentLine,
                x => x.CurrentColumn,
                x => x.LineCount,
                (line, col, count) => $"Ln {line + 1}, Col {col + 1} | {count:N0} lines")
            .Subscribe(status => StatusText = status)
            .DisposeWith(Disposables);

        // Handle command errors
        SaveCommand.ThrownExceptions
            .Subscribe(ex => StatusText = $"Save failed: {ex.Message}")
            .DisposeWith(Disposables);

        UndoCommand.ThrownExceptions
            .Subscribe(ex => StatusText = $"Undo failed: {ex.Message}")
            .DisposeWith(Disposables);

        RedoCommand.ThrownExceptions
            .Subscribe(ex => StatusText = $"Redo failed: {ex.Message}")
            .DisposeWith(Disposables);
    }

    /// <summary>
    /// Opens a file asynchronously with progress reporting
    /// </summary>
    public async Task OpenFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            StatusText = $"Opening {System.IO.Path.GetFileName(filePath)}...";

            var progress = new Progress<int>(value => LoadingProgress = value);

            await _textBuffer.OpenFileAsync(filePath, progress, CancellationToken.None);

            FilePath = filePath;
            FileName = System.IO.Path.GetFileName(filePath);
            LineCount = _textBuffer.LineCount;
            IsDirty = false;

            LoadVisibleLines();
            StatusText = $"Opened {FileName} ({LineCount:N0} lines)";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open file: {ex.Message}";
            throw;
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// Saves the current file
    /// </summary>
    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            await SaveAsAsync();
            return;
        }

        try
        {
            StatusText = "Saving...";
            await _textBuffer.SaveAsync(FilePath, CancellationToken.None);
            IsDirty = false;
            StatusText = $"Saved {FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
            throw;
        }
    }

    /// <summary>
    /// Saves the file with a new path
    /// </summary>
    private async Task SaveAsAsync()
    {
        // This would be triggered by the UI layer providing a file path
        // For now, it's a placeholder
        await Task.CompletedTask;
    }

    /// <summary>
    /// Performs undo operation
    /// </summary>
    private void Undo()
    {
        _undoRedoManager.Undo();
        IsDirty = true;
        LoadVisibleLines();
        StatusText = "Undo";
    }

    /// <summary>
    /// Performs redo operation
    /// </summary>
    private void Redo()
    {
        _undoRedoManager.Redo();
        IsDirty = true;
        LoadVisibleLines();
        StatusText = "Redo";
    }

    /// <summary>
    /// Navigates to a specific line
    /// </summary>
    private void GoToLine(int lineNumber)
    {
        if (lineNumber >= 0 && lineNumber < LineCount)
        {
            CurrentLine = lineNumber;
            FirstVisibleLine = Math.Max(0, lineNumber - _visibleLineCount / 2);
            StatusText = $"Jumped to line {lineNumber + 1}";
        }
    }

    /// <summary>
    /// Loads visible lines based on current scroll position
    /// </summary>
    private void LoadVisibleLines()
    {
        if (_textBuffer.LineCount == 0)
            return;

        VisibleLines.Clear();
        var lines = _textBuffer.GetVisibleLines(FirstVisibleLine, _visibleLineCount);
        
        foreach (var line in lines)
        {
            VisibleLines.Add(line);
        }
    }

    /// <summary>
    /// Applies a text edit
    /// </summary>
    public void ApplyEdit(long offset, int length, string newText)
    {
        var patch = Patch.Replace(offset, length, newText);
        _textBuffer.ApplyEdit(patch);
        _undoRedoManager.Record(patch);
        IsDirty = true;
        LoadVisibleLines();
    }

    public void Dispose()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _textBuffer.Dispose();
        Disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}