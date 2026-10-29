<#
.SYNOPSIS
Takes the user guide's screenshots (Windows): the demo from claudette-demo in the Standard style, dark then light, each
picture cropped to the part of Claudette the page describes.

.DESCRIPTION
GUIDE.md ("Pictures") says what the pictures are. Each shot is a name (<chapter>/<picture>), the steps to it and the part of the window
to crop to, from the control's UI Automation bounds with a margin; it's saved as <Out>\<chapter>\<picture>-<theme>.png.
For each theme, it starts Claudette on the demo (fake-claude as Claude Code, so no account and no tokens) four times:
- signed out (FAKE_CLAUDE_LOGGED_IN=0), for the sign-in screen;
- as the demo is, after visiting every tab so each shows its model and any reviewed icon, for the shots that leave it
  alone: the new tab picker, the sidebar, a tab's info card, the command palette, a Tool calls card, the composer, the
  Changed files, Scratch pad and Project tool windows, a Files tab, the Usage window, the Settings window on General,
  Appearance, a search, Unreal Engine's page in Project tools, About and Accounts, and a tab's Tab settings;
- with a stand-in for the Unreal editor open on starfall (claudette-demo --stand-in-editor, run as UnrealEditor.exe),
  for its log under the tab and in the main area;
- and last, for the shots that change it by sending prompts: a usage limit (LIMIT), a turn that changed files (EDIT_FILE),
  a permission waiting (ASK_PERMISSION) and a process for the process monitor (SPAWN).
Claudette is closed by its window, as a user would, each time.

A run leaves you the focus, the keyboard and the pointer, so you can carry on working meanwhile, a fullscreen app
included (DESIGN.md §9, "Taking screenshots without the focus"). Claudette runs with CLAUDETTE_UNFOCUSED=1, its windows
behind yours and never taking the focus, and is driven through its command channel (CLAUDETTE_AUTOMATION_PIPE): its
controls are found through UI Automation, and clicked, typed in and so on by Claudette itself. Captures go through
PrintWindow, which doesn't need the windows in view. Screenshots.psm1 has the helpers.

.PARAMETER Projects
Where the demo's git repositories go. The conversation shows their paths, so choose a short one that says nothing
about you, such as C:\Demo. It must be empty, or one claudette-demo made.

.PARAMETER Only
The shots to take, by name (tabs/sidebar) or chapter (tabs). Every shot by default.

.PARAMETER Themes
The themes to take them in.

.EXAMPLE
tools\Claudette.Demo\guide-screenshots.ps1 -Projects C:\Demo

.EXAMPLE
tools\Claudette.Demo\guide-screenshots.ps1 -Projects C:\Demo -Only tabs/sidebar, usage -Themes dark
#>
param(
    [Parameter(Mandatory)] [string] $Projects,
    [string] $Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots\guide'),
    [string] $Work = (Join-Path ([IO.Path]::GetTempPath()) 'claudette-demo'),
    [string[]] $Only = @(),
    [string[]] $Themes = @('dark', 'light'),
    # How long Claudette gets to start and restore its tabs before the first click.
    [int] $SettleSeconds = 10
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path

Import-Module (Join-Path $PSScriptRoot 'Screenshots.psm1') -Force -DisableNameChecking
$build = Build-Claudette $repo
$failed = @()

# Whether any of the shots is one to take.
function Test-Wanted([string[]] $names) {
    foreach ($name in $names) {
        if ($Only.Count -eq 0) { return $true }
        foreach ($wanted in $Only) { if ($name -eq $wanted -or $name.StartsWith("$wanted/")) { return $true } }
    }
    $false
}

# Starts Claudette on the demo afresh in the theme, runs $steps with $window and $claudette set, and closes it.
function Use-Claudette([hashtable] $environment, [scriptblock] $steps) {
    New-DemoContent $build.Demo $Work $Projects 'standard' $theme
    $script:claudette = Start-Claudette $build.App $Work $environment
    try {
        $script:window = $claudette.MainWindowHandle
        Start-Sleep -Seconds $SettleSeconds
        & $steps
    }
    finally {
        Stop-Claudette $claudette
    }
}

# One shot: $steps gets the path to save the picture to.
function Shot([string] $name, [scriptblock] $steps) {
    if (-not (Test-Wanted $name)) { return }
    $path = Join-Path $Out "$name-$theme.png"
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    Write-Host "  $name"
    Set-Step $name
    try {
        & $steps $path
    }
    catch {
        Write-Warning "$name ($theme): $_"
        $script:failed += "$name ($theme)"
    }
}

# A tab loads its session when it's first shown: visit each, then come back to Grappling hook.
function Show-Tabs {
    Set-Step 'visiting the tabs'
    $tabs = 'Stamina drains twice on clients', 'Inventory loses items on save', 'Move to the Input System', 'UI on the Input System',
        'Gamepad rumble', 'Pool particle emitters', 'Grappling hook'
    foreach ($text in $tabs) {
        Invoke-ByText $window $text
        Start-Sleep -Seconds 2
    }
}

# Opens Settings, which is a window of its own, from its button in the header, and returns it.
function Open-Settings {
    Invoke-ByText $window 'Settings'
    $settings = Get-OtherWindow $claudette $window 'the Settings window'
    Start-Sleep -Seconds 2
    $settings
}

# A tool window at the right (the top group's, or with -Below the split group's under it), down to the bottom of $last
# (a control in it) when that's given, or $height device-independent pixels from its top: a window's page is often much
# shorter than the window.
function Get-PanelRect($last = $null, [int] $height = 0, [switch] $Below) {
    $panel = Find-ByText $window $(if ($Below) { 'Tool window at the right, below' } else { 'Tool window at the right' })
    $rect = $panel.Current.BoundingRectangle
    $scale = [WindowShot]::Scale($window)
    $bottom = 0
    if ($last) { $bottom = $last.Current.BoundingRectangle.Bottom + 12 * $scale }
    elseif ($height) { $bottom = $rect.Top + $height * $scale }
    if ($bottom) { $rect = New-Object System.Windows.Rect($rect.Left, $rect.Top, $rect.Width, ($bottom - $rect.Top)) }
    $rect
}

# Shows a tool window by its icon on the strip (DESIGN.md §3, "Tool windows"), unless it shows already: its kind, such as
# ScratchPad, finds the icon, and its header's "Hide <title>" says it shows.
function Show-ToolWindow([string] $kind, [string] $title) {
    if (-not (Find-ByText $window "Hide $title" -Optional)) {
        Wait-Until { Find-ById $window "tool-$kind" } "the $title icon" 30
        Send-Click (Find-ById $window "tool-$kind")
        Start-Sleep -Seconds 2
    }
}

# Shows a tool window, such as Scratch pad, for the tab.
function Show-SidePage([string] $tab, [string] $kind, [string] $title) {
    Invoke-ByText $window $tab
    Start-Sleep -Seconds 2
    Show-ToolWindow $kind $title
    Start-Sleep -Seconds 2
}

foreach ($theme in $Themes) {
    Write-Host "standard $theme"

    # Signed out.
    if (Test-Wanted 'getting-started/sign-in') {
        Use-Claudette @{ FAKE_CLAUDE_LOGGED_IN = '0' } {
            Shot 'getting-started/sign-in' {
                param($path)
                Wait-Until { Find-ByText $window 'Sign in' -Optional } 'the sign-in screen' 30
                Start-Sleep -Seconds 1
                Save-Window $window $path
            }
        }
    }

    # As the demo is.
    if (Test-Wanted 'getting-started/new-tab', 'tabs/sidebar', 'tabs/info-card', 'tabs/command-palette', 'conversation/tool-calls',
        'conversation/scratch-pad', 'composer/composer', 'changes/changed-files', 'project-tools/project-page', 'tabs/tool-windows', 'usage/usage-window',
        'settings/settings-window', 'settings/appearance', 'settings/search', 'settings/project-tools', 'settings/about', 'accounts/settings-accounts',
        'tabs/tab-settings', 'project-files/files-tab', 'project-files/markdown-preview') {
        Use-Claudette @{} {
            Show-Tabs

            # The picker, which has the older projects as recent folders.
            Shot 'getting-started/new-tab' {
                param($path)
                Invoke-ByText $window 'New tab'
                Wait-Until { Find-ById $window 'SearchBox' } 'the new tab picker' 20
                Start-Sleep -Seconds 1
                Save-Element $window $path (Get-Parent (Find-ById $window 'SearchBox')) 12
                Invoke-ByText $window 'Cancel'
                Start-Sleep -Seconds 1
            }

            # The sidebar's head, with New tab and History, down to its last group; the foot shows only with a badge.
            Shot 'tabs/sidebar' {
                param($path)
                Save-Elements $window $path ((Find-ByText $window 'New tab'), (Find-ById $window 'GroupList')) 10
            }

            # The card has no control of its own: its tiles, with room around them for its header and edge.
            Shot 'conversation/tool-calls' {
                param($path)
                Invoke-ByText $window 'Grappling hook'
                Start-Sleep -Seconds 2
                $tiles = Get-Parent (Find-ByText $window ('Write ' + (Join-Path $Projects 'starfall\Source\Starfall\Abilities\GrappleComponent.h')))
                Show-Element $window $tiles (Find-ById $window 'ConversationScroll') 50
                Start-Sleep -Seconds 1
                Save-Element $window $path $tiles @(19, 36, 12, 14)
            }

            # The bar, the message box and the status line under it.
            Shot 'composer/composer' {
                param($path)
                Invoke-ByText $window 'Grappling hook'
                Start-Sleep -Seconds 2
                # Room above for Claudette, who stands on its edge.
                Save-Element $window $path (Find-ById $window 'ComposerPanel') @(6, 46, 6, 6)
            }

            Shot 'changes/changed-files' {
                param($path)
                Show-SidePage 'Grappling hook' 'Files' 'Changed files'
                $rows = (Find-ById $window 'ChangedFilesList').FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
                Save-Rect $window $path (Get-PanelRect $rows[$rows.Count - 1]) 0
            }

            Shot 'project-tools/project-page' {
                param($path)
                Show-SidePage 'Grappling hook' 'Project' 'Project'
                Save-Rect $window $path (Get-PanelRect (Find-Lowest (Find-ByText $window 'Tool window at the right'))) 0
            }

            # The right side: Changed files over Scratch pad, with the strip beside them, Changed files' icon filled in.
            Shot 'tabs/tool-windows' {
                param($path)
                Show-SidePage 'Grappling hook' 'Files' 'Changed files'
                Show-ToolWindow 'ScratchPad' 'Scratch pad'
                Save-Elements $window $path @((Find-ByText $window 'Tool window at the right'), (Find-ByText $window 'Tool window at the right, below'), (Find-ByText $window 'Tool windows')) @(8, 8, 0, 8)
            }

            Shot 'usage/usage-window' {
                param($path)
                Invoke-ByText $window 'Usage details'
                $usage = Get-OtherWindow $claudette $window 'the Usage window'
                Start-Sleep -Seconds 3
                Save-Window $usage $path
                Close-Window $usage
                Start-Sleep -Seconds 1
            }

            Shot 'settings/settings-window' {
                param($path)
                $settings = Open-Settings
                Select-ListItem $settings 'General'
                Start-Sleep -Seconds 1
                Save-Window $settings $path
                Close-Window $settings
                Start-Sleep -Seconds 1
            }

            Shot 'settings/appearance' {
                param($path)
                $settings = Open-Settings
                Select-ListItem $settings 'Appearance'
                Start-Sleep -Seconds 1
                Save-Window $settings $path
                Close-Window $settings
                Start-Sleep -Seconds 1
            }

            # What a search finds, with the page of the first result beside it.
            Shot 'settings/search' {
                param($path)
                $settings = Open-Settings
                Set-ElementText (Find-ById $settings 'SearchBox') 'font'
                Wait-Until { Find-ByType $settings 'ListItem' 'Code font' } 'the search results' 10
                Start-Sleep -Seconds 1
                Save-Window $settings $path
                Set-ElementText (Find-ById $settings 'SearchBox') ''
                Close-Window $settings
                Start-Sleep -Seconds 1
            }

            # Project tools' group in the sidebar, on Unreal Engine's page: its switches, then the defaults it declares.
            Shot 'settings/project-tools' {
                param($path)
                $settings = Open-Settings
                # The pages above the selected tab's project scroll, and only those in view are drawn: down to Project tools.
                Send-ElementCommand 'scroll' (Find-ById $settings 'SidebarList') @{ lines = 20 } | Out-Null
                Start-Sleep -Milliseconds 500
                Select-ListItem $settings 'Unreal Engine'
                Start-Sleep -Seconds 1
                Save-Window $settings $path
                Close-Window $settings
                Start-Sleep -Seconds 1
            }

            # About: the version, its links and license, and the trademarks.
            # About Claudette, a window of its own, from the header's menu.
            Shot 'settings/about' {
                param($path)
                $menu = Open-Menu $claudette $window (Find-ByText $window 'Menu') 'About Claudette'
                Start-Sleep -Seconds 1
                Send-Click (Find-ByText $menu 'About Claudette')
                $about = Get-OtherWindow $claudette $window 'the About window'
                Start-Sleep -Seconds 2
                Save-Window $about $path
                Close-Window $about
                Start-Sleep -Seconds 1
            }

            Shot 'accounts/settings-accounts' {
                param($path)
                $settings = Open-Settings
                Select-ListItem $settings 'Accounts'
                Start-Sleep -Seconds 1
                Save-Window $settings $path
                Close-Window $settings
                Start-Sleep -Seconds 1
            }

            # From the tab's menu: the dialog over the main window, the demo's model and effort its own.
            Shot 'tabs/tab-settings' {
                param($path)
                Invoke-ByText $window 'Grappling hook'
                Start-Sleep -Seconds 2
                Send-RightClick (Find-ByText $window 'Grappling hook')
                $menu = Get-OtherWindow $claudette $window 'the tab menu'
                Start-Sleep -Seconds 1
                Send-Click (Find-ByText $menu "Tab settings$([char] 0x2026)")
                Wait-Until { Find-ByPrefix $window 'Settings for ' } 'Tab settings' 20
                Start-Sleep -Seconds 1
                # The dialog has no control of its own: its title and Done, with room for its padding and edge.
                Save-Elements $window $path ((Find-ByPrefix $window 'Settings for '), (Find-ByText $window 'Done')) @(25, 21, 25, 13)
                Invoke-ByText $window 'Done'
                Wait-Until { -not (Find-ByPrefix $window 'Settings for ') } 'Tab settings to close' 20
            }

            # The card over a tab's row, as when the pointer rests on it: a window of its own.
            Shot 'tabs/info-card' {
                param($path)
                $row = Find-ByText $window 'Grappling hook'
                Show-Tip $row
                try {
                    Wait-Until { [WindowShot]::OtherWindow($claudette.Id, $window) -ne [IntPtr]::Zero } "the tab's info card" 20
                    Start-Sleep -Seconds 1
                    Save-Window ([WindowShot]::OtherWindow($claudette.Id, $window)) $path
                }
                finally {
                    Hide-Tip $row
                }
                Wait-Until { [WindowShot]::OtherWindow($claudette.Id, $window) -eq [IntPtr]::Zero } 'the info card to go' 20
            }

            # Its shortcut opens it, and closes it again.
            Shot 'tabs/command-palette' {
                param($path)
                Send-Keys $window 'Ctrl+Shift+P'
                try {
                    Wait-Until { Find-ByText $window 'Command palette' -Optional } 'the command palette' 20
                    Start-Sleep -Seconds 1
                    Save-Element $window $path (Get-Parent (Find-ByText $window 'Command palette')) 12
                }
                finally {
                    if (Find-ByText $window 'Command palette' -Optional) { Send-Keys $window 'Ctrl+Shift+P' }
                }
                Wait-Until { -not (Find-ByText $window 'Command palette' -Optional) } 'the command palette to close' 20
            }

            # The notes go in the project's pad, which every tab of it shares.
            Shot 'conversation/scratch-pad' {
                param($path)
                Show-SidePage 'Grappling hook' 'ScratchPad' 'Scratch pad'
                Set-ElementText (Find-ByType $window 'Edit' 'Scratch pad') "- Let go when the player jumps, or past Range`n- Try AirControl 0.6 with a gamepad: does the swing feel right?`n- Create IA_Grapple in the editor and assign it to Grapple Action`n- Ask Claude about a cooldown"
                Start-Sleep -Seconds 2
                Save-Rect $window $path (Get-PanelRect -height 330 -Below) 0
            }

            # Last, as it takes the layout with it: the group's Files tab, with a changed file open to show Claude's marks.
            Shot 'project-files/files-tab' {
                param($path)
                # Without the tool windows the earlier pictures showed, which would take the editor's width.
                Hide-ToolWindows $window
                Invoke-ByText $window 'Project files'
                Start-Sleep -Seconds 3
                Invoke-ByText $window 'Find a file by name'
                Wait-Until { Find-ById $window 'FindBox' } 'the file finder' 20
                Set-ElementText (Find-ById $window 'FindBox') 'StarfallCharacter.cpp'
                Start-Sleep -Seconds 2
                # A click on what it found opens it, as Enter does; a posted Enter can miss the box once Claudette has
                # come to the front.
                Send-Click (Find-ByText $window 'StarfallCharacter.cpp, in Source\Starfall\Player')
                Wait-Until { Find-ByText $window 'Close the file' -Optional } 'the file to open' 20
                Start-Sleep -Seconds 3
                Save-Window $window $path
            }

            # Starfall's README with its preview beside it (DESIGN.md §18, "Markdown preview").
            Shot 'project-files/markdown-preview' {
                param($path)
                Hide-ToolWindows $window
                Invoke-ByText $window 'Project files'
                Start-Sleep -Seconds 3
                Invoke-ByText $window 'Find a file by name'
                Wait-Until { Find-ById $window 'FindBox' } 'the file finder' 20
                Set-ElementText (Find-ById $window 'FindBox') 'README.md'
                Start-Sleep -Seconds 2
                Send-Click (Find-ByText $window 'README.md')
                Wait-Until { Find-ByText $window 'Edit and preview' -Optional } 'the README to open' 20
                # A mode is a radio button, which has no Invoke for Invoke-ByText to find.
                Send-Click (Find-ByText $window 'Edit and preview')
                Start-Sleep -Seconds 3
                Save-Window $window $path
            }
        }
    }

    # The Unreal editor open on starfall, for its log under the tab: a copy of claudette-demo named UnrealEditor stands in
    # for it, writing the project's log, so Claudette sees an editor with the project on its command line.
    if (Test-Wanted 'project-tools/editor-log') {
        Use-Claudette @{} {
            $standIn = Join-Path (Split-Path $build.Demo) 'UnrealEditor.exe'
            Copy-Item $build.Demo $standIn -Force
            $editor = Start-Process $standIn -ArgumentList '--stand-in-editor', ('"' + (Join-Path $Projects 'starfall\Starfall.uproject') + '"') -WindowStyle Hidden -PassThru
            try {
                Shot 'project-tools/editor-log' {
                    param($path)
                    Invoke-ByText $window 'Grappling hook'
                    Wait-Until { Find-ByText $window 'Starfall.log' -Optional } "the editor's log under the tab" 30
                    Invoke-ByText $window 'Starfall.log'
                    Wait-Until { Find-ByText $window 'Open file' -Optional } 'the log in the main area' 20
                    Start-Sleep -Seconds 2
                    Save-Window $window $path
                }
            }
            finally {
                Stop-Process -Id $editor.Id -Force -ErrorAction SilentlyContinue
                $editor.WaitForExit(10000) | Out-Null
                Remove-Item $standIn -Force -ErrorAction SilentlyContinue
            }
        }
    }

    # Sending prompts, which stay in the conversations: each in its own tab.
    if (Test-Wanted 'usage/limit', 'conversation/turn-end', 'permissions/permission-card', 'tabs/processes') {
        Use-Claudette @{} {
            Show-Tabs

            # First, as Claudette says "Another tab needs you" over the composer once one does, which a limit shows beside.
            Shot 'usage/limit' {
                param($path)
                Invoke-ByText $window 'UI on the Input System'
                Start-Sleep -Seconds 2
                Send-Message $window 'LIMIT 30'
                Wait-Until { Find-ByText $window 'Play a game while you wait' -Optional } 'the limit' 30
                Start-Sleep -Seconds 2
                Save-Elements $window $path ((Find-ByText $window 'LIMIT 30'), (Find-ById $window 'ComposerPanel')) @(20, 40, 12, 4)
            }

            # A turn that changed files ends with the Modified files card and the Turn card under it: an Edit on two files.
            Shot 'conversation/turn-end' {
                param($path)
                Invoke-ByText $window 'Grappling hook'
                Start-Sleep -Seconds 2
                $source = Join-Path $Projects 'starfall\Source\Starfall'
                Send-Message $window ('EDIT_FILE ' + (Join-Path $source 'Player\StarfallCharacter.h') + ' | ' + (Join-Path $source 'Abilities\GrappleComponent.cpp'))
                Wait-Until { Find-ByPrefix $window 'What this turn changed in' } 'the Modified files card' 30
                Wait-Until { Find-ByPrefix $window '5.2k in' } 'the Turn card' 30
                Start-Sleep -Seconds 2
                $tiles = Get-Parent (Find-ByPrefix $window 'What this turn changed in')
                Save-Elements $window $path @($tiles, (Find-ByPrefix $window '5.2k in')) @(19, 36, 12, 8)
            }

            # The card's edge isn't a control's: its text and buttons, with room around them.
            Shot 'permissions/permission-card' {
                param($path)
                Invoke-ByText $window 'Gamepad rumble'
                Start-Sleep -Seconds 2
                Send-Message $window 'ASK_PERMISSION'
                Wait-Until { Find-ByText $window 'Allow this command?' -Optional } 'the permission card' 30
                Start-Sleep -Seconds 1
                Save-Elements $window $path ((Find-ByText $window 'Allow this command?'), (Find-ByText $window 'Allow'), (Find-ByPrefix $window 'Deny with a message')) @(19, 32, 19, 10)
            }

            # The process monitor is off by default: turn it on in Settings, then start a process that outlasts the turn.
            Shot 'tabs/processes' {
                param($path)
                Invoke-ByText $window 'Inventory loses items on save'
                Start-Sleep -Seconds 2
                $settings = Open-Settings
                # Found by search, which scrolls Settings to it on the Tabs page.
                Set-ElementText (Find-ById $settings 'SearchBox') 'process monitor'
                Wait-Until { Find-ByType $settings 'ListItem' 'Show the process monitor' } 'the search result' 10
                Select-ListItem $settings 'Show the process monitor'
                Start-Sleep -Seconds 2
                Set-Toggle $settings 'Show the process monitor' $true
                Close-Window $settings
                Start-Sleep -Seconds 1
                Send-Message $window 'SPAWN 600'
                Show-ToolWindow 'Processes' 'Processes'
                Wait-Until { Find-ByText $window '--child 600' -Optional } 'the spawned process' 60
                Start-Sleep -Seconds 3
                Save-Rect $window $path (Get-PanelRect (Find-ByText $window '--child 600') -Below) 0
            }
        }
    }
}

if ($failed) { Write-Warning "Not taken: $($failed -join ', ')." }
Write-Host "Saved to $Out"
