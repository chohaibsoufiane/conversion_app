using ConversionApp.Models;

namespace ConversionApp.Services;

/// <summary>
/// Translates a raw Word paragraph style name into a <see cref="BlockType"/>.
///
/// Mapping strategy (evaluated in priority order):
///   1. Exact match against the known style dictionary.
///   2. Prefix/substring match for compound style names
///      (e.g. "Heading 3" → still Heading2 as the closest leaf).
///   3. Heuristic content inspection (signature lines).
///   4. Default → <see cref="BlockType.LegalClause"/>.
/// </summary>
internal static class StyleMapper
{
    // -------------------------------------------------------------------------
    // Known style → BlockType mappings.
    // Word style names are NOT localised in the XML; they use English names
    // regardless of the UI language, so this mapping is reliable.
    // -------------------------------------------------------------------------
    private static readonly Dictionary<string, BlockType> ExactMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Titles
            ["Title"]             = BlockType.DocumentTitle,
            ["Doc Title"]         = BlockType.DocumentTitle,

            // Headings
            ["Heading 1"]         = BlockType.Heading1,
            ["Heading1"]          = BlockType.Heading1,
            ["heading 1"]         = BlockType.Heading1,

            ["Heading 2"]         = BlockType.Heading2,
            ["Heading2"]          = BlockType.Heading2,
            ["heading 2"]         = BlockType.Heading2,

            // Treat H3+ as H2 so consumers get a flat two-level hierarchy
            ["Heading 3"]         = BlockType.Heading2,
            ["Heading 4"]         = BlockType.Heading2,
            ["Heading 5"]         = BlockType.Heading2,
            ["Heading 6"]         = BlockType.Heading2,

            // List variants
            ["List Paragraph"]    = BlockType.ListItem,
            ["List Bullet"]       = BlockType.ListItem,
            ["List Bullet 2"]     = BlockType.ListItem,
            ["List Bullet 3"]     = BlockType.ListItem,
            ["List Number"]       = BlockType.ListItem,
            ["List Number 2"]     = BlockType.ListItem,
            ["List Continue"]     = BlockType.ListItem,

            // Signature / execution blocks
            ["Signature"]         = BlockType.SignatureBlock,
            ["Signature Line"]    = BlockType.SignatureBlock,
        };

    // Prefixes that unambiguously identify a category even for unknown variants.
    private static readonly (string Prefix, BlockType Type)[] PrefixMappings =
    [
        ("Heading 1",  BlockType.Heading1),
        ("Heading1",   BlockType.Heading1),
        ("Heading 2",  BlockType.Heading2),
        ("Heading2",   BlockType.Heading2),
        ("Heading",    BlockType.Heading2),   // Heading 3-6 fall-through
        ("List",       BlockType.ListItem),
        ("Signature",  BlockType.SignatureBlock),
        ("Title",      BlockType.DocumentTitle),
    ];

    /// <summary>
    /// Returns the <see cref="BlockType"/> for the given Word style name and
    /// paragraph text.  Never throws; always returns a valid BlockType.
    /// </summary>
    /// <param name="styleName">
    ///   The raw style ID/name from the paragraph properties, or
    ///   <see langword="null"/> / empty for unstyled paragraphs.
    /// </param>
    /// <param name="paragraphText">
    ///   The fully extracted text of the paragraph, used for content-based
    ///   heuristics when the style is ambiguous.
    /// </param>
    public static BlockType Resolve(string? styleName, string paragraphText)
    {
        if (!string.IsNullOrWhiteSpace(styleName))
        {
            // 1. Exact match.
            if (ExactMappings.TryGetValue(styleName, out var exactType))
                return exactType;

            // 2. Prefix match.
            foreach (var (prefix, type) in PrefixMappings)
            {
                if (styleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return type;
            }
        }

        // 3. Heuristic: signature lines typically consist mostly of underscores.
        if (IsSignatureLine(paragraphText))
            return BlockType.SignatureBlock;

        // 4. Default.
        return BlockType.LegalClause;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// A signature line is identified as a paragraph where ≥ 60 % of its
    /// non-whitespace characters are underscores or equals signs.
    /// </summary>
    private static bool IsSignatureLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var nonSpace = text.Where(c => !char.IsWhiteSpace(c)).ToArray();
        if (nonSpace.Length < 4)
            return false;

        var signatureChars = nonSpace.Count(c => c is '_' or '=' or '-');
        return (double)signatureChars / nonSpace.Length >= 0.60;
    }
}
