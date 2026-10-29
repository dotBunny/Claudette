---
title: File tabs
parent: Project files
nav_order: 5
summary: "A row of tabs sits over the editor, one for each open file."
---

# File tabs

A row of tabs sits over the editor, one for each open file. With no file open, the space under it says *No files open* and lists what to do next, each with its shortcut: **Go to file**, **Find in files**, **Replace in files** and **Command palette**.

## What a tab shows

- The file's name. When two open files share a name, each also shows its parent folder: `Player/Config.h` and `Enemy/Config.h`.
- A dot while the file has unsaved edits, which becomes a **×** (**Close**) when you point at it.
- A pin while it's pinned, and a small check while it's reviewed ([Reviewed files](../changes/reviewed-files.md)).

Point at a tab for its path from the project's folder and, when Claude changed the file, who: *Changed by Claude in refactor auth*.

## Preview and kept tabs

A single click on a file in the [explorer](the-explorer.md) opens it in the preview tab, with its name in italics. The next file you open that way replaces it. Typing in it, double-clicking its tab, or opening the file to stay (a double-click or `Enter` in the explorer, or `Enter` on a search result) keeps it.

## Using the row

- A click selects a tab, and a middle click closes it.
- Drag a tab to move it along the row.
- When the tabs don't fit, the row scrolls sideways (the mouse wheel works) and keeps the selected tab in view. The **⋯** at its end, **Every open file**, lists every open file.
- Drag a tab out of Claudette's window to open its file in a window of its own ([below](#in-a-window-of-its-own)).

## In a window of its own

A file can leave the Files tab for a window of its own, so you can keep the code on another screen, or beside Claudette, while the conversation has all of Claudette's window. To open one:

- right-click its tab and choose **Open in a window**;
- click the window button at the right of the row, before **⋯**, for the file showing;
- drag its tab out of Claudette's window and let go where you want it;
- or, with the Files tab focused, choose **Open the file in a window** in the [command palette](../tabs/the-command-palette.md).

The window has the file's name in its title bar, with a dot while it has unsaved edits, and its project's name. Under it, a toolbar has the file's path and **Put back**. The rest is the file as its tab shows it: the editor, Claude's changes, the chip and the Markdown preview, with your edits, the cursor and where you'd scrolled. It follows your zoom, and the Files tab's keys work in it: `Ctrl/Cmd+S` saves it, **Find in files** opens over Claudette's window, and `Ctrl/Cmd+W` puts it back.

Meanwhile its tab stays in the row, with a small window after its name. Click it, or choose it in **⋯**, to bring the window forward. Opening the file again, from a reply, Changed files or **Go to file**, goes to its window too, at the line. `Ctrl+PageDown` and `Ctrl+PageUp` skip it. When every open file is in a window, the editor's place says so.

To put it back, click **Put back** in the window's toolbar, choose **Put back** on its tab's menu, or close the window. The file shows in its tab again, unsaved edits and all. Closing the file from its tab closes its window, asking first about unsaved edits.

Windows aren't kept: the next time Claudette starts, a file that was in one is back in its tab.

## Pinning

Right-click a tab and choose **Pin**, or **Unpin**. Pinned tabs sit first in the row, and the **Close** commands below leave them open.

## Closing

The tab's menu has:

- **Close**
- **Close others**
- **Close to the right**
- **Close saved**, which leaves the files with unsaved edits
- **Close all**

A file with unsaved edits asks first ([Saving](saving.md)).

When the Files tab is beside another tab with its explorer hidden, as it is when a file you clicked in the conversation opened it ([Opening files from the conversation](opening-files-from-the-conversation.md)), closing its last file closes the Files tab too, and the conversation has its width back. With the explorer showing, or with the Files tab on its own, it stays open with no file.

The rest of the menu is **Pin** or **Unpin**, **Open in a window** or **Put back** ([In a window of its own](#in-a-window-of-its-own)), **Reviewed** while Claude has changed the file, **Copy path**, **Copy relative path**, **Reveal in the explorer** (which selects it in the tree, and shows the explorer if it's hidden), **Reveal in file manager** and **Open in the OS's app**.

## Moving between files

`Ctrl+PageDown` and `Ctrl+PageUp` go to the next and previous file, or `Cmd+Option+→` and `Cmd+Option+←` on macOS.

The window's **Close tab** shortcut, `Ctrl/Cmd+W` unless you've changed it, closes the file showing while a Files tab has the focus, and the Files tab itself once no file is open, or with the last file when it's beside another tab with its explorer hidden ([Closing](#closing)).

## Coming back

A Files tab comes back on launch with the files it had open. Unsaved edits come back too ([Saving](saving.md)).

## Shortcuts

| Key | Does |
|---|---|
| `Ctrl+PageDown`, `Ctrl+PageUp` | The next or previous file (macOS: `Cmd+Option+→`, `Cmd+Option+←`) |
| `Ctrl/Cmd+W` | Close the file showing; in a file's own window, put it back |

## See also

- [The explorer](the-explorer.md) and [Finding a file by name](finding-a-file-by-name.md) are how files get opened.
