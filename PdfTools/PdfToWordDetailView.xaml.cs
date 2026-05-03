using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ConversionApp.PdfTools;

public sealed partial class PdfToWordDetailView : UserControl
{
    public PdfToWordViewModel ViewModel { get; private set; } = null!;
    private IntPtr _windowHandle;

    public event Action? BackRequested;

    public PdfToWordDetailView()
    {
        this.InitializeComponent();
    }

    public void Initialize(IntPtr windowHandle, DispatcherQueue dispatcherQueue)
    {
        _windowHandle = windowHandle;
        ViewModel = new PdfToWordViewModel(dispatcherQueue)
        {
            WindowHandle = windowHandle,
        };
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
        picker.FileTypeFilter.Add(".pdf");
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
            e.DragUIOverride.Caption = "Drop to convert";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            DropZone.Opacity = 0.8;
        }
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;

        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            if (item is StorageFile file &&
                file.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                await AcceptFile(file);
                return;
            }
        }
    }

    private async Task AcceptFile(StorageFile file)
    {
        ViewModel.InputFilePath  = file.Path;
        ViewModel.OutputFilePath = System.IO.Path.ChangeExtension(file.Path, ".docx");
        ViewModel.StatusText     = "Ready to convert.";
        ViewModel.IsSuccess      = false;
        ViewModel.IsError        = false;

        SelectedFileName.Text = file.Name;
        var props = await file.GetBasicPropertiesAsync();
        SelectedFileSize.Text = FormatFileSize(props.Size);

        FileSelectionPhase.Visibility = Visibility.Collapsed;
        ConvertingPhase.Visibility    = Visibility.Visible;
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.InputFilePath  = string.Empty;
        ViewModel.OutputFilePath = string.Empty;

        FileSelectionPhase.Visibility = Visibility.Visible;
        ConvertingPhase.Visibility    = Visibility.Collapsed;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private static string FormatFileSize(ulong bytes) => bytes switch
    {
        < 1024         => $"{bytes} B",
        < 1048576      => $"{bytes / 1024.0:F1} KB",
        < 1073741824   => $"{bytes / 1048576.0:F1} MB",
        _              => $"{bytes / 1073741824.0:F2} GB",
    };

    public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
