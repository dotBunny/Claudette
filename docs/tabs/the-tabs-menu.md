---
title: The tab's menu
parent: Tabs and sessions
nav_order: 5
summary: "Right-click a tab's row in the sidebar for its menu. Its items are in sections under small titles, as in the header's **≡** menu, with an icon before each. Some show only when they apply to the tab, such as **Reset name** or **Sync now**, and those that follow another, such as **Reset name** after **Rename**, have no icon of their own. A check item's tick goes before its icon. A tab's square on the rail has the same menu, without **Reset name**, **Move up**, **Move down** and **Close other tabs**, and with **Collapse tab** only on a thread with sub-threads.

The project's actions and links aren't in this menu. They're in the menu of the project's icon on the tab's group's row, which is there for every group. See [Project tools](../project-tools/using-project-tools.md).

## The items

In the order the menu has them.

### Tab

| Item | What it does | More |
|---|---|---|
| **Rename** | Edits the tab's name in place. Double-clicking the row does the same. | [Names, marks and pins](names-marks-and-pins.md#renaming-a-tab) |
| **Reset name** | Goes back to the automatic name. Shown while the tab has a name of yours. | [Names, marks and pins](names-marks-and-pins.md#renaming-a-tab) |
| **Pin tab** or **Unpin tab** | A pinned tab comes back at every launch and stays at the start of its group. | [Names, marks and pins](names-marks-and-pins.md#pinning-a-tab) |
| **Mark ▸** | Puts an icon of your own under the status icon: **Check**, **Cross**, **Question mark**, **Star**, **Flag** or **Pause**. | [Names, marks and pins](names-marks-and-pins.md#marks) |
| **Clear mark** | Takes the mark off. Shown while the tab has one. | [Names, marks and pins](names-marks-and-pins.md#marks) |
| **Duplicate tab** | Opens a copy of the tab. | [Below](#duplicate-tab) |
| **Tab settings…** | The tab's own model, effort, mode and more. A dot on its icon means the tab has its own settings. | [Tab settings](tab-settings.md) |

### View

| Item | What it does | More |
|---|---|---|
| **Open beside** | Shows the tab beside the one you're in. Shown for a tab that isn't on screen. | [Split panes](split-panes.md) |
| **Close pane** | Takes a tab off the screen and leaves it open. Shown for a tab in a pane. | [Split panes](split-panes.md) |
| **Steps ▸** | Folds each chain of Claude's steps in this tab into one line, or shows them all: **Default** (what Settings does), **Folded** or **All**. | [Tool calls](../conversation/tool-calls.md#one-tab-its-own-way) |
| **Collapse tab** or **Expand tab** | Folds away the rows under the tab, such as its sub-threads, subagents, editor logs and project runs, or brings them back. Shown while the tab has rows under it. | [Collapsing a tab](the-sidebar.md#collapsing-a-tab) |
| **Move up**, **Move down** | Move the tab one place in its group. | [Below](#move-up-and-move-down) |

### Session

| Item | What it does | More |
|---|---|---|
| **Thread ▸** | Makes the tab a thread, or a sub-thread of one. See the list below. | [Making a thread](../threads/making-a-thread.md) |
| **Account ▸** | Moves the tab to another account. Shown once there's an account besides Default. | [Moving a tab to another account](../accounts/moving-a-tab-to-another-account.md) |
| **Use usage credits** | A check item: once a limit is used up, the tab carries on with your plan's usage credits. Unticked, it stops and waits for the limit to reset. Not on an API-key account. | [Tabs that don't use usage credits](../usage/tabs-that-dont-use-usage-credits.md) |
| **Trust this folder** | Starts Claude Code again with the folder's own settings, MCP servers and `CLAUDE.md`. Shown for a tab that started without them. | [Folder trust](../permissions/folder-trust.md) |
| **Export conversation…** | Saves the conversation as a Markdown file or a web page. | [Exporting](../conversation/exporting.md) |
| **Copy session ID** | Copies the session's ID. Shown once the tab has a session. | [Below](#copy-session-id) |

### Other devices

| Item | What it does | More |
|---|---|---|
| **Sync to other machines** | A check item: copies the tab's session to the session library after each turn. | [Syncing a tab](../history/syncing-a-tab.md) |
| **Sync now** | Copies the session to the library straight away. Shown while the tab syncs. | [Syncing a tab](../history/syncing-a-tab.md) |
| **Connect to the Claude app** | A check item: connects the tab to the Claude app whenever its session runs. Greyed out, with the reason as its tip, when the account can't use it. | [The Claude app](../away/the-claude-app.md) |
| **Open in the Claude app** | Opens the tab's session in the Claude app. Shown while the tab is connected. | [The Claude app](../away/the-claude-app.md) |

### Closing

Under a line at the foot of the menu.

| Item | What it does | More |
|---|---|---|
| **Close** | Closes the tab, asking first when something would be lost. | [Closing and restoring](closing-and-restoring.md) |
| **Close other tabs** | Closes every other tab except pinned ones. | [Closing and restoring](closing-and-restoring.md) |

The **Thread ▸** submenu offers whichever of these apply to the tab:

- **Make this tab a thread**
- **New sub-thread** and **New sub-thread in a worktree**, on a thread
- **Assign to thread ▸**, listing the group's threads
- **Go to thread**, on a sub-thread
- **Show with its sub-threads**, on a thread
- **Detach from thread**, on a sub-thread
- **Ask before sending to sub-threads**, a check item on a thread
- **Stop being a thread**

## Duplicate tab

**Duplicate tab** opens a new tab in the same group that carries the session on as a copy. The two go their own ways from there, and the original doesn't change.

The copy has the original's folder, settings of its own, kept suffixes, sync choice and account, and works in the same worktree and with the same extra folders. It doesn't take the tab's name, mark or pin. A tab that hasn't had its first turn gets a new session in the same folder instead.

To copy a conversation only up to an earlier message, use **Branch from here**. See [Rewind and branch](../conversation/rewind-and-branch.md).

## Copy session ID

The session's ID is what `claude --resume` takes in a terminal. **Copy session ID** puts it on the clipboard. The same ID is on the [info card](the-tab-info-card.md), under **Session**, with a **Copy** button.

Paste it into another tab's message and that tab's Claude can reach this session too: Claudette tells Claude the name to message it by. See [Reaching other sessions](../threads/reaching-other-sessions.md).

## Move up and Move down

**Move up** and **Move down** move the tab one place within its group, as dragging does. A pinned tab never passes an unpinned one. A thread moves together with its sub-threads, and a sub-thread moves among its thread's.

## See also

- [Folder groups](folder-groups.md), for the menu on a group's label.
- [The command palette](the-command-palette.md), which does most of these for the selected tab.
