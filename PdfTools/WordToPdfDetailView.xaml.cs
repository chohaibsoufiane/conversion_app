using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ConversionApp.PdfTools;

/// <summary>
/// Code-behind for the Word-to-PDF tool detail view.
///
/// Two-phase UI:
///   Phase 1 — Drop zone + "Choose File" button (file selection)
///   Phase 2 — File card + "Convert to PDF" button (conversion)
/// </summary>
public sealed partial class WordToPdfDetailView : UserControl
{
    public WordToPdfViewModel ViewModel { get; private set; } = null!;
    private IntPtr _windowHandle;

    /// <summary>Raised when the user wants to go back to the PDF Tools dashboard.</summary>
    public event Action? BackRequested;

    public WordToPdfDetailView()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Initializes the ViewModel. Must be called after the control is in a Window.
    /// </summary>
    public void Initialize(IntPtr windowHandle, DispatcherQueue dispatcherQueue)
    {
        _windowHandle = windowHandle;
        ViewModel = new WordToPdfViewModel(dispatcherQueue)
        {
            WindowHandle = windowHandle,
        };
        Bindings.Update();

        // Reset to Phase 1
        FileSelectionPhase.Visibility = Visibility.Visible;
        ConvertingPhase.Visibility    = Visibility.Collapsed;
    }

    // -------------------------------------------------------------------------
    // Choose File button
    // -------------------------------------------------------------------------

    private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".docx");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        await AcceptFile(file);
    }

    // -------------------------------------------------------------------------
    // Drag and drop handlers
    // -------------------------------------------------------------------------

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to convert";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;

            // Visual feedback — lighten the drop zone
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

        // Take the first .docx file from the dropped items
        foreach (var item in items)
        {
            if (item is StorageFile file &&
                file.FileType.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                await AcceptFile(file);
                return;
            }
        }
    }

    // -------------------------------------------------------------------------
    // File acceptance — transition from Phase 1 to Phase 2
    // -------------------------------------------------------------------------

    private async Task AcceptFile(StorageFile file)
    {
        ViewModel.InputFilePath  = file.Path;
        ViewModel.OutputFilePath = System.IO.Path.ChangeExtension(file.Path, ".pdf");
        ViewModel.StatusText     = "Ready to convert.";
        ViewModel.IsSuccess      = false;
        ViewModel.IsError        = false;

        // Display filename + size
        SelectedFileName.Text = file.Name;
        var props = await file.GetBasicPropertiesAsync();
        SelectedFileSize.Text = FormatFileSize(props.Size);

        // Switch to Phase 2
        FileSelectionPhase.Visibility = Visibility.Collapsed;
        ConvertingPhase.Visibility    = Visibility.Visible;
    }

    // -------------------------------------------------------------------------
    // Remove file — go back to Phase 1
    // -------------------------------------------------------------------------

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.InputFilePath  = string.Empty;
        ViewModel.OutputFilePath = string.Empty;

        FileSelectionPhase.Visibility = Visibility.Visible;
        ConvertingPhase.Visibility    = Visibility.Collapsed;
    }

    // -------------------------------------------------------------------------
    // Back navigation
    // -------------------------------------------------------------------------

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string FormatFileSize(ulong bytes) => bytes switch
    {
        < 1024         => $"{bytes} B",
        < 1048576      => $"{bytes / 1024.0:F1} KB",
        < 1073741824   => $"{bytes / 1048576.0:F1} MB",
        _              => $"{bytes / 1073741824.0:F2} GB",
    };

    public Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
