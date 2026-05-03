namespace ConversionApp.PdfTools;

/// <summary>
/// Contract that every PDF tool must implement.
///
/// Each tool is a stateless, single-responsibility unit:
///   - It reads from <see cref="ToolRequest.InputPaths"/>
///   - It writes to <see cref="ToolRequest.OutputPath"/>
///   - It returns a <see cref="ToolResult"/> describing success or failure
///
/// Tools should be thread-safe (no shared mutable state).
/// Long-running operations should honour <paramref name="cancellationToken"/>.
/// </summary>
public interface IDocumentTool
{
    /// <summary>Short display name shown in the dashboard card (e.g. "Merge PDFs").</summary>
    string Name { get; }

    /// <summary>One-line description shown below the tool name in the dashboard.</summary>
    string Description { get; }

    /// <summary>
    /// Executes the tool synchronously on a background thread.
    ///
    /// Implementations must NOT dispatch to the UI thread — the caller is
    /// responsible for marshalling results back via DispatcherQueue.
    /// </summary>
    /// <param name="request">Validated input/output specification.</param>
    /// <param name="cancellationToken">Allows the caller to cancel long-running work.</param>
    ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default);
}
