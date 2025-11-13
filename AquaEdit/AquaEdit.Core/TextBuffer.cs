using System.Text;

namespace AquaEdit.Core;

/// <summary>
/// Provides a logical line-based view of file content with edit overlay support
/// </summary>
public class TextBuffer : IDisposable
{
    private readonly FileManager _fileManager;
    private readonly LineIndexer _lineIndexer;
    private readonly EditOverlay _editOverlay;
    private readonly Encoding _encoding;

    public int LineCount => _lineIndexer.LineCount;
    public bool IsIndexed => _lineIndexer.IsIndexed;
    public string? FilePath => _fileManager.FilePath;

    public TextBuffer(Encoding? encoding = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _fileManager = new FileManager();
        _lineIndexer = new LineIndexer(_fileManager, _encoding);
        _editOverlay = new EditOverlay();
    }

    /// <summary>
    /// Opens a file and builds the line index
    /// </summary>
    public async Task OpenFileAsync(string filePath, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        try
        {
            // Close any previously opened file
            _fileManager.Close();
            _editOverlay.Clear();

            // Open the new file
            _fileManager.OpenFile(filePath, FileMode.Open, FileAccess.Read);
            
            // Build the line index
            await _lineIndexer.BuildIndexAsync(progress, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access denied to file: {filePath}", ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"I/O error while opening file: {filePath}", ex);
        }
        catch (OperationCanceledException)
        {
            // Clean up on cancellation
            _fileManager.Close();
            throw;
        }
        catch (Exception ex)
        {
            // Clean up on any error
            _fileManager.Close();
            throw new IOException($"Failed to open file: {filePath}", ex);
        }
    }

    /// <summary>
    /// Reads a specific line from the file
    /// </summary>
    public string ReadLine(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= LineCount)
            return string.Empty;

        var offset = _lineIndexer.GetLineOffset(lineNumber);
        var length = _lineIndexer.GetLineLength(lineNumber);

        if (length == 0)
            return string.Empty;

        var bytes = _fileManager.ReadBytes(offset, length);
        var text = _encoding.GetString(bytes);

        // Apply any edits from the overlay
        return _editOverlay.ApplyEdits(text, offset);
    }

    /// <summary>
    /// Gets a range of visible lines efficiently
    /// </summary>
    public IEnumerable<string> GetVisibleLines(int startLine, int count)
    {
        var endLine = Math.Min(startLine + count, LineCount);
        
        for (int i = startLine; i < endLine; i++)
        {
            yield return ReadLine(i);
        }
    }

    /// <summary>
    /// Applies an edit operation and records it in the overlay
    /// </summary>
    public void ApplyEdit(Patch patch)
    {
        _editOverlay.AddPatch(patch);
    }

    /// <summary>
    /// Gets the file offset for a specific line
    /// </summary>
    public long GetLineOffset(int lineNumber) => _lineIndexer.GetLineOffset(lineNumber);

    /// <summary>
    /// Clears all edits from the overlay
    /// </summary>
    public void ClearEdits() => _editOverlay.Clear();

    /// <summary>
    /// Saves the current buffer (with edits) to a file
    /// </summary>
    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        for (int i = 0; i < LineCount; i++)
        {
            lines.Add(ReadLine(i));
        }

        await _fileManager.SaveFileAsync(filePath, lines, cancellationToken);
    }

    public void Dispose()
    {
        _fileManager.Dispose();
        GC.SuppressFinalize(this);
    }
}