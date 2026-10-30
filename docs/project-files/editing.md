---
title: Editing
parent: Project files
nav_order: 6
summary: "The editor shows one file at a time, in the Files tab, under its file tabs."
---

# Editing

The editor shows one file at a time, in the Files tab, under its [file tabs](file-tabs.md). Click in the text and type. Nothing is written to disk until you [save](saving.md).

## What the editor does

- Line numbers, with the current line drawn faintly.
- Find and replace, with match case, whole word and regular expressions. The find box's **Find in files** button takes the search to the whole project ([Finding in files](finding-in-files.md)).
- **Go to line**, also in the [command palette](../tabs/the-command-palette.md) as **Go to line…**.
- Folding by indentation, in any language.
- Box selection: drag with `Alt` held (`Option` on macOS).
- Undo, back through the file's edits while it's open.
- `Tab` and new lines indent as the file does ([The chip](#the-chip)).

## How it looks

The code font and size are Settings → **Appearance** → **Code font** and **Code font size**, scaled by **Zoom**. The colors are the same as in the diff view, in the app's light or dark theme and style, so code looks the same in a reply's code block, in a diff and here. The selection is a see-through tint, so the code keeps its colors under it.

Web addresses in the code aren't links: a click on one does what a click on any other text does.

Colors come from the file's language, picked by its extension. A file with no extension is picked by its first line when that's a `#!` line (`bash`, `python`, `node`), and otherwise it's *Plain text*. The text on screen is colored first and the rest of a long file in the background, so even a long file opens colored where you're reading. A line over 2,000 characters, such as a minified file's, stays in the default color, since coloring one that long would slow every keystroke.

## Wrapping long lines

Settings → **Files and diffs** → **Wrap long lines** wraps lines at the editor's width. It's off by default. `Alt+Z` (`Option+Z`) turns it on or off for the file showing.

## The chip

A small rounded chip at the editor's bottom right says where the cursor is and what the file is: *Ln 42, Col 7 · C++ · CRLF · UTF-8 · 4 spaces*. It fades while the cursor's line is under it, so it never hides what you're typing.

- **Line endings** are *CRLF* or *LF*, whichever the file mostly has, or *Mixed* while it has both. New lines take the file's. Click to choose **LF** or **CRLF**, which changes every line's. That's an edit like any other: Undo takes it back, and saving writes it.
- **The encoding** is *UTF-8*, *UTF-8 with BOM*, *UTF-16 LE* and so on. Saving keeps it.
- **Indentation** is *4 spaces* or *Tabs*, found from the file's indented lines. A file with none gets Settings → **Files and diffs** → **Indent new lines with**, which is 4 spaces unless you change it. Click to choose **Indent with 2 spaces**, **4 spaces** or **8 spaces**, or **Indent with tabs** and a width of 2, 4 or 8. The choice is for `Tab` and new lines in this file while it's open. The lines already there stay as they are.
- The position, the language and the encoding are only shown. Clicking them does nothing.

## Files that open read-only, or not at all

- Text opens in UTF-8, or in UTF-16 or UTF-32 when its byte order mark says so.
- A file that isn't valid text in one of those, such as Latin-1, opens read-only and says why. Saving a guess would change characters nobody touched.
- A file Claudette isn't allowed to write opens read-only, and says so.
- A file with a line over 1,000,000 characters opens read-only, since typing on a line that long takes about half a second a key.
- A binary file, or one over 10 MB, doesn't open. The editor says so, with **Open in the OS's app**, and for an image, **Open in Claudette's image viewer** ([Images](../conversation/images.md#opening-an-image-claude-names)).

## Screen readers and input methods

A screen reader can't read the editor's text yet. So every file's menu keeps **Open in the OS's app**, for an editor that can be read. The explorer, the file tabs, the chip, the peek's buttons and the banners have names a screen reader can say. See [Accessibility](../settings/accessibility.md).

An input method that composes text, such as for Japanese or Korean, does it in its own window at the cursor, and the text arrives once you've finished.

## Settings

| Setting | Default | Where |
|---|---|---|
| **Wrap long lines** | off | Settings → **Files and diffs** |
| **Indent new lines with** | 4 spaces (**2 spaces**, **4 spaces**, **8 spaces** or **Tabs**) | Settings → **Files and diffs** |
| **Code font**, **Code font size** | | Settings → **Appearance** |

**Reset to defaults** in the **Project Files tab** section of Settings → **Files and diffs** puts both back, with **The explorer is on the**.

## Shortcuts

| Key | Does |
|---|---|
| `Ctrl/Cmd+F` | Find in the file |
| `Ctrl+H` (macOS: `Cmd+Option+F`) | Find and replace |
| `Ctrl+G` | Go to a line |
| `Alt+Z` (macOS: `Option+Z`) | Turn word wrap on or off for this file |

`Ctrl/Cmd+S` and the keys for moving between files are on [Saving](saving.md) and [File tabs](file-tabs.md), and [Keyboard shortcuts](../reference/keyboard-shortcuts.md) lists them all.

## See also

- [Claude's changes in the editor](claudes-changes-in-the-editor.md) for the bars in the gutter.
- [Markdown files](markdown-files.md) for reading a Markdown file rendered, beside the editor or in its place.
