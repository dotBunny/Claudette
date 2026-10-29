---
title: Actions and links
parent: Project tools
nav_order: 6
summary: "A folder's own commands and web links in the project's menu: run the tests, start a server, open the issue board."
---

# Actions and links

Any folder can have commands and web links of its own in the [project's menu](using-project-tools.md): run the tests, start a server, open the issue board, open a pull request for the current branch. They're kept in two files in the tab's folder:

- `claudette.json` is shared. You commit it with the project, so everyone who works on it gets the same actions and links.
- `claudette.local.json` is yours. Put it in `.gitignore`. Its entries come after the shared file's.

You can write the files by hand, which [Reference](../reference/claudette-json.md) describes, or add entries from the app.

## Add an action

1. Open the project's menu and choose **Add an action…**. Settings opens on the project's **Actions** page with a dialog.
2. Fill in **Name** and **Command**. The command runs through your shell: `cmd.exe` on Windows, and your `$SHELL` (or `/bin/sh`) on macOS and Linux.
3. Set **Working folder** if the command shouldn't run in the tab's folder. It's relative to that folder.
4. Optionally, set **Only show when this exists** to a file or folder, relative to the tab's folder, such as the build the action runs. The action shows only while it exists. **Browse…** picks one, and several are separated by semicolons.
5. Choose **Run with output**, which makes a run under the tab in the sidebar, with its log, **Stop** and a notification when it ends, or **Launch and forget**, for programs that keep running on their own.
6. Optionally, tick **Also run it in each new worktree a worktree tab makes** ([below](#setting-up-each-new-worktree)).
7. Under **Save in**, choose **Just me (claudette.local.json)**, the default, or **Shared with the project (claudette.json)**, and then **Save**.

The action appears in the project's menu after the project's own actions, under a separator. Hovering it shows its whole command.

{: .warning }
Actions run when you click them, without asking, whichever file they're in. A project from someone else can put any command behind a friendly name in `claudette.json`, so hover an action to read its command before the first click. Nothing from these files runs by itself, except actions marked to run in each new worktree, and the shared file's only in a folder you trust ([Folder trust](../permissions/folder-trust.md)).

## Add a link

1. Choose **Add a link…** in the project's menu. Settings opens on the project's **Links** page with a dialog.
2. Type the **Name**, which can be empty (the menu then shows the address), and the **Address**.
3. Choose which file it goes in, as for an action, and **Save**.

Links show under **Links** in the project's menu, each marked ↗, the shared file's first. Hovering one shows the address it opens, and a click opens it in your browser.

- Only `https`, `http` and `mailto` links open. Anything else, such as a `file:` address, shows greyed out with the reason, and never opens.
- An address can use placeholders, filled in from the selected tab and escaped for a web address: `{branch}` (the git branch), `{changelist}` (the Perforce changelist Claude is working in) and `{folderName}` (the tab's folder name). A link whose placeholder can't be filled right now is greyed out and says why, for example "No git branch." For example, `https://github.com/org/repo/compare/{branch}?expand=1` opens a pull request for the tab's branch.

## Edit them in Settings

After the pages in the Settings window's sidebar, a group headed by the project's name has **Actions** and **Links** pages.

- **Actions**: choose which file to work on, then use **Add…**, **Edit…**, **Remove**, **Move up**, **Move down** and **Open file**, which opens the file in your editor for anything the page doesn't cover. Entries for other operating systems are listed and marked "only on …", entries with a file they wait for are marked "only when … exists", and entries for new worktrees are marked "runs in each new worktree". An entry that can't be read is listed with its reason, and can only be removed.
- **Links**: the links of both files in the order the menu shows them, each with its address and its file. **Add…**, **Edit…**, **Remove**, **Move up** and **Move down** work as for actions, and a link moves within its own file. A link the menu won't open is listed with the reason.

Each change is saved as you make it, and every tab in the folder reads the files again at once. Saving rewrites the file: it keeps your other keys and each entry's other fields, but **comments in the file are dropped**. A file that isn't valid JSON is never rewritten. The page shows the reason, and you fix the file by hand first.

## When Claudette reads the files again

Claudette doesn't watch the files. It reads them again when you select the tab, when Claudette comes to the front, when a turn ends (Claude may have edited them), when the project's menu opens, and after Settings saves. **Refresh** in the menu does it on demand. An entry it can't use is skipped with a reason, which the menu and the Project page show, such as "claudette.json: actions[2] has no command, so it was skipped."

## Setting up each new worktree

A new worktree is a fresh checkout: no installed packages, and none of the files git ignores, such as a local `.env`. Actions marked to run in each new worktree set it up.

- **When.** Once Claude Code has made the worktree for a [worktree tab](../tabs/worktree-tabs.md), not when a tab opens a worktree that was already there.
- **Which.** Those in `claudette.local.json` always run. Those in `claudette.json` run only while the main checkout is trusted. Otherwise a note names them and says they can be run from the project's menu.
- **How.** They run in the new worktree, with the variables `CLAUDETTE_WORKTREE` and `CLAUDETTE_MAIN_CHECKOUT` in their environment, so a command can copy a file from the main checkout: `copy "%CLAUDETTE_MAIN_CHECKOUT%\.env" .env` on Windows, `cp "$CLAUDETTE_MAIN_CHECKOUT/.env" .` elsewhere. A note says *"Setting up the new worktree: Install packages, Copy .env."* Launch actions start at once. Output actions run as jobs, one after another, each only once the one before it succeeded.

They stay in the project's menu, to run again by hand.

## See also

- [Project tools](using-project-tools.md) for the project's icon, runs and the Project page.
- [`claudette.json`](../reference/claudette-json.md) for the files' format.
- [Worktree tabs](../tabs/worktree-tabs.md).
