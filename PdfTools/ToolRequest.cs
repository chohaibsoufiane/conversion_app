namespace ConversionApp.PdfTools;

/// <summary>
/// Encapsulates a request to execute a PDF tool operation.
/// </summary>
public sealed class ToolRequest
{
    /// <summary>One or more input file/folder paths for the tool to read from.</summary>
    public IReadOnlyList<string> InputPaths { get; init; } = [];

    /// <summary>The destination path (file or folder) for the tool's output.</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Optional key-value options specific to each tool (e.g. DPI, quality).</summary>
    public IReadOnlyDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>();

    // -------------------------------------------------------------------------
    // Convenience factory
    // -------------------------------------------------------------------------

    /// <summary>Creates a simple single-input request.</summary>
    public static ToolRequest Create(string inputPath, string outputPath,
        Dictionary<string, string>? options = null)
        => new()
        {
            InputPaths = [inputPath],
            OutputPath = outputPath,
            Options    = options ?? new Dictionary<string, string>(),
        };

    /// <summary>Creates a multi-input request (e.g. PDF merge).</summary>
    public static ToolRequest CreateMulti(IEnumerable<string> inputPaths, string outputPath,
        Dictionary<string, string>? options = null)
        => new()
        {
            InputPaths = [.. inputPaths],
            OutputPath = outputPath,
            Options    = options ?? new Dictionary<string, string>(),
        };
}
