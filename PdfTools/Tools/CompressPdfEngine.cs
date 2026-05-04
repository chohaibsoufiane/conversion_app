using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class CompressPdfEngine : IDocumentTool
{
    public string Name => "Compress PDF";
    public string Description => "Reduce the file size of your PDF while maintaining excellent visual quality.";

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("This feature is coming soon.");
    }
}
