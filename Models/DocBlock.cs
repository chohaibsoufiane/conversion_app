namespace ConversionApp.Models;

/// <summary>
/// An atomic, immutable unit of parsed document content.
/// Each paragraph in the source .docx maps to exactly one DocBlock.
/// </summary>
public sealed class DocBlock
{
    /// <summary>
    /// Unique identifier for this block within its document.
    /// Assigned at parse time; stable across re-runs for the same input.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The semantic category of this block, inferred from the
    /// Word paragraph style name.
    /// </summary>
    public BlockType Type { get; init; }

    /// <summary>
    /// The clean, whitespace-normalised plain text extracted from
    /// all runs within the paragraph (no markup, no control characters).
    /// </summary>
    public string Content { get; init; } = string.Empty;

    // -------------------------------------------------------------------------
    // Factory helpers keep construction intent explicit at the call site.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a DocBlock with a freshly generated identifier.
    /// </summary>
    public static DocBlock Create(BlockType type, string content) =>
        new() { Type = type, Content = content.Trim() };
}
