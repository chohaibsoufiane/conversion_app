namespace ConversionApp.Models;

/// <summary>
/// Represents the semantic role of a parsed paragraph block
/// within a legal document.  Mapping happens in StyleMapper.
/// </summary>
public enum BlockType
{
    /// <summary>Top-level document title (Word style: "Title").</summary>
    DocumentTitle,

    /// <summary>Primary heading (Word style: "Heading 1").</summary>
    Heading1,

    /// <summary>Secondary heading (Word style: "Heading 2").</summary>
    Heading2,

    /// <summary>
    /// A numbered or bulleted list item
    /// (Word styles: "List Paragraph", "List Bullet", "List Number", etc.).
    /// </summary>
    ListItem,

    /// <summary>
    /// A dedicated signature / execution block
    /// (Word style: "Signature" or paragraphs containing ____).
    /// </summary>
    SignatureBlock,

    /// <summary>
    /// Default: any substantive paragraph that doesn't match
    /// a more specific style is treated as a legal clause body.
    /// </summary>
    LegalClause,
}
