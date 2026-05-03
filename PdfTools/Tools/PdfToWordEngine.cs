using System.Diagnostics;

namespace ConversionApp.PdfTools.Tools;

/// <summary>
/// PDF-to-Word converter powered by the bundled LibreOffice Portable runtime.
///
/// Uses LibreOffice's headless mode to convert PDF files into editable
/// DOCX format. Same execution strategy as the other LibreOffice-based engines.
/// </summary>
public sealed class PdfToWordEngine : IDocumentTool
{
    public string Name        => "PDF to Word";
    public string Description => "Convert PDF documents to editable Word (.docx) files.";

    private const int TimeoutSeconds = 120;

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        if (request.InputPaths.Count == 0)
            return ToolResult.Failure("No input file specified.");

        var inputPath = Path.GetFullPath(request.InputPaths[0]);

        if (!File.Exists(inputPath))
            return ToolResult.Failure($"Input file not found: '{inputPath}'");

        var ext = Path.GetExtension(inputPath);
        if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failure($"Unsupported file type: '{ext}'. Expected .pdf.");

        var outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.ChangeExtension(inputPath, ".docx")
            : Path.GetFullPath(request.OutputPath);

        var outputDir = Path.GetDirectoryName(outputPath)
                        ?? Path.GetDirectoryName(inputPath)!;

        var expectedOutput = Path.Combine(
            outputDir,
            Path.ChangeExtension(Path.GetFileName(inputPath), ".docx"));

        Directory.CreateDirectory(outputDir);

        var sofficePath = ResolveSofficePath();

        if (sofficePath is null || !File.Exists(sofficePath))
        {
            throw new FileNotFoundException(
                $"LibreOffice engine not found.\n" +
                $"Please place your portable LibreOffice installation inside the '_copies_' folder.\n" +
                $"We search for 'LibreOfficePortable.exe' inside any subfolder of '_copies_'.");
        }

        // Convert to docx using the writer_pdf_import filter
        var arguments =
            $"--headless --invisible --nologo --nodefault --nofirststartwizard " +
            $"--infilter=\"writer_pdf_import\" --convert-to docx \"{inputPath}\" --outdir \"{outputDir}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = sofficePath,
                Arguments              = arguments,
                CreateNoWindow         = true,
                UseShellExecute        = false,
                WindowStyle            = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                WorkingDirectory       = Path.GetDirectoryName(sofficePath)!,
            };

            TryDelete(expectedOutput);
            if (!string.Equals(expectedOutput, outputPath, StringComparison.OrdinalIgnoreCase))
                TryDelete(outputPath);

            using var process = Process.Start(psi);
            if (process is null)
                return ToolResult.Failure("Failed to start LibreOffice process.");

            process.WaitForExit();

            var deadlineMs = Environment.TickCount64 + (TimeoutSeconds * 1000L);
            bool fileReady = false;

            while (Environment.TickCount64 < deadlineMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(expectedOutput))
                {
                    try
                    {
                        using var fs = File.Open(expectedOutput, FileMode.Open, FileAccess.Read, FileShare.None);
                        if (fs.Length > 0) { fileReady = true; break; }
                    }
                    catch (IOException) { }
                }

                Thread.Sleep(500);
            }

            if (!fileReady)
                return ToolResult.Failure(
                    "Conversion completed but the output DOCX was not created or timed out.\n" +
                    $"Expected at: {expectedOutput}");

            if (!string.Equals(expectedOutput, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(expectedOutput, outputPath);
            }

            var fileSize = new FileInfo(outputPath).Length;
            return ToolResult.Success(
                $"Converted successfully ({fileSize / 1024} KB).",
                [outputPath]);
        }
        catch (OperationCanceledException)
        {
            TryDelete(expectedOutput);
            TryDelete(outputPath);
            return ToolResult.Failure("Conversion was cancelled.");
        }
        catch (FileNotFoundException) { throw; }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Conversion failed:\n{ex.ToString()}", ex);
        }
    }

    private static string? ResolveSofficePath()
    {
        var possiblePaths = new[]
        {
            @"_copies_\LibreOfficePortable\App\libreoffice\program\soffice.exe",
            @"_copies_\LibreOfficePortable\LibreOfficePortable.exe",
            @"_copies_\LibreOffice\LibreOfficePortable.exe",
            @"_copies_\LibreOffice\program\soffice.exe",
            @"_copies_\program\soffice.exe"
        };

        var basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var dir = new DirectoryInfo(basePath);
        while (dir is not null)
        {
            var dirNameLower = dir.FullName.ToLowerInvariant();
            if (dirNameLower.Contains(@"\bin\") || dirNameLower.Contains(@"\obj\"))
            { dir = dir.Parent; continue; }

            foreach (var relativePath in possiblePaths)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
