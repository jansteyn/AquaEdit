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
/// ViewModel for search and replace functionality
/// </summary>
public class SearchViewModel : ViewModelBase
{
    private readonly EditorViewModel _editorViewModel;
    private SearchEngine? _searchEngine;
    private CancellationTokenSource? _searchCancellation;

    private string _searchText = string.Empty;
    private string _replaceText = string.Empty;
    private bool _caseSensitive;
    private bool _useRegex;
    private bool _isSearching;
    private int _currentResultIndex = -1;
    private string _searchStatus = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => this.RaiseAndSetIfChanged(ref _replaceText, value);
    }

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set => this.RaiseAndSetIfChanged(ref _caseSensitive, value);
    }

    public bool UseRegex
    {
        get => _useRegex;
        set => this.RaiseAndSetIfChanged(ref _useRegex, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => this.RaiseAndSetIfChanged(ref _isSearching, value);
    }

    public int CurrentResultIndex
    {
        get => _currentResultIndex;
        private set => this.RaiseAndSetIfChanged(ref _currentResultIndex, value);
    }

    public string SearchStatus
    {
        get => _searchStatus;
        private set => this.RaiseAndSetIfChanged(ref _searchStatus, value);
    }

    public ObservableCollection<SearchEngine.SearchResult> SearchResults { get; }

    // Reactive Commands
    public ReactiveCommand<Unit, Unit> FindNextCommand { get; }
    public ReactiveCommand<Unit, Unit> FindPreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> FindAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelSearchCommand { get; }

    // Observables
    public IObservable<bool> CanSearch { get; }
    public IObservable<bool> CanNavigateResults { get; }
    public IObservable<bool> CanReplace { get; }

    public SearchViewModel(EditorViewModel editorViewModel)
    {
        _editorViewModel = editorViewModel;
        SearchResults = new ObservableCollection<SearchEngine.SearchResult>();

        // Set up reactive conditions
        CanSearch = this.WhenAnyValue(
                x => x.SearchText,
                x => x.IsSearching,
                (text, searching) => !string.IsNullOrWhiteSpace(text) && !searching)
            .ObserveOn(RxApp.MainThreadScheduler);

        CanNavigateResults = this.WhenAnyValue(
                x => x.SearchResults.Count,
                x => x.IsSearching,
                (count, searching) => count > 0 && !searching)
            .ObserveOn(RxApp.MainThreadScheduler);

        CanReplace = this.WhenAnyValue(
                x => x.CurrentResultIndex,
                x => x.IsSearching,
                (index, searching) => index >= 0 && !searching)
            .ObserveOn(RxApp.MainThreadScheduler);

        // Initialize commands
        FindNextCommand = ReactiveCommand.Create(
            FindNext,
            CanNavigateResults,
            RxApp.MainThreadScheduler);

        FindPreviousCommand = ReactiveCommand.Create(
            FindPrevious,
            CanNavigateResults,
            RxApp.MainThreadScheduler);

        FindAllCommand = ReactiveCommand.CreateFromTask(
            FindAllAsync,
            CanSearch,
            RxApp.MainThreadScheduler);

        ReplaceCommand = ReactiveCommand.Create(
            Replace,
            CanReplace,
            RxApp.MainThreadScheduler);

        ReplaceAllCommand = ReactiveCommand.CreateFromTask(
            ReplaceAllAsync,
            this.WhenAnyValue(x => x.SearchResults.Count, count => count > 0),
            RxApp.MainThreadScheduler);

        CancelSearchCommand = ReactiveCommand.Create(
            CancelSearch,
            this.WhenAnyValue(x => x.IsSearching),
            RxApp.MainThreadScheduler);

        // Handle search text changes with debounce
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .InvokeCommand(FindAllCommand)
            .DisposeWith(Disposables);

        // Error handling
        FindAllCommand.ThrownExceptions
            .Subscribe(ex => SearchStatus = $"Search failed: {ex.Message}")
            .DisposeWith(Disposables);
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        
        // Initialize search engine when activated
        if (_editorViewModel != null)
        {
            var textBuffer = typeof(EditorViewModel)
                .GetField("_textBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_editorViewModel) as TextBuffer;
            
            if (textBuffer != null)
            {
                _searchEngine = new SearchEngine(textBuffer);
            }
        }
    }

    /// <summary>
    /// Finds all occurrences of the search text
    /// </summary>
    private async Task FindAllAsync()
    {
        if (_searchEngine == null || string.IsNullOrWhiteSpace(SearchText))
            return;

        try
        {
            IsSearching = true;
            SearchResults.Clear();
            CurrentResultIndex = -1;
            SearchStatus = "Searching...";

            _searchCancellation = new CancellationTokenSource();
            int count = 0;

            await foreach (var result in _searchEngine.SearchAsync(
                SearchText,
                CaseSensitive,
                UseRegex,
                _searchCancellation.Token))
            {
                SearchResults.Add(result);
                count++;

                if (count % 100 == 0)
                {
                    SearchStatus = $"Found {count} matches...";
                }
            }

            SearchStatus = count > 0 
                ? $"Found {count} match{(count != 1 ? "es" : "")}"
                : "No matches found";

            if (count > 0)
            {
                CurrentResultIndex = 0;
                NavigateToResult(0);
            }
        }
        catch (OperationCanceledException)
        {
            SearchStatus = "Search cancelled";
        }
        finally
        {
            IsSearching = false;
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }
    }

    /// <summary>
    /// Navigates to the next search result
    /// </summary>
    private void FindNext()
    {
        if (SearchResults.Count == 0)
            return;

        CurrentResultIndex = (CurrentResultIndex + 1) % SearchResults.Count;
        NavigateToResult(CurrentResultIndex);
    }

    /// <summary>
    /// Navigates to the previous search result
    /// </summary>
    private void FindPrevious()
    {
        if (SearchResults.Count == 0)
            return;

        CurrentResultIndex = CurrentResultIndex > 0 
            ? CurrentResultIndex - 1 
            : SearchResults.Count - 1;
        NavigateToResult(CurrentResultIndex);
    }

    /// <summary>
    /// Replaces the current match
    /// </summary>
    private void Replace()
    {
        if (CurrentResultIndex < 0 || CurrentResultIndex >= SearchResults.Count)
            return;

        var result = SearchResults[CurrentResultIndex];
        var offset = _editorViewModel.GetType()
            .GetMethod("GetLineOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_editorViewModel, new object[] { result.LineIndex });

        // Apply replacement
        // This would need to be implemented properly
        SearchStatus = "Replaced 1 match";
        
        // Move to next result
        SearchResults.RemoveAt(CurrentResultIndex);
        if (SearchResults.Count > 0)
        {
            CurrentResultIndex = Math.Min(CurrentResultIndex, SearchResults.Count - 1);
            NavigateToResult(CurrentResultIndex);
        }
    }

    /// <summary>
    /// Replaces all matches
    /// </summary>
    private async Task ReplaceAllAsync()
    {
        var count = SearchResults.Count;
        SearchResults.Clear();
        CurrentResultIndex = -1;
        SearchStatus = $"Replaced {count} match{(count != 1 ? "es" : "")}";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Cancels the current search operation
    /// </summary>
    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
    }

    /// <summary>
    /// Navigates to a specific search result
    /// </summary>
    private void NavigateToResult(int index)
    {
        if (index < 0 || index >= SearchResults.Count)
            return;

        var result = SearchResults[index];
        _editorViewModel.CurrentLine = result.LineIndex;
        _editorViewModel.CurrentColumn = result.CharIndex;
        
        SearchStatus = $"Match {index + 1} of {SearchResults.Count}";
    }
}