using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConversionApp.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ConversionApp.Services;

/// <summary>
/// High-throughput batch processor that converts a directory of .docx files
/// into structured JSON files conforming to the Block Editor schema.
///
/// Thread-safety: all shared mutable state is protected via
/// <see cref="Interlocked"/> operations or immutable reads.
///
/// Logging is fully decoupled — callers pass an <see cref="Action{String}"/>
/// callback so this class works in both console and GUI contexts.
/// </summary>
public sealed class DocumentBatchProcessor
{
    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Controls the maximum degree of parallelism.
    /// -1 → let the runtime decide (= Environment.ProcessorCount).
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;

    // -------------------------------------------------------------------------
    // JSON serialisation settings (shared, thread-safe after construction)
    // -------------------------------------------------------------------------
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters           = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static readonly Regex WhitespaceNormaliser =
        new(@"\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // -------------------------------------------------------------------------
    // RTL detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether a character belongs to an RTL script (Arabic, Hebrew, etc.).
    /// </summary>
    private static bool IsRtlChar(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        // Arabic: 0x0600-0x06FF, 0x0750-0x077F, 0x08A0-0x08FF, 0xFB50-0xFDFF, 0xFE70-0xFEFF
        // Hebrew: 0x0590-0x05FF, 0xFB1D-0xFB4F
        return c >= 0x0590 && c <= 0x05FF   // Hebrew
            || c >= 0x0600 && c <= 0x06FF   // Arabic
            || c >= 0x0750 && c <= 0x077F   // Arabic Supplement
            || c >= 0x08A0 && c <= 0x08FF   // Arabic Extended-A
            || c >= 0xFB1D && c <= 0xFB4F   // Hebrew Presentation Forms
            || c >= 0xFB50 && c <= 0xFDFF   // Arabic Presentation Forms-A
            || c >= 0xFE70 && c <= 0xFEFF;  // Arabic Presentation Forms-B
    }

    /// <summary>
    /// Determines whether the majority of filenames contain RTL script characters.
    /// </summary>
    private static bool DetectRtlMajority(string[] filePaths)
    {
        int rtlFiles = 0;
        int ltrFiles = 0;

        foreach (var path in filePaths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            int rtlChars = 0;
            int ltrChars = 0;

            foreach (var c in name)
            {
                if (IsRtlChar(c))
                    rtlChars++;
                else if (char.IsLetter(c))
                    ltrChars++;
            }

            if (rtlChars > ltrChars)
                rtlFiles++;
            else if (ltrChars > 0)
                ltrFiles++;
        }

        return rtlFiles > ltrFiles;
    }

    // -------------------------------------------------------------------------
    // Log line formatting — adapts to detected text direction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Formats a success log line.
    /// LTR: ✓ input.docx → output.json  (16 blocks)
    /// RTL: ✓ output.json ← input.docx  (16 blocks)
    /// In RTL reading order (right-to-left), you see source first on the right,
    /// arrow pointing left toward the destination on the left.
    /// </summary>
    private static string FormatSuccessLine(string inputName, string outputName, int blockCount, bool isRtl)
    {
        if (isRtl)
        {
            // RTL: destination ← source  (reading R→L: source on right, dest on left)
            return $"\u2713 {outputName} \u2190 {inputName}  ({blockCount} blocks)";
        }
        else
        {
            // LTR: source → destination
            return $"\u2713 {inputName} \u2192 {outputName}  ({blockCount} blocks)";
        }
    }

    /// <summary>
    /// Formats an error log line.
    /// </summary>
    private static string FormatErrorLine(string fileName, string message)
    {
        return $"\u2717 ERROR  {fileName}: {message}";
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Discovers all .docx files in <paramref name="inputDirectory"/>,
    /// converts each to a structured JSON file in <paramref name="outputDirectory"/>,
    /// and returns a <see cref="BatchResult"/> containing execution metrics.
    /// </summary>
    /// <param name="inputDirectory">Absolute path to the source .docx directory.</param>
    /// <param name="outputDirectory">Absolute path for .json output. Created if absent.</param>
    /// <param name="log">
    ///   Optional logging callback invoked for each significant event.
    ///   Called from worker threads — callers are responsible for marshalling
    ///   to the UI thread if needed.
    /// </param>
    public BatchResult ProcessBatch(
        string inputDirectory,
        string outputDirectory,
        Action<string>? log = null)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException(
                $"Input directory not found: '{inputDirectory}'");

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.GetFiles(inputDirectory, "*.docx", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            log?.Invoke("No .docx files found in the input directory.");
            return new BatchResult(Total: 0, Succeeded: 0, Failed: 0, Errors: [], IsRtl: false);
        }

        // Detect dominant text direction from filenames
        bool isRtl = DetectRtlMajority(files);

        log?.Invoke($"Found {files.Length} .docx file(s). Starting parallel conversion...");

        int succeeded = 0;
        int failed    = 0;
        var errors    = new ConcurrentBag<FileError>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        };

        Parallel.ForEach(files, parallelOptions, filePath =>
        {
            try
            {
                ProcessSingleFile(filePath, outputDirectory, log, isRtl);
                Interlocked.Increment(ref succeeded);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                errors.Add(new FileError(filePath, ex.Message));
                log?.Invoke(FormatErrorLine(Path.GetFileName(filePath), ex.Message));
            }
        });

        return new BatchResult(
            Total:     files.Length,
            Succeeded: succeeded,
            Failed:    failed,
            Errors:    [.. errors],
            IsRtl:     isRtl);
    }

    // -------------------------------------------------------------------------
    // Single-file pipeline
    // -------------------------------------------------------------------------

    private static void ProcessSingleFile(
        string filePath,
        string outputDirectory,
        Action<string>? log,
        bool isRtl)
    {
        var document = ParseDocx(filePath);

        var outputFileName = Path.ChangeExtension(
            Path.GetFileName(filePath), ".json");
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(outputPath, json, Encoding.UTF8);

        log?.Invoke(FormatSuccessLine(
            Path.GetFileName(filePath), outputFileName, document.TotalBlocks, isRtl));
    }

    // -------------------------------------------------------------------------
    // OpenXML parsing
    // -------------------------------------------------------------------------

    private static LegalDocument ParseDocx(string filePath)
    {
        var blocks = new List<DocBlock>();

        using var wordDoc = WordprocessingDocument.Open(filePath, isEditable: false);

        var mainPart = wordDoc.MainDocumentPart
            ?? throw new InvalidOperationException(
                "The document has no MainDocumentPart — it may be corrupt or empty.");

        var body = mainPart.Document?.Body
            ?? throw new InvalidOperationException(
                "The document body is null — the file may be corrupt.");

        var stylesPart = mainPart.StyleDefinitionsPart;

        foreach (var element in body.Elements<Paragraph>())
        {
            var rawText = ExtractParagraphText(element);

            if (string.IsNullOrWhiteSpace(rawText))
                continue;

            var cleanText = WhitespaceNormaliser.Replace(rawText, " ").Trim();
            var styleName = ResolveParagraphStyleName(element, stylesPart);
            var blockType = StyleMapper.Resolve(styleName, cleanText);

            blocks.Add(DocBlock.Create(blockType, cleanText));
        }

        return new LegalDocument
        {
            DocumentName   = Path.GetFileName(filePath),
            SourcePath     = Path.GetFullPath(filePath),
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            Blocks         = blocks,
        };
    }

    // -------------------------------------------------------------------------
    // Text extraction
    // -------------------------------------------------------------------------

    private static string ExtractParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();

        foreach (var run in paragraph.Elements<Run>())
        {
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text t:
                        sb.Append(t.Text);
                        break;
                    case Break:
                        sb.Append(' ');
                        break;
                    case TabChar:
                        sb.Append(' ');
                        break;
                }
            }

            if (sb.Length > 0 && sb[^1] != ' ')
                sb.Append(' ');
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Style name resolution
    // -------------------------------------------------------------------------

    private static string? ResolveParagraphStyleName(
        Paragraph paragraph,
        StyleDefinitionsPart? stylesPart)
    {
        var styleId = paragraph
            .ParagraphProperties?
            .ParagraphStyleId?
            .Val?
            .Value;

        if (styleId is null)
            return null;

        if (stylesPart?.Styles is not null)
        {
            var matchingStyle = stylesPart.Styles
                .Elements<Style>()
                .FirstOrDefault(s => string.Equals(
                    s.StyleId?.Value, styleId,
                    StringComparison.OrdinalIgnoreCase));

            var friendlyName = matchingStyle?.StyleName?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(friendlyName))
                return friendlyName;
        }

        return styleId;
    }
}
