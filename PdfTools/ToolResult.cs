namespace ConversionApp.PdfTools;

/// <summary>
/// Immutable result returned by every <see cref="IDocumentTool"/> execution.
/// </summary>
public sealed class ToolResult
{
    /// <summary>Whether the tool completed without errors.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Human-readable summary message (always set).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Output paths produced by the tool (may be empty for in-place operations).
    /// </summary>
    public IReadOnlyList<string> OutputPaths { get; init; } = [];

    /// <summary>
    /// If <see cref="IsSuccess"/> is false, the original exception (may be null
    /// for user-facing validation failures).
    /// </summary>
    public Exception? Exception { get; init; }

    // -------------------------------------------------------------------------
    // Static factories keep call sites clean
    // -------------------------------------------------------------------------

    /// <summary>Creates a success result with optional output paths.</summary>
    public static ToolResult Success(string message, IEnumerable<string>? outputPaths = null)
        => new()
        {
            IsSuccess   = true,
            Message     = message,
            OutputPaths = outputPaths is null ? [] : [.. outputPaths],
        };

    /// <summary>Creates a failure result from an exception.</summary>
    public static ToolResult Failure(string message, Exception? ex = null)
        => new()
        {
            IsSuccess = false,
            Message   = message,
            Exception = ex,
        };
}
