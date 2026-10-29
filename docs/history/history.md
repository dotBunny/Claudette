---
title: History
parent: History and syncing
nav_order: 1
summary: "History lists your past sessions, grouped by project, so you can pick one up again in a new tab."
---

# History

History lists your past sessions, grouped by project, so you can pick one up again in a new tab. It shows the sessions Claude Code kept on this computer, including ones you started in a terminal, and the sessions in the [session library](the-session-library.md), which can come from your other machines.

{% include shot.html name="history-standard" alt="History narrowed to the starfall project by its chip, listing that project's sessions with their first prompts and details." %}

## Opening History

- Click the clock at the top of the sidebar, beside **New tab**.
- Press `Ctrl/Cmd+Shift+H`.
- Choose **Open from History…** in the command palette or the new tab picker.
- Choose **History for this folder** in a folder group's menu. History opens on that project's sessions only.

**Close**, `Esc` or a click outside History closes it without opening anything.

## What a row shows

Each session shows its name (after its mark, if it has one), the first prompt, and a line of details: which machine used it last (**This machine**, or the other machine's name), how long ago, how many messages, its git branch, how much it takes in the session library if it's there (*312 MB in the library*, see [Syncing a tab](syncing-a-tab.md#how-much-it-takes)), and *open in a tab* if it's open. Sessions are grouped under their project's folder, the one you used most recently first.

A session that Claude Code ran in a [worktree](../tabs/worktree-tabs.md) is listed under its repository's folder, as its tab is in the sidebar. Its details say *worktree brisk-otter*, and show its branch too when the branch isn't the worktree's own.

With thousands of sessions, History still opens quickly. It shows sessions as it reads them, so the top of the list is there before the rest. **Refresh** reads them again and keeps the list where it is until the new one is ready.

## Narrowing to a project

A row of chips under the search box lists one project's sessions at a time: **All**, then the five folders you used most recently, then **+N more**, which opens a list of the rest with their paths.

- Each chip shows how many of its sessions match your search, and the chips keep their order as you type.
- Pick a chip to list only that project. Pick it again, or **All**, to list every project.
- A project chosen from **+N more** takes the last chip's place while it's selected, so the selection is always visible.
- History opens on **All**, except from **History for this folder**, which opens on that folder's project. If it has no sessions, History says so.

## Searching

Type words in the search box. A session matches when every word is in its name, its folder or any of its prompts, not just the first one. The list follows when you stop typing.

**Search Claude's replies too**, the button under the results (or `Enter` in the search box), also looks through what Claude said in the sessions your words didn't match. It reads each transcript, so it takes longer. Sessions appear as they're found, each with the start of the reply that matched after *Claude:*. A session matches when every word is in its name, prompts, folder or Claude's replies. Subagents' messages and tool calls don't count. Typing again, or **Refresh**, drops those results.

## Opening a session

Click a session to resume it in a new tab, with the earlier conversation loaded so you can scroll back through it. If the session is already open in a tab, History selects that tab instead.

- The tab gets the session's model, effort, permission mode, check-in and process monitor choices back, and the changed files you had ticked as [reviewed](../changes/reviewed-files.md).
- A session that came from the library, or that has a library record, opens with [syncing](syncing-a-tab.md) on. A session that only ever lived on this machine opens with it off.
- If another machine is using the session right now, or this machine can't tell where its project is, History asks first. [Picking up on another machine](picking-up-on-another-machine.md) explains each question.

## Old sessions

Claude Code deletes its own transcripts after 30 days by default, and History can only list what's left. The library's copies aren't affected by that cleanup, so a session that syncs stays in History until it has gone unused for as long as [Delete sessions not used for a while](the-session-library.md#keeping-sessions) says: 15 days, unless you change it. A tab that doesn't sync has no copy once Claude Code has cleaned up its transcript.

## Shortcuts

| Action | Shortcut |
|---|---|
| Open from History | `Ctrl/Cmd+Shift+H` |
| Close History | `Esc` |
| Search Claude's replies too | `Enter` in the search box |

You can change the first in Settings → Keyboard.

## See also

- [Closing and restoring](../tabs/closing-and-restoring.md) for the tabs that come back by themselves when Claudette starts. History is for the ones that don't.
- [The session library](the-session-library.md) for what History adds from other machines.
