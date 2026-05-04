using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ConversionApp.PdfTools;

/// <summary>
/// Lightweight view-model for a single tool card in the PDF Tools dashboard.
/// Populated from <see cref="IDocumentTool"/> metadata at load time.
/// </summary>
public sealed class ToolCardViewModel
{
    public string ToolName        { get; init; } = string.Empty;
    public string ToolDescription { get; init; } = string.Empty;

    /// <summary>
    /// Segoe Fluent Icons glyph code. Override per-tool for a unique icon;
    /// falls back to a generic document icon if not specified.
    /// </summary>
    public string IconGlyph { get; init; } = "\uE8A5"; // Page icon
}

/// <summary>
/// Code-behind for <c>PdfToolsView.xaml</c>.
///
/// Responsibilities:
///   1. Load tool cards from <see cref="ToolRegistry"/> when the view becomes visible.
///   2. Handle card clicks and raise <see cref="ToolRequested"/> for the host window
///      to navigate to the appropriate tool UI.
/// </summary>
public sealed partial class PdfToolsView : UserControl
{
    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised when the user clicks a tool card.
    /// The string argument is the tool's <see cref="IDocumentTool.Name"/>.
    /// The host window listens to this to push the tool's detail view.
    /// </summary>
    public event Action<string>? ToolRequested;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public PdfToolsView()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCards();
    }

    /// <summary>
    /// Rebuilds the card list from the current ToolRegistry snapshot.
    /// Call this if tools are registered after the view is already visible.
    /// </summary>
    public void RefreshCards()
    {
        var tools = ToolRegistry.GetAll();

        if (tools.Count == 0)
        {
            EmptyState.Visibility       = Visibility.Visible;
            ToolScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility       = Visibility.Collapsed;
        ToolScrollViewer.Visibility = Visibility.Visible;

        // Map IDocumentTool → ToolCardViewModel
        // IconGlyph can be extended via a per-tool attribute or dictionary later
        var cards = tools.Select(t => new ToolCardViewModel
        {
            ToolName        = t.Name,
            ToolDescription = t.Description,
            IconGlyph       = ResolveGlyph(t.Name),
        }).ToList();

        ToolCardsRepeater.ItemsSource = cards;
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void ToolCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string toolName })
            ToolRequested?.Invoke(toolName);
    }

    // -------------------------------------------------------------------------
    // Glyph mapping — extend as new tools are added
    // -------------------------------------------------------------------------

    private static string ResolveGlyph(string toolName) => toolName.ToLowerInvariant() switch
    {
        var n when n.Contains("merge")    => "\uE71E", // Merge icon
        var n when n.Contains("split")    => "\uE8C6", // Split icon
        var n when n.Contains("compress") => "\uE8AF", // Compress / zip
        var n when n.Contains("convert")  => "\uE8AB", // Convert
        var n when n.Contains("encrypt")  => "\uE72E", // Lock
        var n when n.Contains("decrypt")  => "\uE785", // Unlock
        var n when n.Contains("extract")  => "\uE7B8", // Extract
        var n when n.Contains("rotate")   => "\uE7AD", // Rotate
        var n when n.Contains("watermark")=> "\uE8D6", // Stamp
        var n when n.Contains("excel")    => "\uE9F9", // Table / spreadsheet
        var n when n.Contains("word")     => "\uE8A5", // Document
        var n when n.Contains("image")    => "\uEB9F", // Image
        var n when n.Contains("pdf")      => "\uEA90", // PDF
        _                                 => "\uE8A5", // Generic page
    };
}
