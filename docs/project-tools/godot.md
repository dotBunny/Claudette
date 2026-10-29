---
title: Godot
parent: Project tools
nav_order: 4
summary: "A Godot project gets a row with its name and version, and actions to open the editor and run the game."
---

<h1 class="godot-title">{% include godot-logo.html %}</h1>

When a tab's folder holds a Godot project (a folder with `project.godot`), the [project's icon](using-project-tools.md) on its group's row is Godot's logo, with its name and version in its tooltip, such as `Godot 4.3`, and its menu has the actions below. Godot 3 and Godot 4 projects both work.

## Finding Godot

There's no standard place to install Godot, so Claudette looks in this order:

1. The executable you picked for the project with **Choose Godot executable…** (**Choose another Godot executable…** once one is found).
2. The path on Godot's page in Settings → Project tools: **Godot executable**.
3. `godot`, `godot4`, `Godot` or `godot-mono` on your `PATH`.
4. On macOS, `Godot.app` or `Godot_mono.app` in Applications.
5. On Windows, Scoop's and WinGet's places.

The **Detect** button in Settings runs steps 3 to 5 and fills in the path. When Godot isn't found, the menu's header says so and the actions are greyed out.

## C# projects

A project is a C# project when its features list `C#`, it has a `[dotnet]` or `[mono]` section, or its folder has a `.sln` or `.csproj`. It needs the .NET build of Godot, whose name has `mono` in it. When the Godot Claudette found isn't that build, the menu's header says so.

## Actions

| Action | What it does |
|---|---|
| **Open in Godot** | Opens the project in the Godot editor. It's the main action. |
| **Run project** | Runs the project's main scene. |
| **Build C#** | C# projects only. A job that runs `dotnet build` on the project's `.sln`, or its `.csproj` when there's no `.sln`. Greyed out until Godot has made them. |
| **Open solution** | C# projects only. Opens the `.sln` with the IDE chosen in Settings, and is greyed out until Godot has made it. |
| **Clean .godot…** | Godot 4. Deletes the `.godot` folder, after asking, and warns that Godot reimports every asset next time. Godot 3 has **Clean .import…** instead. |
| **End all Godot editors…** | Ends every running Godot, whatever its build, and what each started. It asks first and lists each with its project. It never ends Claudette. |

## Settings

On Godot's page in Settings → Project tools, on this machine only. **Open solutions with**, on All tools, also applies here, and is explained on the [Unreal Engine](unreal-engine.md#open-solution-and-open-in-rider) page.

| Setting | Default | What it does |
|---|---|---|
| **Recognize Godot projects** | On | Off, Claudette treats a Godot project's folder as a plain folder: no engine actions, editor logs or note to Claude, though its own actions and links still work. |
| **Tell Claude about Godot projects** | On | Adds a note to the session with the Godot version, whether it's GDScript or C#, how to check the project and its scripts without the editor, how to build the C# code, and not to open the editor unless asked. Applies to tabs started after the change. |
| **Godot executable** | Empty, so Claudette finds it | The path to Godot, with **Browse…** and **Detect**. |

## See also

- [Unreal Engine](unreal-engine.md) and [Unity](unity.md), for the other engines.
- [Editor logs](editor-logs.md), for the log Godot writes, under the tab while it runs.
- [Actions and links](actions-and-links.md), for commands of your own, such as an export script.
