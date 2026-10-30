---
title: Your first tab
parent: Getting started
nav_order: 7
summary: "A tab is one Claude Code session, run as its own claude in a folder you pick."
---

# Your first tab

A tab is one Claude Code session, run as its own `claude` in a folder you pick. You can have as many as you like, and several can share a folder.

## Opening a tab

1. Press `Ctrl/Cmd+T`, or choose **New tab** at the top of the sidebar. The **New tab** picker opens.
2. Pick the project's folder:
   - Type in the search box to filter the list by folder name or path.
   - Choose a folder in the list. Favorites come first, then your recent folders, most recent first. Each shows its path, its git branch if it has one, when you last used it, and how many tabs are open in it.
   - Press `Enter` to open the selected folder, or `1` to `9` to open one of the first nine.
   - Choose **Browse…** for any other folder.
3. The tab opens and starts Claude Code in that folder.

{% include shot.html name="guide/getting-started/new-tab" alt="The New tab picker open over the window, listing recent project folders with their branches and paths" %}

Right-click a folder in the list to add it to your favorites, remove it from the recents, reveal it in your file manager, or copy its path. A folder that no longer exists is dimmed. **Open from History…** resumes a past session instead of starting a new one: see [History](../history/history.md).

A group's own **+** in the sidebar opens a tab in that group's folder without the picker. [Opening tabs](../tabs/opening-tabs.md) lists the other ways in, such as dragging a folder onto the sidebar, and `--folder` on the command line.

## If Claudette asks about the folder

The first time Claude Code would start in a folder, Claudette checks whether the folder's own configuration would run anything: hooks, helper commands, plugins, or MCP servers from a `.mcp.json`. If it would, a card above the message box asks **Trust this folder?** and lists what it found.

- **Trust this folder** starts Claude Code as usual and remembers the folder.
- **Start without it** starts Claude Code without that configuration.
- **Close tab** closes the tab.

If there is nothing to list, the tab just starts. [Folder trust](../permissions/folder-trust.md) explains the choices.

## Sending a message

Click the message box, which says *Message Claude…*, and type. `Enter` sends the message and `Shift+Enter` adds a line. Claude's reply streams in above as it's written, and the tab's row in the sidebar shows when it's working.

- Claude may ask before it does something, such as running a command or editing a file. The question shows as a card in the conversation, with **Allow**, **Always allow** and **Deny**. See [Permission cards](../permissions/permission-cards.md).
- You can keep typing while Claude works. A message sent meanwhile reaches Claude at its next step.
- After your first message, Claude Code gives the session a name, and the tab takes it. Until then the tab shows the folder's name. Double-click a tab to rename it.

## Stopping

**Stop** replaces **Send** while Claude is working. Choose it, or press `Esc`, to interrupt the turn. It doesn't close the session: send another message to carry on. [Sending and stopping](../composer/sending-and-stopping.md) has the rest.

## Closing and coming back

`Ctrl/Cmd+W` closes the tab. Closing one where Claude is working asks first. Pinned tabs come back every time Claudette starts, resuming their sessions: see [Names, marks and pins](../tabs/names-marks-and-pins.md).

## Settings

- Settings → Tabs: the **Model**, **Effort** and **Permission mode** a new tab starts with, **Recent folders to keep**, **Clear recent folders**, and your **Favorite folders**.
- Settings → Claude Code → **Ask before using a new folder's own configuration** turns the trust question off.

## See also

- [A tour of the window](a-tour-of-the-window.md), for what's on screen.
- [Reading the conversation](../conversation/reading-the-conversation.md).
