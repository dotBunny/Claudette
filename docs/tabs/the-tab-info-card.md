---
title: The tab info card
parent: Tabs and sessions
nav_order: 6
summary: "The info card is where a tab's details live, so they don't crowd its name."
---

# The tab info card

The info card is where a tab's details live, so they don't crowd its name. It has the tab's name at the top and a row for each detail.

{% include shot.html name="guide/tabs/info-card" alt="The info card of the Grappling hook tab, listing its folder, branch, model, session, tokens, context and status." %}

## Opening it

- **Hover** a tab's row in the sidebar, or its square on the rail, and rest the pointer there for a moment.
- **Click ⓘ**, last on the line under the message box, for the selected tab. This version stays open until you click away, and its **Session** and **Reached as** rows have a **Copy** button.

## The rows

A row shows only when the tab has something to say. They come in this order:

| Row | What it says |
|---|---|
| **Folder** | The full folder path. |
| **Branch** | The git branch the folder has checked out. |
| **Worktree** | For a tab in a worktree: its name and the checkout it belongs to (*brisk-otter, of C:\code\game*). See [Worktree tabs](worktree-tabs.md). |
| **Extra folders** | The folders Claude may also use, one per line. See [Tab settings](tab-settings.md). |
| **Model** | The model and effort, such as `Opus · High`, with *Ultracode* added while it's on. |
| **Mode** | The permission mode. See [Permission modes](../permissions/permission-modes.md). |
| **Started** | When the session started. For a resumed session, that's its transcript's first entry. |
| **Session** | The session's ID, which `claude --resume` takes. Has **Copy** in the ⓘ card. |
| **Reached as** | The name other Claude Code sessions message this one by. Has **Copy** in the ⓘ card. |
| **Sync size** | For a tab that syncs: how much its session takes in the session library, *312 MB in the session library*, as of its last copy there. See [Syncing a tab](../history/syncing-a-tab.md#how-much-it-takes). |
| **Tokens** | A short count of the tokens the tab has used and its number of turns. See [A tab's tokens](../usage/a-tabs-tokens.md). |
| **Claude Code** | The Claude Code version this tab runs. When a newer one is installed, it says so: open tabs keep their version and new tabs use the newest. See [Claude Code's updates](../updates/claude-codes-updates.md). |
| **Context** | How full the context window is. See [Context and compacting](../usage/context-and-compacting.md). |
| **Agents** | While the tab has subagents: how many are running or waiting on you, or how they ended. See [The agent map](../plans/the-agent-map.md). |
| **Running tasks** | While Claude Code has work going in the background: one line each (*Shell command: Start the dev server*), up to four, then *and 2 more*. |
| **Tasks** | While Claude keeps a task list: *3 of 7 done · Now: Running the tests*. See [The Tasks page](../plans/the-tasks-page.md). |
| **Project** | The project the folder holds, and whether Claude was told how to build it. See [Project tools](../project-tools/using-project-tools.md). |
| **Perforce** and **Changelist** | The Perforce login's state and the tab's changelists. In the ⓘ card each changelist has **Copy** and **Open in P4V**. See [Perforce](../project-tools/perforce.md). |
| **Claude app** | Whether the tab is connected, connecting, or why not. See [The Claude app](../away/the-claude-app.md). |
| **Usage limit** | While a usage limit has stopped the tab's task: when it resets, and whether the task continues then. See [When a limit stops a task](../usage/when-a-limit-stops-a-task.md). |
| **Status** | The same words as the status icon's tip: *Idle*, *Working*, *Needs your input*, or what went wrong. |

**Reached as** is usually the tab's name. It's a variant when another session already had that name, or the one Claude Code made from the folder (`claudette-3f`) while naming is turned off. See [Names, marks and pins](names-marks-and-pins.md#the-name-in-claude-code).

## The Account row

Once you've added an API-key account, the card has an **Account** row after the worktree rows: the tab's account, or *Default*. See [Moving a tab to another account](../accounts/moving-a-tab-to-another-account.md).

## See also

- [The sidebar](the-sidebar.md), for the badges on the row itself.
- [The composer bar](../composer/the-composer-bar.md), with the line under the message box where the ⓘ button is.
