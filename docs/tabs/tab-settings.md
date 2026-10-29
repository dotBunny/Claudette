---
title: Tab settings
parent: Tabs and sessions
nav_order: 7
summary: "Some settings can be different for one tab."
---

# Tab settings

Some settings can be different for one tab. They replace the defaults from Settings for that tab only, and they're saved with it. A tab that has settings of its own shows a small dot beside **Tab settings…** in its menu.

## Opening the dialog

Right-click the tab and choose **Tab settings…**, or use **Tab settings…** in the [command palette](the-command-palette.md) for the selected tab. The dialog is titled *Settings for "‹the tab's name›"*.

Each change applies as you make it. **Done**, or `Esc`, closes the dialog.

{% include shot.html name="guide/tabs/tab-settings" alt="Tab settings for the Grappling hook tab: its own model and effort marked as changed under Model and mode, then At a usage limit, each section with Use defaults, and Done" %}

## The overrides

Each one starts at **Default**, which says what the default is, such as *Default (Opus)*. Pick something else to give the tab its own.

| Section | Setting | Choices | The default comes from |
|---|---|---|---|
| **Model and mode** | **Model** | Default, or a model | Settings → Tabs → New tabs start with → **Model** |
| | **Effort** | Default, or an effort level | Settings → Tabs → New tabs start with → **Effort** |
| | **Permission mode** | Default, or a mode | Settings → Tabs → New tabs start with → **Permission mode** |
| | **Subagents** | Default, **Claude decides**, **Discourage them**, **Don't allow them** | Settings → Tabs → **Subagents** |
| **At a usage limit** | **Continue tasks when a usage limit resets** | Default, **On**, **Off** | Settings → Tabs → At a usage limit → **Continue tasks when a usage limit resets** |
| | **Use usage credits** | Default, **On**, **Off** | Settings → Tabs → At a usage limit → **Use usage credits once a limit is used up** |
| **Process monitor** | **Show the process monitor** | Default, **On**, **Off** | Settings → Tabs → Process monitor → **Show the process monitor** |
| **Conversation** | **Steps** | Default, **Folded**, **All** | Settings → Conversation → Steps and replies → **Fold each chain of steps** |

- **Model** and **Effort** are explained in [Model, effort and output style](../composer/model-effort-and-output-style.md), and **Permission mode** in [Permission modes](../permissions/permission-modes.md). A change reaches the running session at once.
- **Subagents** is whether Claude hands parts of its work to subagents in this tab. It applies at once, and Claude hears about it with your next message. See [Discouraging or turning off subagents](../conversation/subagents.md#discouraging-or-turning-off-subagents).
- **Continue tasks when a usage limit resets** is whether Claude carries on a task by itself when a usage limit resets. See [When a limit stops a task](../usage/when-a-limit-stops-a-task.md).
- **Use usage credits** is whether the tab carries on with your plan's usage credits once a limit is used up, or stops and waits for the reset. The tab's menu has it too, and a tab on an API-key account doesn't have it. See [Tabs that don't use usage credits](../usage/tabs-that-dont-use-usage-credits.md).
- **Show the process monitor** turns the [Processes](processes.md) page and the composer bar's summary on or off for this tab.
- **Steps** folds each chain of Claude's steps between two messages into one line in this tab, or shows every step. **Steps** in the tab's menu is the same choice. See [Folding a chain of steps](../conversation/tool-calls.md#folding-a-chain-of-steps).

### Check-ins on long turns

Turn on **Use different check-ins for this tab** to set its own: **Check in on long turns**, **After the turn has run (minutes)**, **After no output for (minutes)**, the **Message**, and **Notify me when a check-in is sent**. Leave it off to use Settings → Tabs → Check-ins on long turns. See [Check-ins on long turns](../composer/check-ins-on-long-turns.md).

### Going back to the defaults

Each section has its own **Use defaults**, which sets its overrides back to **Default**, or turns off **Use different check-ins for this tab**. It applies at once, like any other change. The **This tab** section has none.

## Settings that aren't overrides

These are in the dialog's **This tab** section, but they're the tab's own choices rather than replacements for a default. No **Use defaults** changes them, and they don't count toward the dot.

- **Sync to other machines** copies the tab's session to the session library after each turn. It's the same switch as the tab's menu. Once the session is there, the line under it says how much it takes: *This session takes 312 MB in the session library.* See [Syncing a tab](../history/syncing-a-tab.md).
- **Connect to the Claude app** connects the tab to the Claude app on your phone or at claude.ai/code while it runs here, reconnecting whenever the tab starts. It's greyed out, with the reason under it, when the account can't use it. See [The Claude app](../away/the-claude-app.md).
- **Extra folders** are folders Claude may also read and edit, besides the tab's own. Choose **Add folder…**, or the remove button beside a folder. Their own settings and `CLAUDE.md` aren't loaded, and a folder that's gone isn't passed on.

Claude Code takes extra folders when it starts, so changing them starts it again once you click **Done**, carrying the conversation on. When Claude is working, that waits until it has nothing left to do: the turn under way, and any messages you sent meanwhile, run first. Extra folders are saved with the tab, copied by **Duplicate tab**, and listed on the [info card](the-tab-info-card.md).

## Project actions

A folder's own actions belong to the folder, not the tab. The dialog's **Project actions** row says where they are (*In Settings → ‹project› → Actions.*) and has an **Open** button that goes there. See [Actions and links](../project-tools/actions-and-links.md).

## See also

- [The tab's menu](the-tabs-menu.md), which has the same **Sync to other machines** and **Connect to the Claude app** switches.
- [The Settings window](../settings/the-settings-window.md), for the defaults.
