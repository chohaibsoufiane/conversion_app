using System.Diagnostics;

namespace ConversionApp.PdfTools.Tools;

/// <summary>
/// Excel-to-PDF converter powered by the bundled LibreOffice Portable runtime.
///
/// Identical execution strategy to <see cref="LibreOfficeEngine"/> — headless,
/// polling-based wait, skip-bin path resolution — but configured specifically
/// for spreadsheet file types (.xlsx, .xls, .ods, .csv).
/// </summary>
public sealed class ExcelToPdfEngine : IDocumentTool
{
    // -------------------------------------------------------------------------
    // IDocumentTool metadata
    // -------------------------------------------------------------------------

    public string Name        => "Excel to PDF";
    public string Description => "High-fidelity conversion between Microsoft Excel and PDF formats.";

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>Maximum seconds to wait before giving up.</summary>
    private const int TimeoutSeconds = 120;

    /// <summary>Supported input extensions (case-insensitive check).</summary>
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".ods", ".csv"
    };

    // -------------------------------------------------------------------------
    // Execute
    // -------------------------------------------------------------------------

    public ToolResult Execute(ToolRequest request, CancellationToken cancellationToken = default)
    {
        // ── Validate inputs ─────────────────────────────────────────────
        if (request.InputPaths.Count == 0)
            return ToolResult.Failure("No input file specified.");

        var inputPath = Path.GetFullPath(request.InputPaths[0]);

        if (!File.Exists(inputPath))
            return ToolResult.Failure($"Input file not found: '{inputPath}'");

        var ext = Path.GetExtension(inputPath);
        if (!SupportedExtensions.Contains(ext))
            return ToolResult.Failure($"Unsupported file type: '{ext}'. Expected .xlsx, .xls, .ods, or .csv.");

        // ── Resolve output path ─────────────────────────────────────────
        var outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.ChangeExtension(inputPath, ".pdf")
            : Path.GetFullPath(request.OutputPath);

        var outputDir = Path.GetDirectoryName(outputPath)
                        ?? Path.GetDirectoryName(inputPath)!;

        var expectedOutput = Path.Combine(
            outputDir,
            Path.ChangeExtension(Path.GetFileName(inputPath), ".pdf"));

        Directory.CreateDirectory(outputDir);

        // ── Locate LibreOffice ──────────────────────────────────────────
        var sofficePath = ResolveSofficePath();

        if (sofficePath is null || !File.Exists(sofficePath))
        {
            throw new FileNotFoundException(
                $"LibreOffice engine not found.\n" +
                $"Please place your portable LibreOffice installation inside the '_copies_' folder.\n" +
                $"We search for 'LibreOfficePortable.exe' inside any subfolder of '_copies_'.");
        }

        // ── Build process arguments ─────────────────────────────────────
        var arguments =
            $"--headless --invisible --nologo --nodefault --nofirststartwizard " +
            $"--convert-to pdf \"{inputPath}\" --outdir \"{outputDir}\"";

        // ── Execute ─────────────────────────────────────────────────────
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
            {
                TryDelete(outputPath);
            }

            using var process = Process.Start(psi);
            if (process is null)
                return ToolResult.Failure("Failed to start LibreOffice process.");

            process.WaitForExit();

            // Poll for the output file
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
                        if (fs.Length > 0)
                        {
                            fileReady = true;
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        // File still being written
                    }
                }

                Thread.Sleep(500);
            }

            if (!fileReady)
                return ToolResult.Failure(
                    "Conversion completed but the output PDF was not created or timed out.\n" +
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
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Conversion failed:\n{ex.ToString()}", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Path resolution (shared logic with LibreOfficeEngine)
    // -------------------------------------------------------------------------

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
            {
                dir = dir.Parent;
                continue;
            }

            foreach (var relativePath in possiblePaths)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
