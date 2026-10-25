---
title: Installing
parent: Getting started
nav_order: 2
summary: "Install Claude Code first, then Claudette."
---

# Installing

Install [Claude Code](https://code.claude.com/docs/en/setup) first, then Claudette. Run `claude --version` in a terminal to check that Claude Code works.

The [Downloads page](../downloads.md) lists the packages for the latest release, and says how to tell which one your computer needs.

## On Windows

Claudette comes as an MSIX package, one for x64 processors and one for Arm64.

1. Download the `.msix` for your processor from the [Downloads page](../downloads.md).
2. Open the file. Windows' App Installer shows the package.
3. Choose **Install**.
4. Start Claudette from the Start menu.

Windows installs only signed packages, so use the files from the Downloads page, not a build you made yourself.

{: .note }
Windows normally gives a packaged app a private copy of the files it writes under your profile. Claudette turns that off. Claude Code and the tools it runs read and write the same files as the `claude` in your terminal, so your settings, sessions and sign-in are shared between the two.

## On macOS

Claudette comes as a `.dmg`, one for Apple silicon and one for Intel. The app is signed and notarized.

1. Download the `.dmg` for your Mac from the [Downloads page](../downloads.md).
2. Open it. It holds `Claudette.app` and a link to your Applications folder.
3. Drag `Claudette.app` onto the Applications link.
4. Eject the disk image and open Claudette from Applications.

Open it from Applications, not from the disk image or from Downloads. macOS runs an app opened from Downloads from a temporary, read-only copy, and that copy can't update itself. If Claudette finds itself running from one, it asks you to move it to Applications.

### Privacy prompts

Claude Code runs as Claudette's child, so when Claude reads a project in Documents, Desktop, Downloads, a removable volume or a network volume, macOS asks whether Claudette may use it. Allow it, or Claude can't read that project. You can change your answer later in System Settings → Privacy & Security.

## On Linux

There is no package yet, so you build Claudette from its source, and it keeps itself current from there. Every change to Claudette is built and tested on Linux, as on Windows and macOS. [Installing on Linux](installing-on-linux.md) takes you through it: Claude Code, the source and .NET, then a launcher in your app menu.

## Upgrading from 0.1.0

0.1.0 came from before Claudette's repository moved. It announces later versions but can't install them, so install the new version by hand. On Windows the new MSIX installs beside 0.1.0's, so both stay installed until you remove 0.1.0.

## Next

[The first launch](the-first-launch.md) shows what Claudette does when it starts. Later versions arrive through Claudette's own updates: see [Claudette's updates](../updates/claudettes-updates.md).
