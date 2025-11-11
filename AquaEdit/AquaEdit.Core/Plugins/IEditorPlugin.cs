namespace AquaEdit.Core.Plugins;

/// <summary>
/// Base interface for AquaEdit plugins
/// </summary>
public interface IEditorPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    
    void Initialize(IPluginHost host);
    void OnDocumentOpened(TextBuffer textBuffer);
    void OnDocumentClosed();
}

/// <summary>
/// Host interface provided to plugins for interacting with the editor
/// </summary>
public interface IPluginHost
{
    TextBuffer? ActiveBuffer { get; }
    void RegisterCommand(string commandName, Action<TextBuffer> action);
    void ShowNotification(string message);
    void LogMessage(string message);
}