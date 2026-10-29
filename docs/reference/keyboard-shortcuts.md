---
title: Keyboard shortcuts
parent: Reference
nav_order: 1
summary: "Every shortcut Claudette handles, with its default on Windows and Linux and on macOS."
---

# Keyboard shortcuts

Every shortcut Claudette handles, with its default on Windows and Linux and on macOS. Settings → Keyboard lists the rebindable ones and lets you change them ([Keyboard](../settings/keyboard.md)). The fixed keys at the end can't be changed. A shortcut on a digit, `+` or `-` also answers to the number pad's key.

On Windows and Linux, `Ctrl` is the command key. On macOS the command key is `Cmd`, and `Ctrl` is the Control key, which `Ctrl+Tab` and `Ctrl+Shift+Tab` use, and the editor's go-to-line key.

## Tabs and windows

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| New tab | `Ctrl+T` | `Cmd+T` | [Opening tabs](../tabs/opening-tabs.md) |
| Close tab | `Ctrl+W` | `Cmd+W` | [Closing and restoring](../tabs/closing-and-restoring.md) |
| Next tab | `Ctrl+Tab` | `Ctrl+Tab` | [The sidebar](../tabs/the-sidebar.md) |
| Previous tab | `Ctrl+Shift+Tab` | `Ctrl+Shift+Tab` | |
| Go to tab 1–9 | `Ctrl+1` to `Ctrl+9` | `Cmd+1` to `Cmd+9` | |
| Go to the next tab waiting for you | `Ctrl+J` | `Cmd+J` | [The command palette](../tabs/the-command-palette.md) |
| Command palette | `Ctrl+Shift+P` | `Cmd+Shift+P` | [The command palette](../tabs/the-command-palette.md) |
| Collapse or expand the sidebar | `Ctrl+B` | `Cmd+B` | [The sidebar](../tabs/the-sidebar.md) |
| Open from History | `Ctrl+Shift+H` | `Cmd+Shift+H` | [History](../history/history.md) |
| Settings | `Ctrl+,` | `Cmd+,` | [The Settings window](../settings/the-settings-window.md) |
| Zoom in | `Ctrl++` | `Cmd++` | [Accessibility](../settings/accessibility.md) |
| Zoom out | `Ctrl+-` | `Cmd+-` | |
| Reset zoom | `Ctrl+0` | `Cmd+0` | |

On macOS, **File → New Tab**, **History…** and **Close Tab**, and **Settings…** in the app menu, show these shortcuts too, including ones you've changed.

## The composer

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Stop Claude | `Esc` | `Esc` | [Sending and stopping](../composer/sending-and-stopping.md) |
| Stash the message | `Ctrl+S` | `Cmd+S` | [Earlier prompts, drafts and the stash](../composer/earlier-prompts-drafts-and-the-stash.md) |
| Quick suffixes menu | `Ctrl+Shift+S` | `Cmd+Shift+S` | [Quick suffixes](../composer/quick-suffixes.md) |

Each quick suffix can also have a shortcut of its own, in Settings → Keyboard → **Quick suffixes**.

## Prompts

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Allow the waiting prompt | `Ctrl+Enter` | `Cmd+Enter` | [Permission cards](../permissions/permission-cards.md) |
| Deny the waiting prompt | `Ctrl+Backspace` | `Cmd+Backspace` | |

They answer the oldest waiting prompt in the tab. With **Send with** set to `Ctrl/Cmd+Enter`, that key sends what's typed, and answers the prompt only when the message box is empty.

## The conversation

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Find in the conversation | `Ctrl+F` | `Cmd+F` | [Finding your way](../conversation/finding-your-way.md) |

### Moving through the conversation

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Previous message in the conversation | `F3` | `F3` | [Finding your way](../conversation/finding-your-way.md) |
| Next message in the conversation | `F4` | `F4` | |
| Go to the first message | `Ctrl+F3` | `Cmd+F3` | |
| Go to the latest message | `Ctrl+F4` | `Cmd+F4` | |

## Project tools

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Run the project's main action | `Ctrl+Shift+E` | `Cmd+Shift+E` | [Project tools](../project-tools/using-project-tools.md) |

## Project files

The shortcut that goes to a file is rebindable. The editor's own keys are fixed, and work while a Project Files tab has the focus. In that tab, **Find**, **Close tab**, and the previous and next message shortcuts act on the editor too: `Ctrl/Cmd+F` finds in the file, `Ctrl/Cmd+W` closes the file showing (and the Files tab once none is open), and `F3` and `F4` go to the previous and next change.

| Command | Windows and Linux | macOS | More |
|---|---|---|---|
| Go to file | `Ctrl+P` | `Cmd+P` | [Finding a file by name](../project-files/finding-a-file-by-name.md) |
| Find in files | `Ctrl+Shift+F` | `Cmd+Shift+F` | [Finding in files](../project-files/finding-in-files.md) |
| Replace in files | `Ctrl+Shift+R` | `Cmd+Shift+R` | [Finding in files](../project-files/finding-in-files.md#replace) |
| Save | `Ctrl+S` | `Cmd+S` | [Saving](../project-files/saving.md) |
| Save all | `Ctrl+Alt+S` | `Cmd+Option+S` | |
| Replace | `Ctrl+H` | `Cmd+Option+F` | [Editing](../project-files/editing.md) |
| Go to a line | `Ctrl+G` | `Ctrl+G` | |
| Wrap long lines in this file | `Alt+Z` | `Option+Z` | |
| Next file | `Ctrl+PageDown` | `Cmd+Option+→` | [File tabs](../project-files/file-tabs.md) |
| Previous file | `Ctrl+PageUp` | `Cmd+Option+←` | |

Dragging with `Alt` (`Option`) held makes a box selection. In the go-to-line box, `Enter` goes, `Esc` closes it, and `42:7` goes to line 42, column 7.

In the explorer, the arrow keys move without opening anything, `Enter` opens a file to stay, `Space` opens it in the preview tab, `Right` and `Left` open and close folders, `Left` on a file or closed folder goes up to its folder, `F2` renames the selected file or folder, and `Delete` (`Cmd+Backspace` on macOS) deletes it, asking first. In the find box, `Up` and `Down` choose, `Enter` opens and `Esc` brings the tree back ([The explorer](../project-files/the-explorer.md)).

## Fixed keys

These aren't in the list of rebindable shortcuts.

| Where | Key | What it does |
|---|---|---|
| The message box | `Enter` | Sends, or starts a new line, as **Send with** says, which the chip before **Send** shows and switches ([The send key](../composer/the-composer-bar.md#the-send-key)). `Shift+Enter` does the other. |
| The message box | `Ctrl/Cmd+Shift+V` | Pastes the clipboard's text as it is, however long ([Attachments and large pastes](../composer/attachments-and-large-pastes.md)). |
| The message box | `Up` on the first line, `Down` on the last | Goes back through the tab's earlier prompts, and forward again. |
| The empty message box | `Tab` or `→` | Takes the suggested next prompt ([Prompt suggestions](../composer/prompt-suggestions.md)). |
| The `/` and `@` lists | `Up`, `Down`, `Enter`, `Tab`, `Esc` | `Up` and `Down` choose, `Enter` or `Tab` takes the choice, `Esc` closes the list ([Slash commands and mentions](../composer/slash-commands-and-mentions.md)). |
| The new tab picker | `Up`, `Down`, `Enter`, `Esc` | Choose, open and close. |
| The new tab picker | `1` to `9` | Opens one of the first nine entries, while the search box is empty. |
| The quick suffixes menu | `1` to `9` | Picks one of the first nine suffixes. |
| The command palette | `Up`, `Down`, `Enter`, `Esc` | Choose, run and close. |
| Find in the conversation | `Enter`, `Shift+Enter`, `Esc` | Next match, previous match, close the bar. |
| History | `Esc` | Closes it. `Enter` in the search box also searches Claude's replies. |
| A tab's name, while renaming | `Enter`, `Esc` | Saves, or cancels. |
| A window of its own (an image, a tool call) | `Esc` | Closes it. |
| The image window | `+`, `-`, `0` | Zooms in, zooms out, and fits the whole image again ([Images](../conversation/images.md#zooming-in)). |
| Settings | `Ctrl/Cmd+F` | Goes to **Search settings**. |
| Settings | `Esc`, `Ctrl/Cmd+W` | Closes the window ([The Settings window](../settings/the-settings-window.md)). |
| Settings, while recording a shortcut | `Esc` | Cancels the recording. |

### Games

While the Games page has the focus, the number keys play tic-tac-toe's squares (1 to 9 from the top left) and Connect Four's columns (1 to 7), and the letters guess in hangman. In Minesweeper, the arrow keys move between the squares, `Space` or `Enter` opens one and `F` flags it. None of it reaches the message box. See [Games with Claudette](../claudette/games-with-claudette.md).

## See also

- [Keyboard](../settings/keyboard.md), for changing a shortcut and the keys Claudette refuses.
