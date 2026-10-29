---
title: Unity
parent: Project tools
nav_order: 3
summary: "A Unity project gets a row with its name and editor version, and actions to open the editor and build."
---

# Unity

When a tab's folder holds a Unity project (a folder with `ProjectSettings/ProjectVersion.txt` and `Assets`), the [project's icon](using-project-tools.md) on its group's row is a cube, with its name and editor version in its tooltip, such as `orbit-game · Unity 2022.3`, and its menu has the actions below.

## The editor

Unity needs the exact editor version the project names. Claudette looks, in this order:

1. The editor you picked with **Choose Unity editor…** (**Choose another Unity editor…** once one is found): the executable, or a version's folder.
2. Unity Hub's default folder for that version.
3. Hub's custom install folder, when you set one.
4. Editors you added to Hub by hand.

When the version isn't installed, the menu's header says so and offers **Choose Unity editor…**. Claudette remembers the pick for the project.

## Actions

| Action | What it does |
|---|---|
| **Open in Unity** | Opens the project in its editor. It's the main action. |
| **Run EditMode tests** | A job that runs the project's EditMode tests in batch mode, and quits Unity when they're done. When it ends, its status adds the counts: *Run EditMode tests failed (exit code 2). 11 passed, 1 failed.* |
| **Regenerate the C# solution** | A job that makes Unity write the C# solution again, through the project's Rider or Visual Studio package. Greyed out, with the reason, when the project has neither package. |
| **Open solution** | Opens the project's solution with the IDE chosen in Settings. Greyed out when it's missing. |
| **Open Editor log** | Opens Unity's editor log in another app. While Unity has the project open, its log is also a row under the tab: see [Editor logs](editor-logs.md). |
| **Open Player log** | Opens the project's player log. Greyed out when there isn't one. |
| **Clean Library…** | Deletes `Library`, `Temp` and `obj` in the project, and nothing else. It asks first, shows the size, and warns that Unity reimports every asset next time, which can take a long while. |
| **End all Unity editors…** | Ends every running Unity editor and what it started. It asks first, lists each editor, and doesn't touch Unity Hub or Unity's own helpers. |

The project's menu also has **Code optimization**, with **Release** and **Debug** as radio items. Debug opens Unity with debug code optimization, for stepping through scripts, and the action reads **Open in Unity (Debug)**. The choice is remembered for the project on this machine.

## While Unity has the project open

Unity locks a project while its editor has it open. Claudette notices, and the main action reads **Unity has this project open** and is greyed out. **Run EditMode tests**, **Regenerate the C# solution** and **Clean Library…** are refused with "Unity has this project open, and locks it while it does: close the editor first." A lock left behind by a crashed editor doesn't count.

## Settings

On Unity's page in Settings → Project tools, on this machine only. **Open solutions with**, on All tools, also applies here, and is explained on the [Unreal Engine](unreal-engine.md#open-solution-and-open-in-rider) page.

| Setting | Default | What it does |
|---|---|---|
| **Recognize Unity projects** | On | Off, Claudette treats a Unity project's folder as a plain folder: no engine actions, editor logs or note to Claude, though its own actions and links still work. |
| **Tell Claude about Unity projects** | On | Adds a note to the session with the Unity version and editor, how to run the EditMode tests in batch mode, that `Library`, `Temp` and `obj` are generated and shouldn't be edited, that a `.meta` file moves with its asset, and not to open the editor unless asked. Applies to tabs started after the change. |
| **Default code optimization** | Release | The optimization for projects that haven't a choice of their own. A project's own choice wins. |

## See also

- [Unreal Engine](unreal-engine.md) and [Godot](godot.md), for the other engines.
- [Actions and links](actions-and-links.md), for commands of your own, such as a build script.
