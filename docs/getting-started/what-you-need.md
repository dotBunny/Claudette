---
title: What you need
parent: Getting started
nav_order: 1
summary: "Claudette runs Claude Code in every tab, so Claude Code has to be installed first."
---

# What you need

Claudette runs Claude Code in every tab, so Claude Code has to be installed first. Beyond that you need a computer it runs on and an account to sign in with.

## A computer

| System | What works | Notes |
|---|---|---|
| Windows | Windows 10 (version 2004 or later) and Windows 11, on x64 and Arm64 processors | Installed from an MSIX package. |
| macOS | macOS 12 or later, on Apple silicon and Intel | Installed from a `.dmg`. |
| Linux | A desktop on x64, with X11 or Wayland, built from source | Every change to Claudette is built and tested on Linux, as on Windows and macOS. There's no package yet: [Installing on Linux](installing-on-linux.md) builds it in a few commands. |

You don't need to install .NET to use a package from the [Downloads page](../downloads.md). Running from source does need it: see [Installing on Linux](installing-on-linux.md) or [Running from source](running-from-source.md).

A few features need extra programs on Linux, such as `notify-send` for notifications. [Something missing on Linux](../troubleshooting/something-missing-on-linux.md) lists them.

## Claude Code

Install [Claude Code](https://code.claude.com/docs/en/setup) before Claudette, any way its docs describe: the native installer, npm, Homebrew, WinGet or your Linux package manager.

- Claudette finds `claude` on your `PATH`. On macOS and Linux it reads the `PATH` your login shell sets up, so a Claudette started from the Dock still finds Homebrew's tools. You can also point it at a `claude` somewhere else in Settings → Claude Code → **Path to claude**.
- It needs Claude Code {{ site.claude_code.minimum }} or later. It was last tested with {{ site.claude_code.tested }}, and newer versions are allowed.
- On Windows, Claude Code itself needs Git for Windows to run commands. If it's missing, the tab tells you.
- On Windows, a Claude Code installed with npm that only provides `claude.cmd` can't be started by Claudette yet. Install the native build, or point Claudette at `claude.exe`.
- The Claude desktop app isn't Claude Code. It keeps a copy of Claude Code for its own use, which Claudette doesn't run, so install Claude Code too.

If Claude Code is missing or too old, Claudette says so on its first screen, and offers to install or update it: see [The first launch](the-first-launch.md).

## An account

Claude Code has to be signed in. Use a Claude subscription (Pro, Max, Team or Enterprise) or an Anthropic Console account billed for API usage. If you aren't signed in yet, Claudette signs you in from its own screen: see [Signing in](signing-in.md).

The usage meters in the header show a subscription plan's limits. An account billed by the API has no plan limits to show.

## Next

[Installing](installing.md) covers the packages, and [Installing on Linux](installing-on-linux.md) building it on Linux.
