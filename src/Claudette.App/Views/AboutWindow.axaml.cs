using Avalonia.Controls;
using Avalonia.Input;
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
