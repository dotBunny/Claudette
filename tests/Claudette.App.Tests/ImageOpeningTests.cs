using System.Net;
using System.Text.Json.Nodes;
using Claudette.App.Conversation;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.Core.Protocol;
using Claudette.Core.Settings;
using Claudette.Core.Tests.Support;
using LiveMarkdown.Avalonia;
using SkiaSharp;

namespace Claudette.App.Tests;

/// <summary>
/// Image files and web images clicked open in Claudette's image window, as Settings → Editor → Open images in says, and
/// an animated GIF plays there (DESIGN.md §18, "Opening images").
/// </summary>
public class ImageOpeningTests
{
    private const string Url = "https://example.com/shots/menu.png";

    /// <summary>Settings → Editor → Open images in the app the OS uses.</summary>
    private static void ImagesInSystem(AppSettings settings) => settings.Editor.OpenImagesIn = OpenImagesIn.System;

    /// <summary>A click on a link in a reply's Markdown, as LiveMarkdown's renderer passes it.</summary>
    private static LinkClickedEventArgs Click(string address) =>
        new(MarkdownTextBlock.LinkClickEvent, new object(), new Uri(address, UriKind.RelativeOrAbsolute));

    /// <summary>The image windows the shell opens from now on.</summary>
    private static List<ImageViewerViewModel> Windows(TabTestHarness h)
    {
        var opened = new List<ImageViewerViewModel>();
        h.Shell.ShowImageWindow = opened.Add;
        return opened;
    }

    private static string Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static ToolImage Gif(byte[] data) => new(new MessageImage("image/gif", data), "Read", "loop.gif", "loop.gif", null, null);

    [Fact]
    public async Task An_image_file_a_reply_links_to_opens_in_the_image_window_saying_where_it_is()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Write(Path.Combine(h.WorkFolder, "docs", "menu.png"), TestImages.Png);
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click("docs/menu.png"));

        var viewer = Assert.Single(opened);
        Assert.Equal(("menu.png", $"{Path.Combine("docs", "menu.png")} · PNG 70 bytes", path), (viewer.Title, viewer.Details, viewer.Image.FilePath));
        Assert.Empty(h.Platform.OpenedFiles);
        Assert.Null(h.Shell.Groups.Single().Files);

        // Open: the file, in the OS's app.
        Assert.Equal("Open", viewer.OpenText);
        await viewer.OpenCommand.ExecuteAsync(null);
        Assert.Equal([path], h.Platform.OpenedFiles);
        viewer.Dispose();
    }

    [Fact]
    public async Task With_the_OS_app_chosen_an_image_file_opens_there_as_before()
    {
        await using var h = new TabTestHarness(ImagesInSystem);
        var tab = await h.OpenTabAsync();
        var path = Write(Path.Combine(h.WorkFolder, "menu.png"), TestImages.Png);
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click("menu.png"));

        Assert.Empty(opened);
        Assert.Equal([path], h.Platform.OpenedFiles);
    }

    [Fact]
    public async Task A_file_named_as_an_image_that_isnt_one_or_is_too_large_opens_as_any_other_file()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "notes.png"), "Not a picture\n"u8.ToArray());
        var huge = new byte[ViewerImages.MaxBytes + 1];
        TestImages.Png.CopyTo(huge, 0);
        var big = Write(Path.Combine(h.WorkFolder, "huge.png"), huge);
        var opened = Windows(h);

        // Text, whatever its name, opens in the editor; an image over 20 MB in the OS's app.
        await tab.OpenLinkCommand.ExecuteAsync(Click("notes.png"));
        await tab.OpenLinkCommand.ExecuteAsync(Click("huge.png"));

        Assert.Empty(opened);
        Assert.Equal(["notes.png"], h.Shell.Groups.Single().Files!.OpenFiles.Select(f => f.Name));
        Assert.Equal([big], h.Platform.OpenedFiles);
    }

    [Fact]
    public async Task A_BMP_shows_as_one_and_saves_as_one()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Write(Path.Combine(h.WorkFolder, "icon.bmp"), TestImages.Bmp());
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click("icon.bmp"));

        var viewer = Assert.Single(opened);
        Assert.Equal("icon.bmp · BMP 58 bytes", viewer.Details);
        h.Platform.SavePathToPick = null;
        await viewer.SaveAsCommand.ExecuteAsync(null);
        Assert.Equal("icon.bmp", h.Platform.LastSaveDialog?.Name);
        Assert.Equal([new SaveFileType("BMP image", "bmp")], h.Platform.LastSaveDialog?.Types);
        viewer.Dispose();
    }

    [Fact]
    public async Task Open_on_a_Read_card_opens_its_image_in_the_window()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var path = Write(Path.Combine(h.WorkFolder, "menu.png"), TestImages.Png);
        h.Transport.Emit(Wire.Tool(null, "t1", "Read", new JsonObject { ["file_path"] = path }));
        h.Transport.Emit(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "t1", ["content"] = "Read the image" }),
            },
        });
        await TabTestHarness.Eventually(() => tab.Items.ToolCalls().SingleOrDefault() is { IsComplete: true }, "the tool's result");
        var opened = Windows(h);

        await tab.OpenToolFileCommand.ExecuteAsync(tab.Items.ToolCalls().Single());

        Assert.Equal(path, Assert.Single(opened).Image.FilePath);
        Assert.Empty(h.Platform.OpenedFiles);
        opened[0].Dispose();
    }

    [Fact]
    public async Task With_the_OS_app_chosen_an_image_from_a_tool_still_opens_in_the_window()
    {
        await using var h = new TabTestHarness(ImagesInSystem);
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        tab.OpenImageCommand.Execute(new ToolImage(new MessageImage("image/png", TestImages.Png), "mcp__playwright__browser_take_screenshot", "Image", "image.png", null, null));

        Assert.Single(opened).Dispose();
    }

    [Fact]
    public async Task A_web_link_to_an_image_loads_through_Claudettes_client_and_shows_in_the_window()
    {
        var http = new FakeHttpHandler().OnBytes(Url, TestImages.Png);
        await using var h = new TabTestHarness(http: http);
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click(Url));

        var viewer = Assert.Single(opened);
        Assert.Equal(("menu.png", "example.com · PNG 70 bytes", "menu.png"), (viewer.Title, viewer.Details, viewer.Image.FileName));
        Assert.Empty(h.Platform.OpenedUrls);

        // The tab keeps it, as it does a reply's web image.
        await tab.OpenLinkCommand.ExecuteAsync(Click(Url));
        Assert.Equal(2, opened.Count);
        Assert.Single(http.Requests);

        // Open in browser: its address.
        Assert.True(viewer.CanOpen);
        Assert.Equal("Open in browser", viewer.OpenText);
        await viewer.OpenCommand.ExecuteAsync(null);
        Assert.Equal([Url], h.Platform.OpenedUrls);
        opened.ForEach(v => v.Dispose());
    }

    [Fact]
    public async Task A_web_link_that_isnt_an_image_after_all_opens_in_the_browser()
    {
        const string Page = "https://example.com/gallery/menu.png";
        const string Missing = "https://example.com/missing.gif";
        var http = new FakeHttpHandler().On(Page, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html><body>A gallery</body></html>") });
        await using var h = new TabTestHarness(http: http);
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click(Page));
        await tab.OpenLinkCommand.ExecuteAsync(Click(Missing));

        Assert.Empty(opened);
        Assert.Equal([Page, Missing], h.Platform.OpenedUrls);
    }

    [Fact]
    public async Task Other_web_links_and_every_web_link_with_the_OS_app_chosen_go_to_the_browser_unasked()
    {
        var http = new FakeHttpHandler().OnBytes(Url, TestImages.Png);
        await using var h = new TabTestHarness(http: http);
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        await tab.OpenLinkCommand.ExecuteAsync(Click("https://example.com/docs"));
        ImagesInSystem(h.Services.Settings);
        await tab.OpenLinkCommand.ExecuteAsync(Click(Url));

        Assert.Empty(opened);
        Assert.Empty(http.Requests);
        Assert.Equal(["https://example.com/docs", Url], h.Platform.OpenedUrls);
    }

    [Fact]
    public async Task In_the_explorer_a_click_previews_an_image_and_a_double_click_opens_it_in_the_window()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "menu.png"), TestImages.Png);
        var tab = await h.OpenTabAsync();
        var group = h.Shell.Groups.Single();
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(group));
        var files = group.Files!;
        await TabTestHarness.Eventually(() => files.Rows.Any(r => r.Name == "menu.png"), "the files");
        var row = files.Rows.Single(r => r.Name == "menu.png");
        var opened = Windows(h);

        // A click previews it, as any file, so going down a folder of images opens no windows.
        await files.OpenRowCommand.ExecuteAsync(row);
        var preview = Assert.Single(files.OpenFiles);
        await TabTestHarness.Eventually(() => preview.IsLoaded, "the preview");
        Assert.True(preview is { IsPreview: true, HasProblem: true, CanOpenInViewer: true });
        Assert.Empty(opened);

        await files.OpenRowToStayCommand.ExecuteAsync(row);
        Assert.Equal("menu.png · PNG 70 bytes", Assert.Single(opened).Details);

        // The preview's own button opens it there whatever the setting says; with the OS's app chosen, a double-click
        // keeps the preview, as before.
        ImagesInSystem(h.Services.Settings);
        await files.OpenFileInViewerCommand.ExecuteAsync(preview);
        await files.OpenRowToStayCommand.ExecuteAsync(row);
        Assert.Equal(2, opened.Count);
        Assert.False(files.OpenFiles.Single().IsPreview);
        opened.ForEach(v => v.Dispose());
        GC.KeepAlive(tab);
    }

    [Fact]
    public async Task Only_a_file_named_as_an_image_offers_the_image_viewer_which_checks_its_bytes()
    {
        await using var h = new TabTestHarness();
        Write(Path.Combine(h.WorkFolder, "huge.gif"), new byte[11 * 1024 * 1024]);
        Write(Path.Combine(h.WorkFolder, "data.bin"), [0, 1, 2]);
        await h.OpenTabAsync();
        var group = h.Shell.Groups.Single();
        InlineDispatcher.Run(() => h.Shell.OpenProjectFilesCommand.Execute(group));
        var files = group.Files!;

        // Opened on the UI thread, where the explorer may still be listing the folder: both watch it.
        var gif = await InlineDispatcher.RunAsync(() => files.OpenFileAsync(Path.Combine(h.WorkFolder, "huge.gif"), preview: false));
        var bin = await InlineDispatcher.RunAsync(() => files.OpenFileAsync(Path.Combine(h.WorkFolder, "data.bin"), preview: false));
        await TabTestHarness.Eventually(() => gif.IsLoaded && bin.IsLoaded, "the files");

        // Over 10 MB but named as an image: the window may still show it, and checks its bytes on the click.
        Assert.True(gif.CanOpenInViewer);
        Assert.False(bin.CanOpenInViewer);
        var opened = Windows(h);
        await files.OpenFileInViewerCommand.ExecuteAsync(gif);
        Assert.Empty(opened);
        Assert.Equal([gif.Path], h.Platform.OpenedFiles);
    }

    [Fact]
    public async Task An_animated_GIF_plays_frame_by_frame_and_pauses()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        tab.OpenImageCommand.Execute(Gif(TestImages.Gif([5, 0, 20])));

        var viewer = Assert.Single(opened);
        Assert.True(viewer is { IsAnimated: true, IsPlaying: true, Frame: 0, PlayText: "Pause" });
        Assert.EndsWith("· 3 frames", viewer.Details, StringComparison.Ordinal);
        h.Time.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(0, viewer.Frame);
        h.Time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, viewer.Frame);
        // A frame of no time shows for a tenth of a second, as browsers show it.
        h.Time.Advance(ImageAnimation.ShortFrameShownFor);
        Assert.Equal(2, viewer.Frame);
        // It loops for ever.
        h.Time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(0, viewer.Frame);

        viewer.PlayPauseCommand.Execute(null);
        Assert.True(viewer is { IsPlaying: false, PlayText: "Play" });
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0, viewer.Frame);
        viewer.PlayPauseCommand.Execute(null);
        h.Time.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, viewer.Frame);

        // Copy image: the frame showing, as a PNG, since the clipboard holds a still image.
        await viewer.CopyCommand.ExecuteAsync(null);
        Assert.Equal(TestImages.Png[..8], h.Platform.ClipboardImage![..8]);
        viewer.Dispose();
    }

    [Fact]
    public async Task A_GIF_that_plays_once_stops_on_its_last_frame_and_Play_starts_it_again()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        tab.OpenImageCommand.Execute(Gif(TestImages.Gif([10, 10], loops: null)));
        var viewer = Assert.Single(opened);
        h.Time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, viewer.Frame);
        h.Time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(viewer is { Frame: 1, IsPlaying: false });
        h.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, viewer.Frame);

        viewer.PlayPauseCommand.Execute(null);
        Assert.True(viewer is { Frame: 0, IsPlaying: true });
        h.Time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, viewer.Frame);
        viewer.Dispose();
    }

    [Fact]
    public async Task With_motion_reduced_an_animation_opens_still_on_its_first_frame_until_Play()
    {
        await using var h = new TabTestHarness();
        h.Services.Settings.Appearance.Motion = MotionSetting.Reduce;
        h.Services.SaveSettings();
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        tab.OpenImageCommand.Execute(Gif(TestImages.Gif([10, 10])));

        var viewer = Assert.Single(opened);
        Assert.True(viewer is { IsAnimated: true, IsPlaying: false, Frame: 0, PlayText: "Play" });
        h.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, viewer.Frame);
        viewer.PlayPauseCommand.Execute(null);
        h.Time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, viewer.Frame);
        viewer.Dispose();
    }

    [Fact]
    public void Each_frame_decodes_over_the_one_before_or_on_its_own_out_of_turn()
    {
        using var animation = ImageAnimation.From("image/gif", TestImages.Gif([10, 10, 10]))!;
        Assert.Equal((3, -1), (animation.FrameCount, animation.Repetitions));

        SKColor Pixel(int frame)
        {
            using var bitmap = SKBitmap.Decode(animation.EncodePng(frame));
            return bitmap.GetPixel(0, 0);
        }
        Assert.Equal(SKColors.Red, Pixel(0));
        Assert.Equal(SKColors.Blue, Pixel(1));
        Assert.Equal(SKColors.Red, Pixel(2));
        Assert.Equal(SKColors.Blue, Pixel(1));
        Assert.Null(animation.EncodePng(3));

        animation.Dispose();
        Assert.Null(animation.EncodePng(0));
    }

    [Fact]
    public void Only_a_GIF_or_WebP_of_more_than_one_frame_animates()
    {
        Assert.Null(ImageAnimation.From("image/gif", TestImages.Gif([10])));
        Assert.Null(ImageAnimation.From("image/png", TestImages.Png));
        Assert.Null(ImageAnimation.From("image/gif", "GIF89a and then nothing"u8.ToArray()));
        Assert.Null(ImageAnimation.From("image/webp", []));
    }

    [Fact]
    public async Task A_still_image_has_nothing_to_play()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var opened = Windows(h);

        var still = TestImages.Gif([10]);
        tab.OpenImageCommand.Execute(Gif(still));
        tab.OpenImageCommand.Execute(new ToolImage(new MessageImage("image/png", TestImages.Png), "Read", "a.png", "a.png", null, null));

        Assert.All(opened, v => Assert.False(v.IsAnimated));
        Assert.Equal($"Read · GIF {still.Length} bytes", opened[0].Details);
        opened.ForEach(v => v.Dispose());
    }

    [Fact]
    public async Task Settings_Editor_chooses_where_images_open_and_Reset_goes_back_to_the_viewer()
    {
        await using var h = new TabTestHarness();
        var settings = new SettingsViewModel(h.Services, null);
        Assert.True(settings.Files.ImagesInViewer);

        settings.Files.ImagesInSystem = true;
        Assert.Equal(OpenImagesIn.System, h.Services.Settings.Editor.OpenImagesIn);
        Assert.False(settings.Files.ImagesInViewer);

        settings.Files.ResetAllSections();
        Assert.Equal(OpenImagesIn.Viewer, h.Services.Settings.Editor.OpenImagesIn);
        Assert.True(settings.Files.ImagesInViewer);
    }
}
