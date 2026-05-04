using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ConversionApp.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ConversionApp.PdfTools;

public sealed partial class ConversionDetailView : UserControl
{
    public MainWindowViewModel ViewModel { get; private set; } = null!;
    private IntPtr _windowHandle;

    public event Action? BackRequested;

    public ConversionDetailView()
    {
        this.InitializeComponent();
    }

    public void Initialize(MainWindowViewModel viewModel, IntPtr windowHandle)
    {
        ViewModel = viewModel;
        _windowHandle = windowHandle;
        Bindings.Update();

        FileSelectionPhase.Visibility = Visibility.Visible;
        ConvertingPhase.Visibility    = Visibility.Collapsed;
    }

    private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        
        // Dynamic filter based on conversion type
        switch (ViewModel.CurrentConversionType)
        {
            case Models.ConversionType.WordToPdf:
                picker.FileTypeFilter.Add(".docx");
                break;
            case Models.ConversionType.PdfToWord:
            case Models.ConversionType.PdfToExcel:
                picker.FileTypeFilter.Add(".pdf");
                break;
            case Models.ConversionType.ExcelToPdf:
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".xls");
                break;
            case Models.ConversionType.ImageToPdf:
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".bmp");
                break;
        }
        
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        await AcceptFile(file);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            DropZone.Opacity = 0.8;
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e) => DropZone.Opacity = 1.0;

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count > 0 && items[0] is StorageFile file)
        {
            await AcceptFile(file);
        }
    }

    private async Task AcceptFile(StorageFile file)
    {
        ViewModel.InputFilePath = file.Path;
        
        var ext = ViewModel.CurrentConversionType switch
        {
            Models.ConversionType.WordToPdf => ".pdf",
            Models.ConversionType.PdfToWord => ".docx",
            Models.ConversionType.ExcelToPdf => ".pdf",
            Models.ConversionType.PdfToExcel => ".xlsx",
            Models.ConversionType.ImageToPdf => ".pdf",
            _ => ".pdf"
        };
        
        ViewModel.OutputFilePath = System.IO.Path.ChangeExtension(file.Path, ext);
        ViewModel.StatusText = "Ready to convert.";
        ViewModel.IsStatusVisible = false;
        ViewModel.IsSuccess = false;
        ViewModel.IsError = false;

        SelectedFileName.Text = file.Name;
        var props = await file.GetBasicPropertiesAsync();
        SelectedFileSize.Text = $"{props.Size / 1024.0:F1} KB";

        FileSelectionPhase.Visibility = Visibility.Collapsed;
        ConvertingPhase.Visibility    = Visibility.Visible;
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.InputFilePath = string.Empty;
        ViewModel.OutputFilePath = string.Empty;
        ViewModel.IsStatusVisible = false;
        FileSelectionPhase.Visibility = Visibility.Visible;
        ConvertingPhase.Visibility    = Visibility.Collapsed;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SwapDirectionCommand.CanExecute(null))
        {
            ViewModel.SwapDirectionCommand.Execute(null);
            SwapAnimation.Begin();
        }
    }

    public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
