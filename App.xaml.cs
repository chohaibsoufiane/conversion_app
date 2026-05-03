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
        this.UnhandledException += (s, e) => 
        {
            System.IO.File.WriteAllText("crash.txt", e.Exception.ToString() + "\n" + e.Message);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Register PDF tools — LibreOffice engine for pixel-perfect conversion
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.LibreOfficeEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.ExcelToPdfEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PdfToWordEngine());

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
