---
title: The first launch
parent: Getting started
nav_order: 4
summary: "When Claudette starts it shows Looking for Claude Code… while it finds claude and asks it for its version."
---

# The first launch

When Claudette starts it shows *Looking for Claude Code…* while it finds `claude` and asks it for its version. On macOS and Linux it also reads your login shell's environment first, which can take a few seconds. If Claude Code is there and recent enough, you go on to [signing in](signing-in.md) when you need to, and then the window.

## The setup screen

If Claude Code can't be used, Claudette shows a setup screen instead, with what it found and what to do:

| Title | What happened |
|---|---|
| **Claude Code isn't installed** | Claudette found no `claude` on your `PATH` or where Claude Code installs to, or no file at the path you gave it, or that path is the Claude desktop app's. |
| **Claude Code needs an update** | Claude Code is installed but older than the minimum version. The screen says which version it found. |
| **Claude Code didn't respond** | `claude --version` failed, timed out or printed something Claudette couldn't read as a version. |
| **This Claude Code install isn't supported yet** | On Windows, Claude Code was installed with npm and only provides `claude.cmd`, which Claudette can't start. |

The buttons under the explanation:

- **Update now** runs the update command for the way Claude Code was installed, and shows its output. It appears only when Claude Code is too old. When the update works, Claudette checks again.
- **Install Claude Code** runs Claude Code's own installer, and shows its output. It appears when Claude Code isn't installed: see [Installing Claude Code from the setup screen](#installing-claude-code-from-the-setup-screen).
- **Check again** looks for `claude` again, after you've installed or fixed it.
- **Choose claude…** opens a file picker for the `claude` program. Claudette remembers your choice in Settings → Claude Code → **Path to claude**.
- **Install instructions** opens Claude Code's [setup page](https://code.claude.com/docs/en/setup).

Claudette needs Claude Code {{ site.claude_code.minimum }} or later. It was last tested with {{ site.claude_code.tested }}. A newer version is allowed, and an older one in between works too, with a quiet note in Settings. [Claude Code missing or too old](../troubleshooting/claude-code-missing-or-too-old.md) has the fixes for each case.

## Installing Claude Code from the setup screen

When Claude Code isn't installed, the setup screen shows the command that Claude Code's [setup page](https://code.claude.com/docs/en/setup) gives for your system, and **Install Claude Code** runs it:

| System | The command |
|---|---|
| Windows | `irm https://claude.ai/install.ps1 \| iex`, in PowerShell |
| macOS and Linux | `curl -fsSL https://claude.ai/install.sh \| bash` |

It runs on the setup screen rather than in a terminal, and shows what the installer prints. The installer prints nothing while it downloads Claude Code, which can take a few minutes. Claude Code goes in your home folder, so nothing asks for an administrator's password. When it's done, Claudette looks for Claude Code again and carries on to [signing in](signing-in.md). If it fails, the screen says why, and [Claude Code missing or too old](../troubleshooting/claude-code-missing-or-too-old.md) has what to try.

### The Claude desktop app isn't Claude Code

The Claude desktop app keeps a copy of Claude Code for its own use, but Claudette doesn't use it: the app replaces that copy whenever it updates, and doesn't make a `claude` you can run. If you have the desktop app and not Claude Code, the setup screen says so above **Install Claude Code**. The two live side by side: installing Claude Code doesn't change the desktop app.

## Where Claudette looks

- Your `PATH`. On macOS and Linux that is the `PATH` your login shell builds, unless you started Claudette from a terminal.
- The folders Claude Code installs to, such as `~/.local/bin` for its own installer. Claudette looks there after your `PATH`, so it finds a Claude Code installed while it's open.
- The path in Settings → Claude Code → **Path to claude**, when you've set one. Leave it empty to find Claude Code automatically. A change there takes effect the next time Claudette starts. If the path stops working and Claudette then finds Claude Code by looking, from **Check again** or **Install Claude Code**, it clears the path.

Claudette never runs the Claude desktop app's files, even when **Path to claude** points at one.

## After it's found

With Claude Code found, Claudette checks whether it's signed in. If it isn't, you see the [sign-in screen](signing-in.md). Once you're signed in, the window opens with no tabs on a first launch, and [Your first tab](your-first-tab.md) takes it from there.

On later launches, tabs you pinned come back where they left off. Unpinned tabs come back too when Settings → General → **Also restore unpinned tabs when Claudette starts** is on. [Closing and restoring](../tabs/closing-and-restoring.md) has the details.
