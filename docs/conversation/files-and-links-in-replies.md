---
title: Files and links in replies
parent: The conversation
nav_order: 12
summary: "Claude names files as it works: weapons.md is written, see docs/specs/weapons.md, Player.cpp:42."
---

# Files and links in replies

Claude names files as it works: *`weapons.md` is written*, *see `docs/specs/weapons.md`*, *`Player.cpp:42`*. Click one and Claudette opens it.

## Web links

A click on a web link in a reply, a plan, or a subagent's prompt or report opens it in your browser. Only `https`, `http` and `mailto` links open this way, so a reply can't start a program through an app's own address scheme. A link to an image on the web opens in Claudette's image window instead ([Images](images.md#opening-an-image-claude-names)).

## Files and folders
These mentions are clickable:

- A Markdown link whose address is a path, relative to the tab's folder or not, or a `file:` address on this computer. A line or heading in it (`#L42`, `#guns`) is left out.
- Inline code that names a file or folder: a path (`docs/specs/weapons.md`, `src/utils/`, `~/.claude/settings.json`, `C:\Work\Game\Config\DefaultGame.ini`) or a name with an extension (`weapons.md`, `.gitignore`), with or without a line (`Player.cpp:42`, `:42:7`, `:42-51`, `#L42-L51`).

Inline code becomes a link only once its file is found, so code that only looks like a name (`System.IO.Path`) stays code, and code that can't be a file (`Foo.Bar()`, `git add weapons.md`) isn't looked for. A found one keeps the code's look, underlined, with the hand pointer.

### Where the file is

A path is read from the tab's folder (the worktree, for a tab in one), then its extra folders. A path that starts from an environment variable is read with yours, written the way any shell writes it: `%TEMP%\shot.png`, `$env:TEMP\shot.png`, `$TMPDIR/shot.png` or `${TMPDIR}/shot.png`. On Windows, Claude runs its commands in Git Bash, so it can name files the way Git Bash does: `/c/Work/Game/Config.ini` is `C:\Work\Game\Config.ini`, and `/tmp/shot.png` is in your temporary folder. A name, or a path that isn't there from the folder (`specs/weapons.md`), is the file with that name that Claude's tools last read or changed, a subagent's too. Failing that, it is a file in the project with that name, the shallowest first.

### What a click does

1. A file opens in the app your OS uses for it. With Project files, it opens in Claudette's own editor instead, at the line named. Settings → Files and diffs → **Open files in** chooses: **Claudette's editor, at the line named** or **The app the OS uses for the file**. The OS's apps have no common way to take a line, so none is passed to them. ([Opening files from the conversation](../project-files/opening-files-from-the-conversation.md) has which tab it opens in.)
2. An image (a PNG, JPEG, GIF, WebP or BMP) opens in Claudette's image window, unless Settings → Files and diffs → **Open images in** says to use your OS's app ([Images](images.md#opening-an-image-claude-names)).
3. A folder opens in Finder, Explorer or your file manager.
4. A program, script, shortcut or installer (`.exe`, `.bat`, `.ps1`, `.js`, `.sh`, `.py`, `.lnk`, `.app`, `.desktop` and the like, or on macOS and Linux a file with no extension whose execute bit is set) is shown in its folder instead of opening, since opening one would run it, and a reply can be misled into pointing at one under an innocent name. Scripts open as text in Claudette's editor, where nothing runs.
5. A name that matches several files offers them in a menu at the click, with their folders (`docs/weapons.md`, `Plugins/Combat/weapons.md`). Pick one to open it; close the menu to open none.
6. A link to a file that isn't there adds a note: *Couldn't find docs/specs/shots.md.*

### What Claudette looks at

Inline code is looked for as the reply shows, before you click, and some paths reach the network just by being looked up (`/net/<host>` on macOS). So that lookup touches only the tab's folders, your home folder and, on Windows, the computer's own drives, not a mapped network drive. A file elsewhere becomes a link only when Claude's tools named it. A click looks wherever its path points. A network path (`\\server\share\…`, `//server/…`, `file://server/…`) is never looked at, since Windows would hand its server your credentials.

## Settings
Where a clicked file opens is set in Settings → Files and diffs: **Open files in**, and **Show a file clicked in a conversation** (**Beside the conversation** or **In its place**). [Opening files from the conversation](../project-files/opening-files-from-the-conversation.md) explains both.

## See also

- [Images](images.md) for images in a reply, which wait for a click when they come from the web, and the image window.
- [Tool calls](tool-calls.md): a tile's **Open** opens the file the call worked on.
- [Changed files](../changes/changed-files.md): its **Open** acts the same way.
