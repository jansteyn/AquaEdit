using System.Text;
using System.Text.RegularExpressions;

namespace AquaEdit.Core;

/// <summary>
/// Provides search and replace functionality with async streaming support
/// </summary>
public class SearchEngine
{
    private readonly TextBuffer _textBuffer;

    public record SearchResult(int LineIndex, int CharIndex, int Length, string LineText);

    public SearchEngine(TextBuffer textBuffer)
    {
        _textBuffer = textBuffer;
    }

    /// <summary>
    /// Performs an async search across the entire document
    /// </summary>
    public async IAsyncEnumerable<SearchResult> SearchAsync(
        string searchTerm,
        bool caseSensitive = false,
        bool useRegex = false,
        CancellationToken cancellationToken = default)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Regex? regex = useRegex ? new Regex(searchTerm, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) : null;

        for (int i = 0; i < _textBuffer.LineCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = _textBuffer.ReadLine(i);

            if (useRegex && regex != null)
            {
                var matches = regex.Matches(line);
                foreach (Match match in matches)
                {
                    yield return new SearchResult(i, match.Index, match.Length, line);
                }
            }
            else
            {
                int index = 0;
                while ((index = line.IndexOf(searchTerm, index, comparison)) != -1)
                {
                    yield return new SearchResult(i, index, searchTerm.Length, line);
                    index += searchTerm.Length;
                }
            }

            // Yield periodically to keep UI responsive
            if (i % 1000 == 0)
                await Task.Yield();
        }
    }

    /// <summary>
    /// Synchronous search for backward compatibility
    /// </summary>
    public IEnumerable<SearchResult> Search(string searchTerm, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int i = 0; i < _textBuffer.LineCount; i++)
        {
            var line = _textBuffer.ReadLine(i);
            int index = 0;

            while ((index = line.IndexOf(searchTerm, index, comparison)) != -1)
            {
                yield return new SearchResult(i, index, searchTerm.Length, line);
                index += searchTerm.Length;
            }
        }
    }

    /// <summary>
    /// Finds the next occurrence from a starting position
    /// </summary>
    public SearchResult? FindNext(long fromOffset, string searchTerm, bool caseSensitive = false)
    {
        var startLine = 0; // Would use LineIndexer to get line from offset
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int i = startLine; i < _textBuffer.LineCount; i++)
        {
            var line = _textBuffer.ReadLine(i);
            var index = line.IndexOf(searchTerm, comparison);

            if (index != -1)
            {
                return new SearchResult(i, index, searchTerm.Length, line);
            }
        }

        return null;
    }
}