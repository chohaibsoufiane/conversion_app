using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ConversionApp.Services;

public static class LibreOfficeProcessor
{
    private const int TimeoutSeconds = 120;

    public static async Task ConvertAsync(string inputPath, string outputPath, string format, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var outputDir = Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Invalid output path.");
        Directory.CreateDirectory(outputDir);

        var sofficePath = ResolveSofficePath();
        if (sofficePath == null || !File.Exists(sofficePath))
            throw new FileNotFoundException("LibreOffice engine not found.");

        var arguments = $"--headless --invisible --nologo --nodefault --nofirststartwizard " +
                        $"--convert-to {format} \"{inputPath}\" --outdir \"{outputDir}\"";

        var psi = new ProcessStartInfo
        {
            FileName = sofficePath,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(sofficePath)!
        };

        // Expected output name from LibreOffice (it uses the input filename + new extension)
        var expectedOutput = Path.Combine(outputDir, Path.ChangeExtension(Path.GetFileName(inputPath), format));
        
        // Cleanup existing to avoid ambiguity
        if (File.Exists(expectedOutput)) File.Delete(expectedOutput);
        if (!string.Equals(expectedOutput, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to start LibreOffice.");

        await process.WaitForExitAsync(cancellationToken);

        // Poll for the file (LibreOfficePortable returns before the file is fully ready sometimes)
        var deadlineMs = Environment.TickCount64 + (TimeoutSeconds * 1000L);
        bool fileReady = false;

        while (Environment.TickCount64 < deadlineMs)
        {
            if (cancellationToken.IsCancellationRequested) break;

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
                catch (IOException) { /* File locked */ }
            }
            await Task.Delay(500, cancellationToken);
        }

        if (!fileReady) throw new TimeoutException("Conversion timed out or output file not found.");

        if (!string.Equals(expectedOutput, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(expectedOutput, outputPath, true);
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

        while (dir != null)
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
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
