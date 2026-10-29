---
title: Windows, macOS and Linux
parent: Reference
nav_order: 6
summary: "Claudette runs on all three, and almost everything works the same."
---

# Windows, macOS and Linux

Claudette runs on all three, and almost everything works the same. This page collects what differs, with a link to the page that explains each part.

## What you need

- **Windows**: an MSIX package for x64 or Arm64.
- **macOS**: a `.dmg` for Apple silicon or Intel, on macOS 12 or later.
- **Linux**: a desktop on x64, with X11 or Wayland. There's no package yet, so you build Claudette from source, in a few commands. Every change to Claudette is built and tested on Linux, as on Windows and macOS.
- **Everywhere**: [Claude Code](https://code.claude.com/docs), installed first.

See [What you need](../getting-started/what-you-need.md), [Installing](../getting-started/installing.md), [Installing on Linux](../getting-started/installing-on-linux.md) and [Running from source](../getting-started/running-from-source.md).

## Installing and updating

- **Windows**: the MSIX installs like any app, and Claudette can install its own updates. A copy that isn't the MSIX can't.
- **macOS**: drag `Claudette.app` from the `.dmg` to Applications. macOS runs an app opened straight from Downloads from a temporary read-only copy, which can't update itself, so move it to Applications first.
- **Linux**: a build from source keeps up with the branch its checkout tracks instead of releases: it offers to pull, build and restart into new commits, and to restart into each build you make yourself. See [Installing on Linux](../getting-started/installing-on-linux.md#keeping-it-current). A copy that isn't a package and isn't run from its checkout can't install updates, but still hears about each release, with a link to its page.

Updating restarts Claudette with every tab as it was. See [Claudette's updates](../updates/claudettes-updates.md) and [When an update fails](../updates/when-an-update-fails.md).

## Look and feel

- **Windows**: the header is the window's title bar, as in JetBrains Rider, with minimize, maximize and close at its right; drag the header's gaps to move the window. Claudette's other windows have their own title bars too, the same way. Settings → Appearance → **Use the system's title bar** puts Windows' own title bar over each window instead.
- **macOS**: the header is the window's title bar too, with the traffic lights at its left (or, with **Use the system's title bar**, the native title bar over it), and a menu bar with **File → New Tab**, **Open Recent**, **History…** and **Close Tab**, **Help → User Guide**, **Ask on Discord** and **Report an Issue**, and **Settings…** in the app menu. Each command with a shortcut shows it, from Settings → Keyboard.
- **Linux** draws Claudette's window through X11, and through XWayland on a Wayland desktop, with your window manager's title bar over the header, always: a window manager may not draw the buttons of a window that takes its title bar. Its menu entry is a desktop entry you add ([Installing on Linux](../getting-started/installing-on-linux.md#add-it-to-your-app-menu)).
- In the Claude style, Claude's replies are set in a serif: Charter on macOS, Georgia or Cambria on Windows, and Charter, Noto Serif or DejaVu Serif on Linux.

See [Styles and themes](../settings/styles-and-themes.md).

## Shortcuts

The command key is `Ctrl` on Windows and Linux and `Cmd` on macOS. A shortcut you set on one machine means the same on the other when settings sync. Zoom, motion and the rest of the window work the same everywhere. See [Keyboard shortcuts](keyboard-shortcuts.md).

## Opening folders

- **Windows**: the taskbar's jump list lists your favorite and recent folders. It stays empty if you turned off **Show recently opened items** in Windows' Start settings.
- **macOS**: **File → Open Recent** and the Dock icon's menu list them.
- **Everywhere**: drag a folder onto the sidebar, or start Claudette with `--folder`. See [Opening tabs](../tabs/opening-tabs.md) and [Command line and environment](command-line-and-environment.md).

The project menu's **Show in Explorer** is **Show in Finder** on macOS and **Show in file manager** on Linux.

## Notifications and the icon

- **Windows** shows toasts. The taskbar button's overlay shows the number of tabs needing input, Claude's spark while tabs work and an hourglass while a tab waits for a usage limit, and the button flashes when a tab starts needing you while Claudette isn't in front.
- **macOS** uses its notification center, which needs `Claudette.app`: a build run with `dotnet run` has no notifications, and Settings says so. The Dock icon has the number as a badge, and Claudette on it types, waves or waits by an hourglass.
- **Linux** uses `notify-send`. Without it there are no notifications, and Settings says so. The badge and the icon's animation are for the Windows taskbar and the macOS Dock.

See [Notifications](../away/notifications.md) and [The Dock and taskbar icon](../away/the-dock-and-taskbar-icon.md).

## Keeping the computer awake

[Keeping the computer awake](../away/keeping-the-computer-awake.md) uses the OS's own call on Windows, `caffeinate` on macOS, and `systemd-inhibit` on Linux. Without `systemd-inhibit`, the header's cup and the setting are disabled and say why.

## Starting at login

The entry that **Start Claudette when I log in** makes is different on each system, and can be turned off outside Claudette:

- **Windows**: the MSIX's startup task, or a `Run` entry for other builds. Task Manager's Startup apps can turn either off.
- **macOS**: a LaunchAgent. **Allow in the Background**, in System Settings → General → Login Items, can turn it off.
- **Linux**: a `claudette.desktop` file in your autostart folder.

See [Starting at login](../away/starting-at-login.md).

## Passwords and keys

Perforce passwords and API keys go in the OS's credential store: Windows Credential Manager, the macOS Keychain, or the Secret Service on Linux. The Secret Service needs `secret-tool`, from the `libsecret-tools` package. Without it, Settings says so, and passwords can only be asked for. See [Perforce](../project-tools/perforce.md) and [Passwords and keys](../privacy/passwords-and-keys.md).

## Your environment

Windows apps get your full environment. On macOS and Linux, an app started from the Dock, Finder or a desktop launcher gets only a minimal one, so Claudette reads your login shell's environment, as VS Code does, and gives it to `claude` and the other programs it starts. See [Command line and environment](command-line-and-environment.md#what-a-login-shell-sees) and [Claude can't find a program](../troubleshooting/claude-cant-find-a-program.md).

## Smaller differences

- **CPU on the Processes page.** On Windows, 100% means all cores, as in Task Manager. On macOS, 100% means one core, as in Activity Monitor. See [Processes](../tabs/processes.md).
- **Your own actions** run through `cmd.exe` on Windows and your `$SHELL` elsewhere. See [Actions and links](../project-tools/actions-and-links.md).
- **Project tools** look for engines in each system's places: Epic's launcher and registry on Windows, its support folder on macOS and none on Linux, Unity Hub's folders, and Scoop and WinGet for Godot on Windows. **Project files for** starts as Visual Studio on Windows, Xcode on macOS and VS Code on Linux. See [Unreal Engine](../project-tools/unreal-engine.md), [Unity](../project-tools/unity.md) and [Godot](../project-tools/godot.md).
- **Pasted screenshots.** Windows' Snipping Tool and `Win+Shift+S`, macOS screenshots and PNG or JPEG pictures on Linux's clipboard are all attached. See [Attachments and large pastes](../composer/attachments-and-large-pastes.md).
- **Reduced motion** follows Windows' *Show animations in Windows*, macOS's *Reduce motion* and GNOME's `enable-animations`. See [Accessibility](../settings/accessibility.md).

## What hasn't been tried on a real machine

Some parts have so far been checked only by automatic tests, not on a real machine of each kind. If one misbehaves, tell us with [Reporting an issue](../troubleshooting/reporting-an-issue.md).

- Notifications and the Dock or taskbar badge, on Windows and macOS.
- Starting at login: the MSIX's startup task and the macOS LaunchAgent.
- Installing Claudette's updates on Windows and macOS.
- The login shell's environment in a Claudette started from the Dock or a desktop launcher.
- Keeping the computer awake, on all three.
- Perforce against a real server, with P4V and single sign-on, and the credential stores in the running app.
- Unreal Engine, Unity and Godot projects on machines with the engines installed.
