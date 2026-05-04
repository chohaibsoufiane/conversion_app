using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConversionApp.Models;
using ConversionApp.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace ConversionApp.ViewModels;

/// <summary>
/// ViewModel for the main batch converter window.
/// Uses CommunityToolkit.Mvvm source generators for boilerplate-free MVVM.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;

    // -------------------------------------------------------------------------
    // Observable properties
    // -------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartConversionCommand))]
    private string _inputDirectory = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartConversionCommand))]
    private string _outputDirectory = string.Empty;

    public ObservableCollection<string> LogMessages { get; } = new();

    public ObservableCollection<ConversionHistoryItem> HistoryItems => HistoryService.Instance.Items;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartConversionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectInputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectOutputFolderCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private ConversionType _currentConversionType = ConversionType.WordToPdf;

    [ObservableProperty]
    private string _activeToolTitle = "Word to PDF";

    [ObservableProperty]
    private string _sourceFormat = "Word";

    [ObservableProperty]
    private string _targetFormat = "PDF";

    [ObservableProperty]
    private string _inputFilePath = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    /// <summary>
    /// The flow direction for the log panel, dynamically set based on
    /// whether the majority of processed filenames are RTL or LTR.
    /// </summary>
    [ObservableProperty]
    private FlowDirection _logFlowDirection = FlowDirection.LeftToRight;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isError;

    // -------------------------------------------------------------------------
    // Window handle — set by code-behind for FolderPicker interop
    // -------------------------------------------------------------------------

    /// <summary>
    /// The native HWND of the host window.  Must be set from code-behind
    /// before any folder picker command is invoked, otherwise WinRT will
    /// throw a COMException.
    /// </summary>
    public IntPtr WindowHandle { get; set; }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public MainWindowViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    // -------------------------------------------------------------------------
    // Commands — folder selection
    // -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task SelectInputFolderAsync()
    {
        var path = await PickFolderAsync();
        if (path is not null)
            InputDirectory = path;
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task SelectOutputFolderAsync()
    {
        var path = await PickFolderAsync();
        if (path is not null)
            OutputDirectory = path;
    }

    private bool CanBrowse() => !IsProcessing;

    // -------------------------------------------------------------------------
    // Command — batch conversion
    // -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanStartConversion))]
    private async Task StartConversionAsync()
    {
        IsProcessing = true;
        LogMessages.Clear();
        StatusText   = "Processing...";

        // Reset to LTR before we know the file language
        LogFlowDirection = FlowDirection.LeftToRight;

        AppendLog("═══════════════════════════════════════════════════");
        AppendLog("  Batch Document Conversion — Starting");
        AppendLog("═══════════════════════════════════════════════════");
        AppendLog($"  Input:  {InputDirectory}");
        AppendLog($"  Output: {OutputDirectory}");
        AppendLog("───────────────────────────────────────────────────");

        var inputDir  = InputDirectory;
        var outputDir = OutputDirectory;

        try
        {
            var processor = new DocumentBatchProcessor
            {
                MaxDegreeOfParallelism = -1,
            };

            // Run the CPU-bound batch on a background thread so the
            // UI thread stays responsive.  The log callback marshals
            // each message back to the UI thread via DispatcherQueue.
            var result = await Task.Run(() =>
                processor.ProcessBatch(inputDir, outputDir, msg => AppendLog(msg)));

            // Update flow direction based on detected language
            LogFlowDirection = result.IsRtl
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            // Final summary
            AppendLog("───────────────────────────────────────────────────");
            AppendLog($"  Total:     {result.Total}");
            AppendLog($"  Succeeded: {result.Succeeded}");
            AppendLog($"  Failed:    {result.Failed}");

            if (result.Errors.Count > 0)
            {
                AppendLog("");
                AppendLog("  Failed files:");
                foreach (var err in result.Errors)
                    AppendLog($"    \u2022 {Path.GetFileName(err.FilePath)}: {err.Reason}");
            }

            AppendLog("═══════════════════════════════════════════════════");

            StatusText = result.Failed == 0
                ? $"Done — {result.Succeeded} file(s) converted successfully."
                : $"Done — {result.Succeeded} succeeded, {result.Failed} failed.";
        }
        catch (Exception ex)
        {
            AppendLog($"\u2717 FATAL: {ex.Message}");
            StatusText = "Error — see log for details.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void SwapDirection()
    {
        switch (CurrentConversionType)
        {
            case ConversionType.WordToPdf:
                CurrentConversionType = ConversionType.PdfToWord;
                ActiveToolTitle = "PDF to Word";
                SourceFormat = "PDF";
                TargetFormat = "Word";
                break;
            case ConversionType.PdfToWord:
                CurrentConversionType = ConversionType.WordToPdf;
                ActiveToolTitle = "Word to PDF";
                SourceFormat = "Word";
                TargetFormat = "PDF";
                break;
            case ConversionType.ExcelToPdf:
                CurrentConversionType = ConversionType.PdfToExcel;
                ActiveToolTitle = "PDF to Excel";
                SourceFormat = "PDF";
                TargetFormat = "Excel";
                break;
            case ConversionType.PdfToExcel:
                CurrentConversionType = ConversionType.ExcelToPdf;
                ActiveToolTitle = "Excel to PDF";
                SourceFormat = "Excel";
                TargetFormat = "PDF";
                break;
            case ConversionType.ImageToPdf:
                // No reverse direction implemented yet, so just keep it
                break;
        }

        // Auto-update extensions if paths exist
        if (!string.IsNullOrEmpty(InputFilePath))
        {
            var ext = CurrentConversionType switch
            {
                ConversionType.WordToPdf => ".pdf",
                ConversionType.PdfToWord => ".docx",
                ConversionType.ExcelToPdf => ".pdf",
                ConversionType.PdfToExcel => ".xlsx",
                ConversionType.ImageToPdf => ".pdf",
                _ => ".pdf"
            };
            OutputFilePath = Path.ChangeExtension(InputFilePath, ext);
        }
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (string.IsNullOrEmpty(InputFilePath) || string.IsNullOrEmpty(OutputFilePath))
        {
            StatusText = "Please select input and output files.";
            return;
        }

        IsProcessing = true;
        IsStatusVisible = true;
        IsSuccess = false;
        IsError = false;
        StatusText = "Initializing engine...";

        try
        {
            await Task.Run(async () =>
            {
                switch (CurrentConversionType)
                {
                    case ConversionType.WordToPdf:
                        await LibreOfficeProcessor.ConvertAsync(InputFilePath, OutputFilePath, "pdf");
                        break;
                    case ConversionType.PdfToWord:
                        await LibreOfficeProcessor.ConvertAsync(InputFilePath, OutputFilePath, "docx");
                        break;
                    case ConversionType.ExcelToPdf:
                        await LibreOfficeProcessor.ConvertAsync(InputFilePath, OutputFilePath, "pdf");
                        break;
                    case ConversionType.PdfToExcel:
                        await LibreOfficeProcessor.ConvertAsync(InputFilePath, OutputFilePath, "xlsx");
                        break;
                    case ConversionType.ImageToPdf:
                        await LibreOfficeProcessor.ConvertAsync(InputFilePath, OutputFilePath, "pdf");
                        break;
                }
            });

            StatusText = "Conversion successful!";
            IsSuccess = true;
            HistoryService.Instance.AddItem(OutputFilePath, ActiveToolTitle, CurrentConversionType == ConversionType.PdfToWord ? "\uE8A5" : "\uEA90");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            IsError = true;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void OpenFile(ConversionHistoryItem? item)
    {
        if (item == null || !File.Exists(item.FilePath)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { /* Ignore open errors */ }
    }

    [RelayCommand]
    private void OpenOutput()
    {
        if (string.IsNullOrEmpty(OutputFilePath) || !File.Exists(OutputFilePath)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OutputFilePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { /* Ignore open errors */ }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrEmpty(OutputFilePath)) return;
        var folder = Path.GetDirectoryName(OutputFilePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { /* Ignore open errors */ }
    }

    private bool CanStartConversion() =>
        !IsProcessing &&
        !string.IsNullOrWhiteSpace(InputDirectory) &&
        !string.IsNullOrWhiteSpace(OutputDirectory);

    // -------------------------------------------------------------------------
    // Logging helper — thread-safe UI update
    // -------------------------------------------------------------------------

    private readonly ConcurrentQueue<string> _logQueue = new();
    private int _isLogTimerRunning = 0;

    /// <summary>
    /// Appends a line to the log.
    /// Safe to call from any thread. Uses a batched dispatch queue to prevent
    /// freezing the UI thread when thousands of files are processed rapidly.
    /// </summary>
    private void AppendLog(string message)
    {
        _logQueue.Enqueue(message);

        // Ensure only one flush operation is queued at a time
        if (Interlocked.Exchange(ref _isLogTimerRunning, 1) == 0)
        {
            _dispatcherQueue.TryEnqueue(FlushLogs);
        }
    }

    private void FlushLogs()
    {
        int count = 0;
        // Batch size limit: process up to 100 lines per tick so the UI stays responsive
        while (count < 100 && _logQueue.TryDequeue(out var msg))
        {
            LogMessages.Add(msg);
            count++;
        }

        Interlocked.Exchange(ref _isLogTimerRunning, 0);

        // If items are still left, re-queue the flush for the next UI tick
        if (!_logQueue.IsEmpty && Interlocked.Exchange(ref _isLogTimerRunning, 1) == 0)
        {
            _dispatcherQueue.TryEnqueue(FlushLogs);
        }
    }

    // -------------------------------------------------------------------------
    // Folder picker — requires HWND for WinRT interop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the native Windows folder picker dialog.
    /// Returns the selected folder path or <c>null</c> if cancelled.
    /// </summary>
    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        // CRITICAL: WinUI 3 (unpackaged) requires the Window HWND to be
        // associated with the picker, otherwise a COMException is thrown.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
