using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Claudette.App.Services;
using Claudette.App.ViewModels;
using Claudette.Core.Accessibility;
using Claudette.Core.Development;
using Claudette.Core.Settings;

namespace Claudette.App.Views;

/// <summary>
/// A Project Files tab's file in a window of its own (DESIGN.md §18, "Project files", "A file in a window of its own"):
/// its × puts the file back in its tab, it follows Settings → Appearance → Zoom, and the Files tab's keys work in it. It
/// closes with the file, its Files tab, or Claudette.
/// </summary>
public partial class ProjectFileWindow : Window
{
    private readonly WindowPlacementTracker _placement;
    private ProjectFileViewModel? _file;

    /// <summary>It has let go of its file: put back, closed, or with Claudette quitting.</summary>
    private bool _letGo;

    private int _zoomPercent = 100;

    public ProjectFileWindow()
    {
        InitializeComponent();
        _placement = new WindowPlacementTracker(this);
        // Back from another editor, perhaps, with the file changed there, as Claudette's window looks again.
        Activated += (_, _) => _ = _file?.CheckDiskAsync();
        // Tunnel, so the keys work wherever the focus is, as the main window's do.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        ActualThemeVariantChanged += (_, _) => TellTheme();
    }

    /// <summary>
    /// Opens <paramref name="file"/>'s window at <paramref name="place"/> (or where the OS puts it, when that's on no
    /// screen), to the front with the keyboard in the editor.
    /// </summary>
    internal static ProjectFileWindow Open(ProjectFileViewModel file, WindowPlacement place)
    {
        var window = new ProjectFileWindow { DataContext = file };
        window.Use(file);
        window._placement.Apply(place);
        window.Show();
        if (!UnfocusedWindows.IsOn)
        {
            file.RequestFocus();
        }
        return window;
    }

    private void Use(ProjectFileViewModel file)
    {
        _file = file;
        file.PropertyChanged += OnFileChanged;
        file.Closed += OnFileClosed;
        file.Services.SettingsChanged += OnSettingsChanged;
        UseAppearance(file.Services.Settings.Appearance);
        TellTheme();
    }

    /// <summary>The diff modes' colors follow the theme, as the Files tab's view tells them, for a tab not shown yet.</summary>
    private void TellTheme()
    {
        if (_file is { } file)
        {
            file.Files.IsDarkTheme = ActualThemeVariant == ThemeVariant.Dark;
        }
    }

    /// <summary>To the front, with the keyboard in the editor: a click on its tab, or the file opened again.</summary>
    internal void BringForward()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        UnfocusedWindows.Activate(this);
        if (!UnfocusedWindows.IsOn)
        {
            _file?.RequestFocus();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_file is { } file)
        {
            UseAppearance(file.Services.Settings.Appearance);
        }
    }

    /// <summary>The Claude style's shapes, which are styles under its class, as the main window's are, and the zoom.</summary>
    private void UseAppearance(AppearanceSettings appearance)
    {
        Appearance.Classes.Set("claude", appearance.Style == AppStyle.Claude);
        UseZoom(Zoom.Clamp(appearance.Zoom));
    }

    /// <summary>Scales what's under the title bar, as the main window scales its content.</summary>
    private void UseZoom(int percent)
    {
        if (percent == _zoomPercent)
        {
            return;
        }
        _zoomPercent = percent;
        var zoom = percent / 100.0;
        Zoomed.LayoutTransform = percent == 100 ? null : new ScaleTransform(zoom, zoom);
    }

    // ---- Letting go of the file ----------------------------------------------------------------------------------

    /// <summary><b>Put back</b>, from its toolbar or its tab's menu: the window goes, and the tab's view takes the file.</summary>
    private void OnFileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectFileViewModel.IsInWindow) && _file is { IsInWindow: false })
        {
            LetGoAndClose();
        }
    }

    /// <summary>The file closed, or its Files tab did, Claudette quitting too.</summary>
    private void OnFileClosed() => LetGoAndClose();

    private void LetGoAndClose()
    {
        LetGo();
        Close();
    }

    /// <summary>Its editor gives the file up first: an editor takes its file's requests, and the tab's view takes them next.</summary>
    private void LetGo()
    {
        _letGo = true;
        FileView.DataContext = null;
    }

    /// <summary>Its ×, the OS's close, or Close tab: the file goes back to its tab.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _letGo || e.IsProgrammatic || e.CloseReason != WindowCloseReason.WindowClosing || _file is not { } file)
        {
            return;
        }
        LetGo();
        file.Files.PutBack(file);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_file is { } file)
        {
            file.PropertyChanged -= OnFileChanged;
            file.Closed -= OnFileClosed;
            file.Services.SettingsChanged -= OnSettingsChanged;
        }
        _file = null;
    }

    // ---- The keys -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The Files tab's keys (DESIGN.md §18, "Project files", "Keyboard"): <c>Ctrl/Cmd+S</c> saves the file and
    /// <c>Ctrl/Cmd+Alt+S</c> every file of its Files tab, Find opens the editor's find, the previous and next message go
    /// through the changes, Find in files and Replace in files open the popup over Claudette's window, and Close tab puts
    /// the file back.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_file is not { } file)
        {
            return;
        }
        var keyboard = file.Services.Settings.Keyboard;
        bool Is(string id) => Shortcuts.Matches(keyboard, id, e.Key, e.KeyModifiers);
        var primary = Shortcuts.IsMac ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.S && e.KeyModifiers == primary)
        {
            _ = file.SaveAsync();
        }
        else if (e.Key == Key.S && e.KeyModifiers == (primary | KeyModifiers.Alt))
        {
            file.Files.SaveAllCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.Find) && file.ShowsEditor)
        {
            file.RequestFind(replace: false);
        }
        else if (Is(KeyboardShortcuts.PreviousMessage))
        {
            file.PreviousChangeCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.NextMessage))
        {
            file.NextChangeCommand.Execute(null);
        }
        else if (Is(KeyboardShortcuts.FindInFiles) || Is(KeyboardShortcuts.ReplaceInFiles))
        {
            file.Files.FindInFilesFrom(file, replace: Is(KeyboardShortcuts.ReplaceInFiles));
        }
        else if (Is(KeyboardShortcuts.CloseTab))
        {
            file.PutBackCommand.Execute(null);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }
}
