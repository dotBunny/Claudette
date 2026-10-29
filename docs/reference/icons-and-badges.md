---
title: Icons and badges
parent: Reference
nav_order: 2
summary: "Every status icon, badge and dot in Claudette's window, what it means, and the page that explains it."
---

# Icons and badges

Every status icon, badge and dot in Claudette's window, what it means, and the page that explains it. Hovering an icon shows a tooltip that says the same.

{% include shot.html name="guide/tabs/sidebar" alt="The sidebar with three folder groups and seven tabs, showing status icons, branches, marks and context rings." %}

## A tab's status icon

The icon at the start of a tab's row. See [The sidebar](../tabs/the-sidebar.md).

| Icon | Tooltip | What it means |
|---|---|---|
| A pulsing dot | *Working*, or *Starting…* | Claude is working on a turn, or its Claude Code is starting. With reduced motion on, the dot doesn't pulse. |
| A pulsing dot | *Possibly stuck: no reply to two check-ins* | A long turn got no answer to two check-ins. See [Check-ins on long turns](../composer/check-ins-on-long-turns.md) and [A tab that seems stuck](../troubleshooting/a-tab-that-seems-stuck.md). |
| `!`, highlighted | *Needs your input* | A permission prompt, question or plan is waiting. See [Permission cards](../permissions/permission-cards.md). |
| A small dot | *Finished while in the background* | A turn ended while you were looking at another tab. It goes once you look. |
| `✕` in the error color | The error | Claude Code stopped with an error, or couldn't start. The row and the info card say why. See [A tab that won't start](../troubleshooting/a-tab-that-wont-start.md). |
| An empty circle | *Not started yet*, or *Not running* | A restored tab that hasn't started its Claude Code yet, or one whose Claude Code has stopped. See [Closing and restoring](../tabs/closing-and-restoring.md). |
| No icon | *Idle* | Nothing is happening. |

A collapsed group shows only the most urgent status of its tabs.

## On a tab's row

| Icon | What it means | More |
|---|---|---|
| A check mark, cross, question mark, star, flag or pause sign under the status icon | The mark you gave the tab, each in its own color | [Names, marks and pins](../tabs/names-marks-and-pins.md) |
| A green disc with a check cut out of it, in the mark's place | Every file Claude changed in this session is ticked as reviewed (*All changed files reviewed*) | [Reviewed files](../changes/reviewed-files.md) |
| A pin | The tab is pinned, so it comes back every launch | [Names, marks and pins](../tabs/names-marks-and-pins.md) |
| A sync icon | *Synced to the session library*, with how much the session takes there (*· 312 MB*) | [Syncing a tab](../history/syncing-a-tab.md) |
| A thread icon before the name | A thread, or a sub-thread indented under it | [Making a thread](../threads/making-a-thread.md) |
| A branch icon with a name | The tab works in a worktree of that name | [Worktree tabs](../tabs/worktree-tabs.md) |
| A phone | *Connected to the Claude app*. It's dimmed while connecting or reconnecting | [The Claude app](../away/the-claude-app.md) |
| A gear | A process the tab started is using CPU | [Processes](../tabs/processes.md) |
| A small number | Subagents and tasks still running after the turn ended, such as *1 agent and 2 tasks still running* | [The agent map](../plans/the-agent-map.md) |
| `3/7` | How many of Claude's tasks are done. A click opens the Tasks page | [The Tasks page](../plans/the-tasks-page.md) |
| A small bordered badge with a name, such as `main` | The git branch (or the worktree's name, or a short commit on a detached HEAD). A click offers **Copy branch name** | [The sidebar](../tabs/the-sidebar.md) |
| `CL 12345` | The Perforce changelist Claude is working in, with **Show changelist on tabs** on | [Perforce](../project-tools/perforce.md) |
| A ring at the row's end | How full the context window is: muted, amber near auto-compact, red from 95% | [Context and compacting](../usage/context-and-compacting.md) |

The second line of a row shows the model and effort, or what needs attention: *Needs your input*, the error, *Possibly stuck*, *Usage limit · continues at 15:45* ([When a limit stops a task](../usage/when-a-limit-stops-a-task.md)), or the task Claude is working on.

## Under a tab's row

Each project job, such as a build, is a run under its tab's row. See [Project tools](../project-tools/using-project-tools.md).

| Icon | What it means |
|---|---|
| A busy dot | The job is running. **Stop** takes the place of × |
| A check mark | It succeeded |
| `✕` in the error color | It failed, with its exit code |
| A muted square | You stopped it |

## The collapsed sidebar

The rail shows each group's color, then a square per tab with the first letter of its name, a small status icon at its top right, and the mark or the reviewed icon at its bottom left. Hovering a square shows the tab's info card. See [The sidebar](../tabs/the-sidebar.md).

## The foot of the sidebar

| Icon | What it means | More |
|---|---|---|
| Unreal Engine's logo, a cube or Godot's logo, with the project's name | The selected tab's project row: an Unreal Engine, Unity or Godot project. A folder when the tab's folder has no project Claudette knows. A busy dot replaces the icon while a job runs | [Project tools](../project-tools/using-project-tools.md) |
| A Claude Code update badge | A newer Claude Code is available | [Claude Code's updates](../updates/claude-codes-updates.md) |
| A Claudette update badge | A new Claudette release is available | [Claudette's updates](../updates/claudettes-updates.md) |
| **New build ready** | A source build of Claudette has a new build to restart into | [Running from source](../getting-started/running-from-source.md) |
| *3 new commits on origin/main* | A source build's checkout is behind the branch it tracks | [Running from source](../getting-started/running-from-source.md#when-there-are-new-commits) |

## The header

| Icon | What it means | More |
|---|---|---|
| The session and weekly meters | How much of your plan's limits you've used, with a warning color near the limit | [The usage header](../usage/the-usage-header.md) |
| CPU and memory totals | Every tab's processes added up, while the process monitor is on | [Processes](../tabs/processes.md) |
| A cup | **Keep this computer awake**: accent-colored while it's on, and steaming while sleep is held off | [Keeping the computer awake](../away/keeping-the-computer-awake.md) |
| A green dot | Claude's services are operational | [Claude is having problems](../troubleshooting/claude-is-having-problems.md) |
| An amber dot | A service is degraded, or under maintenance | |
| A red dot | A service is in an outage | |
| A grey dot | The status is unknown, or hasn't been checked yet | |
| Three lines | The menu: **Settings**, **User Guide**, **Ask on Discord**, **Report an issue** and **About Claudette** | [A tour of the window](../getting-started/a-tour-of-the-window.md#the-header) |

A banner under the header says what's wrong while Claude has an incident. A line under the meters says so when usage is on pace to hit a limit.

## Tool windows

- On the strip at the window's right, a dot on an icon means its window has something going on: subagents active on **Agents**, a job running on **Project**, a server that needs attention on **MCP**. An icon is filled in while its window shows. See [Tool windows](../tabs/tool-windows.md).
- **Changed files** marks each file with `M` for modified or `A` for added. See [Changed files](../changes/changed-files.md).
- **Agents** marks each agent with a dot while it runs or when it's done, `!` while it waits for your permission, `✕` when it failed, and a square when it was stopped. See [The agent map](../plans/the-agent-map.md).

## The Dock and taskbar icon

On Windows, the taskbar button's overlay shows the number of tabs needing input, Claude's spark while tabs work, and an hourglass while a tab waits for a usage limit. On macOS, the Dock icon shows the number as a badge, and Claudette types, waves or waits by an hourglass. See [The Dock and taskbar icon](../away/the-dock-and-taskbar-icon.md).

## Accounts, Project files and the conversation

| Icon | What it means | More |
|---|---|---|
| A small tag with an account's name beside a group's label, or on a tab's row | New tabs here run under that API-key account, or this tab does. Default's tag is muted | [A folder's account](../accounts/a-folders-account.md) |
| The key's estimated spend in the header | A tab on an API-key account is selected | [Spend and its warnings](../accounts/spend-and-its-warnings.md) |
| `M` or `A` at the right of an explorer row, and a dot on a folder | Claude changed or created that file, or a file inside the folder. A reviewed file's letter is faint | [Claude's changes in the editor](../project-files/claudes-changes-in-the-editor.md) |
| Marks beside the conversation's scroll bar | Your messages, waiting prompts and errors. `F3` and `F4` move between messages | [Finding your way](../conversation/finding-your-way.md) |
| *Cache 52m* beside the context | How long your next message can read the conversation from Claude's cache. It turns amber in its last three minutes | [The prompt cache](../usage/the-prompt-cache.md) |
