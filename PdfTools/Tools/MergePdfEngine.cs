using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class MergePdfEngine : IDocumentTool
{
    public string Name => "Merge PDF";
    public string Description => "Combine multiple PDF files into a single, organized document in seconds.";

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("This feature is coming soon.");
    }
}
