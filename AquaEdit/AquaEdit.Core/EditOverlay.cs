namespace AquaEdit.Core;

/// <summary>
/// Manages in-memory edits as an overlay on top of the base file
/// </summary>
public class EditOverlay
{
    private readonly List<Patch> _patches = new();

    public IReadOnlyList<Patch> Patches => _patches.AsReadOnly();
    public bool HasEdits => _patches.Count > 0;

    /// <summary>
    /// Adds a new patch to the overlay
    /// </summary>
    public void AddPatch(Patch patch)
    {
        _patches.Add(patch);
        // TODO: Merge overlapping patches for optimization
    }

    /// <summary>
    /// Applies all relevant patches to a text segment
    /// </summary>
    public string ApplyEdits(string baseText, long baseOffset)
    {
        if (!HasEdits)
            return baseText;

        var result = baseText;
        var currentOffset = baseOffset;

        foreach (var patch in _patches.OrderBy(p => p.StartOffset))
        {
            if (patch.StartOffset < currentOffset || patch.StartOffset >= currentOffset + baseText.Length)
                continue; // Patch doesn't affect this segment

            var relativeOffset = (int)(patch.StartOffset - currentOffset);

            result = patch.Type switch
            {
                PatchType.Insert => result.Insert(relativeOffset, patch.NewText),
                PatchType.Delete => result.Remove(relativeOffset, Math.Min(patch.OriginalLength, result.Length - relativeOffset)),
                PatchType.Replace => result.Remove(relativeOffset, patch.OriginalLength).Insert(relativeOffset, patch.NewText),
                _ => result
            };
        }

        return result;
    }

    /// <summary>
    /// Clears all patches
    /// </summary>
    public void Clear() => _patches.Clear();
}