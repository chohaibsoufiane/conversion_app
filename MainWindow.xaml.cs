using ConversionApp.ViewModels;
using Microsoft.UI.Xaml;

namespace ConversionApp;

/// <summary>
/// Code-behind for MainWindow.
///
/// Responsibilities (kept intentionally thin):
///   1. Wire the ViewModel with the native HWND for folder picker interop.
///   2. Auto-scroll the log panel when new content arrives.
///   3. Set initial window dimensions.
///
/// All business logic lives in <see cref="MainWindowViewModel"/>.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        this.InitializeComponent();

        // --- Obtain the native window handle (HWND) ---
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // --- Create ViewModel with DispatcherQueue + HWND ---
        ViewModel = new MainWindowViewModel(DispatcherQueue)
        {
            WindowHandle = hwnd,
        };

        // --- Window configuration ---
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(920, 720));

        // --- Auto-scroll log when new messages arrive ---
        ViewModel.LogMessages.CollectionChanged += (sender, e) =>
        {
            if (ViewModel.LogMessages.Count > 0)
            {
                // Dispatch to ensure the ListView has rendered the new item before scrolling
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogListView.ScrollIntoView(ViewModel.LogMessages[^1]);
                });
            }
        };

        // Initialize default backdrop (Mica Alt)
        SetBackdrop(1);
    }

    private void BackdropComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (BackdropComboBox != null)
        {
            SetBackdrop(BackdropComboBox.SelectedIndex);
        }
    }

    private void SetBackdrop(int index)
    {
        switch (index)
        {
            case 0: // Mica (Default)
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
                break;
            case 1: // Mica (Alt - High Tint)
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
                break;
            case 2: // Acrylic (Glassy)
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                break;
        }
    }

    private void NavView_ItemInvoked(
        Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        HideAllViews();

        if (args.IsSettingsInvoked)
        {
            SettingsView.Visibility = Visibility.Visible;
        }
        else if (args.InvokedItemContainer?.Tag is string tag)
        {
            switch (tag)
            {
                case "PdfTools":
                    PdfToolsView.Visibility = Visibility.Visible;
                    PdfToolsView.RefreshCards();
                    break;
                default:
                    HomeView.Visibility = Visibility.Visible;
                    break;
            }
        }
        else
        {
            HomeView.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Navigates from the PDF dashboard into a specific tool's detail view.
    /// </summary>
    private void PdfToolsView_ToolRequested(string toolName)
    {
        switch (toolName)
        {
            case "Word to PDF":
                HideAllViews();
                WordToPdfDetailView.Initialize(
                    WinRT.Interop.WindowNative.GetWindowHandle(this),
                    DispatcherQueue);
                WordToPdfDetailView.Visibility = Visibility.Visible;
                break;

            case "Excel to PDF":
                HideAllViews();
                ExcelToPdfDetailView.Initialize(
                    WinRT.Interop.WindowNative.GetWindowHandle(this),
                    DispatcherQueue);
                ExcelToPdfDetailView.Visibility = Visibility.Visible;
                break;

            case "PDF to Word":
                HideAllViews();
                PdfToWordDetailView.Initialize(
                    WinRT.Interop.WindowNative.GetWindowHandle(this),
                    DispatcherQueue);
                PdfToWordDetailView.Visibility = Visibility.Visible;
                break;
        }
    }

    /// <summary>Returns from the Word-to-PDF view back to the dashboard.</summary>
    private void WordToPdfDetailView_BackRequested()
    {
        HideAllViews();
        PdfToolsView.Visibility = Visibility.Visible;
        PdfToolsView.RefreshCards();
    }

    /// <summary>Returns from the Excel-to-PDF view back to the dashboard.</summary>
    private void ExcelToPdfDetailView_BackRequested()
    {
        HideAllViews();
        PdfToolsView.Visibility = Visibility.Visible;
        PdfToolsView.RefreshCards();
    }

    /// <summary>Returns from the PDF-to-Word view back to the dashboard.</summary>
    private void PdfToWordDetailView_BackRequested()
    {
        HideAllViews();
        PdfToolsView.Visibility = Visibility.Visible;
        PdfToolsView.RefreshCards();
    }

    private void HideAllViews()
    {
        HomeView.Visibility              = Visibility.Collapsed;
        PdfToolsView.Visibility          = Visibility.Collapsed;
        SettingsView.Visibility          = Visibility.Collapsed;
        WordToPdfDetailView.Visibility   = Visibility.Collapsed;
        ExcelToPdfDetailView.Visibility  = Visibility.Collapsed;
        PdfToWordDetailView.Visibility   = Visibility.Collapsed;
    }
}
