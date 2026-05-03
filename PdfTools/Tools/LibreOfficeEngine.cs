using System.Diagnostics;

namespace ConversionApp.PdfTools.Tools;

/// <summary>
/// Pixel-perfect document-to-PDF converter powered by a locally bundled
/// LibreOffice Portable runtime.
///
/// The engine shells out to <c>soffice.exe</c> in fully headless mode —
/// no windows, no taskbar entries, no user interaction.  The conversion
/// uses LibreOffice's complete rendering pipeline, producing output
/// identical to "Print → PDF".
///
/// Expected layout relative to the application's base directory:
///   <c>_copies_\LibreOfficePortable\App\libreoffice\program\soffice.exe</c>
/// </summary>
public sealed class LibreOfficeEngine : IDocumentTool
{
    // -------------------------------------------------------------------------
    // IDocumentTool metadata
    // -------------------------------------------------------------------------

    public string Name        => "Word to PDF";
    public string Description => "Pixel-perfect conversion powered by a bundled LibreOffice engine.";

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Relative path from the application's base directory to the bundled
    /// LibreOffice Portable <c>LibreOfficePortable.exe</c>.
    /// </summary>
    private const string RelativeSofficePath =
        @"_copies_\LibreOfficePortable\LibreOfficePortable.exe";

    /// <summary>Maximum seconds to wait before killing the process.</summary>
    private const int TimeoutSeconds = 120;

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

        // ── Resolve output path ─────────────────────────────────────────
        var outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? Path.ChangeExtension(inputPath, ".pdf")
            : Path.GetFullPath(request.OutputPath);

        var outputDir = Path.GetDirectoryName(outputPath)
                        ?? Path.GetDirectoryName(inputPath)!;

        // LibreOffice writes <original-name>.pdf into --outdir
        var expectedOutput = Path.Combine(
            outputDir,
            Path.ChangeExtension(Path.GetFileName(inputPath), ".pdf"));

        Directory.CreateDirectory(outputDir);

        // ── Locate soffice.exe ──────────────────────────────────────────
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

            // Remove existing file to avoid false positives
            TryDelete(expectedOutput);
            if (!string.Equals(expectedOutput, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(outputPath);
            }

            using var process = Process.Start(psi);
            if (process is null)
                return ToolResult.Failure("Failed to start LibreOffice process.");

            // The launcher (soffice.exe) delegates to soffice.bin and exits almost immediately.
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
                        // Check if the file is completely written and unlocked
                        using var fs = File.Open(expectedOutput, FileMode.Open, FileAccess.Read, FileShare.None);
                        if (fs.Length > 0)
                        {
                            fileReady = true;
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        // File is locked, LibreOffice is still writing it
                    }
                }
                else
                {
                    // We must just wait for the deadline because LibreOfficePortable.exe 
                    // takes a variable amount of time to spawn soffice.bin.
                    // DO NOT abort early here.
                }

                Thread.Sleep(500); // Polling interval
            }

            // ── Verify output file exists ───────────────────────────────
            if (!fileReady)
                return ToolResult.Failure(
                    "Conversion completed but the output PDF was not created or timed out.\n" +
                    $"Expected at: {expectedOutput}");

            // If the user's requested path differs, move the file there
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
            throw; // re-throw — caller should see this
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Conversion failed:\n{ex.ToString()}", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Path resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks upward from <see cref="AppContext.BaseDirectory"/> (the build
    /// output, e.g. <c>bin\x64\Debug\…</c>) to the project root, then
    /// resolves the executable.  We explicitly skip any candidate found
    /// inside <c>bin\</c> or <c>obj\</c> directories because
    /// <c>LibreOfficePortable.exe</c> is a PortableApps launcher that
    /// breaks when copied away from its original install location.
    /// </summary>
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

        // Start from the executable's directory and walk upward.
        // We use Environment.ProcessPath to handle single-file deployments correctly.
        var basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var dir = new DirectoryInfo(basePath);

        while (dir is not null)
        {
            // Skip build-output directories — the copied exe doesn't work there
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
