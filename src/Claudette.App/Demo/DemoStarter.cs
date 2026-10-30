using System.ComponentModel;
using Claudette.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.Demo;

/// <summary>
/// <b>Try the demo</b>, where it's offered (DESIGN.md §11, "Demo mode"): the sign-in screen, the setup screen and About
/// Claudette. Starts a demo in a window of its own, and says so, or why it couldn't.
/// </summary>
public sealed partial class DemoStarter(AppServices services) : ObservableObject
{
    public const string Tip = "Claudette on made-up projects, with a stand-in for Claude: no account needed, and nothing leaves this computer";

    /// <summary>Not in a demo: there's no demo of the demo.</summary>
    public bool IsOffered => services.Demo is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    public partial bool IsStarting { get; private set; }

    public string ButtonText => IsStarting ? "Starting the demo…" : "Try the demo";

    /// <summary>What happened: that it opened in a window of its own, or why it didn't start; null before.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    public bool HasNote => Note is not null;

    [ObservableProperty]
    public partial bool IsProblem { get; private set; }

    [RelayCommand]
    private async Task TryAsync()
    {
        if (IsStarting)
        {
            return;
        }
        IsStarting = true;
        Note = null;
        try
        {
            await services.StartDemoAsync();
            (Note, IsProblem) = ("The demo opens in a window of its own. Leave demo, at its top, closes it.", false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            (Note, IsProblem) = ($"The demo didn't start: {ex.Message}", true);
        }
        finally
        {
            IsStarting = false;
        }
    }
}
