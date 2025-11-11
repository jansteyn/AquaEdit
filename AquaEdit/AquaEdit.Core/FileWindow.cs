using System.IO.MemoryMappedFiles;

namespace AquaEdit.Core;

/// <summary>
/// Represents a windowed view into a memory-mapped file
/// </summary>
public class FileWindow : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private long _offset;
    private readonly long _windowSize;

    public long Offset => _offset;
    public long WindowSize => _windowSize;

    public FileWindow(MemoryMappedFile mmf, long offset, long windowSize)
    {
        _mmf = mmf;
        _offset = offset;
        _windowSize = windowSize;
        _accessor = _mmf.CreateViewAccessor(_offset, _windowSize, MemoryMappedFileAccess.Read);
    }

    /// <summary>
    /// Reads a single byte at the specified index within this window
    /// </summary>
    public byte ReadByte(long index)
    {
        if (_accessor == null)
            throw new ObjectDisposedException(nameof(FileWindow));

        return _accessor.ReadByte(index);
    }

    /// <summary>
    /// Reads a range of bytes from this window
    /// </summary>
    public byte[] ReadRange(long offset, int count)
    {
        if (_accessor == null)
            throw new ObjectDisposedException(nameof(FileWindow));

        var buffer = new byte[count];
        _accessor.ReadArray(offset, buffer, 0, count);
        return buffer;
    }

    /// <summary>
    /// Reads a span of bytes from this window (zero-allocation)
    /// </summary>
    public void ReadSpan(long offset, Span<byte> destination)
    {
        if (_accessor == null)
            throw new ObjectDisposedException(nameof(FileWindow));

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = _accessor.ReadByte(offset + i);
        }
    }

    /// <summary>
    /// Slides the window to a new offset (recreates the accessor)
    /// </summary>
    public void SlideTo(long newOffset)
    {
        _accessor?.Dispose();
        _offset = newOffset;
        _accessor = _mmf.CreateViewAccessor(_offset, _windowSize, MemoryMappedFileAccess.Read);
    }

    public void Dispose()
    {
        _accessor?.Dispose();
        _accessor = null;
        GC.SuppressFinalize(this);
    }
}