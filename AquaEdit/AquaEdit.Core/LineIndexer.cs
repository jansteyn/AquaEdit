using System.Text;

namespace AquaEdit.Core;

/// <summary>
/// Builds and maintains an index of line start offsets for fast line-based navigation
/// </summary>
public class LineIndexer
{
    private readonly FileManager _fileManager;
    private readonly List<long> _lineOffsets;
    private readonly Encoding _encoding;
    private bool _isIndexed;

    public int LineCount => _lineOffsets.Count;
    public bool IsIndexed => _isIndexed;

    public LineIndexer(FileManager fileManager, Encoding? encoding = null)
    {
        _fileManager = fileManager;
        _encoding = encoding ?? Encoding.UTF8;
        _lineOffsets = new List<long> { 0 }; // Line 0 starts at offset 0
    }

    /// <summary>
    /// Builds the line index asynchronously in the background
    /// </summary>
    public async Task BuildIndexAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        _lineOffsets.Clear();
        _lineOffsets.Add(0);

        if (!_fileManager.IsOpen)
        {
            throw new InvalidOperationException("FileManager must have an open file before building index.");
        }

        if (_fileManager.FileSize == 0)
        {
            // Empty file - just mark as indexed
            _isIndexed = true;
            progress?.Report(100);
            return;
        }

        try
        {
            const int chunkSize = 1024 * 1024; // 1 MB chunks
            long offset = 0;
            int progressCounter = 0;

            while (offset < _fileManager.FileSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytesToRead = (int)Math.Min(chunkSize, _fileManager.FileSize - offset);
                
                if (bytesToRead <= 0)
                    break;

                byte[] buffer;
                try
                {
                    buffer = _fileManager.ReadBytes(offset, bytesToRead);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to read file chunk at offset {offset}", ex);
                }

                if (buffer.Length == 0)
                    break;

                // Scan for newlines
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        _lineOffsets.Add(offset + i + 1);
                    }
                }

                offset += buffer.Length;
                progressCounter++;

                // Report progress every 10 MB
                if (progressCounter % 10 == 0)
                {
                    var progressPercentage = _fileManager.FileSize > 0 
                        ? (int)((offset * 100) / _fileManager.FileSize) 
                        : 100;
                    progress?.Report(progressPercentage);
                    await Task.Yield(); // Keep UI responsive
                }
            }

            _isIndexed = true;
            progress?.Report(100);
        }
        catch (OperationCanceledException)
        {
            // Reset state on cancellation
            _lineOffsets.Clear();
            _lineOffsets.Add(0);
            _isIndexed = false;
            throw;
        }
        catch (Exception ex)
        {
            // Reset state on error
            _lineOffsets.Clear();
            _lineOffsets.Add(0);
            _isIndexed = false;
            throw new IOException("Failed to build line index", ex);
        }
    }

    /// <summary>
    /// Gets the file offset where a specific line starts
    /// </summary>
    public long GetLineOffset(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= _lineOffsets.Count)
            return 0;

        return _lineOffsets[lineNumber];
    }

    /// <summary>
    /// Gets the line number for a given file offset
    /// </summary>
    public int GetLineNumber(long offset)
    {
        int index = _lineOffsets.BinarySearch(offset);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>
    /// Gets the length of a specific line (excluding newline)
    /// </summary>
    public int GetLineLength(int lineNumber)
    {
        // Validate input
        if (lineNumber < 0 || lineNumber >= _lineOffsets.Count)
            return 0;

        // Get start offset
        var startOffset = _lineOffsets[lineNumber];
        
        // Calculate end offset
        long endOffset;
        if (lineNumber < _lineOffsets.Count - 1)
        {
            // Not the last line - use next line's offset
            endOffset = _lineOffsets[lineNumber + 1];
            
            // Subtract newline characters (\n or \r\n)
            if (endOffset > startOffset)
            {
                endOffset--; // Remove \n
                
                // Check if there's a \r before the \n
                if (endOffset > startOffset)
                {
                    try
                    {
                        var bytes = _fileManager.ReadBytes(endOffset - 1, 1);
                        if (bytes.Length > 0 && bytes[0] == (byte)'\r')
                        {
                            endOffset--; // Remove \r
                        }
                    }
                    catch
                    {
                        // Ignore errors reading the byte
                    }
                }
            }
        }
        else
        {
            // Last line - use file size
            endOffset = _fileManager.FileSize;
        }

        // Calculate length (ensure it's not negative)
        var length = (int)Math.Max(0, endOffset - startOffset);
        
        // Prevent reading beyond file size
        if (startOffset + length > _fileManager.FileSize)
        {
            length = (int)Math.Max(0, _fileManager.FileSize - startOffset);
        }

        return length;
    }
}