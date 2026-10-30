---
title: How Claudette uses Claude Code
parent: Getting started
nav_order: 9
summary: "Claudette doesn't replace Claude Code and doesn't call Claude's API itself."
---

# How Claudette uses Claude Code

Claudette doesn't replace Claude Code and doesn't call Claude's API itself. Each tab runs the real `claude` program in the tab's folder and draws what it says as a window instead of a terminal. This page says what that means for your settings, your sessions and the programs you see running.

## Your configuration loads as in a terminal

A tab runs Claude Code in its streaming mode, which takes its messages as data. Claudette doesn't turn off any of your setup, so these load as they do in a terminal:

- your [`CLAUDE.md` files](https://code.claude.com/docs/en/memory) and [settings](https://code.claude.com/docs/en/settings);
- your [hooks](https://code.claude.com/docs/en/hooks) and [MCP servers](https://code.claude.com/docs/en/mcp);
- your [skills](https://code.claude.com/docs/en/skills) and [plugins](https://code.claude.com/docs/en/plugins);
- Claude Code's environment variables, which pass through to `claude`, `CLAUDE_CONFIG_DIR` among them.

Claudette keeps its own settings in its own folder, separate from Claude Code's, and doesn't try to manage Claude Code's. See [Where Claudette keeps things](../privacy/where-claudette-keeps-things.md).

## Where a tab differs from a terminal

- **A folder's own configuration.** A terminal `claude` asks before it works in a new folder. A tab doesn't get that question from Claude Code, so Claudette asks first, before Claude Code starts there: see [Folder trust](../permissions/folder-trust.md). A tab started without the folder's configuration leaves out the folder's settings, its `.mcp.json` servers and its `CLAUDE.md`.
- **Allow rules in a project's shared settings.** Claude Code holds these back in this mode until you've trusted the folder in Claude Code itself. Trusting a folder in Claudette doesn't change that.
- **Prompts.** Claude Code's permission questions arrive in Claudette, which shows them as cards: see [Permission cards](../permissions/permission-cards.md).
- **The session's name.** Claudette gives each session the tab's name in Claude Code, so other sessions can message it by name and `claude --resume <name>` finds it. Settings → Claude Code → **Name each tab's session in Claude Code after the tab** turns that off. See [Reaching other sessions](../threads/reaching-other-sessions.md).

## Where Claude Code keeps sessions

Claude Code writes each session to a file, a transcript, in the `projects` folder of its configuration folder: normally `.claude/projects` in your home folder, or the folder `CLAUDE_CONFIG_DIR` names. Claudette reads these files to show the earlier conversation when a tab comes back, and for [History](../history/history.md). Claude Code deletes transcripts after 30 days by default. A tab that syncs keeps a copy in the [session library](../history/the-session-library.md), which that cleanup doesn't touch.

## Starting Claudette from a terminal inside Claude Code

Claude Code sets variables for the programs it starts, to tell them they're inside a session. If Claudette inherited them, every tab would behave as a child session, and can report you as not signed in. So Claudette removes exactly those variables from the environment of each `claude` it starts, and leaves your own configuration alone. You can run Claudette from a Claude Code session without a problem.

## The programs you see running

Open Task Manager or Activity Monitor and you'll see a `claude` for each tab that has started, plus one more. That one is a hidden helper that never receives a prompt and saves no session. It asks Claude Code for what doesn't belong to a tab: the list of models, your plan's usage and sign-in. Claudette also runs `claude` briefly to check its version and how it was installed, and to update it.

Tabs you haven't touched since launch don't start `claude` until you select them or send a message, so many pinned tabs cost nothing until used. Closing a tab stops its `claude`, and what it started unless you choose to leave that running. Closing Claudette stops every tab's. See [Processes](../tabs/processes.md).

## What Claudette asks Claude Code for on its own

- After your first message in a new session, one small request for a title, which becomes the tab's name.
- A check on a turn that has gone quiet, if you leave check-ins on: see [Check-ins on long turns](../composer/check-ins-on-long-turns.md).

Both are small model calls that count toward your usage, and the tab's token counts include them. Claudette also asks Claude Code for your plan's usage from time to time, for the header.

## Versions

- Claudette needs Claude Code {{ site.claude_code.minimum }} or later and was last tested with {{ site.claude_code.tested }}. Newer versions are allowed.
- It decides what to offer from what Claude Code says it can do, not from its version number.
- When Claude Code sends something Claudette doesn't know, it's skipped and counted, and the session carries on. Settings → Advanced → **Diagnostics** counts them: see [Diagnostics and logs](../troubleshooting/diagnostics-and-logs.md).
- A tab keeps running the version of Claude Code it started with. See [Claude Code's updates](../updates/claude-codes-updates.md).

## See also

- [A tour of the window](a-tour-of-the-window.md)
- [Claude is having problems](../troubleshooting/claude-is-having-problems.md), for when Claude itself is at fault.
