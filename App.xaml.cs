using Microsoft.UI.Xaml;

namespace ConversionApp;

/// <summary>
/// WinUI 3 application entry point.
/// Launches the main window on startup.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Register PDF tools — LibreOffice engine for pixel-perfect conversion
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.LibreOfficeEngine());

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
