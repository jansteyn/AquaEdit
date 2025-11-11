namespace AquaEdit.Core;

/// <summary>
/// Represents a single edit operation (insert, delete, or replace)
/// </summary>
public class Patch
{
    public long StartOffset { get; init; }
    public int OriginalLength { get; init; }
    public string NewText { get; init; } = string.Empty;
    public PatchType Type { get; init; }

    public Patch(long startOffset, int originalLength, string newText, PatchType type)
    {
        StartOffset = startOffset;
        OriginalLength = originalLength;
        NewText = newText;
        Type = type;
    }

    /// <summary>
    /// Creates an insert patch
    /// </summary>
    public static Patch Insert(long offset, string text) =>
        new(offset, 0, text, PatchType.Insert);

    /// <summary>
    /// Creates a delete patch
    /// </summary>
    public static Patch Delete(long offset, int length) =>
        new(offset, length, string.Empty, PatchType.Delete);

    /// <summary>
    /// Creates a replace patch
    /// </summary>
    public static Patch Replace(long offset, int length, string text) =>
        new(offset, length, text, PatchType.Replace);
}

public enum PatchType
{
    Insert,
    Delete,
    Replace
}