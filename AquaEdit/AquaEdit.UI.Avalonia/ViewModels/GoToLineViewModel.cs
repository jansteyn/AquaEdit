using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Go To Line dialog
/// </summary>
public class GoToLineViewModel : ViewModelBase
{
    private int _lineNumber = 1;
    private int _maxLineNumber;
    private string _errorMessage = string.Empty;

    public int LineNumber
    {
        get => _lineNumber;
        set
        {
            this.RaiseAndSetIfChanged(ref _lineNumber, value);
            ValidateLineNumber();
        }
    }

    public int MaxLineNumber
    {
        get => _maxLineNumber;
        set => this.RaiseAndSetIfChanged(ref _maxLineNumber, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string PromptText => $"Enter line number (1 - {MaxLineNumber:N0}):";

    // Commands
    public ReactiveCommand<Unit, Unit> GoToCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    // Observable for valid line number
    public IObservable<bool> CanGoTo { get; }

    public GoToLineViewModel(int maxLineNumber)
    {
        MaxLineNumber = maxLineNumber;

        CanGoTo = this.WhenAnyValue(
                x => x.LineNumber,
                x => x.ErrorMessage,
                (line, error) => line > 0 && line <= MaxLineNumber && string.IsNullOrEmpty(error))
            .ObserveOn(RxApp.MainThreadScheduler);

        GoToCommand = ReactiveCommand.Create(
            () => { },
            CanGoTo,
            RxApp.MainThreadScheduler);

        CancelCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        // Subscribe to max line number changes to update prompt
        this.WhenAnyValue(x => x.MaxLineNumber)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PromptText)))
            .DisposeWith(Disposables);
    }

    private void ValidateLineNumber()
    {
        if (LineNumber < 1)
        {
            ErrorMessage = "Line number must be greater than 0.";
        }
        else if (LineNumber > MaxLineNumber)
        {
            ErrorMessage = $"Line number cannot exceed {MaxLineNumber:N0}.";
        }
        else
        {
            ErrorMessage = string.Empty;
        }
    }
}