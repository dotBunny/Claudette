using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Claudette.App.Controls;
using Claudette.App.Services;
using Claudette.App.ViewModels;

namespace Claudette.App.Views;

/// <summary>
/// The Settings window (DESIGN.md §14). It isn't modal: it stays open beside the main window, and its project pages
/// follow the tab selected there. Esc (or <c>Ctrl/Cmd+W</c>) closes it; <c>Ctrl/Cmd+F</c> goes to the search box.
/// </summary>
public partial class SettingsWindow : Window
{
    private SettingsViewModel? _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        // Tunnel, so the key press is recorded before a focused button or text box acts on it.
        AddHandler(KeyDownEvent, OnRecordingKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings.AboutRequested -= OnAboutRequested;
        }
        _settings = DataContext as SettingsViewModel;
        if (_settings is not null)
        {
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            _settings.AboutRequested += OnAboutRequested;
            if (_settings.ScrollToKey is not null)
            {
                ScrollToSoon();
            }
        }
    }

    /// <summary>Help's <b>About Claudette</b>: its window, over Settings (DESIGN.md §14, "About").</summary>
    private void OnAboutRequested()
    {
        if (_settings is { } settings)
        {
            _ = AboutWindow.ShowAsync(settings.Services, this);
        }
    }

    /// <summary>While a shortcut is being recorded (Settings → Keyboard), the next key press is the new shortcut.</summary>
    private void OnRecordingKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel { IsRecordingShortcut: true } settings)
        {
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            settings.CancelRecording();
        }
        else if (Shortcuts.FromKeyPress(e.Key, e.KeyModifiers) is { } chord)
        {
            settings.RecordShortcut(chord);
        }
    }

    /// <summary>
    /// The arrow keys go through every page (DESIGN.md §14), from the categories' list on into the selected tab's
    /// project's at the foot, and back: Down past the last category picks the project's first page, Up before it the last
    /// category. Each list moves within itself.
    /// </summary>
    private void OnSidebarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _settings is null || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var lastCategory = _settings.NavEntries.LastOrDefault(n => !n.IsHeading);
        var firstOfProject = _settings.ProjectNavEntries.FirstOrDefault();
        var (list, entry) = e.Key switch
        {
            Key.Down when sender == SidebarList && firstOfProject is not null && _settings.SelectedNavEntry == lastCategory => (ProjectList, firstOfProject),
            Key.Up when sender == ProjectList && lastCategory is not null && _settings.SelectedProjectNavEntry == firstOfProject => (SidebarList, lastCategory),
            _ => (null, null),
        };
        if (list is null || entry is null)
        {
            return;
        }
        _settings.SelectedCategory = entry.Category;
        list.ScrollIntoView(entry);
        list.ContainerFromItem(entry)?.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>
    /// Esc clears the search, else closes the window, unless something open in it took it first (a dropdown, a menu, a
    /// dialog); <c>Ctrl/Cmd+W</c> closes it; <c>Ctrl/Cmd+F</c> goes to the search box.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _settings is null)
        {
            return;
        }
        var primary = Shortcuts.IsMac ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && !(_settings.Project?.HasEditor ?? false))
        {
            if (_settings.IsSearching)
            {
                _settings.SearchText = "";
            }
            else
            {
                Close();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.W && e.KeyModifiers == primary)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == primary)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ScrollToKey) && _settings?.ScrollToKey is not null)
        {
            ScrollToSoon();
        }
    }

    /// <summary>Once the page has been laid out, brings the row or section search or a link asked for into view.</summary>
    private void ScrollToSoon() => Dispatcher.UIThread.Post(ScrollTo, DispatcherPriority.Loaded);

    private void ScrollTo()
    {
        if (_settings?.ScrollToKey is not { } key)
        {
            return;
        }
        var found = PageHost.GetLogicalDescendants().OfType<Control>()
            .FirstOrDefault(c => c is SettingRow row && row.Key == key || c is SettingsSection section && section.Key == key);
        if (found is null)
        {
            return;
        }
        // A row shown only in some states, such as a custom diff tool's command, is brought into view by the row it's in.
        var target = found.IsEffectivelyVisible
            ? found
            : found.GetLogicalAncestors().OfType<Control>().FirstOrDefault(c => c is SettingRow or SettingsSection && c.IsEffectivelyVisible) ?? found;
        _settings.ScrollToKey = null;
        target.BringIntoView();
    }
}
