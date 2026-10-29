---
title: The composer bar
parent: The composer
nav_order: 2
summary: "The bar under the message box holds your choices on its left and the counts for the tab on its right, with Send last."
---

# The composer bar

The bar under the message box holds your choices on its left and the counts for the tab on its right, with the key that sends and **Send** last. The status line under the whole composer says where the tab is working and what it's doing.

{% include shot.html name="guide/composer/composer" alt="The composer's bar: the paper clip, Suffixes, the model, effort and permission mode on the left, and the counts and Send on the right, with the status line under the box." %}

When the bar doesn't fit on one line, as in a narrow window, the counts and **Send** move to a second line under your choices, still at the right. **Send** stays in view, with the send key chip before it.

## On the left

Everything here is a button that opens a menu, or acts at once.

| Button | What it does | More |
|---|---|---|
| Paper clip (**Attach a file**) | Picks a file or image to attach. | [Attachments and large pastes](attachments-and-large-pastes.md) |
| **Suffixes ▾** | Adds a saved snippet to the message. | [Quick suffixes](quick-suffixes.md) |
| **Stashed (2) ▾** | Messages you put aside. It shows only while the stash has something. | [Earlier prompts, drafts and the stash](earlier-prompts-drafts-and-the-stash.md) |
| The model, shown by its name and ▾ | Switches the model. | [Model, effort and output style](model-effort-and-output-style.md) |
| The effort, such as **High ▾** | Sets the effort, **Ultracode** and the **Output style**. | [Model, effort and output style](model-effort-and-output-style.md) |
| The permission mode, such as **Manual ▾** | Picks what Claude may do without asking. | [Permission modes](../permissions/permission-modes.md) |

## On the right

Most of these show only when they apply to the tab.

| Item | What it says | More |
|---|---|---|
| Processes, such as *3 procs · 42% CPU · 1.1 GB* | What the tab's Claude started and left running. It shows when the process monitor is on and the Processes tool window isn't showing the tab. | [Processes](../tabs/processes.md) |
| **Agents (3)** or *2 agents running* | The tab has subagents. It opens the Agents page. | [The agent map](../plans/the-agent-map.md) |
| *2 running tasks* | Work Claude Code keeps going in the background. It opens a list with **Stop**. | [The working line and running tasks](../conversation/the-working-line-and-running-tasks.md) |
| **Files (4)** | How many files Claude changed. It opens the Changed files page. | [Changed files](../changes/changed-files.md) |
| *Context 42%* | How full the context window is. It turns the caution color when Claude Code is about to compact the conversation. Click it for what fills the window and **Compact now**. | [Context and compacting](../usage/context-and-compacting.md) |
| The tab's tokens, such as *12.3k tok* | Click for a breakdown by model. | [A tab's tokens](../usage/a-tabs-tokens.md) |
| *⏎ sends* or *Ctrl+⏎ sends* | Which key sends the message. Click it to switch. | [The send key](#the-send-key), below |
| **Send** | Sends the message. While Claude works it's **Stop**. | [Sending and stopping](sending-and-stopping.md) |

## The send key

{% include unreleased.html %}

Just before **Send**, a chip says which key sends your message: *⏎ sends* when it's `Enter`, or *Ctrl+⏎ sends* when it's `Ctrl+Enter` (*⌘⏎ sends* on macOS). It's always there.

Click it to switch between the two. With the keyboard, `Tab` to it and press `Space`. Its tooltip says what each key does and what clicking does, such as *Enter sends, Shift+Enter starts a new line. Click to send with Ctrl+Enter instead.*

It's the same choice as Settings → Keyboard → **Send with**, so it applies to every tab: each tab's chip and placeholder change with it, and so does the Settings window if it's open. **Send**'s tooltip names the key too, such as *Send (Ctrl+Enter)*. [Sending and stopping](sending-and-stopping.md#change-the-key-that-sends) says more about the two keys.

## The prompt cache

After the context, a chip such as *Cache 52m* says how long your next message can read the conversation from the prompt cache, which costs much less than Claude reading it all again. In its last three minutes it turns the caution color, then reads *Cache <1m* and *Cache expired*. It shows only between turns, once the conversation has made its first request.

Settings → Conversation → **Show when the prompt cache expires** turns it off. [The prompt cache](../usage/the-prompt-cache.md) explains it.

## The status line

Under the box, left to right:

- The folder the tab works in.
- The tab's status, such as *Working* or *Needs your input*, and **Restart** when `claude` has stopped.
- At the right, how Claude's plan and tasks are going, such as *3/7*. Click it to open the Tasks page ([The Tasks page](../plans/the-tasks-page.md)).
- Last, **ⓘ**: the tab's info card, with its folder, session, model and more ([The tab info card](../tabs/the-tab-info-card.md)).

## Settings

- Settings → Keyboard → **Send with**, **Enter, with Shift+Enter for a new line** by default, is what the send key chip shows and switches. See [Keyboard](../settings/keyboard.md#send-with).
- Settings → Conversation → **Show when the prompt cache expires**, on by default, for the Cache chip.
- Settings → Conversation → Tab rows → **Show context** is for the ring on the sidebar's rows, not the bar. See [The sidebar](../tabs/the-sidebar.md).

## See also

- [Tool windows](../tabs/tool-windows.md), where the Processes, Agents and Changed files buttons lead.
