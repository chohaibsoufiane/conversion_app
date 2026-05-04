using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class ImageToPdfEngine : IDocumentTool
{
    public string Name => "Image to PDF";
    public string Description => "Convert JPG, PNG, BMP, and other image formats directly to PDF.";

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("This feature is coming soon.");
    }
}
