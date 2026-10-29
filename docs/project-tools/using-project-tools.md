---
title: Using project tools
parent: Project tools
nav_order: 1
summary: "A tab can do things for the project in its folder: launch the editor, generate project files, build, open the solution."
---

# Using project tools

A tab can do things for the project in its folder: launch the editor, generate project files, build, open the solution. Which things you get depends on the project. Claudette knows [Unreal Engine](unreal-engine.md), [Unity](unity.md) and [Godot](godot.md) projects, and a folder can have its own [actions and links](actions-and-links.md). [Perforce](perforce.md) has a page of its own.

{% include shot.html name="guide/project-tools/project-page" alt="The Project tool window for the Unreal project, with the project's details." %}

## The project's icon

Each folder's group in the sidebar has its project's icon on its row, before the **Project files** button: its kind's icon (Unreal Engine's logo, a cube for Unity, Godot's logo for Godot). Its tooltip has the project's name, version and kind, such as *NightOwl · UE 5.4 (Unreal Engine)*, and the main action's shortcut. It's there for every group, whatever the folder holds, so you can add a first action or link from it. When no project is recognized, it's a folder icon, and its tooltip has the folder's path.

It's for the group's tab you used last: that tab runs its jobs, and while one runs a busy dot replaces the icon and the job's name leads its tooltip. When the sidebar is collapsed to its rail, the icon is under the group's color.

Click the icon to open its menu. From the top, it has:

- A header with the project's name and kind, the engine's version and folder, or what's wrong ("The engine for EngineAssociation "5.9" wasn't found on this machine"). Without a project, it shows the folder's name and path.
- Notes about entries in `claudette.json` that were skipped, and why.
- **Projects in this folder**, when there are several, as radio items. Your pick is remembered for that folder on this machine.
- The project's actions.
- The project's choice, such as Unreal's **Editor configuration**.
- The project's fix, such as **Choose engine folder…**.
- Your own actions, then your **Links**. See [Actions and links](actions-and-links.md).
- **Show in Explorer** (**Show in Finder** on macOS, **Show in file manager** on Linux), which opens the project's folder, **Show output…**, which shows the newest run's log once an action has run, **Add an action…**, **Add a link…** and **Refresh**.

An item that's greyed out says why in its tooltip, for example "Generate project files first." The menu looks at the project's files again each time it opens, so it's current. **Refresh** does the same on demand.

## How a project is found

When a tab opens, when its folder changes, when the menu opens and on **Refresh**, Claudette looks for a project in the tab's folder, in its subfolders two levels down (for `Game/NightOwl/NightOwl.uproject`), and in the parent folders up to the repository's root. It skips generated and hidden folders such as `Intermediate`, `Library`, `node_modules` and `bin`. With several projects, the nearest comes first, and the menu's **Projects in this folder** lets you pick another.

## Running an action

An action is one of four kinds:

- **Launch** starts a program that keeps running on its own, such as the editor. Claudette doesn't track it.
- **Run** is a job, such as a build. It gets a run under the tab's row, with its own log.
- **Open** opens a file or folder in the OS's app, or a solution in the IDE you chose in Settings.
- **Destructive** actions, such as deleting folders or ending editors, ask you first.

One job runs at a time per tab. The other jobs stay greyed out until it ends. Jobs and launches get your own environment, so a build finds the same tools a terminal does ([Command line and environment](../reference/command-line-and-environment.md)).

## Runs

Each job is a run. It shows under its tab's row in the sidebar, newest last, with a glyph for how it's going: the busy dot while it runs, a check mark when it succeeded, a red cross when it failed and a muted square when you stopped it. Under its name, it says how long it has run (*Running · 1m 05s*) or how it ended (*Failed · exit code 6 · 14:32*).

- Click a run to show its log where the conversation is (below). Starting a run doesn't take you away from the conversation.
- A running one has **Stop** in the place of the × button, so a stray click never ends a build. A finished one has ×, which takes the run and its log away.
- Right-click a run for **Open beside**, **Close pane**, **Copy output**, **Stop** while it runs, and **Close** once it's done.
- Runs stay until you close them, whatever the result. Closing the tab takes its runs away, and they aren't saved when Claudette quits or restarts.
- Launch and Open actions start a program with no output, so they don't make runs.

## A run's log

A run's log shows in the main area, in the conversation's place, without the message box, so a build's errors have room. Click the tab's own row to go back to the conversation.

- At the top: how the run went (running, succeeded, failed with its exit code, or stopped), when it started and how long it took, with **Stay current** and **Wrap lines** (below), **Stop**, which ends the job and every process it started, **Copy**, which copies the whole log, and **Close** once it's done.
- Under it, the output, following the newest line as it comes. A run keeps its last 5,000 lines, and the log says how many older ones were dropped.
- Select text and copy it as anywhere else, and `Ctrl/Cmd+F` finds in the log.
- **Links.** Web addresses open in your browser. Files and folders the log names open with a click, as files Claude names do: in Claudette's [editor](../project-files/opening-files-from-the-conversation.md) at the line an error points to, such as `Source/Player.cpp(42,7)` or `src/main.rs:42:7`, or in the OS's app, as Settings → Files and diffs says. A path is a link only once Claudette finds it, from the folder the action ran in or the tab's, so text that only looks like a path stays plain. A program or script shows in its folder rather than running.
- To see the log and the conversation at once, right-click the run and pick **Open beside**, or drag the run onto the conversation. See [Split panes](../tabs/split-panes.md).
- `Ctrl/Cmd+W` closes a finished run's log. On a running one, it goes back to the tab, so it never stops a build.

### Stay current and Wrap lines

- **Stay current** keeps the newest line in view as the output comes. Scroll up to read something and it turns off, so the log holds still; scroll back to the end, or click it, and it follows again.
- **Wrap lines** wraps long lines at the width of the pane. Turn it off and they stay on one line, and the log scrolls sideways. The next log you open, a run's or an [editor's](editor-logs.md), starts the way you left it.

## The Project page

The **Project** tool window, beside Changed files, Agents and Processes, shows the project's details: its file, the engine's version, folder and kind, the editor target and the configuration. It also lists entries of your project files that were skipped. The runs' logs are in the main area.

The page's tab shows a busy dot while a job runs. It's there when the tab has project tools: a project, actions of your own, or skipped entries. A running job's processes also show on the [Processes](../tabs/processes.md) page when the process monitor is on. Closing the tab stops a running job along with its other processes.

## Run the project's main action

`Ctrl/Cmd+Shift+E` runs the project's main action: **Launch editor** for Unreal, **Open in Unity** for Unity, **Open in Godot** for Godot, and otherwise the folder's first action of your own. You can rebind it in Settings → Keyboard.

## Telling Claude about the project

For Unreal, Unity and Godot projects, Claudette adds a short note to the session when a tab starts: the engine's version and folder, how to build or check the project without the editor, and not to open the editor unless asked. Each engine has its own switch on its page in Settings → Project tools, such as **Tell Claude about Unreal Engine projects**. The tab's [info card](../tabs/the-tab-info-card.md) has a **Project** row that says whether Claude was told.

## A project action finishes

When a job finishes or fails while Claudette isn't in front, a notification says so: *"Build editor failed (exit code 6)."* A job you stopped doesn't notify. Click it to see that run's log. The switch is Settings → Notifications → **A project action finishes**, on by default. See [Notifications](../away/notifications.md).

## Settings

**Project tools**, a group of pages in the Settings sidebar, holds the defaults every project starts with. They stay on this machine and don't sync.

{% include shot.html name="guide/settings/project-tools" alt="Settings on Unreal Engine's page in the Project tools group, under All tools: its Recognize and Tell Claude switches under Projects, then its default editor configuration and project files under Defaults" %}

- **All tools** has what every kind of project shares:
  - **Solutions** → **Open solutions with**: the IDE a solution opens in. See [Unreal Engine](unreal-engine.md#open-solution-and-open-in-rider).
  - **Editor logs** → **Show editor logs under the tab**: the rows for the logs the editor writes. See [Editor logs](editor-logs.md).
- **Unreal Engine**, **Unity** and **Godot** have a page each, with the engine's icon. Each starts with **Recognize Unreal Engine projects** (or Unity, or Godot), on by default. Turn it off and Claudette treats that engine's projects as plain folders: the project row shows the folder, with no engine actions, editor logs or note to Claude, though the folder's own actions and links still work. The sidebar then says *Off* beside the engine's page, which you can still open to switch it back on. The rest of each page is explained on its engine's page in this guide:
  - **Unreal Engine**: **Tell Claude about Unreal Engine projects**, then under **Defaults**, **Default editor configuration** and **Project files for**. See [Unreal Engine](unreal-engine.md).
  - **Unity**: **Tell Claude about Unity projects**, then **Default code optimization**. See [Unity](unity.md).
  - **Godot**: **Tell Claude about Godot projects**, then **Godot executable**. See [Godot](godot.md).
- **Perforce** is the last page in the group. See [Perforce](perforce.md).

After the pages in the Settings window, a group headed by the selected tab's project has three pages: **Links**, **Actions** and **Tools**. The group follows the tab selected in the main window, even while Settings stays open, and each page says which tab in a line at the top.

The **Tools** page shows this machine's choices for the tab's project, the same ones the project's menu makes:

- The project, when the folder has several.
- The project's own choice: Unreal's editor configuration, or Unity's code optimization.
- What you picked with **Choose…**: Unreal's engine folder, the Unity editor or the Godot executable. It shows what's in use now, and whether you chose it or Claudette found it. **Clear** forgets your pick so Claudette looks again.

At its foot, **Open Unreal Engine** (or Unity, or Godot) shows the defaults on that engine's page in Project tools. Without a project, the page says "No project Claudette knows in this folder" and links to **All tools**. These choices are remembered on this machine, keyed by the project, and don't sync. No **Reset to defaults** in Project tools touches them.

## See also

- [Actions and links](actions-and-links.md) for your own commands and web pages.
- [The sidebar](../tabs/the-sidebar.md) for the rest of a tab's row.
- [The Settings window](../settings/the-settings-window.md) for how the project's pages sit in it.
