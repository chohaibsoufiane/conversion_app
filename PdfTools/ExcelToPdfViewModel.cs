using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConversionApp.PdfTools.Tools;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;

namespace ConversionApp.PdfTools;

/// <summary>
/// ViewModel for the Excel-to-PDF tool detail view.
/// Handles file picking, background execution, and status reporting.
/// </summary>
public partial class ExcelToPdfViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;

    // -------------------------------------------------------------------------
    // Observable properties
    // -------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private string _inputFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectInputFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectOutputFileCommand))]
    private bool _isConverting;

    [ObservableProperty]
    private string _statusText = "Select a spreadsheet file to convert.";

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isStatusVisible;

    // -------------------------------------------------------------------------
    // Window handle — required for WinRT file picker interop
    // -------------------------------------------------------------------------

    public IntPtr WindowHandle { get; set; }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ExcelToPdfViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    // -------------------------------------------------------------------------
    // Commands — file picking
    // -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task SelectInputFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xls");
        picker.FileTypeFilter.Add(".ods");
        picker.FileTypeFilter.Add(".csv");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        InputFilePath = file.Path;

        if (string.IsNullOrWhiteSpace(OutputFilePath))
        {
            OutputFilePath = Path.ChangeExtension(file.Path, ".pdf");
        }

        StatusText = "Ready to convert.";
        IsSuccess  = false;
        IsError    = false;
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task SelectOutputFileAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName      = Path.GetFileNameWithoutExtension(InputFilePath),
        };
        picker.FileTypeChoices.Add("PDF Document", [".pdf"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            OutputFilePath = file.Path;
        }
    }

    private bool CanInteract() => !IsConverting;

    // -------------------------------------------------------------------------
    // Command — conversion
    // -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        IsConverting = true;
        IsSuccess    = false;
        IsError      = false;
        IsStatusVisible = true;
        StatusText   = "Starting conversion engine...";

        var engine  = new ExcelToPdfEngine();
        var request = ToolRequest.Create(InputFilePath, OutputFilePath);

        try
        {
            StatusText = "Converting spreadsheet to PDF...";
            await Task.Delay(100);
            
            var result = await Task.Run(() => engine.Execute(request));

            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusText   = result.Message;
                IsSuccess    = result.IsSuccess;
                IsError      = !result.IsSuccess;
                IsConverting = false;
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusText   = $"Unexpected error:\n{ex.ToString()}";
                IsError      = true;
                IsConverting = false;
            });
        }
    }

    private bool CanConvert() =>
        !IsConverting &&
        !string.IsNullOrWhiteSpace(InputFilePath) &&
        !string.IsNullOrWhiteSpace(OutputFilePath);

    // -------------------------------------------------------------------------
    // Commands — post-conversion actions
    // -------------------------------------------------------------------------

    [RelayCommand]
    private void OpenFile()
    {
        if (File.Exists(OutputFilePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = OutputFilePath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (File.Exists(OutputFilePath))
        {
            string argument = $"/select,\"{OutputFilePath}\"";
            System.Diagnostics.Process.Start("explorer.exe", argument);
        }
        else
        {
            string directory = Path.GetDirectoryName(OutputFilePath) ?? string.Empty;
            if (Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start("explorer.exe", directory);
            }
        }
    }

    [RelayCommand]
    private void CopyError()
    {
        if (!string.IsNullOrEmpty(StatusText))
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(StatusText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
    }
}
