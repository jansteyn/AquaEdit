using System.IO.MemoryMappedFiles;
using System.Text;

namespace AquaEdit.Core;

/// <summary>
/// Handles async file operations with chunking and memory-mapped file support for large files
/// </summary>
public class FileManager : IDisposable
{
    private const int BufferSize = 8192;
    private const long DefaultWindowSize = 16 * 1024 * 1024; // 16 MB windows

    private MemoryMappedFile? _mmf;
    private string? _filePath;
    private long _fileSize;
    private readonly LRUCache<long, FileWindow> _windowCache;

    public string? FilePath => _filePath;
    public long FileSize => _fileSize;
    public bool IsOpen => _mmf != null;

    public FileManager(int cacheSize = 10)
    {
        _windowCache = new LRUCache<long, FileWindow>(cacheSize);
    }

    /// <summary>
    /// Opens a file using memory-mapped files for efficient large file access
    /// </summary>
    public void OpenFile(string filePath, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
    {
        Close();

        _filePath = filePath;
        var fileInfo = new FileInfo(filePath);
        _fileSize = fileInfo.Length;

        _mmf = MemoryMappedFile.CreateFromFile(
            filePath,
            mode,
            null,
            _fileSize,
            MemoryMappedFileAccess.Read);
    }

    /// <summary>
    /// Gets or creates a windowed view into the file
    /// </summary>
    public FileWindow GetWindow(long offset, long? size = null)
    {
        if (_mmf == null)
            throw new InvalidOperationException("No file is currently open.");

        var windowSize = size ?? DefaultWindowSize;
        var alignedOffset = AlignToPageBoundary(offset);

        // Check cache first
        if (_windowCache.TryGet(alignedOffset, out var cachedWindow))
            return cachedWindow;

        // Create new window
        var actualSize = Math.Min(windowSize, _fileSize - alignedOffset);
        var window = new FileWindow(_mmf, alignedOffset, actualSize);
        _windowCache.Add(alignedOffset, window);

        return window;
    }

    /// <summary>
    /// Reads a range of bytes from the file
    /// </summary>
    public byte[] ReadBytes(long offset, int count)
    {
        var window = GetWindow(offset, count);
        return window.ReadRange(offset - window.Offset, count);
    }

    /// <summary>
    /// Legacy method: Load entire file for small files (fallback)
    /// </summary>
    public async Task<string[]> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lines.Add(line);
        }
        
        return lines.ToArray();
    }

    /// <summary>
    /// Saves content to file (used when saving edits)
    /// </summary>
    public async Task SaveFileAsync(string filePath, IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        using var writer = new StreamWriter(fileStream, Encoding.UTF8);
        
        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Aligns offset to 4KB page boundary for optimal performance
    /// </summary>
    private static long AlignToPageBoundary(long offset)
    {
        const long pageSize = 4096;
        return (offset / pageSize) * pageSize;
    }

    public void Close()
    {
        _windowCache.Clear();
        _mmf?.Dispose();
        _mmf = null;
        _filePath = null;
        _fileSize = 0;
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}