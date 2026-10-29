---
title: Editor logs
parent: Project tools
nav_order: 5
summary: "While the project's editor runs, its log is a row under the tab, read as it's written, and it stays after the editor closes."
---

# Editor logs

While the Unreal editor, Unity or Godot has a tab's project open, the log it writes is a row under the tab. Click the row to read the log where the conversation is, as it's written, with its errors and warnings colored, Unreal's lines in tidy columns, and a filter to show only the levels, categories or text you want. The row stays after the editor closes, so you can read what happened, and it picks up the new log when the editor starts again. Claudette only opens the file for a moment each time it reads, so the editor can write, rename or delete it as it always does.

{% include shot.html name="guide/project-tools/editor-log" alt="The sidebar with Starfall.log under the Grappling hook tab, and the Unreal editor's log in the main area: Stay current and Wrap lines beside Open file, a chip for each level with its count, Categories and Filter lines, then the log in columns of time, category, verbosity and message, with an error in red and warnings in amber." %}

## The row

The row sits under the tab's row, after its subagents and before its project runs. It has:

- The log icon, in the accent color while the editor runs and muted once it has closed or moved its log, or a red ✕ when the log says it crashed.
- The file's name, such as `NightOwl.log`.
- Under it, what's writing it and how many errors and warnings it has so far, such as *Unreal editor · 2 errors · 14 warnings*. Once the editor has closed it says when: *Closed · 15:02 · 2 errors*, or *Crashed · 15:02* in red. When Unity has moved its log to the project's, it says where: *Moved to Logs/Editor.log* (see [below](#when-unity-moves-its-log)).

Hover the row for its status and the file's path. Right-click it for **Open beside**, **Close pane**, **Copy log**, **Open file**, **Show in Explorer** (**Show in Finder** on macOS, **Show in file manager** on Linux) and **Close**.

A commandlet, a game run with `-game`, or a Unity run in batch mode gets a row of its own too, for the log it writes. With the editor open, an Unreal commandlet writes `NightOwl_2.log`, so the tab has two rows.

## Reading the log

Click the row to show the log in the focused pane, as a project run's log is. Over it are the file's name, what's writing it (*Unreal editor running since 14:30 (PID 4412).*), the counts, the file's path, and these buttons:

- **Stay current** keeps the newest line in view as the editor writes. Scroll up to read something and it turns off, so the log holds still; scroll back to the end, or click it, and it follows again.
- **Wrap lines** wraps long lines at the width of the pane. Turn it off and they stay on one line, and the log scrolls sideways. The next log you open starts the way you left it.
- **Open file** opens the log in Claudette's editor or the OS's app, as Settings → Files and diffs says.
- **Copy** copies the lines the filter shows, as the file has them.
- **Close** closes it. See [below](#closing-it).

Under them is the log's [filter](#filtering-the-log), then the log. Error lines are in red and warnings in amber, as the engine marks them: Unreal's `Error:` and `Warning:`, Unity's compiler errors and exceptions, and Godot's `ERROR:` and `WARNING:`. A message's other lines, such as a stack trace under an error, go with it, and stack traces are dimmed. Paths and web addresses are links, as in a run's log. `Ctrl/Cmd+F` finds in it.

Unreal's lines start `[2026.10.20-19.01.22:123][  0]LogBlueprint: Error:`: the time, the frame number, the log category and the verbosity. Claudette shows them in columns: the time in your computer's clock (Unreal writes it in UTC), then the category, the verbosity and the message, each lined up under the one before. The frame number is left out. Only how they look changes: select, copy or find, and you get the line as the file has it.

Claudette keeps the last 20,000 lines. A longer log says so over it, and the rest is in the file, which **Open file** opens.

To keep the log beside the conversation, right-click its row and choose **Open beside**, or drag the row onto the conversation. See [Split panes](../tabs/split-panes.md).

## Filtering the log

The bar under the log's header narrows what it shows:

- **A chip for each level**, with how many of them the log has. Unreal's are **Errors**, **Warnings**, **Display**, **Log** and **Verbose**; Unity's and Godot's are **Errors**, **Warnings** and **Messages**. Click a chip to hide its lines, and again to show them.
- **Categories ▾**, in an Unreal log: every log category it has, with how many lines each has. Untick one to hide it, or type in **Find a category** to look for one. **Hide all**, then ticking one, shows only that one. While some are hidden, the button says how many are shown, such as *Categories (38 of 41)*.
- **Filter lines**: type, and only the lines with that text in them are shown, whatever their case. Click **×** in the box to clear it.

While anything is hidden, the bar says how much is shown, such as *Showing 1,204 of 20,000 lines*.

The levels and categories you hide belong to the project: Claudette remembers them, and the project's next log, such as a commandlet's, starts with them hidden too. Show them all again and they're forgotten. What you type in **Filter lines** is only for that log.

## When the editor closes, crashes or starts again

- **It closes:** the row stays, with the whole log, and says *Closed*.
- **It crashes:** Unreal's `=== Critical error: ===`, Unity's `Crash!!!` and Godot's crash handler are in the log, so the row says *Crashed* in red.
- **It starts again:** Unreal keeps the old log as a backup (`NightOwl-backup-2026.10.20-15.30.00.log`) and starts a new one. The same row comes back with the new log from its first line.

## When Unity moves its log

From Unity 6.5, the editor writes Unity's own `Editor.log`, the one every project shares, only while it starts. Once it has the project open, it carries on in the project's `Logs/Editor.log`, and writes in the shared one that it has moved, and where to. Claudette follows it there:

- The project's `Logs/Editor.log` gets a row of its own under the same tab, read from its first line, so it has what the editor wrote as it started too.
- The shared log's row stops where the editor said it moved, so other editors starting later don't add their lines to it. It says *Moved to Logs/Editor.log*, and over its log, *Unity editor moved its log to Logs/Editor.log at 14:31.*
- When the editor closes, the shared log's row goes, and the project's row stays, *Closed*, with the whole log.

If the editor already had the project open when Claudette started, the project's log is the only row. **Use Global Editor Log** in Unity's preferences, or starting Unity with `-useGlobalLog`, keeps everything in the shared log, and its row carries on as before.

## Closing it

Click **×** on the row, **Close** over the log, or press `Ctrl/Cmd+W` while it has the focus.

- While the editor is running, the row goes until the editor next starts, when it comes back with the new log.
- Once the editor has closed, the row goes for good. The next time the editor starts, it gets a new row.

Logs aren't saved with your tabs. If the editor is still running when Claudette starts again, its row comes back within a few seconds.

## Which tab it's under

A log is under one tab, even when several tabs have the project:

1. The tab that started it, as when Claude runs a commandlet there.
2. Otherwise the tab whose **Launch editor** (or **Open in Unity**, **Open in Godot**) started the editor.
3. Otherwise the project's first tab in the sidebar, so a thread's log is under the thread rather than its sub-threads.

If you close that tab, the log moves to the next of the project's tabs, keeping its lines. Once no tab has the project, it goes.

When a tab is [collapsed](../tabs/the-sidebar.md#collapsing-a-tab), its logs fold away with its other rows. The collapsed row's badge shows a red ✕ when a log has crashed, or has errors you haven't looked at yet.

## Which logs

| Engine | What gets a row | The log |
|---|---|---|
| Unreal Engine | The editor, `UnrealEditor-Cmd` and commandlets (`-run=`), `-game` and `-server` runs, with the project's `.uproject` on their command line. | `Saved/Logs/<project>.log`, or `<project>_2.log` and on while another has that one, or the file `-abslog=` or `LOG=` names. |
| Unity | The editor and batch mode runs with the project after `-projectPath`, or a relative path such as `.` from the project's folder. | Unity's `Editor.log` (the one **Open Editor log** opens), and from Unity 6.5 the project's `Logs/Editor.log` once the editor has the project open, or the file `-logFile` names. Claudette's own **Run EditMode tests** writes its output to the run instead. |
| Godot | The editor and the game with the project after `--path`, or a relative path such as `.` from the project's folder. | `logs/godot.log` in the project's user data folder, or wherever the project's file logging settings or `--log-file` say. |

Claudette looks at which programs are running every couple of seconds (every five on macOS), so a row appears a moment after the editor starts.

## Settings

In Settings → Project tools → All tools, on this machine only.

| Setting | Default | What it does |
|---|---|---|
| **Show editor logs under the tab** | On | Shows the rows. Off, Claudette doesn't look for editors or read their logs, and the rows showing go. |

## Shortcuts

| Shortcut | What it does |
|---|---|
| `Ctrl/Cmd+F` | Finds in the log that has the focus, among the lines its filter shows. |
| `Ctrl/Cmd+W` | Closes the log that has the focus. |

## See also

- [Using project tools](using-project-tools.md) for a project run's log, which is the output of a job Claudette runs.
- [Unreal Engine](unreal-engine.md), [Unity](unity.md) and [Godot](godot.md) for each engine's actions, among them opening its log in another app.
- [The sidebar](../tabs/the-sidebar.md) for the rest of a tab's row.
