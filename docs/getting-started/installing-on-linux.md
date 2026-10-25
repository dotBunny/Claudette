---
title: Installing on Linux
parent: Getting started
nav_order: 3
summary: "There's no package for Linux yet, so you build Claudette from its source: a few commands, then a launcher in your app menu."
---

# Installing on Linux

There's no package for Linux yet, so you build Claudette from its source. It takes a few commands and a few minutes. From then on Claudette keeps itself current: when there are new changes, it pulls them, builds them and restarts into them, with your tabs as they were.

Linux isn't an afterthought. Every change to Claudette is built and tested on Linux, as on Windows and macOS, and Linux is where the tests drive the real Claude Code.

## What you need

- **A Linux desktop on x64.** Every change is tested on x64. Arm64 builds the same way, but hasn't been tried yet.
- **X11 or Wayland.** Claudette draws its window through X11. On a Wayland desktop, such as GNOME or KDE Plasma, it runs through XWayland, which they include. It also needs the X11 and font libraries every desktop has: libX11, libICE, libSM and fontconfig.
- **git and curl**, to get the source and the installers.
- **Claude Code** and **the .NET SDK**. The steps below install them.

A few features use programs a desktop may not have: `notify-send` for notifications, `secret-tool` for Perforce passwords and API keys, and `systemd-inhibit`, which comes with systemd, for keeping the computer awake. Claudette runs without them and says what's missing ([Something missing on Linux](../troubleshooting/something-missing-on-linux.md)). Install them with the rest:

| Distribution | Command |
|---|---|
| Debian, Ubuntu and the like | `sudo apt install git curl libnotify-bin libsecret-tools` |
| Fedora | `sudo dnf install git curl libnotify libsecret` |
| Arch | `sudo pacman -S git curl libnotify libsecret` |

## Install Claude Code

```sh
curl -fsSL https://claude.ai/install.sh | bash
claude --version
```

That's Claude Code's own installer, which puts `claude` in `~/.local/bin`. Any way its [setup page](https://code.claude.com/docs/en/setup) describes works too. You can also skip this step: when Claudette doesn't find Claude Code, its first screen offers **Install Claude Code**, which runs the same installer ([The first launch](the-first-launch.md)).

## Get the source

```sh
git clone https://github.com/dotBunny/Claudette.git ~/Claudette
cd ~/Claudette
```

The rest of this page uses `~/Claudette`. Anywhere else works: change the paths to match.

This is `main`, which is built and tested with every change, and which Claudette keeps up with ([below](#keeping-it-current)). To stay on a release instead, check out its tag, such as `git checkout v0.6.0`. Claudette doesn't look for new commits in a checkout that isn't on a branch, so you move it on yourself.

## Install the .NET SDK

Claudette builds with the .NET SDK version that the checkout's `global.json` names. Microsoft's install script reads that file, so run it in the checkout:

```sh
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --jsonfile global.json --install-dir ~/.dotnet
```

It installs .NET in `~/.dotnet`, for you alone, without `sudo`. Then add it to your shell, at the end of `~/.bashrc` (or your shell's own startup file):

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

Open a new terminal, and `dotnet --version` in the checkout prints the SDK's version.

{: .note }
Your distribution's own .NET package is usually an earlier SDK, such as 10.0.1xx, which `global.json` doesn't accept: `dotnet` says it can't find a compatible SDK. The script's .NET sits beside it, and comes first on your `PATH`.

## Build and run it

```sh
dotnet run --project src/Claudette.App -c Release
```

The first build fetches Claudette's packages and takes a few minutes. `-c Release` builds it optimized, as a release is.

Claudette opens, and `dotnet run` returns straight away: a build from source runs from a copy of itself in Claudette's data folder, so you can build again while it runs ([Running from a copy](running-from-source.md#running-from-a-copy)). [The first launch](the-first-launch.md) and [Signing in](signing-in.md) take it from there.

## Add it to your app menu

`dotnet run` needs a terminal. To start Claudette from your desktop's app menu, as any other app, give it a launcher, a desktop entry:

```sh
mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/claudette.desktop <<EOF
[Desktop Entry]
Type=Application
Name=Claudette
Comment=Claude Code in a desktop window
Exec=$HOME/.dotnet/dotnet $HOME/Claudette/src/Claudette.App/bin/Release/net10.0/Claudette.dll
Icon=$HOME/Claudette/packaging/icon/claudette-1024.png
Categories=Development;
StartupWMClass=Claudette
Terminal=false
EOF
```

The shell fills in `$HOME` as it writes the file. **Claudette** is now in your app menu, with its icon, and its window is grouped under the launcher in your dock or taskbar.

- The launcher starts the build in the checkout with the .NET you installed above, which a launcher can't find by itself in `~/.dotnet`. If your .NET is somewhere else, use its `dotnet`.
- The build starts a copy of itself, as with `dotnet run`, so the launcher always starts the newest build you've made.
- `net10.0` in the path is the .NET version Claudette is built for. When Claudette moves to a newer one, the build's folder changes with it, and so does this line.
- Started from the menu, Claudette reads your login shell's environment, so `claude`, `git`, `dotnet` and the rest are found as in a terminal ([Claude can't find a program](../troubleshooting/claude-cant-find-a-program.md)).

To start Claudette when you log in, turn on Settings → General → **Start Claudette when I log in**. It adds `claudette.desktop` to `~/.config/autostart`, which starts this build the same way, with the .NET it runs on. See [Starting at login](../away/starting-at-login.md).

## Keeping it current

Claudette checks the branch your checkout tracks, `origin/main`, at launch and every hour, with your own git. When there are new commits, *3 new commits on origin/main* appears at the foot of the sidebar. **Pull, build and restart** pulls them, builds Claudette in the configuration you built (Release, above) and restarts into the new build, keeping every tab, its session and the message you were typing.

It only ever fast-forwards, so a checkout with changes of your own is never thrown away: the card says why it won't pull instead. To update by hand, `git pull` and `dotnet build src/Claudette.App -c Release`, then **New build ready** offers the restart. [Running from source](running-from-source.md#when-there-are-new-commits) has the details.

## Everything else

Claudette works on Linux as on Windows and macOS. Shortcuts use `Ctrl`, as on Windows. Settings are in `~/.config/claudette` and everything else Claudette keeps in `~/.local/share/claudette` ([Where Claudette keeps things](../privacy/where-claudette-keeps-things.md)). [Windows, macOS and Linux](../reference/windows-macos-and-linux.md) lists the few differences.

## If it doesn't start

- *You must install .NET to run this application*: something started Claudette's own program, `Claudette`, which looks for .NET only where `DOTNET_ROOT` says, else where `/etc/dotnet/install_location` says, else in `/usr/share/dotnet`. Start it with `dotnet`, as [the launcher](#add-it-to-your-app-menu) does, or register your .NET for every program: `sudo mkdir -p /etc/dotnet` and `echo "$HOME/.dotnet" | sudo tee /etc/dotnet/install_location`.
- *XOpenDisplay failed*: there's no desktop to draw on, as over SSH. Start it from your desktop session.
- **An error naming `libICE`, `libSM` or `libfontconfig`**: the system lacks a desktop's usual libraries. On Debian and Ubuntu, `sudo apt install libice6 libsm6 libfontconfig1`.
- **`dotnet` can't find a compatible SDK**: install the one `global.json` names, as in [Install the .NET SDK](#install-the-net-sdk).
- **Claude can't run `node`, `dotnet` or another program** in a tab started from the menu: see [Claude can't find a program](../troubleshooting/claude-cant-find-a-program.md).

Anything else, ask on [dotBunny's Discord](../discord.md) or see [Reporting an issue](../troubleshooting/reporting-an-issue.md).

## See also

- [Running from source](running-from-source.md), for how a build from source runs and restarts, and the demo you can try it on without an account.
- [Something missing on Linux](../troubleshooting/something-missing-on-linux.md)
- [Windows, macOS and Linux](../reference/windows-macos-and-linux.md)
