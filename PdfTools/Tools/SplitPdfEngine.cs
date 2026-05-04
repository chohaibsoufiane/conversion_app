using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class SplitPdfEngine : IDocumentTool
{
    public string Name => "Split PDF";
    public string Description => "Extract pages or split a large PDF into multiple smaller files.";

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("This feature is coming soon.");
    }
}
