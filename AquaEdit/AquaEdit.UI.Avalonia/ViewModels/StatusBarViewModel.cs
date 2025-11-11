using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the status bar
/// </summary>
public class StatusBarViewModel : ViewModelBase
{
    private readonly EditorViewModel? _editorViewModel;
    
    private string _position = "Ln 1, Col 1";
    private string _encoding = "UTF-8";
    private string _lineEnding = "CRLF";
    private string _fileSize = "";
    private string _message = "Ready";

    public string Position
    {
        get => _position;
        private set => this.RaiseAndSetIfChanged(ref _position, value);
    }

    public string Encoding
    {
        get => _encoding;
        set => this.RaiseAndSetIfChanged(ref _encoding, value);
    }

    public string LineEnding
    {
        get => _lineEnding;
        set => this.RaiseAndSetIfChanged(ref _lineEnding, value);
    }

    public string FileSize
    {
        get => _fileSize;
        private set => this.RaiseAndSetIfChanged(ref _fileSize, value);
    }

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public StatusBarViewModel(EditorViewModel? editorViewModel = null)
    {
        _editorViewModel = editorViewModel;

        if (_editorViewModel != null)
        {
            // Subscribe to editor position changes
            _editorViewModel.WhenAnyValue(
                    x => x.CurrentLine,
                    x => x.CurrentColumn,
                    (line, col) => $"Ln {line + 1}, Col {col + 1}")
                .Subscribe(pos => Position = pos)
                .DisposeWith(Disposables);

            // Subscribe to line count for file size display
            _editorViewModel.WhenAnyValue(x => x.LineCount)
                .Subscribe(count => FileSize = $"{count:N0} lines")
                .DisposeWith(Disposables);

            // Subscribe to status text
            _editorViewModel.WhenAnyValue(x => x.StatusText)
                .Subscribe(status => Message = status ?? "Ready")
                .DisposeWith(Disposables);
        }
    }
}