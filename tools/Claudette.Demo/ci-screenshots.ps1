<#
.SYNOPSIS
Takes screenshots of the demo on whichever system it runs on, Windows, macOS or Linux, to compare how Claudette looks on
each: in each style and theme, the main window, Settings on Appearance, and About Claudette.

.DESCRIPTION
For .github/workflows/screenshots.yml, which keeps each system's pictures as an artifact of its run. Unlike
screenshots.ps1, which takes the README's pictures on Windows through UI Automation and PrintWindow, it drives Claudette
only through its command channel (CLAUDETTE_AUTOMATION_PIPE), naming windows by their titles, and has Claudette save each
window as it draws it (the channel's capture command). So it needs only PowerShell 7 and a display, and on Linux a
virtual one does. The window's buttons, whether the OS or Avalonia draws them, aren't in the pictures, nor are popups.

Build Claudette and the demo first, in the configuration given (Debug by default). Claudette runs on the demo with
fake-claude as Claude Code, so no account and no tokens.

.PARAMETER Out
Where the pictures go.

.PARAMETER Projects
Where the demo's git repositories go. The conversation shows their paths. It must be empty, or one claudette-demo made.

.EXAMPLE
pwsh tools/Claudette.Demo/ci-screenshots.ps1 -Out screenshots
#>
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $Projects = (Join-Path ([IO.Path]::GetTempPath()) 'Demo'),
    [string] $Work = (Join-Path ([IO.Path]::GetTempPath()) 'claudette-ci-demo'),
    [string] $Configuration = 'Debug',
    # How long Claudette gets to start and restore its tabs before the first click.
    [int] $SettleSeconds = 10
)

$ErrorActionPreference = 'Stop'
# Written for Windows PowerShell 5.1 too, so it runs on any Windows without PowerShell 7.
$repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
$exe = if ([Environment]::OSVersion.Platform -eq 'Win32NT') { '.exe' } else { '' }
$app = [IO.Path]::Combine($repo, 'src', 'Claudette.App', 'bin', $Configuration, 'net10.0', "Claudette$exe")
$demo = [IO.Path]::Combine($repo, 'tools', 'Claudette.Demo', 'bin', $Configuration, 'net10.0', "claudette-demo$exe")
foreach ($built in $app, $demo) {
    if (-not (Test-Path $built)) { throw "$built isn't built; build Claudette and the demo ($Configuration) first." }
}
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path

# One command to Claudette, one JSON line each way, on its pipe (a Unix domain socket on macOS and Linux).
function Send-Command([string] $pipe, [hashtable] $command) {
    $client = [System.IO.Pipes.NamedPipeClientStream]::new('.', $pipe, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $client.Connect(10000)
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        $writer = [System.IO.StreamWriter]::new($client, $utf8)
        $writer.AutoFlush = $true
        $reader = [System.IO.StreamReader]::new($client, $utf8)
        $writer.WriteLine(($command | ConvertTo-Json -Compress))
        $line = $reader.ReadLine()
    }
    finally {
        $client.Dispose()
    }
    if (-not $line) { throw "Claudette didn't answer '$($command.verb)'." }
    $answer = $line | ConvertFrom-Json
    if (-not $answer.ok) { throw "Claudette couldn't $($command.verb): $($answer.error)" }
    $answer
}

# Tries $command until Claudette can carry it out: a window, or a control, shows a moment after what opened it.
function Send-WhenReady([string] $pipe, [hashtable] $command, [string] $what, [int] $seconds = 60) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ($true) {
        try {
            return Send-Command $pipe $command
        }
        catch {
            if ((Get-Date) -gt $deadline) { throw "Timed out waiting for $what`: $_" }
            Start-Sleep -Milliseconds 500
        }
    }
}

# The demo's tabs, each visited so it loads its session, then the first one's again; the same as screenshots.ps1's.
$tabs = 'Stamina drains twice on clients', 'Inventory loses items on save', 'Move to the Input System', 'UI on the Input System',
    'Gamepad rumble', 'Pool particle emitters', 'Grappling hook', 'Files (5)'

foreach ($style in 'standard', 'claude') {
    foreach ($theme in 'dark', 'light') {
        Write-Host "$style $theme"
        & $demo $Work --theme $theme --style $style --projects $Projects --still --quiet | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'claudette-demo failed.' }

        # Short: on macOS and Linux the pipe is a socket at $TMPDIR/CoreFxPipe_<name>, and macOS allows 104 characters
        # for its path, which the runner's long temporary folder and a whole GUID went past.
        $pipe = "claudette-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $start = [System.Diagnostics.ProcessStartInfo]::new($app)
        $start.UseShellExecute = $false
        $environment = @{
            CLAUDETTE_HOME = Join-Path $Work 'home'
            CLAUDE_CONFIG_DIR = Join-Path $Work 'claude-config'
            FAKE_CLAUDE_USAGE = Join-Path $Work 'usage.json'
            CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC = '1'
            # A source build otherwise copies itself to its data folder and runs from there.
            CLAUDETTE_RUN_IN_PLACE = '1'
            # On Windows, behind the user's windows, for a run on a desktop someone's using.
            CLAUDETTE_UNFOCUSED = '1'
            CLAUDETTE_AUTOMATION_PIPE = $pipe
        }
        foreach ($name in $environment.Keys) { $start.Environment[$name] = $environment[$name] }
        $claudette = [System.Diagnostics.Process]::Start($start)
        try {
            # Its main window, ready to draw.
            Send-WhenReady $pipe @{ verb = 'capture'; path = (Join-Path $Work 'ready.png') } 'the main window' | Out-Null
            Start-Sleep -Seconds $SettleSeconds
            foreach ($tab in $tabs) {
                Send-WhenReady $pipe @{ verb = 'click'; name = $tab } $tab | Out-Null
                Start-Sleep -Seconds 2
            }
            Start-Sleep -Seconds 2
            Send-Command $pipe @{ verb = 'capture'; path = (Join-Path $Out "$style-$theme.png") } | Out-Null

            # Settings on Appearance, whose preview shows the style and theme.
            Send-Command $pipe @{ verb = 'click'; name = 'Settings' } | Out-Null
            Send-WhenReady $pipe @{ verb = 'click'; title = 'Settings'; name = 'Appearance'; type = 'ListItem' } 'Settings' | Out-Null
            Start-Sleep -Seconds 2
            Send-Command $pipe @{ verb = 'capture'; title = 'Settings'; path = (Join-Path $Out "settings-$style-$theme.png") } | Out-Null
            Send-Command $pipe @{ verb = 'close'; title = 'Settings' } | Out-Null
            Start-Sleep -Seconds 1

            # About Claudette, from the header's menu.
            Send-Command $pipe @{ verb = 'click'; name = 'Menu' } | Out-Null
            Send-WhenReady $pipe @{ verb = 'click'; name = 'About Claudette' } 'the menu' | Out-Null
            Send-WhenReady $pipe @{ verb = 'capture'; title = 'About Claudette'; path = (Join-Path $Out "about-$style-$theme.png") } 'the About window' | Out-Null
            Send-Command $pipe @{ verb = 'close'; title = 'About Claudette' } | Out-Null
        }
        finally {
            # Closed by its window, so it stops each tab's Claude Code as it would for a user.
            try { Send-Command $pipe @{ verb = 'close' } | Out-Null } catch { Write-Warning "Couldn't close Claudette: $_" }
            if (-not $claudette.WaitForExit(30000)) {
                Write-Warning "Claudette (pid $($claudette.Id)) didn't close; ending it."
                $claudette.Kill()
            }
        }
    }
}
Remove-Item (Join-Path $Work 'ready.png') -ErrorAction SilentlyContinue
Write-Host "Saved to $Out"
