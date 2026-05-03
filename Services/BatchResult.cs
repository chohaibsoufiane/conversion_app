namespace ConversionApp.Services;

/// <summary>
/// Immutable snapshot of a batch run's execution metrics.
/// </summary>
/// <param name="Total">Total number of .docx files discovered.</param>
/// <param name="Succeeded">Number of files successfully converted to JSON.</param>
/// <param name="Failed">Number of files that threw an unrecoverable exception.</param>
/// <param name="Errors">Ordered list of per-file error details.</param>
public sealed record BatchResult(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<FileError> Errors,
    bool IsRtl);

/// <summary>
/// Captures the failure details for a single document that could not be parsed.
/// </summary>
/// <param name="FilePath">Absolute path to the file that failed.</param>
/// <param name="Reason">Human-readable error message from the caught exception.</param>
public sealed record FileError(string FilePath, string Reason);
