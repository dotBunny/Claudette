---
title: A tour of the window
parent: Getting started
nav_order: 7
summary: "The usage header, the sidebar, the conversation, the tool windows and the composer, and where each is explained."
---

# A tour of the window

The window has five parts: the usage header across the top, the sidebar on the left, the conversation in the middle, the tool windows on the right and the composer under the conversation. This page names each part and says where to read more.

{% include shot.html name="standard" alt="Claudette's window: seven tabs over three game projects in the sidebar, a conversation with tool calls and a code block, the Changed files tool window, the plan's usage in the header, and Claudette herself standing on the message box" %}

## The header

The header is always visible. From left to right it shows:

- **The usage meters.** Your session (5-hour) usage is the most prominent, with the weekly limit and each model's own limit beside it, their reset countdowns, and a short trend that says whether you'll reach the limit. The chevron at its right opens the header up into charts. See [The usage header](../usage/the-usage-header.md).
- **The processes' total**, when the process monitor is on: the CPU and memory of every tab's processes. See [Processes](../tabs/processes.md).
- **The cup**, which keeps the computer awake while it steams. See [Keeping the computer awake](../away/keeping-the-computer-awake.md).
- **A colored dot**, which shows whether Claude itself is having trouble. See [Claude is having problems](../troubleshooting/claude-is-having-problems.md).
- **Your account** and its plan. See [Your Claude sign-in](../accounts/your-claude-sign-in.md).
- **The menu**, three lines, last: **Settings** ([The Settings window](../settings/the-settings-window.md)) and **User Guide**, which opens this guide in your browser, then **Ask on Discord**, **Report an issue** ([Reporting an issue](../troubleshooting/reporting-an-issue.md)) and **About Claudette** ([About](../settings/about.md)).

A banner can run across the top, under the header: Claude's service status when there's an incident, **Claude Code needs you to sign in.**, or a usage alert. Each has a button to deal with it or **Dismiss**.

## The sidebar

The sidebar lists your tabs, one row each, grouped by working folder. A row shows a status icon (idle, working, needs your input, finished while you were away, or an error), the tab's name, its model and effort, and its git branch, mark and context ring. See [The sidebar](../tabs/the-sidebar.md), [Folder groups](../tabs/folder-groups.md) and [Icons and badges](../reference/icons-and-badges.md).

- **New tab** is at the top: see [Your first tab](your-first-tab.md).
- At the foot are the selected tab's project, with its actions and links ([Project tools](../project-tools/using-project-tools.md)), **History** ([History](../history/history.md)) and the update badges ([Updates](../updates/index.md)).
- `Ctrl/Cmd+B` collapses the sidebar to a rail of status icons.
- `Ctrl/Cmd+J` jumps to the next tab waiting for you, and `Ctrl/Cmd+Shift+P` opens the [command palette](../tabs/the-command-palette.md), which reaches everything else.

## The conversation

The middle shows the selected tab's conversation: your messages, Claude's replies as Markdown with highlighted code, its tool calls, its thinking, and the questions it asks you. Permission prompts, questions and plans are cards with buttons, in the conversation itself. Read on in [The conversation](../conversation/index.md) and [Permissions and prompts](../permissions/permission-cards.md).

## Tool windows

The tool windows are on a strip of icons down the window's right edge, as in JetBrains Rider. Click an icon to show its window beside the strip, and click it again to hide it. They show the tab you're looking at.

| Window | What it shows | Where to read more |
|---|---|---|
| **Changed files** | The files this tab changed, with their diffs | [Changed files](../changes/changed-files.md) |
| **Agents** | Claude's subagents as a tree | [The agent map](../plans/the-agent-map.md) |
| **Project** | The project's tools, actions and their output, when the folder has any | [Project tools](../project-tools/using-project-tools.md) |
| **Processes** | The tab's processes, when the monitor is on | [Processes](../tabs/processes.md) |
| **Tasks** | Claude's plan and task list, while it keeps one | [The Tasks page](../plans/the-tasks-page.md) |
| **MCP** | The session's MCP servers, when it has any | [MCP servers](../tabs/mcp-servers.md) |
| **Scratch pad** | Your notes for the project | [The scratch pad](../conversation/the-scratch-pad.md) |
| **Find** | The searches you kept from Find in files, along the bottom | [Finding in files](../project-files/finding-in-files.md) |

[Tool windows](../tabs/tool-windows.md) covers the strip's groups, windows that hide when you click elsewhere, and moving and sizing them.

## The composer

Under the conversation is the message box, with a bar above it and a status line under it. The bar holds the tab's model, effort and permission mode, and its counts: processes, files changed, context and tokens, with **Send** (or **Stop**) at the right. The status line shows the working folder and the tab's status, with **Restart** if it has stopped. See [The composer](../composer/index.md) and [The composer bar](../composer/the-composer-bar.md).

Claudette herself, the pixel mascot, stands on the message box and reacts to what Claude is doing. See [Claudette on the composer](../claudette/claudette-on-the-composer.md).

## On macOS and Windows

- **macOS:** the menu bar has **Settings…** in the app menu, **File** with **New Tab**, **Open Recent**, **History…** and **Close Tab**, and **Help** with **User Guide**, **Ask on Discord** and **Report an Issue**. The Dock icon's menu lists your recent folders, and its badge counts the tabs that need you.
- **Windows:** the taskbar button's jump list lists your recent folders, and an overlay on the button counts the tabs that need you.

See [Notifications](../away/notifications.md) for the rest of what Claudette does to get your attention.
