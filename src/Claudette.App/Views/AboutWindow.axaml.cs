using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Claudette.App.Services;
using Claudette.App.ViewModels.Settings;

namespace Claudette.App.Views;

/// <summary>
/// About Claudette (DESIGN.md §14, "About"), a window of its own over the window it was opened from: the header's menu,
/// Settings' Help, the command palette and the macOS app menu open it. One at a time; opened again, it comes forward.
/// </summary>
public partial class AboutWindow : Window
{
    private static AboutWindow? _open;

    private bool _holdingCtrl;

    public AboutWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                Close();
                e.Handled = true;
            }
        };
        // Before a control with the focus takes the key, and whether it does or not.
        AddHandler(KeyDownEvent, (_, e) => HoldCtrl(IsCtrl(e.Key) || e.KeyModifiers.HasFlag(KeyModifiers.Control)),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, e) => HoldCtrl(!IsCtrl(e.Key) && e.KeyModifiers.HasFlag(KeyModifiers.Control)),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        // Its key up goes to whichever window has the focus then.
        Deactivated += (_, _) => HoldCtrl(false);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AboutPage page)
        {
            page.Demo.PropertyChanged += (_, _) => ShowFoot();
        }
        ShowFoot();
    }

    private static bool IsCtrl(Key key) => key is Key.LeftCtrl or Key.RightCtrl;

    private void HoldCtrl(bool holding)
    {
        if (_holdingCtrl != holding)
        {
            _holdingCtrl = holding;
            ShowFoot();
        }
    }

    /// <summary>
    /// Try the demo, at the foot (DESIGN.md §11, "Demo mode"), shows while Ctrl is held, and stays while the demo starts
    /// and to say what happened.
    /// </summary>
    private void ShowFoot()
    {
        var demo = (DataContext as AboutPage)?.Demo;
        AboutFoot.IsVisible = demo is { IsOffered: true } && (_holdingCtrl || demo.IsStarting || demo.HasNote);
    }

    /// <summary>Shows it over <paramref name="owner"/> until it's closed, or brings the one open forward.</summary>
    public static Task ShowAsync(AppServices services, Window owner)
    {
        if (_open is { } open)
        {
            UnfocusedWindows.Activate(open);
            return Task.CompletedTask;
        }
        var window = new AboutWindow { DataContext = new AboutPage(new SettingsContext(services)) };
        _open = window;
        window.Closed += (_, _) => _open = null;
        return window.ShowDialog(owner);
    }
}
