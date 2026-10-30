---
title: Finding in files
parent: Project files
nav_order: 4
summary: "Find text in a project's files, and replace it, from anywhere in the window."
---

# Finding in files

{% include unreleased.html %}

Find text in a project's files, and replace it, as JetBrains Rider's **Find in Files** does. It works from anywhere in the window, a conversation or a Files tab, and looks in the focused tab's project.

## Open it

- Press `Ctrl+Shift+F` (`Cmd+Shift+F` on macOS) to find, or `Ctrl+Shift+R` (`Cmd+Shift+R`) to replace.
- Or choose **Find in files…** or **Replace in files…** in the [command palette](../tabs/the-command-palette.md).
- Or right-click a folder in the [explorer](the-explorer.md) for **Find in folder…** or **Replace in folder…**, which look only in that folder.

A Files tab offers it too:

- With no file open, it lists **Find in files** and **Replace in files**, with their shortcuts.
- The explorer's **⋯** menu, and right-clicking the space under its tree, have **Find in files…** and **Replace in files…**.
- In [Go to file](finding-a-file-by-name.md), click **Find "…" in files** under the list to look for what you typed in the files' text instead, or press `Ctrl+Shift+F` (`Cmd+Shift+F`) in the box.
- The editor's find box (`Ctrl+F`, `Cmd+F` on macOS) has a **Find in files** button before its **×**. It takes what you're looking for to the whole project, with **Aa**, **W** and **.\*** as you set them, and replaces if the find box is replacing. Pressing `Ctrl+Shift+F` (`Cmd+Shift+F`) in the find box does the same.

Text you've selected on one line in the editor is what it looks for. Otherwise it starts with what you looked for last.

## Find

Type in the box. The matches come in as you type, each a line with the match in bold, and the file and line at its right. Under them, the selected match shows in its file, colored, with the match picked out.

Beside the box:

- **Aa** matches case.
- **W** matches whole words.
- **.\*** takes what you type as a regular expression. It matches within a line.

Under it, **File mask** narrows the files, such as `*.cs, *.h`; put `!` before one to leave it out, as in `!*.Designer.cs`. **In project** looks through the whole project, and **Directory** only in the folder beside it; **⋯** picks another.

It looks in the same files the composer's `@` list offers: those git tracks, and new ones it doesn't ignore. It skips files that aren't text and files over 10 MB. A file you have open in a Files tab is searched as it is there, unsaved changes and all. It shows the first 1,000 matches and says when there are more.

`Up` and `Down` choose a match. `Enter`, or a double-click, opens it in the project's [Files tab](opening-project-files.md), with the match selected. `Esc`, or a click outside it, closes it.

## Replace

In **Replace in files**, type what goes in place of the match in the box under the one you search with. The chevron at the left of the boxes switches between finding and replacing, as in the editor's find box, and so do `Ctrl/Cmd+Shift+R` and `Ctrl/Cmd+Shift+F`. What you're looking for, and the matches, stay as they are.

- **Replace** replaces the selected match.
- **Replace all** asks first, saying how many matches in how many files.

With **.\*** on, the replacement can use the expression's groups, such as `$1`.

A file you have open changes in its editor, unsaved, so **Undo** takes it back. Other files change on disk, keeping their encoding and line endings. In a Perforce workspace, they're checked out first ([Perforce](../project-tools/perforce.md#opening-files-in-perforce)). Each file is searched again just before, so one that changed since still gets its matches replaced where they are now. A file that can't be changed is listed with why.

## Keep the results

**Open in Find window**, or `Ctrl+Enter` (`Cmd+Enter`), keeps the matches in the Find window, along the bottom of the window, with a tab on its header for each search, which its **×** closes. The Find icon at the foot of the [tool window strip](../tabs/tool-windows.md) shows and hides it while you have a search kept. There they're grouped by file. Click a file to fold its matches away, and a match to open it. **Search again** runs the same search. In replace mode, untick a match to leave it out of **Replace all**.

## Shortcuts

| | Windows and Linux | macOS |
|---|---|---|
| Find in files | `Ctrl+Shift+F` | `Cmd+Shift+F` |
| Replace in files | `Ctrl+Shift+R` | `Cmd+Shift+R` |
| Open the selected match | `Enter` | `Enter` |
| Keep the results in the Find window | `Ctrl+Enter` | `Cmd+Enter` |
| Close | `Esc` | `Esc` |

Both shortcuts can be changed in Settings → Keyboard.

## See also

- [Finding a file by name](finding-a-file-by-name.md), with `Ctrl/Cmd+P`.
- [Editing](editing.md), whose find and replace work in one file.
