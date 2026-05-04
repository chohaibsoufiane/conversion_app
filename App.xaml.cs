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
        // Register PDF tools
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.LibreOfficeEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PdfToWordEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.ExcelToPdfEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PdfToExcelEngine());
        
        // Register placeholder tools to fill the dashboard
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.MergePdfEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.SplitPdfEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.CompressPdfEngine());
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.ImageToPdfEngine());
        
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PDF to JPG", "Convert PDF pages to JPG images."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PDF to PNG", "Convert PDF pages to PNG images with transparency."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Protect PDF", "Encrypt your PDF with a password."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Unlock PDF", "Remove passwords and restrictions from PDFs."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Watermark PDF", "Add image or text watermarks to your PDF."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Rotate PDF", "Rotate your PDFs the way you need them."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("HTML to PDF", "Convert webpages in HTML to PDF."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Organize PDF", "Sort, add, and delete PDF pages."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Extract Pages", "Extract specific pages from a PDF."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PDF to PowerPoint", "Convert PDF to editable PPTX slideshows."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PowerPoint to PDF", "Convert your PPTX slideshows to PDF."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PDF to Markdown", "Extract text and format to Markdown."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("EPUB to PDF", "Convert eBooks to PDF format."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("PDF to EPUB", "Convert PDFs to reflowable EPUB format."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Add Page Numbers", "Add page numbers into PDFs with ease."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Repair PDF", "Repair a damaged or corrupted PDF file."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Sign PDF", "Add a signature to your PDF document."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Edit PDF", "Edit text and images directly in your PDF."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Compare PDF", "Compare two PDFs to highlight differences."));
        PdfTools.ToolRegistry.Register(new PdfTools.Tools.PlaceholderTool("Crop PDF", "Crop PDF margins and page sizes."));

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
