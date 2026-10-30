using Claudette.App.Services;
using Claudette.Core;
using Claudette.Core.Files;
using Claudette.Core.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// An image full size in a window of its own (DESIGN.md §18, "Images from Claude's tools", "Opening images"): from a
/// tool's result, a reply, or an image file or web image clicked. It has <b>Copy image</b>, <b>Save as…</b> and, for a
/// file, <b>Copy path</b> and <b>Open</b> (<b>Open in browser</b> for a web image). An animated GIF or WebP plays, with <b>Pause</b> and
/// <b>Play</b>; with motion reduced it opens on its first frame. It opens fitting the window, and zooms in and out in
/// steps.
/// </summary>
public sealed partial class ImageViewerViewModel : ViewModelBase, IDisposable
{
    private readonly AppServices _services;
    private readonly UiTimeout _copied;
    private readonly UiTimeout _pathCopied;
    private readonly UiTimeout _nextFrame;

    /// <summary>How many times it has played through since <b>Play</b>.</summary>
    private int _plays;

    public ImageViewerViewModel(ToolImage image, AppServices services)
    {
        Image = image;
        _services = services;
        _copied = new UiTimeout(services.Time, services.Dispatcher);
        _pathCopied = new UiTimeout(services.Time, services.Dispatcher);
        _nextFrame = new UiTimeout(services.Time, services.Dispatcher);
        Animation = ImageAnimation.From(image.Image.MediaType, image.Image.Data);
        Details = string.Join(" · ", new[]
        {
            image.Tool,
            $"{TypeName} {Formats.Size(image.Image.Data.LongLength)}",
            Animation is { } animation ? $"{animation.FrameCount} frames" : null,
            image.ScaledFrom is { } original ? $"scaled down from {original} to send" : null,
        }.OfType<string>());
        if (Animation is not null && !services.ReduceMotion)
        {
            Play();
        }
    }

    public ToolImage Image { get; }

    /// <summary>The window's title: the file's name, or "report.pdf, page 3".</summary>
    public string Title => Image.Name;

    /// <summary>The image's bytes, which the view decodes.</summary>
    public byte[] Data => Image.Image.Data;

    /// <summary>"Read · PNG 12 KB · scaled down from 3000 × 1000 to send", "docs/intro.gif · GIF 2.4 MB · 48 frames".</summary>
    public string Details { get; }

    /// <summary>The file it's from, which can be opened in the app the OS uses for it, or the web address, in the browser.</summary>
    public bool CanOpen => Image.FilePath is not null || IsWebImage;

    private bool IsWebImage => Image.Url?.Scheme is "http" or "https";

    /// <summary><b>Open</b> for a file, <b>Open in browser</b> for a web image.</summary>
    public string OpenText => Image.FilePath is null && IsWebImage ? "Open in browser" : "Open";

    public string OpenTip => Image.FilePath is null && IsWebImage
        ? "Open the image's address in your browser"
        : "Open the file in the app your system uses for it";

    /// <summary>Its frames, for an animated GIF or WebP; null for a still image.</summary>
    public ImageAnimation? Animation { get; }

    public bool IsAnimated => Animation is not null;

    /// <summary>The frame showing, which the view decodes as it changes.</summary>
    [ObservableProperty]
    public partial int Frame { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayText))]
    public partial bool IsPlaying { get; private set; }

    /// <summary><b>Pause</b> while it plays, else <b>Play</b>.</summary>
    public string PlayText => IsPlaying ? "Pause" : "Play";

    /// <summary><b>Copy image</b> says "Copied" for a moment.</summary>
    [ObservableProperty]
    public partial bool IsCopied { get; private set; }

    /// <summary>It's from a file, whose path <b>Copy path</b> copies.</summary>
    public bool CanCopyPath => Image.FilePath is not null;

    /// <summary><b>Copy path</b> says "Copied" for a moment.</summary>
    [ObservableProperty]
    public partial bool IsPathCopied { get; private set; }

    /// <summary>Why the last save didn't work; null when it did, or before one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    public bool HasProblem => Problem is not null;

    /// <summary>The zoom's steps, as percentages of the image's own size.</summary>
    public static IReadOnlyList<int> ZoomSteps { get; } = [10, 25, 33, 50, 67, 75, 100, 125, 150, 200, 300, 400, 600, 800, 1200, 1600];

    /// <summary>The step it's zoomed to, as a percentage; null while it fits the window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFit), nameof(ZoomPercent), nameof(ZoomText))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand), nameof(FitCommand))]
    public partial int? Zoom { get; private set; }

    /// <summary>
    /// The percentage the image fits the window at, never over 100 since it never grows past its own size. The view
    /// says it as the window changes size.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercent), nameof(ZoomText))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public partial double FitPercent { get; set; } = 100;

    /// <summary>It fits the window: the whole image, shrunk to fit but never grown.</summary>
    public bool IsFit => Zoom is null;

    /// <summary>The percentage of its own size it's shown at.</summary>
    public double ZoomPercent => Zoom ?? FitPercent;

    /// <summary>"45%", between <b>−</b> and <b>+</b>.</summary>
    public string ZoomText => $"{Shown}%";

    /// <summary>The percentage as it reads, which zooming steps on from: "50%" fitting goes in to 67%, not to 50%.</summary>
    private int Shown => (int)Math.Round(ZoomPercent);

    /// <summary><b>+</b>: the next step up.</summary>
    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() => Zoom = ZoomSteps.First(step => step > Shown);

    private bool CanZoomIn() => Shown < ZoomSteps[^1];

    /// <summary><b>−</b>: the next step down.</summary>
    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() => Zoom = ZoomSteps.Last(step => step < Shown);

    private bool CanZoomOut() => Shown > ZoomSteps[0];

    /// <summary><b>Fit</b>: the whole image in the window again.</summary>
    [RelayCommand(CanExecute = nameof(CanFit))]
    private void Fit() => Zoom = null;

    private bool CanFit() => Zoom is not null;

    private string TypeName => Image.Image.MediaType switch
    {
        "image/jpeg" => "JPEG",
        "image/gif" => "GIF",
        "image/webp" => "WebP",
        "image/bmp" => "BMP",
        _ => "PNG",
    };

    /// <summary><b>Copy image</b>: the frame showing, for an animated one, since the clipboard holds a still image.</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        var frame = Frame;
        var data = Animation is { } animation ? await Task.Run(() => animation.EncodePng(frame)) ?? Data : Data;
        await _services.Platform.SetClipboardImageAsync(data);
        IsCopied = true;
        _copied.Restart(TabViewModel.CopiedFor, () => IsCopied = false);
    }

    /// <summary><b>Copy path</b>: the file's full path, with any <c>..</c> in it resolved.</summary>
    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (Image.FilePath is not { } path)
        {
            return;
        }
        await _services.Platform.SetClipboardTextAsync(Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : path);
        IsPathCopied = true;
        _pathCopied.Restart(TabViewModel.CopiedFor, () => IsPathCopied = false);
    }

    /// <summary><b>Save as…</b>: the image as it came, so an animated one stays animated.</summary>
    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var extension = Path.GetExtension(Image.FileName).TrimStart('.');
        if (await _services.Platform.PickSaveFileAsync("Save image", Image.FileName, [new SaveFileType($"{TypeName} image", extension)]) is not { } path)
        {
            return;
        }
        try
        {
            await Task.Run(() => AtomicFile.WriteAllBytes(path, Data));
            Problem = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Problem = $"Couldn't save the image: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task OpenAsync()
    {
        if (Image.FilePath is { } path)
        {
            return _services.Platform.OpenFileAsync(path);
        }
        return IsWebImage ? _services.Platform.OpenUrlAsync(Image.Url!.AbsoluteUri) : Task.CompletedTask;
    }

    /// <summary><b>Pause</b>, or <b>Play</b>: on from the frame showing, or from the start once it has played out.</summary>
    [RelayCommand]
    private void PlayPause()
    {
        if (IsPlaying)
        {
            _nextFrame.Cancel();
            IsPlaying = false;
        }
        else
        {
            Play();
        }
    }

    private void Play()
    {
        if (Animation is not { } animation)
        {
            return;
        }
        if (HasPlayedOut(animation))
        {
            Frame = 0;
            _plays = 0;
        }
        IsPlaying = true;
        _nextFrame.Restart(animation.Duration(Frame), Advance);
    }

    /// <summary>The next frame, after the last the first again, until it has played as many times as it says.</summary>
    private void Advance()
    {
        if (Animation is not { } animation)
        {
            return;
        }
        if (Frame + 1 < animation.FrameCount)
        {
            Frame++;
        }
        else
        {
            _plays++;
            if (HasPlayedOut(animation))
            {
                IsPlaying = false;
                return;
            }
            Frame = 0;
        }
        _nextFrame.Restart(animation.Duration(Frame), Advance);
    }

    /// <summary>It stopped on its last frame after its last time through.</summary>
    private bool HasPlayedOut(ImageAnimation animation) => animation.Repetitions >= 0 && _plays > animation.Repetitions;

    /// <summary>The window closed.</summary>
    public void Dispose()
    {
        _copied.Dispose();
        _pathCopied.Dispose();
        _nextFrame.Dispose();
        Animation?.Dispose();
    }
}
