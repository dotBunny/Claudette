using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Conversation;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>
/// The composer's large pastes and the stash (DESIGN.md §5, "Attachments", "Drafts and the stash"), long messages shown
/// short, a turn's files on its footer (DESIGN.md §8, "Changes per turn"), and Ctrl/Cmd+Enter as the send key (DESIGN.md
/// §14) with the chip that switches it (§5, "Composer"), rendered.
/// </summary>
public class ComposerExtrasUiTests
{
    private static readonly RawInputModifiers Primary = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static readonly string BigLog = string.Join("\n", Enumerable.Range(1, 2000).Select(i => $"log line {i:D5} with something in it"));

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task A_large_paste_is_a_chip_and_its_message_shows_its_start()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });

        tab.ComposerText = "why does this fail?";
        tab.AddPastedText(BigLog);
        UiText.Settle(window);

        var chips = Named<ItemsControl>(window, "PastedTextList");
        Assert.True(chips.IsEffectivelyVisible);
        var shown = UiText.Describe(chips);
        Assert.Contains($"Pasted text · {2000:N0} lines", shown, StringComparison.Ordinal);
        Assert.Contains(chips.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Remove pasted text");

        await tab.SendCommand.ExecuteAsync(null);
        await UiText.SettleUntilAsync(window, () => tab.Items.OfType<UserMessageItem>().Any(), "the message");
        UiText.Settle(window);
        Assert.False(chips.IsEffectivelyVisible);
        var card = tab.Items.OfType<UserMessageItem>().Last();
        var showAll = window.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, card.ShowAllCommand));
        Assert.True(showAll.IsEffectivelyVisible);
        Assert.Contains("Show all (", UiText.Describe(showAll), StringComparison.Ordinal);

        showAll.Command!.Execute(null);
        UiText.Settle(window);
        Assert.False(showAll.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_stash_button_shows_while_the_stash_has_something_and_its_menu_puts_one_back()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var button = Named<Button>(window, "StashButton");
        Assert.False(button.IsEffectivelyVisible);

        tab.ComposerText = "for later";
        var composer = Named<TextBox>(window, "Composer");
        composer.Focus();
        window.KeyPressQwerty(PhysicalKey.S, Primary);
        window.KeyReleaseQwerty(PhysicalKey.S, Primary);
        UiText.Settle(window);

        Assert.Equal("", tab.ComposerText);
        Assert.True(button.IsEffectivelyVisible);
        Assert.Contains("Stashed (1) ▾", UiText.Describe(button), StringComparison.Ordinal);
        var flyout = Assert.IsType<Flyout>(button.Flyout);
        flyout.ShowAt(button);
        UiText.Settle(window);
        var menu = Assert.IsAssignableFrom<Control>(flyout.Content);
        await UiText.SettleUntilAsync(window, () => UiText.Describe(menu).Contains("\"for later\"", StringComparison.Ordinal), "the entry");
        Assert.Contains(menu.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Delete from the stash");

        var entry = menu.GetVisualDescendants().OfType<Button>().First(b => b.DataContext is StashEntry && ReferenceEquals(b.Command, tab.UnstashCommand));
        entry.Command!.Execute(entry.CommandParameter);
        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiText.Settle(window);

        Assert.Equal("for later", tab.ComposerText);
        Assert.False(button.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task A_turns_footer_lists_its_files()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var path = Path.Combine(h.WorkFolder, "auth.cs");
        tab.ComposerText = "change it";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.Emit(new JsonObject { ["type"] = "system", ["subtype"] = "init", ["session_id"] = "s1", ["model"] = "claude-opus-5-5" });
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_use", ["id"] = "e1", ["name"] = "Edit", ["input"] = new JsonObject { ["file_path"] = path } }) },
        });
        await File.WriteAllTextAsync(path, "one\ntwo\n", TestContext.Current.CancellationToken);
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "e1", ["content"] = "The file has been updated." }) },
            ["tool_use_result"] = new JsonObject { ["filePath"] = path, ["oldString"] = "a", ["originalFile"] = "one\n", ["structuredPatch"] = new JsonArray() },
        });
        h.Transport.Emit("""{"type":"result","subtype":"success","is_error":false,"session_id":"s1","duration_ms":1200}""");
        await UiText.SettleUntilAsync(window, () => tab.Items.LastOrDefault() is TurnSummaryItem { HasFiles: true } t && t.Files[0].Stats is not null, "the footer");
        UiText.Settle(window);

        // A Modified files card, each file on a tile that opens the turn's changes to it (DESIGN.md §18, "Conversation cards").
        var card = window.GetVisualDescendants().OfType<Card>().Single(c => c.Classes.Contains("files"));
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal("Modified files", card.Label);
        Assert.Equal("1 file changed", card.Detail);
        await UiText.SettleUntilAsync(window, () => UiText.Describe(card).Contains("\"+1 −0\"", StringComparison.Ordinal), "the file's counts");
        Assert.Contains("\"auth.cs\"", UiText.Describe(card), StringComparison.Ordinal);
        var row = card.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "What this turn changed in auth.cs");
        Assert.True(row.Command!.CanExecute(row.CommandParameter));
    }

    /// <summary>The tab's info card opens from the end of the line under the composer, after the tasks' status (DESIGN.md §5, "Composer").</summary>
    [AvaloniaFact]
    public async Task The_tab_info_button_is_last_on_the_line_under_the_composer()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "tool_use", ["id"] = "t1", ["name"] = "TodoWrite",
                    ["input"] = new JsonObject
                    {
                        ["todos"] = new JsonArray(
                            new JsonObject { ["content"] = "Retake the pictures", ["status"] = "in_progress", ["activeForm"] = "Retaking the pictures" },
                            new JsonObject { ["content"] = "Merge", ["status"] = "pending", ["activeForm"] = "Merging" }),
                    },
                }),
            },
        });
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        await UiText.SettleUntilAsync(window, () => tab.TodoList.HasStatus, "the tasks' status");
        var info = Named<Button>(window, "TabInfo");
        var tasks = Named<Button>(window, "TaskStatus");
        var composer = Named<TextBox>(window, "Composer");
        Point At(Control c) => c.TranslatePoint(default, window)!.Value;

        // Under the box, on the status line, after the tasks: the last thing on it.
        Assert.True(At(info).Y > At(composer).Y + composer.Bounds.Height);
        Assert.Equal(At(tasks).Y + tasks.Bounds.Height / 2, At(info).Y + info.Bounds.Height / 2, 1);
        Assert.True(At(info).X > At(tasks).X + tasks.Bounds.Width);
        Assert.Equal("Tab info", AutomationProperties.GetName(info));
        Assert.IsType<Flyout>(info.Flyout);
        // Not in the composer's bar any more.
        var bar = Named<Button>(window, "SendButton").GetVisualAncestors().OfType<WrapPanel>().First();
        Assert.DoesNotContain(bar.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Tab info");
    }

    /// <summary>
    /// The send key chip before Send (DESIGN.md §5, "Composer"): it says which key sends, a click switches Settings →
    /// Keyboard → Send with (§14) and the placeholder and Send's tooltip follow, and the keyboard reaches it.
    /// </summary>
    [AvaloniaFact]
    public async Task The_send_key_chip_says_which_key_sends_and_a_click_switches_it()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var chip = Named<Button>(window, "SendKeyChip");
        var send = Named<Button>(window, "SendButton");
        var composer = Named<TextBox>(window, "Composer");
        // The right-hand group: the counts, the chip and Send.
        var group = send.GetVisualAncestors().OfType<WrapPanel>().First();
        // macOS writes Cmd+Enter as ⌘↩; the snapshots read as on Windows and Linux.
        (string, string)[] asOnWindows = Shortcuts.IsMac ? [("⌘⏎", "Ctrl+⏎"), ("⌘↩", "Ctrl+Enter")] : [];
        string Shown() => UiText.Describe(group, asOnWindows) + string.Join("\n",
            $"chip tip: {ToolTip.GetTip(chip)}", $"Send tip: {ToolTip.GetTip(send)}", $"placeholder: {composer.PlaceholderText}", "")
            .Replace("⌘↩", "Ctrl+Enter", StringComparison.Ordinal).Replace("⎋", "Esc", StringComparison.Ordinal);

        Assert.True(chip.IsEffectivelyVisible);
        Assert.Same(group, chip.GetVisualAncestors().OfType<WrapPanel>().First());
        Assert.Equal(group.Children.IndexOf(send) - 1, group.Children.IndexOf(chip));
        Assert.True(chip.Focusable && chip.IsTabStop, "The keyboard should reach the chip, as it does the bar's other buttons");
        // ⏎ comes from Inter, which Claudette ships, rather than whichever font fallback finds it in.
        var glyph = chip.GetVisualDescendants().OfType<TextBlock>().Single().Inlines!.OfType<Run>().Single(r => r.Classes.Contains("returnkey"));
        Assert.Equal(ShortcutTips.ReturnGlyph, glyph.Text);
        Assert.Contains("fonts:Inter", glyph.FontFamily.ToString(), StringComparison.Ordinal);
        var enter = Shown();

        Click(window, chip);

        Assert.Equal(SendKey.PrimaryEnter, h.Services.Settings.Keyboard.SendKey);
        var primaryEnter = Shown();

        // The keyboard switches it back: Space on the focused chip.
        chip.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.Equal(SendKey.Enter, h.Services.Settings.Keyboard.SendKey);
        Assert.Equal(enter, Shown());
        Assert.Equal("", tab.ComposerText);

        // Verify resumes off the UI thread, so it comes last.
        await Verify($"With Enter:\n{enter}\nWith Ctrl+Enter:\n{primaryEnter}");
    }

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        UiText.Settle(window);
    }

    [AvaloniaFact]
    public async Task With_Ctrl_Enter_as_the_send_key_Enter_starts_a_new_line()
    {
        await using var h = new TabTestHarness(s => s.Keyboard.SendKey = SendKey.PrimaryEnter, dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var composer = Named<TextBox>(window, "Composer");
        composer.Focus();
        Assert.Contains("Enter for a new line", composer.PlaceholderText, StringComparison.Ordinal);

        window.KeyTextInput("first line");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyTextInput("second line");
        UiText.Settle(window);
        Assert.DoesNotContain(tab.Items, i => i is UserMessageItem);
        Assert.Contains('\n', tab.ComposerText);

        window.KeyPressQwerty(PhysicalKey.Enter, Primary);
        window.KeyReleaseQwerty(PhysicalKey.Enter, Primary);
        await UiText.SettleUntilAsync(window, () => tab.Items.OfType<UserMessageItem>().Any(), "the message");

        Assert.Equal("first line\nsecond line", tab.Items.OfType<UserMessageItem>().Single().Text.ReplaceLineEndings("\n"));
        Assert.Equal("", tab.ComposerText);
    }
}
