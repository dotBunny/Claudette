---
title: Every setting
parent: Settings
nav_order: 2
summary: "A table for each page in the Settings window, in the order its sidebar lists them."
---

# Every setting

A table for each page in [the Settings window](the-settings-window.md), in the order its sidebar lists them, and within a page in the order of its sections. Find a setting, then follow its link to the page that explains what it does.

The columns:

- **Setting** is its label, as the app shows it.
- **Section** is the section of the page it's in, whose **Reset to defaults** puts it back.
- **Default** is what a new install uses, and what **Reset to defaults** puts back.
- **Syncs** says whether **Sync Claudette's settings through the library** carries it to your other machines ([Settings sync](../history/settings-sync.md)). **No** means it stays on this machine, and Settings marks it **This computer**.
- **Per tab** says whether [Tab settings…](../tabs/tab-settings.md) can change it for one tab, and under what name. **Own switch** means each tab has a switch of its own, which this setting sets for tabs you open from now on. **No** means every tab uses it.

## General

None of this page syncs, and a tab can't change it for itself.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Start Claudette when I log in** | Starting and closing | Off (your OS keeps it) | [Starting at login](../away/starting-at-login.md) |
| **Also restore unpinned tabs when Claudette starts** | Starting and closing | On | [Closing and restoring](../tabs/closing-and-restoring.md) |
| **Confirm before closing a tab where Claude is working** | Starting and closing | On | [Closing and restoring](../tabs/closing-and-restoring.md) |
| **Keep this computer awake** | Keeping the computer awake | Off | [Keeping the computer awake](../away/keeping-the-computer-awake.md) |
| **When**: **Only while Claude has work to finish** or **Whenever Claudette is running** | Keeping the computer awake | Only while Claude has work to finish | [Keeping the computer awake](../away/keeping-the-computer-awake.md) |
| **Also while tabs are connected to the Claude app** | Keeping the computer awake | On | [The Claude app](../away/the-claude-app.md) |
| **Show Claude's service status** | Updates and status | On | [Claude is having problems](../troubleshooting/claude-is-having-problems.md) |
| **Check for Claudette updates automatically** | Updates and status | On | [Claudette's updates](../updates/claudettes-updates.md) |
| **Include pre-releases** | Updates and status | Off | [Claudette's updates](../updates/claudettes-updates.md) |

The page also shows Claudette's version and update status, with **Check now**, **Download** and **Release notes**. Those are on [Claudette's updates](../updates/claudettes-updates.md).

{: .note }
**Also restore unpinned tabs when Claudette starts** is on for new installs from 0.3.0. Settings saved by 0.2.0 or earlier keep it off until you turn it on, or use the **Reset to defaults** of **Starting and closing**.

## Appearance

The page starts with a preview that shows your choices as you make them.

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **Theme** | Look | System | Yes | [Styles and themes](styles-and-themes.md#theme) |
| **Style** | Look | Standard | Yes | [Styles and themes](styles-and-themes.md#style) |
| **Density** | Look | Comfortable | Yes | [Styles and themes](styles-and-themes.md#density) |
| **Full-width conversation** | Look | Off | Yes | [Reading the conversation](../conversation/reading-the-conversation.md) |
| **Conversation font** | Text | Claudette's default | Yes | [Styles and themes](styles-and-themes.md#fonts) |
| **Conversation font size** | Text | 14 (from 9 to 28) | Yes | [Styles and themes](styles-and-themes.md#fonts) |
| **Code font** | Text | Cascadia Mono, Consolas or Menlo | Yes | [Styles and themes](styles-and-themes.md#fonts) |
| **Code font size** | Text | 13 (from 8 to 28) | Yes | [Styles and themes](styles-and-themes.md#fonts) |
| **Zoom** | Accessibility | 100% | No | [Accessibility](accessibility.md#zoom) |
| **Motion** | Accessibility | Follow the system | Yes | [Accessibility](accessibility.md#motion) |
| **Show Claudette on the composer** | Claudette | On | Yes | [Claudette on the composer](../claudette/claudette-on-the-composer.md) |
| **Size** | Claudette | Medium (or Small or Large) | Yes | [Claudette on the composer](../claudette/claudette-on-the-composer.md) |
| **How often** | Claudette | Lively: every 10 to 30 seconds (or Calm: every 30 to 90) | Yes | [Claudette on the composer](../claudette/claudette-on-the-composer.md) |
| **Shares tips** | Claudette | On | Yes | [Claudette on the composer](../claudette/claudette-on-the-composer.md) |
| **Games with Claudette** | Claudette | On | Yes | [Games with Claudette](../claudette/games-with-claudette.md) |

## Conversation

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **Quieter in-between steps** | Steps and replies | On | Yes | [Reading the conversation](../conversation/reading-the-conversation.md) |
| **Show thinking expanded** | Steps and replies | Off | Yes | [Thinking, notes and hooks](../conversation/thinking-notes-and-hooks.md) |
| **Show subagents' work** | Steps and replies | Under the tab in the sidebar | Yes | [Subagents](../conversation/subagents.md#subagents-under-their-tab) |
| **Show every hook run** | Steps and replies | Off | No | [Thinking, notes and hooks](../conversation/thinking-notes-and-hooks.md) |
| **Show fun words** | While Claude works | On | Yes | [The working line and running tasks](../conversation/the-working-line-and-running-tasks.md) |
| **Show what Claude is doing** | While Claude works | On | Yes | [The working line and running tasks](../conversation/the-working-line-and-running-tasks.md) |
| **Messages sent while Claude works** | While Claude works | Send them into the turn (the other choice queues them) | No | [Sending and stopping](../composer/sending-and-stopping.md) |
| **Show when the prompt cache expires** | Message box | On | Yes | [The prompt cache](../usage/the-prompt-cache.md) |
| **Suggest my next prompt** | Message box | Off | No | [Prompt suggestions](../composer/prompt-suggestions.md) |
| **Show context** | Tab rows | On | Yes | [Styles and themes](styles-and-themes.md#what-the-tab-rows-show) |
| **Show task progress** | Tab rows | On | Yes | [Styles and themes](styles-and-themes.md#what-the-tab-rows-show) |

## Notifications

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **A tab finishes its turn** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **A tab needs permission or an answer** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **A tab's Claude Code stops with an error** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **Usage crosses a warning level, is on pace to run out, or resets** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **Claude Code needs me to sign in** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **A Claude Code update is ready** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **A project action finishes** | Notify me when | On | Yes | [Notifications](../away/notifications.md) |
| **Show how many tabs need you** | The Dock or taskbar icon | On | Yes | [The Dock and taskbar icon](../away/the-dock-and-taskbar-icon.md) |
| **Animate it while tabs work or wait** | The Dock or taskbar icon | On | Yes | [The Dock and taskbar icon](../away/the-dock-and-taskbar-icon.md) |

The notification for a check-in is **Notify me when a check-in is sent**, on the Tabs page.

## Keyboard

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **Send with** | Sending | Enter, with Shift+Enter for a new line | Yes | [Keyboard](keyboard.md#send-with) |
| Each command's shortcut | Shortcuts | The defaults in [Keyboard shortcuts](../reference/keyboard-shortcuts.md) | Yes | [Keyboard](keyboard.md#changing-a-shortcut) |
| The list of suffixes, each with a label, its text and an optional shortcut, and **Add suffix** | Quick suffixes | Five: Clarify first, Plan only, Keep it small, Test it and Explain | Yes (as one list) | [Quick suffixes](../composer/quick-suffixes.md) |

## Claude Code

None of this page syncs.

| Setting | Section | Default | Per tab | Explained in |
|---|---|---|---|---|
| **Check for Claude Code updates automatically** | Claude Code | On | No | [Claude Code's updates](../updates/claude-codes-updates.md) |
| **Path to claude** | Claude Code | Found automatically | No | [The first launch](../getting-started/the-first-launch.md) |
| **Name each tab's session in Claude Code after the tab** | Starting a session | On | No | [Names, marks and pins](../tabs/names-marks-and-pins.md) |
| **Ask before using a new folder's own configuration** | Starting a session | On | No | [Folder trust](../permissions/folder-trust.md) |
| **Use my login shell's environment** (macOS and Linux only) | Starting a session | On | No | [Claude can't find a program](../troubleshooting/claude-cant-find-a-program.md) |
| **Keep copies of files Claude changes, so prompts can be rewound** | Starting a session | On | No | [Rewind and branch](../conversation/rewind-and-branch.md) |
| **Give Claude a task list** | Starting a session | On | No | [The Tasks page](../plans/the-tasks-page.md) |
| **Summarize subagents' progress** | Starting a session | Off | No | [The agent map](../plans/the-agent-map.md) |
| **Fallback model** | Starting a session | None | No | [Model, effort and output style](../composer/model-effort-and-output-style.md) |
| **Connect new tabs to the Claude app** | Claude app | Off | Own switch (**Connect to the Claude app**) | [The Claude app](../away/the-claude-app.md) |

The page also shows the installed Claude Code and how it was installed, with **Check now**, **Update now**, **Update on next launch** and **What's new** ([Claude Code's updates](../updates/claude-codes-updates.md)), and the signed-in account with **Sign in** and **Sign out…** ([Your Claude sign-in](../accounts/your-claude-sign-in.md)).

## Accounts

This page is this machine's own. None of it syncs, and a tab can't change it for itself. Accounts and their keys are on the [Accounts](../accounts/index.md) pages.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **New folders use** | Accounts | Default | [A folder's account](../accounts/a-folders-account.md) |
| An account's name and tag color | Accounts | Chosen when you add it | [API-key accounts](../accounts/api-key-accounts.md) |
| **Warn when today's estimated spend passes ($)** and **Warn when this month's estimated spend passes ($)** | Accounts | No warning | [Spend and its warnings](../accounts/spend-and-its-warnings.md) |
| **Change key…** and **Remove…** | Accounts | | [API-key accounts](../accounts/api-key-accounts.md) |
| **Add an API key**: a **Name** and the key | Add an API key | No accounts besides Default | [API-key accounts](../accounts/api-key-accounts.md) |

## Tabs

What each new tab starts with, and the folders you open them in.

| Setting | Section | Default | Syncs | Per tab | Explained in |
|---|---|---|---|---|---|
| **Model** | New tabs start with | Claude Code's default | Yes | Yes (**Model**) | [Model, effort and output style](../composer/model-effort-and-output-style.md) |
| **Effort** | New tabs start with | The model's default | Yes | Yes (**Effort**) | [Model, effort and output style](../composer/model-effort-and-output-style.md) |
| **Permission mode** | New tabs start with | Claude Code's default, named with the mode it gives | Yes | Yes (**Permission mode**) | [Permission modes](../permissions/permission-modes.md) |
| **Subagents** | New tabs start with | Claude decides | No | Yes (**Subagents**) | [Subagents](../conversation/subagents.md#discouraging-or-turning-off-subagents) |
| **Continue tasks when a usage limit resets** | At a usage limit | On | Yes | Yes | [When a limit stops a task](../usage/when-a-limit-stops-a-task.md) |
| **Use usage credits once a limit is used up** | At a usage limit | On | Yes | Yes (**Use usage credits**, and the tab's menu) | [Tabs that don't use usage credits](../usage/tabs-that-dont-use-usage-credits.md) |
| **Check in on long turns** | Check-ins on long turns | On | Yes | Yes (**Use different check-ins for this tab**) | [Check-ins on long turns](../composer/check-ins-on-long-turns.md) |
| **After the turn has run (minutes)** | Check-ins on long turns | 15 (0 turns it off) | Yes | Yes | [Check-ins on long turns](../composer/check-ins-on-long-turns.md) |
| **After no output for (minutes)** | Check-ins on long turns | 5 (0 turns it off) | Yes | Yes | [Check-ins on long turns](../composer/check-ins-on-long-turns.md) |
| **Message** | Check-ins on long turns | "Everything OK? Give me a one or two sentence status update: what you're doing, and whether you're stuck or waiting on something." | Yes | Yes | [Check-ins on long turns](../composer/check-ins-on-long-turns.md) |
| **Notify me when a check-in is sent** | Check-ins on long turns | Off | Yes | Yes | [Check-ins on long turns](../composer/check-ins-on-long-turns.md) |
| **Show the process monitor** | Process monitor | Off | Yes | Yes | [Processes](../tabs/processes.md) |
| **Refresh every (seconds)** | Process monitor | 2 (from 1 to 60) | Yes | No | [Processes](../tabs/processes.md) |
| **Show command lines** | Process monitor | On | Yes | No | [Processes](../tabs/processes.md) |
| **Recent folders to keep** | New tab picker | 20 (from 1 to 100) | Yes | No | [Opening tabs](../tabs/opening-tabs.md) |
| **Clear recent folders** | New tab picker | | No | No | [Opening tabs](../tabs/opening-tabs.md) |
| **Favorite folders**, with **Add folder…** | New tab picker | None | No | No | [Opening tabs](../tabs/opening-tabs.md) |
| **Show Claude Code's projects in the sidebar** | Claude Code's projects | Off | No | No | [Folder groups](../tabs/folder-groups.md#claude-codes-projects) |

Your favorite and recent folders belong to this machine. They stay put when settings sync is on, and **Reset to defaults** leaves them.

## Usage

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **Warn at (% of the session used)** | Warnings | 75 | Yes | [The usage header](../usage/the-usage-header.md) |
| **Alert at (% of the session used)** | Warnings | 90 | Yes | [The usage header](../usage/the-usage-header.md) |
| **Burn rate window (minutes)** | Warnings | 30 (from 5 to 300) | Yes | [The usage header](../usage/the-usage-header.md) |
| **Show model-specific weekly limits** | The usage header | On | Yes | [The usage header](../usage/the-usage-header.md) |
| **If Claude Code stops reporting them, read them from /usage** | The usage header | Off | Yes | [The usage header](../usage/the-usage-header.md) |
| **Detailed usage header** | The usage header | Off | No | [The detailed header](../usage/the-detailed-header.md) |
| **Keep usage history**, with **Clear…** | History | 1 month (or 1 day, 1 week, 1 year or Forever) | Yes | [Usage history](../usage/usage-history.md) |
| **Share usage with my other machines** | History | Off | Yes | [Sharing usage across machines](../usage/sharing-usage-across-machines.md) |
| **Clear usage history on every machine…** (while sharing is on) | History | | No | [Sharing usage across machines](../usage/sharing-usage-across-machines.md) |

**Keep usage history** isn't put back by **Reset to defaults**.

## Advanced

None of this page syncs.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Extra arguments for every claude process** | Running claude | Empty | [Advanced](advanced.md#extra-arguments-for-claude) |
| **Log protocol traffic**, with **Open log folder** | Running claude | Off | [Advanced](advanced.md#log-protocol-traffic) |
| **Claudette's data folder**, with **Open data folder** | Running claude | | [Advanced](advanced.md#the-log-and-data-folders) |
| What Claude Code sent that Claudette doesn't know, with **Copy diagnostics** | Diagnostics | | [Advanced](advanced.md#diagnostics) |

## Sessions

None of this page syncs.

| Setting | Section | Default | Per tab | Explained in |
|---|---|---|---|---|
| **Folder**, with **Browse…**, **Move library…** and **Use the default folder** | Session library | A folder in Claudette's data folder | No | [The session library](../history/the-session-library.md) |
| **Sync new tabs to the session library** | Session library | Off | Own switch (**Sync to other machines**) | [Syncing a tab](../history/syncing-a-tab.md) |
| **Name for this machine** | Session library | The computer's name | No | [The session library](../history/the-session-library.md) |
| **Delete sessions not used for a while**, with **After going unused for (days)** | Session library | On, after 15 days | No | [The session library](../history/the-session-library.md#keeping-sessions) |
| **Sync Claudette's settings through the library** | Settings sync | Off | No | [Settings sync](../history/settings-sync.md) |

The library's **Folder** and **Sync Claudette's settings through the library** aren't put back by **Reset to defaults**.

## Files and diffs

None of this page syncs, and a tab can't change it for itself.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Open files in** | Opening files | Claudette's editor, at the line named (or the app the OS uses) | [Opening files from the conversation](../project-files/opening-files-from-the-conversation.md) |
| **Show a file clicked in a conversation** | Opening files | Beside the conversation (or In its place) | [Opening files from the conversation](../project-files/opening-files-from-the-conversation.md) |
| **Open images in** | Opening files | Claudette's image viewer (or the app the OS uses) | [Images](../conversation/images.md#opening-an-image-claude-names) |
| **The explorer is on the** | Project Files tab | Left | [The explorer](../project-files/the-explorer.md) |
| **Wrap long lines** | Project Files tab | Off | [Editing](../project-files/editing.md) |
| **Indent new lines with** | Project Files tab | 4 spaces (or 2 spaces, 8 spaces or Tabs) | [Editing](../project-files/editing.md) |
| **Diff tool**: the built-in view, a tool installed here, or a custom command | Diff tool | Built-in diff view | [External diff tools](../changes/external-diff-tools.md) |
| **Command** (for a custom command), with **Test** | Diff tool | Empty | [External diff tools](../changes/external-diff-tools.md) |
| **Open in the diff window** | Diff tool | Opens the file, as Open files in says (or in the app the OS uses, or the changes in the diff tool) | [The diff view](../changes/the-diff-view.md#opening-the-file) |

## All tools

The first page under **Project tools**: what every kind of project shares. None of this page syncs, nor do the project types' pages after it, since program paths and the editors you have installed differ from one machine to the next.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Open solutions with** | Solutions | The app the OS uses for it (or Rider, Visual Studio, VS Code or another program) | [Unreal Engine](../project-tools/unreal-engine.md) |
| **Show editor logs under the tab** | Editor logs | On | [Editor logs](../project-tools/editor-logs.md) |

## Unreal Engine

Each kind of project Claudette knows has a page, which starts with two switches under **Projects**: **Recognize … projects** (off, Claudette treats its projects as plain folders, and their own actions and links still work; the sidebar says *Off* beside the page) and **Tell Claude about … projects**. Its own settings are under **Defaults**. None of them syncs.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Recognize Unreal Engine projects** | Projects | On | [Unreal Engine](../project-tools/unreal-engine.md) |
| **Tell Claude about Unreal Engine projects** | Projects | On | [Unreal Engine](../project-tools/unreal-engine.md) |
| **Default editor configuration** | Defaults | Development (or DebugGame) | [Unreal Engine](../project-tools/unreal-engine.md) |
| **Project files for** | Defaults | The OS's own: Visual Studio on Windows, Xcode on macOS, VS Code on Linux | [Unreal Engine](../project-tools/unreal-engine.md) |

## Unity

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Recognize Unity projects** | Projects | On | [Unity](../project-tools/unity.md) |
| **Tell Claude about Unity projects** | Projects | On | [Unity](../project-tools/unity.md) |
| **Default code optimization** | Defaults | Release (or Debug) | [Unity](../project-tools/unity.md) |

## Godot

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Recognize Godot projects** | Projects | On | [Godot](../project-tools/godot.md) |
| **Tell Claude about Godot projects** | Projects | On | [Godot](../project-tools/godot.md) |
| **Godot executable**, with **Browse…** and **Detect** | Defaults | Found automatically | [Godot](../project-tools/godot.md) |

## Git

The first page under **Source control**. A tab can't change any of it for itself.

| Setting | Section | Default | Syncs | Explained in |
|---|---|---|---|---|
| **Show the git branch** | Tab rows | On | Yes | [Styles and themes](styles-and-themes.md#what-the-tab-rows-show) |
| **Remove worktrees once everything in them is merged** | Worktrees | Off | No | [Worktree tabs](../tabs/worktree-tabs.md) |
| **Remove worktrees no tab has used for** | Worktrees | Never (or 7, 14, 30 or 90 days), with **Clean up now** | No | [Worktree tabs](../tabs/worktree-tabs.md) |
| **Path to git**, with **Browse…** | Git | Empty: the first git on your `PATH` | No | [Git](git.md) |
| **New Files tabs show** | Project Files tab | Claude's changes (or Changes against git HEAD) | No | [Claude's changes in the editor](../project-files/claudes-changes-in-the-editor.md#what-the-changes-are-against) |
| **Files git ignores** | Project Files tab | Dimmed (or Hidden) | No | [The explorer](../project-files/the-explorer.md) |

## Perforce

The second page under **Source control**. None of this page syncs.

| Setting | Section | Default | Explained in |
|---|---|---|---|
| **Keep Perforce logins fresh** | Logins | Off | [Perforce](../project-tools/perforce.md) |
| **Password source** | Logins | Stored by Claudette (recommended) | [Perforce](../project-tools/perforce.md) |
| **Renew the ticket when less is left than (minutes)** | Logins | 30 | [Perforce](../project-tools/perforce.md) |
| **Request tickets valid on all hosts** | Logins | Off | [Perforce](../project-tools/perforce.md) |
| **Show changelist on tabs** | Logins | Off | [Perforce](../project-tools/perforce.md) |
| **Check out files before they're changed** | Opening files in Perforce | On | [Perforce](../project-tools/perforce.md#opening-files-in-perforce) |
| **Add new files** | Opening files in Perforce | On | [Perforce](../project-tools/perforce.md#opening-files-in-perforce) |
| **Mark deleted files for delete** | Opening files in Perforce | On | [Perforce](../project-tools/perforce.md#opening-files-in-perforce) |
| **Rename with p4 move** | Opening files in Perforce | On | [Perforce](../project-tools/perforce.md#opening-files-in-perforce) |
| **Show Perforce state in the Files tab** | Opening files in Perforce | On | [Perforce](../project-tools/perforce.md#in-the-files-tab) |
| A password for a server and user, with **Save password** and **Forget password** | Stored password | None | [Perforce](../project-tools/perforce.md) |
| Each folder's server and user, with **Add folder…** | Per-folder server and user | None | [Perforce](../project-tools/perforce.md) |

## The project's pages

The **Links**, **Actions** and **Tools** pages at the end of the sidebar aren't in this list. They belong to a project rather than to Claudette: [the Settings window](the-settings-window.md#the-projects-pages) says how they work, and [Actions and links](../project-tools/actions-and-links.md) what they hold.
