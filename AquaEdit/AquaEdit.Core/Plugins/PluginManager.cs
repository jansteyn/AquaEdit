using System.Reflection;

namespace AquaEdit.Core.Plugins;

/// <summary>
/// Discovers and manages editor plugins
/// </summary>
public class PluginManager : IPluginHost
{
    private readonly List<IEditorPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, Action<TextBuffer>> _commands = new();
    private TextBuffer? _activeBuffer;

    public IReadOnlyList<IEditorPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();
    public TextBuffer? ActiveBuffer => _activeBuffer;

    /// <summary>
    /// Discovers plugins from a directory
    /// </summary>
    public void DiscoverPlugins(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
            return;

        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");

        foreach (var file in pluginFiles)
        {
            try
            {
                LoadPlugin(file);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load plugin from {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Loads a single plugin assembly
    /// </summary>
    private void LoadPlugin(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IEditorPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IEditorPlugin plugin)
            {
                plugin.Initialize(this);
                _loadedPlugins.Add(plugin);
                LogMessage($"Loaded plugin: {plugin.Name} v{plugin.Version}");
            }
        }
    }

    /// <summary>
    /// Notifies all plugins when a document is opened
    /// </summary>
    public void NotifyDocumentOpened(TextBuffer textBuffer)
    {
        _activeBuffer = textBuffer;
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                plugin.OnDocumentOpened(textBuffer);
            }
            catch (Exception ex)
            {
                LogMessage($"Plugin {plugin.Name} error on document opened: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Notifies all plugins when a document is closed
    /// </summary>
    public void NotifyDocumentClosed()
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                plugin.OnDocumentClosed();
            }
            catch (Exception ex)
            {
                LogMessage($"Plugin {plugin.Name} error on document closed: {ex.Message}");
            }
        }
        _activeBuffer = null;
    }

    // IPluginHost implementation
    public void RegisterCommand(string commandName, Action<TextBuffer> action)
    {
        _commands[commandName] = action;
        LogMessage($"Registered command: {commandName}");
    }

    public void ShowNotification(string message)
    {
        // This would be hooked up to the UI layer
        Console.WriteLine($"[Notification] {message}");
    }

    public void LogMessage(string message)
    {
        // This would be hooked up to a logging system
        Console.WriteLine($"[PluginManager] {message}");
    }

    /// <summary>
    /// Executes a registered command
    /// </summary>
    public void ExecuteCommand(string commandName)
    {
        if (_activeBuffer != null && _commands.TryGetValue(commandName, out var action))
        {
            action(_activeBuffer);
        }
    }
}