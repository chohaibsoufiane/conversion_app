using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ConversionApp.PdfTools.Tools;

/// <summary>
/// High-fidelity Word to PDF converter using Microsoft Word's COM automation.
///
/// This produces pixel-perfect output identical to "Print to PDF" in Word or Edge,
/// because it uses Word's own rendering engine to generate the PDF.
///
/// Requirements:
///   - Microsoft Word must be installed on the machine.
///
/// How it works:
///   1. Opens Word in the background (invisible, no add-ins).
///   2. Opens the .docx file.
///   3. Calls Word's native ExportAsFixedFormat (SaveAs PDF).
///   4. Closes the document and quits Word.
///   5. Releases all COM objects to avoid ghost processes.
/// </summary>
public sealed class WordToPdfEngine : IDocumentTool
{
    public string Name        => "Word to PDF";
    public string Description => "Pixel-perfect conversion using Microsoft Word's rendering engine.";

    // Word constants for PDF export
    private const int wdExportFormatPDF       = 17;
    private const int wdExportOptimizeForPrint = 0;
    private const int wdExportAllDocument      = 0;
    private const int wdDoNotSaveChanges       = 0;

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        if (request.InputPaths.Count == 0)
            return ToolResult.Failure("No input file specified.");

        var inputPath  = request.InputPaths[0];
        var outputPath = request.OutputPath ?? Path.ChangeExtension(inputPath, ".pdf");

        if (!File.Exists(inputPath))
            return ToolResult.Failure($"Input file not found: '{inputPath}'");

        // Resolve to absolute paths (Word COM requires full paths)
        inputPath  = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);

        // Ensure output directory exists
        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        dynamic? wordApp = null;
        dynamic? doc     = null;

        try
        {
            // Create Word.Application COM object
            var wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType is null)
                return ToolResult.Failure("Microsoft Word is not installed on this machine.");

            wordApp = Activator.CreateInstance(wordType);
            if (wordApp is null)
                return ToolResult.Failure("Failed to create Word.Application instance.");

            wordApp.Visible        = false;
            wordApp.DisplayAlerts  = 0; // wdAlertsNone
            wordApp.ScreenUpdating = false;

            // Open the document (read-only, no add-ins interference)
            doc = wordApp.Documents.Open(
                inputPath,                     // FileName
                false,                         // ConfirmConversions
                true,                          // ReadOnly
                false,                         // AddToRecentFiles
                Type.Missing,                  // PasswordDocument
                Type.Missing,                  // PasswordTemplate
                Type.Missing,                  // Revert
                Type.Missing,                  // WritePasswordDocument
                Type.Missing,                  // WritePasswordTemplate
                Type.Missing,                  // Format
                Type.Missing,                  // Encoding
                false                          // Visible
            );

            cancellationToken.ThrowIfCancellationRequested();

            // Export as PDF using Word's native renderer
            doc.ExportAsFixedFormat(
                outputPath,                    // OutputFileName
                wdExportFormatPDF,             // ExportFormat
                false,                         // OpenAfterExport
                wdExportOptimizeForPrint,      // OptimizeFor
                wdExportAllDocument,           // Range
                0,                             // From (ignored for AllDocument)
                0,                             // To (ignored for AllDocument)
                Type.Missing,                  // Item
                true,                          // IncludeDocProps
                true,                          // KeepIRM
                Type.Missing,                  // CreateBookmarks
                true,                          // DocStructureTags
                true,                          // BitmapMissingFonts
                false                          // UseISO19005_1 (PDF/A)
            );

            // Close doc without saving
            doc.Close(wdDoNotSaveChanges);
            doc = null;

            wordApp.Quit(wdDoNotSaveChanges);
            wordApp = null;

            var fileSize = new FileInfo(outputPath).Length;
            return ToolResult.Success(
                $"Converted successfully ({fileSize / 1024} KB).",
                [outputPath]);
        }
        catch (OperationCanceledException)
        {
            CleanupCom(ref doc, ref wordApp);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            return ToolResult.Failure("Conversion was cancelled.");
        }
        catch (COMException comEx)
        {
            CleanupCom(ref doc, ref wordApp);
            return ToolResult.Failure($"Word COM error: {comEx.Message}", comEx);
        }
        catch (Exception ex)
        {
            CleanupCom(ref doc, ref wordApp);
            return ToolResult.Failure($"Conversion failed: {ex.Message}", ex);
        }
        finally
        {
            // Belt-and-suspenders: ensure COM objects are released
            CleanupCom(ref doc, ref wordApp);
        }
    }

    /// <summary>
    /// Safely releases COM objects and kills any orphaned Word processes.
    /// </summary>
    private static void CleanupCom(ref dynamic? doc, ref dynamic? wordApp)
    {
        try
        {
            if (doc is not null)
            {
                doc.Close(wdDoNotSaveChanges);
                Marshal.ReleaseComObject(doc);
                doc = null;
            }
        }
        catch { /* swallow — best-effort cleanup */ }

        try
        {
            if (wordApp is not null)
            {
                wordApp.Quit(wdDoNotSaveChanges);
                Marshal.ReleaseComObject(wordApp);
                wordApp = null;
            }
        }
        catch { /* swallow — best-effort cleanup */ }

        // Force GC to release COM references immediately
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
