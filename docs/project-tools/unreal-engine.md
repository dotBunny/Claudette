---
title: Unreal Engine
parent: Project tools
nav_order: 2
summary: "An Unreal Engine project gets a row with its name and engine, and actions to launch the editor, generate project files and build."
---

<h1 class="unreal-title">{% include unreal-logo.html full=true %}</h1>

When a tab's folder holds an Unreal Engine project (a `.uproject` file), the [project's icon](using-project-tools.md) on its group's row is Unreal Engine's logo, with its name and engine in its tooltip, such as `NightOwl · UE 5.4`, and its menu has the actions below. Unreal Engine 4 projects work too.

## The engine

Claudette looks for the engine the project uses, in this order:

1. The folder you picked with **Choose engine folder…** (**Choose another engine folder…** once one is found). It must hold `Engine/Build/Build.version`, though you can pick the `Engine` folder itself. Claudette remembers the pick for the project.
2. For a project with no engine named, or an empty name, an engine in a folder above the project.
3. For a project that names a version such as `5.4`, the Epic Games Launcher's install of it. On Linux there's no launcher.
4. For anything else, usually a long ID, a build registered with Unreal's version selector.

The menu's header says which engine it found, its version, folder and kind: launcher install, source build, installed build, engine in a parent folder, or chosen by you. When none is found, it says so, the engine's actions are greyed out ("The engine wasn't found: choose its folder in this menu.") and **Choose engine folder…** is offered.

## Actions

| Action | What it does |
|---|---|
| **Launch editor** | Opens the project in the editor. It's the main action, and it's greyed out until the editor is built. |
| **Generate project files** | A job that makes the project files for the IDE you chose. Greyed out for a project with no C++ code. |
| **Build editor** | A job that builds the project's editor target. |
| **Build and launch** | Builds the editor, then launches it if the build succeeded. |
| **Open solution** | Opens the project's solution. Greyed out with "Generate project files first." until it exists. With Rider chosen, it's **Open in Rider** instead (below). |
| **Open latest log** | Opens the project's latest log in another app, once the editor has made one. While the editor runs, its log is also a row under the tab: see [Editor logs](editor-logs.md). |
| **Clean intermediates…** | Deletes generated folders. It asks first. |
| **End all Unreal editors…** | Ends every running Unreal editor. It asks first. |

The project's menu also has **Editor configuration**, with **Development** and **DebugGame** as radio items. DebugGame loads the project's DebugGame modules, and the labels say so: **Launch editor (DebugGame)**, **Build editor (DebugGame)**. A project's own choice is remembered on this machine, and the default for projects that haven't one is Settings → Project tools → Unreal Engine → **Default editor configuration**.

## Open solution and Open in Rider

**Open solution** opens the Visual Studio solution, the Xcode workspace or the VS Code workspace, whichever the **Project files for** setting says Generate project files makes. It opens it with the OS's app, or the IDE you chose in **Open solutions with**.

With **Open solutions with** set to Rider, the action reads **Open in Rider** and opens the `.uproject` itself, on every OS. Rider has its own Unreal project model, so there are no project files to generate first, and none to regenerate when you add source files. Rider reads a `.uproject` from Unreal Engine 4.25.4 on Windows and 4.26 on macOS and Linux. With an older engine, the action stays **Open solution**.

If Rider can't be found, a note says so instead of letting the OS open the project in the Unreal editor. Choose **Another program…** in Settings → Project tools → All tools → **Open solutions with** to point at it.

## The ones that ask first

- **Clean intermediates…** deletes `Binaries` and `Intermediate` in the project and in each plugin under `Plugins/`, and nothing else: not `Saved`, `Content` or `Config`. The question lists the folders and their total size, and warns when an editor seems to have the project open. **Delete** runs it as a job with its own run and log.
- **End all Unreal editors…** ends every Unreal editor running on this machine, not just this project's, along with the processes each started, such as shader compilers. The question lists each with its PID and, when it can tell, its project. **End it** or **End them** ends them at once, and unsaved work in them is lost. The action is greyed out with "No Unreal editor is running." when none is.

## Settings

In Settings → Project tools, on this machine only: the **Unreal Engine** page, and **All tools** for **Open solutions with**.

| Setting | Default | What it does |
|---|---|---|
| **Recognize Unreal Engine projects** | On | Off, Claudette treats an Unreal project's folder as a plain folder: no engine actions, editor logs or note to Claude, though its own actions and links still work. |
| **Tell Claude about Unreal Engine projects** | On | Adds a note to the session with the engine's version and folder and the commands to build the editor and regenerate project files, and asks Claude not to start the editor or packaging unless you ask. Applies to tabs started after the change. |
| **Default editor configuration** | Development | The configuration for projects that haven't a choice of their own. |
| **Project files for** | Visual Studio on Windows, Xcode on macOS, VS Code on Linux | Which project files **Generate project files** makes, and which solution **Open solution** opens. |
| **Open solutions with** | The app the OS uses for it | **The app the OS uses for it**, **Rider**, **Visual Studio**, **VS Code** or **Another program…**, with **Browse…**. Unity and Godot solutions open the same way. |

## See also

- [Unity](unity.md) and [Godot](godot.md), for the other engines.
- [Actions and links](actions-and-links.md), for commands of your own, such as a packaging script.
- [Perforce](perforce.md), which many Unreal projects use.
