<#
.SYNOPSIS
Takes the README's screenshots (Windows): the demo from claudette-demo in each style and theme, with the main window,
the diff view, the thread's Tasks page and agent map, the detailed usage header, History and the Project Files tab of
each.

.DESCRIPTION
For Standard and Claude, dark and light: makes the demo content afresh, starts the Debug build of Claudette on it
(fake-claude as Claude Code, so no account and no tokens), visits every tab so each shows its model and any reviewed
icon, opens the Grappling hook tab's Changed files, and captures the window as <style>-<theme>.png, and Claudette standing on
the composer, close up, as claudette-<style>-<theme>.png (the demo runs with motion reduced, so she stands still at the
composer's right rather than being caught part-way through falling off it). Then it:
- opens the diff of StarfallCharacter.cpp from an Edit's tile and captures that window as diff-<style>-<theme>.png;
- selects the Move to the Input System thread, shows its Tasks tool window and captures the window as
  tasks-<style>-<theme>.png, then on Agents, with the first subagent selected, as agents-<style>-<theme>.png;
- expands the usage header and captures the top of the window as header-<style>-<theme>.png;
- collapses it again, selects Pool particle emitters, which runs on the Studio API key, and captures the window, with
  the key's spend in the header and the account's tags, as accounts-<style>-<theme>.png;
- opens Settings on Appearance, with its preview, and captures that window as settings-<style>-<theme>.png;
- selects Grappling hook, opens History, picks starfall's chip and captures the window as history-<style>-<theme>.png;
- closes History, hides the tool windows, opens starfall's Project Files tab, opens StarfallCharacter.cpp from its explorer and captures the
  window, with Claude's changes marked in the explorer and the editor's gutter, as files-<style>-<theme>.png;
and closes Claudette by its window, as a user would. Last, it has MascotPictureTests draw Claudette on the composer's
animation from her art and steps, as claudette.gif, and social-card.ps1 draw the site's social card with the new
standard-dark.png.

A run leaves you the focus, the keyboard and the pointer, so you can carry on working meanwhile, a fullscreen app
included (DESIGN.md §9, "Taking screenshots without the focus"). Claudette runs with CLAUDETTE_UNFOCUSED=1, its windows
behind yours and never taking the focus, and is driven through its command channel (CLAUDETTE_AUTOMATION_PIPE): its
controls are found through UI Automation, and clicked, typed in and so on by Claudette itself. Captures go through
PrintWindow, which doesn't need the windows in view.

.PARAMETER Projects
Where the demo's git repositories go. The conversation shows their paths, so choose a short one that says nothing
about you, such as C:\Demo. It must be empty, or one claudette-demo made.

.EXAMPLE
tools\Claudette.Demo\screenshots.ps1 -Projects C:\Demo
#>
param(
    [Parameter(Mandatory)] [string] $Projects,
    [string] $Out = (Join-Path $PSScriptRoot '..\..\docs\screenshots'),
    [string] $Work = (Join-Path ([IO.Path]::GetTempPath()) 'claudette-demo'),
    # How long Claudette gets to start and restore its tabs before the first click.
    [int] $SettleSeconds = 10
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path

Import-Module (Join-Path $PSScriptRoot 'Screenshots.psm1') -Force -DisableNameChecking
$build = Build-Claudette $repo
$app = $build.App
$demo = $build.Demo

foreach ($style in 'standard', 'claude') {
    foreach ($theme in 'dark', 'light') {
        Write-Host "$style $theme"
        New-DemoContent $demo $Work $Projects $style $theme
        $claudette = Start-Claudette $app $Work
        try {
            $window = $claudette.MainWindowHandle
            Start-Sleep -Seconds $SettleSeconds

            # A tab loads its session when it's first shown: visit each, then come back to the first.
            Set-Step 'visiting the tabs'
            $tabs = 'Stamina drains twice on clients', 'Inventory loses items on save', 'Move to the Input System', 'UI on the Input System',
                'Gamepad rumble', 'Pool particle emitters', 'Grappling hook', 'Files (5)'
            foreach ($text in $tabs) {
                Invoke-ByText $window $text
                Start-Sleep -Seconds 2
            }
            Set-Step 'the window'
            Save-Window $window (Join-Path $Out "$style-$theme.png")

            # Claudette on the composer, close up: with motion reduced she stands still at its right, over Send.
            $send = (Find-ByText $window 'Send').Current.BoundingRectangle
            Save-Rect $window (Join-Path $Out "claudette-$style-$theme.png") $send @((616 - $send.Width / [WindowShot]::Scale($window)), 150, 24, 34)

            # An Edit's tile opens its detail, which has Open in the diff view.
            Set-Step 'the diff view'
            # The last of its tiles, which is in view: the first can be scrolled up out of it.
            Send-Click (Find-LowestByText $window ('Edit ' + (Join-Path $Projects 'starfall\Source\Starfall\Player\StarfallCharacter.cpp')))
            Wait-Until { Find-ByText $window 'Open in the diff view' -Optional } "the Edit tile's detail" 20
            Invoke-ByText $window 'Open in the diff view'
            $diff = Get-OtherWindow $claudette $window 'the diff view'
            [WindowShot]::Resize($diff, 1000, 470)
            Start-Sleep -Seconds 2
            Save-Window $diff (Join-Path $Out "diff-$style-$theme.png")
            # Closed, so the next window of Claudette's found is Settings.
            Close-Window $diff
            Start-Sleep -Seconds 1

            # The thread: its plan and tasks, then its subagents with the first one's details.
            Set-Step 'the thread'
            Invoke-ByText $window 'Move to the Input System'
            Start-Sleep -Seconds 2
            # By their icons on the tool window strip (DESIGN.md §3, "Tool windows").
            Send-Click (Find-ById $window 'tool-Tasks')
            Start-Sleep -Seconds 2
            Save-Window $window (Join-Path $Out "tasks-$style-$theme.png")
            Send-Click (Find-ById $window 'tool-Agents')
            Start-Sleep -Seconds 1
            Select-TreeItem $window 'Agents' 1
            Start-Sleep -Seconds 2
            Save-Window $window (Join-Path $Out "agents-$style-$theme.png")

            # The detailed usage header, down to its edge: just above the sidebar's first button.
            Set-Step 'the usage header'
            Invoke-ByText $window 'Expand the usage header'
            Start-Sleep -Seconds 3
            $sidebarTop = (Find-ByText $window 'New tab').Current.BoundingRectangle.Top
            Save-Window $window (Join-Path $Out "header-$style-$theme.png") ([int] ($sidebarTop - 10 * [WindowShot]::Scale($window)))

            # A tab on the Studio API key: its spend in the header, and the account's tags on orbit-game's group and on the
            # starfall tab moved to it.
            Set-Step 'the API-key account'
            Invoke-ByText $window 'Collapse the usage header'
            Start-Sleep -Seconds 1
            Invoke-ByText $window 'Pool particle emitters'
            Wait-Until { Find-ByText $window 'Estimated API spend' -Optional } 'the spend header' 20
            Start-Sleep -Seconds 2
            Save-Window $window (Join-Path $Out "accounts-$style-$theme.png")

            # Settings on Appearance, whose preview shows the style and theme.
            Set-Step 'Settings'
            Invoke-ByText $window 'Settings'
            $settings = Get-OtherWindow $claudette $window 'the Settings window'
            Start-Sleep -Seconds 2
            Select-ListItem $settings 'Appearance'
            Start-Sleep -Seconds 2
            Save-Window $settings (Join-Path $Out "settings-$style-$theme.png")
            Close-Window $settings
            Start-Sleep -Seconds 1

            # History from a starfall tab, narrowed to starfall by its chip: its tabs' sessions and older ones, one in a worktree.
            Set-Step 'History'
            Invoke-ByText $window 'Grappling hook'
            Start-Sleep -Seconds 2
            Invoke-ByText $window 'History'
            Wait-Until { Find-ByPrefix $window 'starfall, ' } 'History to list starfall' 20
            Start-Sleep -Seconds 2
            Invoke-Element (Find-ByPrefix $window 'starfall, ') 'starfall'
            Start-Sleep -Seconds 2
            Save-Window $window (Join-Path $Out "history-$style-$theme.png")

            # Last, as it takes the layout with it: starfall's Files tab, with a changed file open to show Claude's marks.
            # History is over the window, so Esc closes it first. The file is opened by clicks down the explorer.
            Set-Step 'Project files'
            Send-WindowKey $window 'Esc'
            Wait-Until { -not (Find-ById $window 'SearchBox') } 'History to close' 20
            # Without the tool windows the earlier pictures showed, which would take the editor's width.
            Hide-ToolWindows $window
            Invoke-ByText $window 'Project files'
            Wait-Until { Find-ById $window 'TreeList' } 'the explorer' 20
            $explorer = Find-ById $window 'TreeList'
            $path = 'Source', 'Starfall', 'Player', 'StarfallCharacter.cpp'
            for ($i = 0; $i -lt $path.Count; $i++) {
                $name = $path[$i]
                Wait-Until { Find-ByText $explorer $name -Optional } "$name in the explorer" 20
                # A folder already open stays open: a click would close it.
                if ($i -eq $path.Count - 1 -or -not (Find-ByText $explorer $path[$i + 1] -Optional)) {
                    Send-Click (Find-ByText $explorer $name)
                    Start-Sleep -Milliseconds 500
                }
            }
            Wait-Until { Find-ByText $window 'Close the file' -Optional } 'the file to open' 20
            Start-Sleep -Seconds 3
            Save-Window $window (Join-Path $Out "files-$style-$theme.png")
        }
        finally {
            Stop-Claudette $claudette
        }
    }
}
# Claudette on the composer's animation, drawn from her art and steps rather than captured, so it's the same every time.
Write-Host "Claudette's animation"
$env:CLAUDETTE_MASCOT_GIF = Join-Path $Out 'claudette.gif'
try {
    & dotnet test (Join-Path $repo 'tests\Claudette.App.Tests\Claudette.App.Tests.csproj') --filter 'FullyQualifiedName~MascotPictureTests' -v q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Drawing her animation failed.' }
}
finally {
    Remove-Item Env:CLAUDETTE_MASCOT_GIF
}
# The site's social card shows the main window, so it's drawn again with the new one.
Write-Host 'The social card'
& (Join-Path $PSScriptRoot 'social-card.ps1')
Write-Host "Saved to $Out"
