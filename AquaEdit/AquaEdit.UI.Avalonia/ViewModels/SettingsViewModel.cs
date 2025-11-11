using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Text;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for application settings dialog
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private string _fontFamily = "Consolas";
    private int _fontSize = 14;
    private int _tabSize = 4;
    private bool _insertSpaces = true;
    private bool _wordWrap;
    private bool _showLineNumbers = true;
    private bool _showWhitespace;
    private string _theme = "System";
    private string _defaultEncoding = "UTF-8";
    private bool _autoSave;
    private int _autoSaveInterval = 5;
    private int _windowCacheSize = 10;
    private long _windowSize = 16;

    public string FontFamily
    {
        get => _fontFamily;
        set => this.RaiseAndSetIfChanged(ref _fontFamily, value);
    }

    public int FontSize
    {
        get => _fontSize;
        set => this.RaiseAndSetIfChanged(ref _fontSize, value);
    }

    public int TabSize
    {
        get => _tabSize;
        set => this.RaiseAndSetIfChanged(ref _tabSize, value);
    }

    public bool InsertSpaces
    {
        get => _insertSpaces;
        set => this.RaiseAndSetIfChanged(ref _insertSpaces, value);
    }

    public bool WordWrap
    {
        get => _wordWrap;
        set => this.RaiseAndSetIfChanged(ref _wordWrap, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set => this.RaiseAndSetIfChanged(ref _showLineNumbers, value);
    }

    public bool ShowWhitespace
    {
        get => _showWhitespace;
        set => this.RaiseAndSetIfChanged(ref _showWhitespace, value);
    }

    public string Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    public string DefaultEncoding
    {
        get => _defaultEncoding;
        set => this.RaiseAndSetIfChanged(ref _defaultEncoding, value);
    }

    public bool AutoSave
    {
        get => _autoSave;
        set => this.RaiseAndSetIfChanged(ref _autoSave, value);
    }

    public int AutoSaveInterval
    {
        get => _autoSaveInterval;
        set => this.RaiseAndSetIfChanged(ref _autoSaveInterval, value);
    }

    public int WindowCacheSize
    {
        get => _windowCacheSize;
        set => this.RaiseAndSetIfChanged(ref _windowCacheSize, value);
    }

    public long WindowSize
    {
        get => _windowSize;
        set => this.RaiseAndSetIfChanged(ref _windowSize, value);
    }

    public ObservableCollection<string> AvailableFonts { get; }
    public ObservableCollection<string> AvailableThemes { get; }
    public ObservableCollection<string> AvailableEncodings { get; }

    // Commands
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }

    public SettingsViewModel()
    {
        AvailableFonts = new ObservableCollection<string>
        {
            "Consolas",
            "Courier New",
            "Monaco",
            "Fira Code",
            "Source Code Pro",
            "JetBrains Mono"
        };

        AvailableThemes = new ObservableCollection<string>
        {
            "System",
            "Light",
            "Dark"
        };

        AvailableEncodings = new ObservableCollection<string>
        {
            "UTF-8",
            "UTF-16",
            "UTF-32",
            "ASCII",
            "ISO-8859-1"
        };

        ApplyCommand = ReactiveCommand.Create(
            Apply,
            outputScheduler: RxApp.MainThreadScheduler);

        OkCommand = ReactiveCommand.Create(
            Ok,
            outputScheduler: RxApp.MainThreadScheduler);

        CancelCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        ResetToDefaultsCommand = ReactiveCommand.Create(
            ResetToDefaults,
            outputScheduler: RxApp.MainThreadScheduler);

        LoadSettings();
    }

    /// <summary>
    /// Loads settings from configuration
    /// </summary>
    private void LoadSettings()
    {
        // Would load from config file
        // For now, using defaults
    }

    /// <summary>
    /// Applies settings without closing dialog
    /// </summary>
    private void Apply()
    {
        SaveSettings();
    }

    /// <summary>
    /// Applies settings and closes dialog
    /// </summary>
    private void Ok()
    {
        SaveSettings();
    }

    /// <summary>
    /// Saves settings to configuration
    /// </summary>
    private void SaveSettings()
    {
        // Would save to config file
        // For now, placeholder
    }

    /// <summary>
    /// Resets all settings to defaults
    /// </summary>
    private void ResetToDefaults()
    {
        FontFamily = "Consolas";
        FontSize = 14;
        TabSize = 4;
        InsertSpaces = true;
        WordWrap = false;
        ShowLineNumbers = true;
        ShowWhitespace = false;
        Theme = "System";
        DefaultEncoding = "UTF-8";
        AutoSave = false;
        AutoSaveInterval = 5;
        WindowCacheSize = 10;
        WindowSize = 16;
    }
}