using System.Threading;

namespace ConversionApp.PdfTools.Tools;

public sealed class PdfToExcelEngine : IDocumentTool
{
    public string Name => "PDF to Excel";
    public string Description => "Extract tables and data from PDF documents into Excel spreadsheets.";

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        return ToolResult.Failure("Routed through UI engine.");
    }
}
