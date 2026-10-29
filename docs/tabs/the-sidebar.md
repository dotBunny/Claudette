---
title: The sidebar
parent: Tabs and sessions
nav_order: 2
summary: "The sidebar lists your tabs down the left of the window, grouped by working folder, so long session names, a status line and many tabs all fit."
---

# The sidebar

The sidebar lists your tabs down the left of the window, grouped by working folder, so long session names, a status line and many tabs all fit. Each tab is one row with two lines.

{% include shot.html name="guide/tabs/sidebar" alt="The sidebar with three folder groups and seven tabs, showing status icons, branches, marks and context rings." %}

## A tab's row

The first line holds the tab's name, with small icons before it. The second line shows the model and effort (for example `Opus · High`), or what needs your attention instead. At the right of the second line are the badges, and at the end of the row is the context ring.

- Click a row to select the tab. Double-click it to [rename it](names-marks-and-pins.md#renaming-a-tab).
- Right-click a row for the [tab's menu](the-tabs-menu.md).
- Hover a row for a moment to see the [tab info card](the-tab-info-card.md).
- The close button shows on the row you hover and on the selected tab.
- A tab in Bypass permissions mode has a thin outline around its row. See [Permission modes](../permissions/permission-modes.md).

## Status icons

The icon at the start of the row says what the tab is doing. Hover it for the words.

| Icon | Meaning |
|---|---|
| `○` | Not started yet, or not running. A restored tab doesn't start its `claude` until you first select it. |
| `●` (pulsing) | Starting, or Claude is working. |
| `!` (highlighted) | Needs your input: a permission prompt, a question or a plan is waiting. |
| `•` | Finished while you were in another tab. It clears when you look at the tab. |
| `✕` | An error, or the process exited. The row's second line says what went wrong. |
| none | Idle. |

A tab's [mark](names-marks-and-pins.md#marks) sits under the status icon, level with the second line.

## The second line

The second line shows the first of these that applies:

- **Needs your input**, or the error, while the tab waits for you or has failed.
- **Possibly stuck**, when two [check-ins](../composer/check-ins-on-long-turns.md) got no reply.
- The task Claude is working on, in its active form (*Running the tests*), when it keeps a [task list](../plans/the-tasks-page.md).
- *Usage limit · continues at 15:45*, while a [usage limit](../usage/when-a-limit-stops-a-task.md) has stopped the task and it will carry on at the reset.
- *Thread · 2 of 3 working*, on a [thread](../threads/making-a-thread.md) while its sub-threads work.
- Otherwise, the model and effort.

## Badges

Small icons before the name and bordered badges at the right of the second line say more about the tab.

| Badge | Meaning | More |
|---|---|---|
| Pin | The tab is pinned. | [Names, marks and pins](names-marks-and-pins.md#pinning-a-tab) |
| Sync arrows | The tab's session is copied to the session library. Its tip says how much the session takes there. | [Syncing a tab](../history/syncing-a-tab.md) |
| Thread icon | The tab is a thread. Its sub-threads sit indented under it. | [Making a thread](../threads/making-a-thread.md) |
| Branch icon | The tab works in a git worktree. | [Worktree tabs](worktree-tabs.md) |
| Phone | The tab is connected to the Claude app. It dims while it connects. | [The Claude app](../away/the-claude-app.md) |
| `⚙` | A process the tab started is using CPU. | [Processes](processes.md) |
| A number | Tasks (subagents and background tasks) still running after the turn ended. Hover it for which. | [The working line and running tasks](../conversation/the-working-line-and-running-tasks.md) |
| `3/7` | How many of Claude's tasks are done. Click it to open the Tasks page. | [The Tasks page](../plans/the-tasks-page.md) |
| Branch name | The git branch the folder has checked out, such as `main`. Click it for **Copy branch name**. A worktree tab shows the worktree's name. | [Worktree tabs](worktree-tabs.md) |
| `CL 12345` | The Perforce changelist. | [Perforce](../project-tools/perforce.md) |
| Ring | How full the tab's context window is. It turns amber near auto-compact and red from 95%. Click it to see what fills the context, without selecting the tab. | [Context and compacting](../usage/context-and-compacting.md) |

The ring is hidden until the tab has context data, so a tab that hasn't started has none.

## Account tags

Once you've added an API-key account, a folder group whose new tabs use an account other than Default has a small tag with the account's name beside its label, and a tab whose account differs from its group's has the tag on its row. See [A folder's account](../accounts/a-folders-account.md).

## Threads, subagents, editor logs and project runs

A thread's sub-threads sit right under it, indented, and move with it.

Each subagent Claude starts has a small row under its tab, with the agents icon before its task. Click it to see its work in the tab. Settings → Conversation → **Show subagents' work** → **In the conversation** leaves the rows out. See [Subagents under their tab](../conversation/subagents.md#subagents-under-their-tab).

While the project's editor runs, the log it writes has a row under one of the project's tabs, with its errors and warnings counted. Click it to read the log where the conversation is. See [Editor logs](../project-tools/editor-logs.md).

Each time one of a tab's project actions runs as a job, such as a build, a small entry appears under the tab's row with how it's going. Click it to show its log where the conversation is, and click the tab's row to go back. See [Project tools](../project-tools/using-project-tools.md).

When a tab has a lot under it, collapse it to fold them away. See [Collapsing a tab](#collapsing-a-tab).

## Collapsing a tab

A tab with rows under it, such as a thread's sub-threads, subagents, editor logs or project runs, has a chevron on its folder group's colored line, level with its name. It takes no room in the row, so the tab's name stays in line with the others in its group. Click the chevron, or choose **Collapse tab** in the [tab's menu](the-tabs-menu.md) or the [command palette](the-command-palette.md), to fold them all away under the tab's row. A sub-thread's own rows go with it. Click it again, or choose **Expand tab**, to bring them back.

- **The collapsed row** keeps its two lines and adds a small badge to the first: how many rows it hides, then the most urgent of them in its own color. That's `!` for one waiting on you; `✕` for an error, a failed run, or an editor's log that crashed or has errors you haven't looked at; the busy dot while one works; and `•` for one that finished while you were elsewhere. Hover the badge to see what it hides, such as *Collapsed: 2 sub-threads, 1 log and 1 run*, with a line for each one waiting on you or failed.
- **It opens again by itself** when you go to something under it: a sub-thread from its notification or **Go to tab** in the thread's strip, a log from **Show output…** or its notification, or a subagent's work from the Agents page or its card. It also opens when a sub-thread joins the thread, and when something under it starts waiting on you. A run, an editor's log or a subagent starting doesn't open it, so a big fan-out can stay folded.
- Collapsing leaves the selection alone: a sub-thread or a log that's showing stays showing. `Ctrl+Tab` and `Ctrl/Cmd+1…9` still count a collapsed thread's sub-threads.
- The tab stays collapsed when Claudette restarts. Once nothing is left under its row, it forgets, so the next row to come shows.

On the rail, which lists only a thread's sub-threads under a tab, **Collapse tab** is in the menu of a thread with sub-threads.

## The top and the foot

At the top, over a line, **New tab** opens the [picker](opening-tabs.md), and a button beside it collapses the sidebar. At the foot, under a line, are the selected tab's project (a row that opens its menu of actions and links), **History**, the Claude Code and Claudette update badges when there's an update, and a source build's new commits and **New build ready**. **Settings** and **User Guide** are in the menu at the header's right ([A tour of the window](../getting-started/a-tour-of-the-window.md#the-header)).

## Resizing and collapsing

- **Resize** the sidebar by dragging its edge. It's at least 180 pixels wide and 248 by default, and it always leaves the conversation room. Double-click the edge for the default width. The width is remembered.
- **Collapse** it to a rail with the button at its top or `Ctrl/Cmd+B`. The rail shows each group's color, then a square per tab with the first letter of its name, a small status icon at its top right and the tab's mark at its bottom left. Hover a square for the info card. Right-click a group's color bar for the group's menu. The rail doesn't show the context ring, editor logs or project runs. Whether the sidebar is collapsed is remembered.
- **Narrow windows.** Below 900 pixels wide the sidebar collapses to the rail by itself, and expands again when the window is wider. If you expand it by hand in a narrow window, it stays open until the window is widened.

## Settings

Settings → Conversation → Tab rows:

- **Show context**: the ring. On by default.
- **Show task progress**: `3/7`, and the task Claude is working on in place of the model while it works. On by default.

Settings → Git → Tab rows → **Show the git branch**: the branch badge. On by default. With it off, a worktree tab shows a branch icon before its name instead.

Settings → Perforce → **Show changelist on tabs** shows the changelist badge.

## Shortcuts

| Shortcut | What it does |
|---|---|
| `Ctrl/Cmd+T` | New tab |
| `Ctrl/Cmd+B` | Collapse or expand the sidebar |
| `Ctrl/Cmd+1` to `Ctrl/Cmd+9` | Go to a tab by its place in the sidebar |
| `Ctrl+Tab` and `Ctrl+Shift+Tab` | Next and previous tab |
| `Ctrl/Cmd+J` | Go to the next tab waiting for you |

## See also

- [Icons and badges](../reference/icons-and-badges.md) lists every icon in one place.
- [Folder groups](folder-groups.md) covers the labels the tabs sit under.
