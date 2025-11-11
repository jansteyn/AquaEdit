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
            return;

        const int chunkSize = 1024 * 1024; // 1 MB chunks
        long offset = 0;
        int progressCounter = 0;

        while (offset < _fileManager.FileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesToRead = (int)Math.Min(chunkSize, _fileManager.FileSize - offset);
            var buffer = _fileManager.ReadBytes(offset, bytesToRead);

            // Scan for newlines
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    _lineOffsets.Add(offset + i + 1);
                }
            }

            offset += bytesToRead;
            progressCounter++;

            // Report progress every 10 MB
            if (progressCounter % 10 == 0)
            {
                progress?.Report((int)((offset * 100) / _fileManager.FileSize));
                await Task.Yield(); // Keep UI responsive
            }
        }

        _isIndexed = true;
        progress?.Report(100);
    }

    /// <summary>
    /// Gets the file offset where a specific line starts
    /// </summary>
    public long GetLineOffset(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= _lineOffsets.Count)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

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
        if (lineNumber < 0 || lineNumber >= _lineOffsets.Count - 1)
            return 0;

        var startOffset = _lineOffsets[lineNumber];
        var endOffset = lineNumber < _lineOffsets.Count - 1 
            ? _lineOffsets[lineNumber + 1] - 1  // -1 to exclude \n
            : _fileManager.FileSize;

        return (int)(endOffset - startOffset);
    }
}