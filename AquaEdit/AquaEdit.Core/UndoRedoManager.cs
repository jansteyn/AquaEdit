namespace AquaEdit.Core;

/// <summary>
/// Manages undo/redo operations using a command pattern
/// </summary>
public class UndoRedoManager
{
    private readonly Stack<Patch> _undoStack = new();
    private readonly Stack<Patch> _redoStack = new();
    private readonly TextBuffer _textBuffer;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public UndoRedoManager(TextBuffer textBuffer)
    {
        _textBuffer = textBuffer;
    }

    /// <summary>
    /// Records a new edit operation
    /// </summary>
    public void Record(Patch patch)
    {
        _undoStack.Push(patch);
        _redoStack.Clear(); // Clear redo stack when new action is performed
    }

    /// <summary>
    /// Undoes the last operation
    /// </summary>
    public Patch? Undo()
    {
        if (!CanUndo)
            return null;

        var patch = _undoStack.Pop();
        _redoStack.Push(patch);

        // Create inverse patch
        var inversePatch = CreateInversePatch(patch);
        _textBuffer.ApplyEdit(inversePatch);

        return patch;
    }

    /// <summary>
    /// Redoes the last undone operation
    /// </summary>
    public Patch? Redo()
    {
        if (!CanRedo)
            return null;

        var patch = _redoStack.Pop();
        _undoStack.Push(patch);
        _textBuffer.ApplyEdit(patch);

        return patch;
    }

    /// <summary>
    /// Creates an inverse patch to undo an operation
    /// </summary>
    private static Patch CreateInversePatch(Patch original)
    {
        return original.Type switch
        {
            PatchType.Insert => Patch.Delete(original.StartOffset, original.NewText.Length),
            PatchType.Delete => Patch.Insert(original.StartOffset, string.Empty), // Would need original text
            PatchType.Replace => Patch.Replace(original.StartOffset, original.NewText.Length, string.Empty),
            _ => original
        };
    }

    /// <summary>
    /// Clears all undo/redo history
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}