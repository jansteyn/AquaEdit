using ReactiveUI;
using System;
using System.Reactive;
using System.Reflection;

namespace AquaEdit.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the About dialog
/// </summary>
public class AboutViewModel : ViewModelBase
{
    public string ApplicationName => "AquaEdit";
    public string Version { get; }
    public string Copyright { get; }
    public string Description => "High-Performance Text Editor for Large Files";
    public string LicenseInfo => "MIT License";
    public string GitHubUrl => "https://github.com/jansteyn/AquaEdit";

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewLicenseCommand { get; }

    public AboutViewModel()
    {
        // Get version from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        Version = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";
        Copyright = $"© {DateTime.Now.Year} AquaEdit Contributors";

        CloseCommand = ReactiveCommand.Create(
            () => { },
            outputScheduler: RxApp.MainThreadScheduler);

        OpenGitHubCommand = ReactiveCommand.Create(
            OpenGitHub,
            outputScheduler: RxApp.MainThreadScheduler);

        ViewLicenseCommand = ReactiveCommand.Create(
            ViewLicense,
            outputScheduler: RxApp.MainThreadScheduler);
    }

    private void OpenGitHub()
    {
        // Open browser to GitHub URL
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Handle error
        }
    }

    private void ViewLicense()
    {
        // Open license file or dialog
    }
}