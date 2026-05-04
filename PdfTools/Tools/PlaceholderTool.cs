using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class PlaceholderTool : IDocumentTool
{
    public string Name { get; }
    public string Description { get; }

    public PlaceholderTool(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("This feature is coming soon.");
    }
}
