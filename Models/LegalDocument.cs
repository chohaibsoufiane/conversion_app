namespace ConversionApp.Models;

/// <summary>
/// The root model for a fully-parsed legal document.
/// This object is serialised 1-to-1 to the output JSON file.
/// </summary>
public sealed class LegalDocument
{
    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Original filename of the source .docx (without the directory path).
    /// Example: "contract_2024_acme.docx"
    /// </summary>
    public string DocumentName { get; init; } = string.Empty;

    /// <summary>
    /// Absolute path to the source file at the time of processing.
    /// Useful for audit / reprocessing workflows.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp of when this document was processed.
    /// </summary>
    public DateTimeOffset ProcessedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Schema version of the Block Editor output format.
    /// Increment when breaking changes are made to the JSON shape.
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0.0";

    // -------------------------------------------------------------------------
    // Content
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ordered list of parsed content blocks, preserving document reading order.
    /// </summary>
    public List<DocBlock> Blocks { get; init; } = [];

    // -------------------------------------------------------------------------
    // Derived statistics (written to JSON for downstream consumers)
    // -------------------------------------------------------------------------

    /// <summary>Total number of parsed blocks.</summary>
    public int TotalBlocks => Blocks.Count;

    /// <summary>Number of blocks classified as legal clauses.</summary>
    public int ClauseCount => Blocks.Count(b => b.Type == BlockType.LegalClause);
}
