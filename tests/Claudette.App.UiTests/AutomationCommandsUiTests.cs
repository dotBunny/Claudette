using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Claudette.App.Services.Automation;
using Claudette.Platform.Shell.Mac;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.UiTests;

/// <summary>
/// The screenshot scripts' commands, carried out by Claudette itself (DESIGN.md §9, "Taking screenshots without the
/// focus"): each finds the control UI Automation saw, by its window, name, id and type, and does to it what a click, a
/// right-click, a switch, typing, the wheel, the pointer resting or keys would, through its automation peer.
/// </summary>
public class AutomationCommandsUiTests
{
    private const long MainHandle = 1;
    private const long MenuHandle = 2;

    private sealed class Page
    {
        public required Window Window { get; init; }
        public required ListBox List { get; init; }
        public required ToggleSwitch Switch { get; init; }
        public required TextBox Box { get; init; }
        public required Border Target { get; init; }
        public required Button Tipped { get; init; }
        public required ScrollViewer Scroller { get; init; }
        public required ContextMenu Menu { get; init; }
        public required Button Second { get; init; }
        public int Clicks { get; set; }
        public int SecondClicks { get; set; }
        public int MenuClicks { get; set; }
        public List<(Key Key, KeyModifiers Modifiers)> Keys { get; } = [];
        public required AutomationCommands Commands { get; init; }

        public AutomationResponse Run(AutomationRequest request)
        {
            var response = Commands.Execute(request);
            UiText.Settle(Window);
            return response;
        }
    }

    private static Page Open()
    {
        Page? page = null;
        var menu = new ContextMenu { Items = { new MenuItem { Header = "Do it", Command = new RelayCommand(() => page!.MenuClicks++) } } };
        var target = new Border { Width = 60, Height = 20, Background = Brushes.Gray, ContextMenu = menu };
        AutomationProperties.SetName(target, "Target");
        var toggle = new ToggleSwitch();
        AutomationProperties.SetName(toggle, "Switch");
        var tipped = new Button { Content = "Tipped" };
        ToolTip.SetTip(tipped, "Its card");
        var window = new Window { Width = 400, Height = 600 };
        var first = new Button { Content = "Go", Command = new RelayCommand(() => page!.Clicks++) };
        var second = new Button { Content = "Go", Command = new RelayCommand(() => page!.SecondClicks++) };
        var list = new ListBox { ItemsSource = new[] { "One", "Two" } };
        var box = new TextBox { Name = "Box" };
        var scroller = new ScrollViewer { Name = "Scroller", Height = 60, Content = new Border { Height = 2000 } };
        window.Content = new StackPanel { Children = { first, list, toggle, box, target, tipped, scroller, second } };
        page = new Page
        {
            Window = window, List = list, Switch = toggle, Box = box, Target = target, Tipped = tipped, Scroller = scroller, Menu = menu, Second = second,
            Commands = new AutomationCommands(handle => handle switch
            {
                MainHandle => window,
                // A popup of its own on the desktop; in the window's overlay where popups draw there instead.
                MenuHandle => TopLevel.GetTopLevel(menu),
                _ => null,
            }, () => TopLevel.GetTopLevel(menu) is { } popup && popup != window ? [popup] : []),
        };
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => page.Keys.Add((e.Key, e.KeyModifiers)), RoutingStrategies.Tunnel);
        window.Show();
        UiText.Settle(window);
        return page;
    }

    [AvaloniaFact]
    public void A_click_invokes_a_button_selects_a_list_item_and_flips_a_switch_found_by_name_or_its_text()
    {
        var page = Open();

        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Go", Type = "Button" }).Ok);
        Assert.Equal(1, page.Clicks);
        // Found by its text, a TextBlock in the button: the click goes to the nearest control around it that takes one.
        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Go", Type = "Text" }).Ok);
        Assert.Equal(2, page.Clicks);

        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Two", Type = "ListItem" }).Ok);
        Assert.Equal(1, page.List.SelectedIndex);

        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Switch" }).Ok);
        Assert.True(page.Switch.IsChecked);
        page.Window.Close();
    }

    [AvaloniaFact]
    public void Of_controls_named_the_same_the_one_nearest_where_UI_Automation_saw_it_is_clicked()
    {
        var page = Open();
        var second = page.Second;
        var middle = second.PointToScreen(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2));

        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Go", Type = "Button", X = middle.X, Y = middle.Y }).Ok);

        Assert.Equal((0, 1), (page.Clicks, page.SecondClicks));
        page.Window.Close();
    }

    [AvaloniaFact]
    public void A_switch_is_turned_on_or_off_whichever_it_was_and_says_which_it_is()
    {
        var page = Open();

        Assert.Equal("On", page.Run(new() { Verb = "toggle", Window = MainHandle, Name = "Switch", On = true }).Value);
        Assert.Equal("On", page.Run(new() { Verb = "toggle", Window = MainHandle, Name = "Switch", On = true }).Value);
        Assert.True(page.Switch.IsChecked);
        Assert.Equal("Off", page.Run(new() { Verb = "toggle", Window = MainHandle, Name = "Switch", On = false }).Value);
        Assert.False(page.Switch.IsChecked);
        page.Window.Close();
    }

    [AvaloniaFact]
    public void Text_goes_into_a_box_found_by_its_id()
    {
        var page = Open();

        Assert.True(page.Run(new() { Verb = "settext", Window = MainHandle, Id = "Box", Text = "make the grapple gentler" }).Ok);

        Assert.Equal("make the grapple gentler", page.Box.Text);
        page.Window.Close();
    }

    [AvaloniaFact]
    public void A_right_click_opens_the_context_menu_whose_item_is_then_clicked_in_its_popup()
    {
        var page = Open();

        Assert.True(page.Run(new() { Verb = "rightclick", Window = MainHandle, Name = "Target" }).Ok);
        Assert.True(page.Menu.IsOpen);
        // UI Automation shows a popup in the window it's over, so the item is named with the window's handle.
        Assert.True(page.Run(new() { Verb = "click", Window = MainHandle, Name = "Do it" }).Ok);

        Assert.Equal(1, page.MenuClicks);
        Assert.False(page.Menu.IsOpen);
        page.Window.Close();
    }

    [AvaloniaFact]
    public void A_tip_shows_and_hides_as_when_the_pointer_rests_on_its_control_and_leaves()
    {
        var page = Open();

        Assert.True(page.Run(new() { Verb = "hover", Window = MainHandle, Name = "Tipped" }).Ok);
        Assert.True(ToolTip.GetIsOpen(page.Tipped));
        Assert.True(page.Run(new() { Verb = "unhover", Window = MainHandle, Name = "Tipped" }).Ok);
        Assert.False(ToolTip.GetIsOpen(page.Tipped));
        page.Window.Close();
    }

    [AvaloniaFact]
    public void A_scroll_viewer_scrolls_by_lines_and_a_control_comes_into_view()
    {
        var page = Open();

        Assert.True(page.Run(new() { Verb = "scroll", Window = MainHandle, Id = "Scroller", Lines = 3 }).Ok);
        var down = page.Scroller.Offset.Y;
        Assert.True(down > 0);
        Assert.True(page.Run(new() { Verb = "scroll", Window = MainHandle, Id = "Scroller", Lines = -1 }).Ok);
        Assert.True(page.Scroller.Offset.Y < down);
        Assert.True(page.Run(new() { Verb = "show", Window = MainHandle, Name = "Tipped" }).Ok);
        page.Window.Close();
    }

    [AvaloniaFact]
    public void Keys_pressed_together_reach_the_windows_handlers_and_close_closes_it()
    {
        var page = Open();
        var closed = false;
        page.Window.Closed += (_, _) => closed = true;

        Assert.True(page.Run(new() { Verb = "key", Window = MainHandle, Keys = "Ctrl+Shift+P" }).Ok);
        Assert.True(page.Run(new() { Verb = "key", Window = MainHandle, Keys = "Esc" }).Ok);
        Assert.Equal([(Key.P, KeyModifiers.Control | KeyModifiers.Shift), (Key.Escape, KeyModifiers.None)], page.Keys);

        Assert.True(page.Run(new() { Verb = "close", Window = MainHandle }).Ok);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public void What_cant_be_done_says_why()
    {
        var page = Open();

        Assert.Contains("No window", page.Run(new() { Verb = "click", Window = 99, Name = "Go" }).Error, StringComparison.Ordinal);
        Assert.Contains("No control named 'Missing'", page.Run(new() { Verb = "click", Window = MainHandle, Name = "Missing" }).Error, StringComparison.Ordinal);
        Assert.Contains("isn't a switch", page.Run(new() { Verb = "toggle", Window = MainHandle, Name = "Go", Type = "Button", On = true }).Error, StringComparison.Ordinal);
        Assert.Contains("has a context menu", page.Run(new() { Verb = "rightclick", Window = MainHandle, Name = "Go", Type = "Button" }).Error, StringComparison.Ordinal);
        Assert.Contains("no key 'Plink'", page.Run(new() { Verb = "key", Window = MainHandle, Keys = "Ctrl+Plink" }).Error, StringComparison.Ordinal);
        Assert.Contains("no command 'dance'", page.Run(new() { Verb = "dance", Window = MainHandle }).Error, StringComparison.Ordinal);
        Assert.True(page.Run(new() { Verb = "ping" }).Ok);
        page.Window.Close();
    }

    /// <summary>
    /// Without a handle, as a script has on macOS and Linux: the window by its title, or the main window; and capture
    /// saves the window as Claudette draws it, for screenshots on every OS (.github/workflows/screenshots.yml).
    /// </summary>
    [AvaloniaFact]
    public void Without_a_handle_the_main_window_or_one_by_its_title_and_capture_saves_its_picture()
    {
        var clicks = 0;
        var main = new Window { Width = 300, Height = 200, Title = "Main", Content = new Button { Content = "Go", Command = new RelayCommand(() => clicks++) } };
        var other = new Window { Width = 240, Height = 160, Title = "Settings", Content = new Border { Background = Brushes.SteelBlue } };
        var commands = new AutomationCommands(_ => null, findByTitle: title => title switch { null => main, "Settings" => other, _ => null });
        main.Show();
        other.Show();
        UiText.Settle(main);
        UiText.Settle(other);

        Assert.True(commands.Execute(new() { Verb = "click", Name = "Go" }).Ok);
        Assert.Equal(1, clicks);
        Assert.Contains("called 'Nope'", commands.Execute(new() { Verb = "click", Title = "Nope", Name = "Go" }).Error, StringComparison.Ordinal);

        var folder = Directory.CreateTempSubdirectory("claudette-capture").FullName;
        try
        {
            var path = Path.Combine(folder, "shots", "settings.png");
            var saved = commands.Execute(new() { Verb = "capture", Title = "Settings", Path = path });
            Assert.True(saved.Ok, saved.Error);
            // Saved where it was asked to be. The test platform draws nothing, so the picture itself is the real app's to show.
            Assert.Equal(path, saved.Value);
            Assert.True(File.Exists(path));
            Assert.Contains("needs a path", commands.Execute(new() { Verb = "capture" }).Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            other.Close();
            main.Close();
        }
    }

    /// <summary>
    /// macOS's traffic lights, which AppKit draws over the window rather than Claudette, are drawn into its picture where
    /// AppKit has them, so the title bar's left isn't a gap: asked for of the window captured, and only that one.
    /// </summary>
    [AvaloniaFact]
    public void Capture_draws_in_the_OSs_buttons_over_the_window()
    {
        var main = new Window { Width = 300, Height = 200, Title = "Main" };
        var asked = new List<TopLevel>();
        var commands = new AutomationCommands(_ => null, findByTitle: _ => main, windowButtons: window =>
        {
            asked.Add(window);
            return
            [
                new MacWindowButton(MacWindowButtonKind.Close, 13, 13, 14, 16, IsEnabled: true),
                new MacWindowButton(MacWindowButtonKind.Minimize, 33, 13, 14, 16, IsEnabled: true),
                new MacWindowButton(MacWindowButtonKind.Zoom, 53, 13, 14, 16, IsEnabled: false),
            ];
        });
        main.Show();
        UiText.Settle(main);
        var folder = Directory.CreateTempSubdirectory("claudette-capture").FullName;
        try
        {
            var path = Path.Combine(folder, "main.png");
            var saved = commands.Execute(new() { Verb = "capture", Path = path });

            Assert.True(saved.Ok, saved.Error);
            Assert.True(File.Exists(path));
            Assert.Equal([main], asked);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            main.Close();
        }
    }

    /// <summary>
    /// A window resized to the size asked, in device-independent pixels, as the scripts make each system's the same size
    /// (macOS fits a window to its screen as it opens), taking it in its next layout; its largest size, which Windows
    /// keeps it within, is at least that.
    /// </summary>
    [AvaloniaFact]
    public void Resize_gives_a_window_the_size_asked()
    {
        var main = new Window { Width = 300, Height = 200, Title = "Main" };
        var commands = new AutomationCommands(_ => null, findByTitle: title => title is null ? main : null);
        main.Show();
        UiText.Settle(main);

        var resized = commands.Execute(new() { Verb = "resize", Width = 640, Height = 480 });
        UiText.Settle(main);

        Assert.True(resized.Ok, resized.Error);
        Assert.Equal(new Size(640, 480), main.ClientSize);
        Assert.Equal((640.0, 480.0), (main.MaxWidth, main.MaxHeight));
        Assert.Contains("needs a width and a height", commands.Execute(new() { Verb = "resize", Width = 640 }).Error, StringComparison.Ordinal);
        main.Close();
    }
}
