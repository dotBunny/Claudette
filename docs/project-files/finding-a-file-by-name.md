---
title: Finding a file by name
parent: Project files
nav_order: 3
summary: "Type part of a file's name and the explorer swaps its tree for a list of the files that match, best first."
---

# Finding a file by name

Type part of a file's name and the explorer swaps its tree for a list of the files that match, best first.

## Starting a search

- Click **Find file** (the magnifier) in the explorer's header.
- Or press `Ctrl/Cmd+P`, which works from anywhere, or choose **Go to file…** in the [command palette](../tabs/the-command-palette.md). From a conversation, it first opens the project's Files tab, beside the conversation or in its place, as **Show a file clicked in a conversation** says ([Opening files from the conversation](opening-files-from-the-conversation.md)).
- With no file open, the empty Files tab lists **Go to file**, with its shortcut.

A box, **Find a file by name**, appears at the top of the explorer with the keyboard in it.

## What's searched

It searches the names of the files the composer's `@` lists ([Slash commands and mentions](../composer/slash-commands-and-mentions.md)): the files git tracks and the new ones it doesn't ignore. Outside a repository, it's a walk of the folder that skips `node_modules`, `bin`, `obj` and the like. Files git ignores are left to the tree.

Only names are searched, not what's in the files. To look for text in them, click **Find "…" in files** under the list, which takes what you typed to [Find in files](finding-in-files.md), or press `Ctrl+Shift+F` (`Cmd+Shift+F` on macOS) in the box.

## Matching

The letters you type are looked for in order, in the file's name first, then in its path. A match at the start of the name or of a word, or with the letters together, comes first, then shorter paths.

## The results

Each result is one line: the name, with the letters that matched in bold, then, muted, its folder relative to the project's: `PlayerController.cpp  Source/Game/Player`. That tells files with the same name apart at a glance.

- A path too long for the width is cut short from its start (`…/Game/Player`), and its tip shows the whole path.
- A file at the top of the project shows no folder.
- The list holds the best 200, and says so: *200 of 1,204 files. Type more to narrow them.*
- While the list is first being made, it says *Listing files…*. With nothing matching, it says *No file's name matches.*
- A project with more files than the `@` list's 100,000 says that the list is cut short.

## Choosing

- `Up` and `Down` choose a result. `Enter`, or a click, opens it to stay.
- A line number after the name goes to that line: `player:42`.
- `Esc`, or emptying the box, brings the tree back, with the file you just opened selected in it.

## Shortcuts

| Key | Does |
|---|---|
| `Ctrl/Cmd+P` | Start a search. You can rebind it in Settings → **Keyboard** (**Go to file**). |
| `Up`, `Down` | Choose a result |
| `Enter` | Open it to stay |
| `Esc` | Back to the tree |

## See also

- [The explorer](the-explorer.md) is the tree the search replaces while you type.
- [File tabs](file-tabs.md) explains what opening "to stay" means.
