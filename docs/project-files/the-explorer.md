---
title: The explorer
parent: Project files
nav_order: 2
summary: "The explorer is the tree of the project's folders and files at the side of a Files tab."
---

# The explorer

The explorer is the tree of the project's folders and files at the side of a Files tab. It marks the files Claude changed, and it keeps up with the disk.

## The header

The header has the group's name, then:

- **New file**, a page with a plus ([New files and folders](#new-files-and-folders));
- **Find file**, a magnifier, which is `Ctrl/Cmd+P` ([Finding a file by name](finding-a-file-by-name.md));
- **Collapse all**;
- **⋯**, which has **New folder**, **Find in files…** and **Replace in files…** in the whole project ([Finding in files](finding-in-files.md)), **Show only changed files**, **Claude's changes** and **Against git HEAD** (in a repository), **Move to the right** (or **Move to the left**) and **Refresh**.

## The tree

- Folders come first, then files, each by name, ignoring case. A folder's entries are read when you expand it.
- `.git` isn't shown.
- What git ignores is shown dimmed. Outside a repository, the folders the composer's `@` list skips (`node_modules`, `bin`, `obj` and the like) are dimmed instead. To leave them out of the tree altogether, set Settings → Git → **Files git ignores** to **Hidden**; the open Files tabs follow at once.
- A link to a folder has a link badge and expands like a folder, unless it leads back up into the tree.
- **It follows the disk.** Half a second after files appear, go or are renamed, the tree is drawn again, keeping what's expanded and selected.

## Opening a file

- A click opens the file in the preview tab, a double-click or `Enter` opens it to stay ([File tabs](file-tabs.md)). An image's double-click, `Enter` or **Open** shows it in Claudette's image window instead, as Settings → Files and diffs → **Open images in** says ([Images](../conversation/images.md#opening-an-image-claude-names)).
- The arrow keys move without opening anything. `Right` and `Left` expand and collapse a folder.

## Claude's changes in the tree

- A file Claude changed has a letter at its right: **M** for modified, **A** for one Claude created. A reviewed file's letter is faint.
- A folder holding a changed file has a dot.
- **Show only changed files** lists just those files, plus the ones Claude deleted, struck through. Opening a deleted file shows its diff. With nothing changed, it says *Nothing has changed.*

How the changes are worked out, and **Claude's changes** against **Against git HEAD**, are on [Claude's changes in the editor](claudes-changes-in-the-editor.md).

## New files and folders

1. Choose where it goes:
   - right-click a folder and choose **New file** or **New folder** to make it in that folder;
   - right-click a file for the same, to make it beside the file;
   - right-click the empty space under the tree to make it at the top of the project;
   - or use the header's **New file**, or **⋯** → **New folder**: they go in the selected folder, beside the selected file, or at the top of the project when nothing is selected.
2. The folder opens, and a box for the name appears at the top of it. Type the name. Slashes make folders on the way: `AI/Brain.cpp` makes the `AI` folder and the file in it.
3. Press `Enter`.

A new file is empty and opens to stay, ready to type in. A new folder is selected in the tree.

`Esc`, or a click elsewhere, closes the box without making anything, and so does `Enter` with the box empty.

As you type, the line under the box says if the name can't be used, and `Enter` keeps the box until it can: a file or folder with that name is already there, the name is `.` or `..`, or it has a character your system doesn't allow in names. On Windows, that's `< > : " | ? *`, a name ending in a space or a dot, and the names Windows keeps for itself, such as `CON`, `NUL` and `COM1`.

Claudette never writes over a file. If one with the name appears while you type, it stays as it is and the box says so.

## Renaming

{% include unreleased.html %}

1. Right-click a file or folder and choose **Rename**, or select it and press `F2`.
2. A box takes its place with its name in it. For a file, the name up to its extension is selected, so what you type keeps the extension.
3. Type the new name and press `Enter`.

`Esc`, or a click elsewhere, leaves the name as it was. The line under the box says when a name can't be used, as it does for a new file. A rename keeps it in its folder, so the name can't have a slash. Changing only the case, `brain.cpp` to `Brain.cpp`, works too.

Files you have open from it follow it, with any unsaved edits. A folder that was expanded stays expanded.

Claudette renames it on disk, as your file manager would. Git notices the rename by itself. Perforce needs `p4 reconcile`, or **Reconcile Offline Work** in P4V.

## Deleting

{% include unreleased.html %}

1. Right-click a file or folder and choose **Delete**, or select it and press `Delete` (`Cmd+Backspace` on macOS).
2. A question under the explorer's header says what goes and where: *"Delete Brain.cpp? It goes to the Recycle Bin."* For a folder, it says how many files are in it. If a file open from it has unsaved edits, it says those go too.
3. Choose **Delete**, or press `Enter`. **Cancel**, or `Esc`, keeps it.

It goes to the Recycle Bin on Windows, the Trash on macOS, or your desktop's trash on Linux, so you can put it back from there. On Linux that needs `gio`, which comes with GLib's tools (`libglib2.0-bin` on Debian and Ubuntu).

Where it can't go to the trash, such as on a network share, nothing is deleted yet. The question says so and why, and offers **Delete permanently**, which can't be put back.

Files you had open from it close, and the next row in the tree is selected.

## Menus

Right-click a file for **Open**, **Open in the OS's app**, **Open diff** (when Claude changed it), **New file**, **New folder**, **Copy path**, **Copy relative path**, **Reveal in file manager**, **Rename** and **Delete**.

On a program or shortcut that isn't text (`.exe`, `.lnk`, `.app`, `.desktop`), **Open in the OS's app** shows it in its folder rather than running it.

Right-click a folder for **New file**, **New folder**, **Copy path**, **Copy relative path**, **Reveal in file manager**, **Collapse**, **Find in folder…**, **Replace in folder…** ([Finding in files](finding-in-files.md)), **Rename** and **Delete**.

Right-click the empty space under the tree for **New file** and **New folder**, at the top of the project, and **Find in files…** and **Replace in files…**.

In a Perforce workspace, a file's and a folder's menus also have a Perforce section, **Check out**, **Mark for add**, **Mark for delete**, **Revert** and **Diff against the depot**, and the tree marks what Perforce has open, such as *edit* beside a file you've checked out. Renaming, deleting and new files are opened in Perforce too. See [Perforce](../project-tools/perforce.md#in-the-files-tab).

**Reveal in file manager** opens Explorer on Windows, Finder on macOS and your file manager on Linux.

## Its side and its width

- The explorer is on the left by default. Settings → **Files and diffs** → **The explorer is on the** has **Left** and **Right**, and **Move to the right** (or **Move to the left**) in the explorer's **⋯** menu changes the same setting.
- Drag the explorer's edge to make it wider or narrower: at least 180 pixels, 260 by default. A double-click on the edge goes back to that. The width is the same for every Files tab, and Claudette remembers it.
- In a narrow pane, the explorer narrows first, to leave the editor 360 pixels, down to its own least. Only then does the editor narrow.

## Hiding it

The button at the start of the file tabs' row, with the tip **Hide the explorer** or **Show the explorer**, hides and shows the explorer. Claudette remembers which. The [command palette](../tabs/the-command-palette.md) has the same command, and **Show only changed files**, while a Files tab has the focus.

A Files tab that a conversation opened beside it for its first file starts with its explorer hidden ([Opening files from the conversation](opening-files-from-the-conversation.md)).

Opening the Files tab from its group, with the **Project files** button or **Open project files**, always shows the explorer ([Opening project files](opening-project-files.md)).

## Settings

| Setting | Default | Where |
|---|---|---|
| **The explorer is on the** | Left | Settings → **Files and diffs** |
| **Files git ignores** | Dimmed | Settings → **Git** |

## Shortcuts

| Key | Does |
|---|---|
| `Ctrl/Cmd+P` | Find a file by name |
| `Up`, `Down` | Move through the tree |
| `Right`, `Left` | Expand or collapse a folder |
| `Enter` | Open the file to stay; in a new file's or folder's box, make it; in a rename's box, rename it |
| `Esc` | In a new file's or folder's box, or a rename's, close it without making or renaming anything; in the delete question, keep it |
| `F2` | Rename the selected file or folder |
| `Delete` (`Cmd+Backspace` on macOS) | Delete the selected file or folder, asking first |

## See also

- [Changed files](../changes/changed-files.md) is the same idea for one session, in a tool window beside a conversation.
