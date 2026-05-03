namespace ConversionApp.PdfTools;

/// <summary>
/// Lightweight registry / factory for <see cref="IDocumentTool"/> implementations.
///
/// Usage:
///   // At app startup (or when the PDF Tools section is first accessed):
///   ToolRegistry.Register(new MergePdfTool());
///   ToolRegistry.Register(new SplitPdfTool());
///
///   // In the dashboard ViewModel:
///   var tools = ToolRegistry.GetAll();
///
///   // By name, for deep-link or keyboard navigation:
///   var tool = ToolRegistry.Get("Merge PDFs");
///
/// The registry is intentionally static and process-scoped.
/// All registrations should happen once at startup before any tool is used.
/// Thread-safety: registrations are protected by a lock; reads are snapshot-based.
/// </summary>
public static class ToolRegistry
{
    private static readonly Dictionary<string, IDocumentTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    // -------------------------------------------------------------------------
    // Registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a tool. Replaces any existing registration with the same
    /// <see cref="IDocumentTool.Name"/> (last-write wins — useful for hot reload).
    /// </summary>
    public static void Register(IDocumentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        lock (_lock)
        {
            _tools[tool.Name] = tool;
        }
    }

    /// <summary>Registers multiple tools at once.</summary>
    public static void RegisterAll(IEnumerable<IDocumentTool> tools)
    {
        foreach (var tool in tools)
            Register(tool);
    }

    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all registered tools in registration order, as a snapshot.
    /// </summary>
    public static IReadOnlyList<IDocumentTool> GetAll()
    {
        lock (_lock)
        {
            return [.. _tools.Values];
        }
    }

    /// <summary>
    /// Looks up a tool by name (case-insensitive).
    /// Returns null if not found.
    /// </summary>
    public static IDocumentTool? Get(string name)
    {
        lock (_lock)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
    }

    /// <summary>Returns true if any tools are registered.</summary>
    public static bool HasTools
    {
        get { lock (_lock) { return _tools.Count > 0; } }
    }

    /// <summary>Clears all registrations (useful for unit tests).</summary>
    public static void Clear()
    {
        lock (_lock) { _tools.Clear(); }
    }
}
