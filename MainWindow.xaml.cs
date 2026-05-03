using ConversionApp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

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
        appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
        var overlapped = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        overlapped?.Maximize();

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

        // Initialize default backdrop (Max Mica - BaseAlt)
        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };

        // Ensure NavView selects the first item (Home) when starting up
        NavView.Loaded += (s, e) =>
        {
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
            // Seed the current view tracker so the first real navigation animates out correctly.
            _currentView = DashboardView;
        };
    }

    private void BackdropSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // 0 = Solid, 1 = Mica, 2 = Mica Alt, 3 = Acrylic
        switch (e.NewValue)
        {
            case 0:
                this.SystemBackdrop = null;
                break;
            case 1:
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
                break;
            case 2:
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt };
                break;
            case 3:
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                break;
        }

        if (MicaOverlay != null)
        {
            MicaOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // Tracks which view is currently shown so the out-animation knows what to collapse.
    private UIElement? _currentView = null;
    // Guard: prevent re-entrant navigation while an out-animation is running.
    private bool _isAnimating = false;

    /// <summary>
    /// Animates the current view out (CubicEase, 250 ms), then animates <paramref name="inView"/> in
    /// (BackEase bounce, 400 ms). Each call creates brand-new Storyboard instances so there is
    /// no shared-state / SetTarget-on-running-storyboard crash.
    /// </summary>
    private void NavigateTo(UIElement inView, System.Action? onBeforeIn = null)
    {
        if (_isAnimating || inView == _currentView) return;

        var leaving = _currentView;
        _currentView = inView;

        if (leaving != null && leaving.Visibility == Visibility.Visible)
        {
            _isAnimating = true;
            var animOut = BuildAnimateOut(leaving);
            animOut.Completed += (s, e) =>
            {
                leaving.Visibility = Visibility.Collapsed;
                _isAnimating = false;
                onBeforeIn?.Invoke();
                BuildAnimateIn(inView).Begin();
            };
            animOut.Begin();
        }
        else
        {
            onBeforeIn?.Invoke();
            BuildAnimateIn(inView).Begin();
        }
    }

    /// <summary>Creates a fresh AnimateIn storyboard targeting <paramref name="view"/>.</summary>
    private Storyboard BuildAnimateIn(UIElement view)
    {
        view.Visibility = Visibility.Visible;
        var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
        var dur = new Duration(TimeSpan.FromMilliseconds(400));
        var sb = new Storyboard();

        var opacity = new DoubleAnimation { From = 0, To = 1, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(opacity, view);
        Storyboard.SetTargetProperty(opacity, "Opacity");

        var ty = new DoubleAnimation { From = 40, To = 0, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(ty, view);
        Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");

        var sx = new DoubleAnimation { From = 0.95, To = 1, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(sx, view);
        Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

        var sy = new DoubleAnimation { From = 0.95, To = 1, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(sy, view);
        Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

        sb.Children.Add(opacity);
        sb.Children.Add(ty);
        sb.Children.Add(sx);
        sb.Children.Add(sy);
        return sb;
    }

    /// <summary>Creates a fresh AnimateOut storyboard targeting <paramref name="view"/>.</summary>
    private Storyboard BuildAnimateOut(UIElement view)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var dur = new Duration(TimeSpan.FromMilliseconds(250));
        var sb = new Storyboard();

        var opacity = new DoubleAnimation { From = 1, To = 0, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(opacity, view);
        Storyboard.SetTargetProperty(opacity, "Opacity");

        var ty = new DoubleAnimation { From = 0, To = -40, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(ty, view);
        Storyboard.SetTargetProperty(ty, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");

        var sx = new DoubleAnimation { From = 1, To = 0.95, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(sx, view);
        Storyboard.SetTargetProperty(sx, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

        var sy = new DoubleAnimation { From = 1, To = 0.95, Duration = dur, EasingFunction = ease };
        Storyboard.SetTarget(sy, view);
        Storyboard.SetTargetProperty(sy, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

        sb.Children.Add(opacity);
        sb.Children.Add(ty);
        sb.Children.Add(sx);
        sb.Children.Add(sy);
        return sb;
    }

    private void NavView_ItemInvoked(
        Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigateTo(SettingsView);
        }
        else if (args.InvokedItemContainer?.Tag is string tag)
        {
            switch (tag)
            {
                case "PdfTools":
                    NavigateTo(PdfToolsView, () => PdfToolsView.RefreshCards());
                    break;
                case "BatchConverter":
                    NavigateTo(BatchConverterView);
                    break;
                default:
                    NavigateTo(DashboardView);
                    break;
            }
        }
        else
        {
            NavigateTo(DashboardView);
        }
    }

    private void DashboardCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is Microsoft.UI.Xaml.Controls.NavigationViewItem navItem && navItem.Tag as string == tag)
                {
                    NavView.SelectedItem = navItem;
                    if (tag == "BatchConverter")
                        NavigateTo(BatchConverterView);
                    else if (tag == "PdfTools")
                        NavigateTo(PdfToolsView, () => PdfToolsView.RefreshCards());
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Navigates from the PDF dashboard into a specific tool's detail view.
    /// </summary>
    private void PdfToolsView_ToolRequested(string toolName)
    {
        var type = toolName switch
        {
            "Word to PDF" => Models.ConversionType.WordToPdf,
            "Excel to PDF" => Models.ConversionType.ExcelToPdf,
            _ => Models.ConversionType.WordToPdf
        };

        ViewModel.CurrentConversionType = type;
        ViewModel.ActiveToolTitle = toolName;

        ConversionDetailView.Initialize(ViewModel, WinRT.Interop.WindowNative.GetWindowHandle(this));
        NavigateTo(ConversionDetailView);
    }

    /// <summary>Returns from the conversion view back to the PDF tools list.</summary>
    private void ConversionDetailView_BackRequested()
        => NavigateTo(PdfToolsView, () => PdfToolsView.RefreshCards());

    private void SplashOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        SplashStoryboard.Begin();
    }

    private void SplashStoryboard_Completed(object sender, object e)
    {
        SplashOverlay.Visibility = Visibility.Collapsed;
    }

    private void HistoryControl_ItemClick(object sender, Models.ConversionHistoryItem item)
    {
        ViewModel.OpenFileCommand.Execute(item);
    }
}
