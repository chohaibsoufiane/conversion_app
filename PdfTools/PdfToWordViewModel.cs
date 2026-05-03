using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConversionApp.PdfTools.Tools;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;

namespace ConversionApp.PdfTools;

public partial class PdfToWordViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;

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
    private string _statusText = "Select a PDF file to convert.";

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isStatusVisible;

    public IntPtr WindowHandle { get; set; }

    public PdfToWordViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task SelectInputFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".pdf");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        InputFilePath = file.Path;

        if (string.IsNullOrWhiteSpace(OutputFilePath))
            OutputFilePath = Path.ChangeExtension(file.Path, ".docx");

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
        picker.FileTypeChoices.Add("Word Document", [".docx"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
            OutputFilePath = file.Path;
    }

    private bool CanInteract() => !IsConverting;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        IsConverting    = true;
        IsSuccess       = false;
        IsError         = false;
        IsStatusVisible = true;
        StatusText      = "Starting conversion engine...";

        var engine  = new PdfToWordEngine();
        var request = ToolRequest.Create(InputFilePath, OutputFilePath);

        try
        {
            StatusText = "Converting PDF to Word...";
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
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{OutputFilePath}\"");
        }
        else
        {
            string directory = Path.GetDirectoryName(OutputFilePath) ?? string.Empty;
            if (Directory.Exists(directory))
                System.Diagnostics.Process.Start("explorer.exe", directory);
        }
    }

    [RelayCommand]
    private void CopyError()
    {
        if (!string.IsNullOrEmpty(StatusText))
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(StatusText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
    }
}
