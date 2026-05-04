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
    public string SourceIconPath  { get; init; } = string.Empty;
    public string TargetIconPath  { get; init; } = string.Empty;
    public bool IsDirectional { get; init; }
    public Visibility ArrowVisibility => IsDirectional ? Visibility.Visible : Visibility.Collapsed;
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
        var cards = tools.Select(t => {
            var icons = ResolveIcons(t.Name);
            return new ToolCardViewModel
            {
                ToolName        = t.Name,
                ToolDescription = t.Description,
                SourceIconPath  = icons.Source,
                TargetIconPath  = icons.Target,
                IsDirectional   = icons.IsDirectional,
            };
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
    // Icon mapping — extend as new tools are added
    // -------------------------------------------------------------------------

    private static (string Source, string Target, bool IsDirectional) ResolveIcons(string toolName)
    {
        var lower = toolName.ToLowerInvariant();
        if (lower.Contains(" to "))
        {
            var parts = lower.Split(" to ");
            return (ResolveSingleIcon(parts[0]), ResolveSingleIcon(parts[1]), true);
        }
        // Return a dummy valid path for Target to prevent WinUI Image parsing crash, but it won't be shown
        return (ResolveSingleIcon(lower), "ms-appx:///Assets/Icons/generic.svg", false);
    }

    private static string ResolveSingleIcon(string namePart) => namePart switch
    {
        var n when n.Contains("excel")    => "ms-appx:///Assets/Icons/excel.png",
        var n when n.Contains("word")     => "ms-appx:///Assets/Icons/word.png",
        var n when n.Contains("powerpoint")=> "ms-appx:///Assets/Icons/powerpoint.png",
        var n when n.Contains("image")    => "ms-appx:///Assets/Icons/image.svg",
        var n when n.Contains("pdf")      => "ms-appx:///Assets/Icons/pdf.png",
        _                                 => "ms-appx:///Assets/Icons/generic.svg",
    };
}
