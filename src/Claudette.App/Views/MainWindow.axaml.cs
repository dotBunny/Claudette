using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Claudette.Platform.Shell.Windows;
using Claudette.App.Services;
using Claudette.App.ViewModels;

namespace Claudette.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private UsageWindow? _usageWindow;

    public MainWindow()
    {
        InitializeComponent();
        // Notifications are skipped for what's already in front of the user (DESIGN.md §10).
        Activated += (_, _) =>
        {
            _viewModel?.Services.Notifications.SetAppActive(true);
            // Back from the OS's settings, perhaps with Reduce motion changed (DESIGN.md §3, "Accessibility").
            _ = _viewModel?.Services.ReadMotionPreferencesAsync(onlyIfStale: true);
            // Back from an editor, perhaps with claudette.json changed: the actions and links follow (DESIGN.md §18).
            if (_viewModel?.Shell?.SelectedTab is { } tab)
            {
                _ = tab.ProjectTools.RefreshFileAsync();
            }
            // Back from a terminal, perhaps, with a branch switched there: the tab rows follow (DESIGN.md §4, "Sidebar").
            foreach (var each in _viewModel?.Shell?.AllTabs ?? [])
            {
                each.RefreshBranch();
            }
            // Back from another machine, perhaps, with a scratch pad changed there (DESIGN.md §18, "Scratch pad").
            _viewModel?.Services.ScratchPads.OnAppActivated();
            // Back from another editor, perhaps, with an open file changed there (DESIGN.md §18, "Project files").
            _viewModel?.Shell?.OnAppActivated();
        };
        Deactivated += (_, _) => _viewModel?.Services.Notifications.SetAppActive(false);
        // A narrow window leaves the busiest tabs, then the weekly chart, out of the detailed header (DESIGN.md §6).
        SizeChanged += (_, e) => _viewModel?.Usage?.SetDetailsWidth(e.NewSize.Width / _zoom);
        LayoutUpdated += (_, _) => FitTitleBar();
        // With the header as the title bar, the logo is the window's icon to Windows: a click opens the window menu and a
        // double-click closes the window, as any window's icon does (DESIGN.md §3, "Window frame"). Avalonia 12.1's
        // hit-test values stop short of the icon's, which it passes on to Windows all the same. Elsewhere it drags.
        if (OperatingSystem.IsWindows())
        {
            Win32Properties.SetNonClientHitTestResult(HeaderIcon, (Win32Properties.Win32HitTestValue)WindowsTitleBar.WindowIcon);
        }
        else
        {
            HeaderIcon.IsHitTestVisible = false;
        }
    }

    /// <summary>The window's buttons Avalonia draws on Windows, when they can't be found: three 45 pixels wide, 2 apart.</summary>
    private const double DrawnButtonsWidth = (3 * 45) + (2 * 2);

    /// <summary>Room at the header's left for macOS's traffic lights.</summary>
    private const double TrafficLightsWidth = 72;

    /// <summary>The header's padding, and its gap to the window's buttons.</summary>
    private const double HeaderPadding = 10;

    /// <summary>
    /// With the header as the window's title bar (DESIGN.md §3, "Window frame"): the title bar as tall as the header's first
    /// row, which the window's buttons fill, and the header clear of them, at its right on Windows and its left on macOS.
    /// The buttons aren't zoomed with the header.
    /// </summary>
    private void FitTitleBar()
    {
        var merged = IsExtendedIntoWindowDecorations;
        double right = HeaderPadding, left = HeaderPadding;
        if (merged && OperatingSystem.IsMacOS())
        {
            left = TrafficLightsWidth / _zoom;
        }
        else if (merged)
        {
            var buttons = this.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p is { Name: "PART_OverlayPanel", IsEffectivelyVisible: true });
            right = ((buttons?.Bounds.Width ?? DrawnButtonsWidth) / _zoom) + (HeaderPadding / 2);
        }
        var padding = new Thickness(left, 6, right, 6);
        if (Header.Padding != padding)
        {
            Header.Padding = padding;
        }
        var height = (HeaderGrid.RowDefinitions[0].ActualHeight + padding.Top + padding.Bottom) * _zoom;
        if (merged && height > 0 && Math.Abs(ExtendClientAreaTitleBarHeightHint - height) > 0.5)
        {
            ExtendClientAreaTitleBarHeightHint = height;
        }
    }

    private int _zoomPercent = 100;

    private double _zoom = 1;

    /// <summary>
    /// Scales the window's content (DESIGN.md §3, "Accessibility"): <paramref name="percent"/> of its size, 100 for none.
    /// Popups and menus keep their size.
    /// </summary>
    public void UseZoom(int percent)
    {
        if (percent == _zoomPercent)
        {
            return;
        }
        _zoomPercent = percent;
        _zoom = percent / 100.0;
        Zoomed.LayoutTransform = percent == 100 ? null : new ScaleTransform(_zoom, _zoom);
        if (_viewModel?.Usage is { } usage && Bounds.Width > 0)
        {
            usage.SetDetailsWidth(Bounds.Width / _zoom);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel.BringToFrontRequested -= BringToFront;
            _viewModel.Services.SettingsChanged -= OnSettingsChanged;
        }
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
            _viewModel.BringToFrontRequested += BringToFront;
            _viewModel.Services.SettingsChanged += OnSettingsChanged;
            _viewModel.Services.Notifications.SetAppActive(IsActive);
            if (_viewModel.Usage is { } usage)
            {
                UseUsage(usage);
            }
        }
    }

    /// <summary>Settings → Appearance → Use the system's title bar applies at once (DESIGN.md §3, "Window frame").</summary>
    private void OnSettingsChanged(object? sender, EventArgs e) => _viewModel?.TitleBarSettingChanged();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Usage) && _viewModel?.Usage is { } usage)
        {
            UseUsage(usage);
        }
    }

    private void UseUsage(UsageViewModel usage)
    {
        usage.ShowUsagePanel = ShowUsagePanelAsync;
        if (Bounds.Width > 0)
        {
            usage.SetDetailsWidth(Bounds.Width / _zoom);
        }
    }

    /// <summary>
    /// For a clicked notification or another launch: restore the window if minimized and bring it to the front; not while
    /// Claudette leaves the focus alone (DESIGN.md §9, "Taking screenshots without the focus").
    /// </summary>
    private void BringToFront()
    {
        if (UnfocusedWindows.IsOn)
        {
            return;
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        // Toggling Topmost gets past Windows' foreground lock, which otherwise only flashes the taskbar button.
        Topmost = true;
        Topmost = false;
        Activate();
    }

    /// <summary>The Usage panel (DESIGN.md §6). One at a time; clicking the header again brings it forward.</summary>
    private Task ShowUsagePanelAsync()
    {
        if (_viewModel is not { Usage: { } header, Services.Usage: { } tracker } main)
        {
            return Task.CompletedTask;
        }
        if (_usageWindow is not null)
        {
            (_usageWindow.DataContext as UsagePanelViewModel)?.Refresh();
            UnfocusedWindows.Activate(_usageWindow);
            return Task.CompletedTask;
        }
        var panel = new UsagePanelViewModel(main.Services, tracker, header, id => main.Shell?.AllTabs.FirstOrDefault(t => t.Id == id)?.DisplayName);
        void OnTurnRecorded(string tabId) => panel.Refresh();
        void OnTurnsImported(IReadOnlyCollection<string> accountNames) => panel.Refresh();
        tracker.TurnRecorded += OnTurnRecorded;
        tracker.SamplesImported += panel.Refresh;
        tracker.TurnsImported += OnTurnsImported;
        _usageWindow = new UsageWindow { DataContext = panel };
        _usageWindow.Closed += (_, _) =>
        {
            tracker.TurnRecorded -= OnTurnRecorded;
            tracker.SamplesImported -= panel.Refresh;
            tracker.TurnsImported -= OnTurnsImported;
            _usageWindow = null;
        };
        _usageWindow.Show(this);
        return Task.CompletedTask;
    }
}
