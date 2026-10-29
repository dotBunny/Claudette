---
title: The Settings window
parent: Settings
nav_order: 1
summary: "Everything you can change in Claudette is in the Settings window: its pages down the left, in groups, and the one you pick on the right, in sections."
---

# The Settings window

Everything you can change in Claudette is in the Settings window: its pages down the left, in groups, and the one you pick on the right, in sections.

{% include shot.html name="guide/settings/settings-window" alt="The Settings window on General: the pages in their groups and a search box down the left, with the version and Help at its foot, and General's sections on the right, each with its settings and Reset to defaults" %}

## Opening it

Any of these opens Settings:

- **Settings** in the menu at the header's right, the three lines after your plan.
- `Ctrl/Cmd+,`.
- **Settings** in the [command palette](../tabs/the-command-palette.md) (`Ctrl/Cmd+Shift+P`). The palette also lists each page by name, so typing "keyboard" there opens Settings on Keyboard.
- On macOS, **Settings…** in the app menu.
- A feature's own menu, which opens Settings at that feature's settings and picks them out for a moment: **Claudette settings…** in [Claudette](../claudette/claudette-on-the-composer.md)'s right-click menu, **Keep-awake settings…** in the right-click menu of the header's cup, **Diff tool settings…** in a changed file's, and **Edit suffixes…** in **Suffixes ▾**.

Settings opens on the page you left it on. It's a window of its own that stays open beside the main window, so you can change a setting and see what it does as you work. Asking for it again while it's open brings it to the front. Esc or `Ctrl/Cmd+W` closes it, and `Ctrl/Cmd+F` goes to the search box.

## The pages

The sidebar lists the pages in groups:

- **Claudette**: General, Appearance, Conversation, Notifications, Keyboard and [About](about.md).
- **Claude**: Claude Code, Accounts, Tabs, Usage and Advanced.
- **Your work**: Sessions, and Files and diffs.
- **Project tools**: All tools, which has what every kind of project shares, then a page for each kind of project Claudette knows, Unreal Engine, Unity and Godot, each with its own icon (Unreal Engine's and Godot's are their logos). A kind of project you've switched off says *Off* beside its name, and its page is still there to switch it back on.
- **Source control**: [Git](git.md), with a branch for its icon, then [Perforce](../project-tools/perforce.md), with Perforce's logo.
- At the foot, under a line, the [selected tab's project](#the-projects-pages), and under another line the [version and Help](#the-version-and-help).

The arrow keys go up and down through every page. [Every setting](every-setting.md) has a table for each.

Each page starts with a line saying what's on it, then its sections. In the Claude style each section is a card.

## A setting

Each setting is a row: its name, a line saying what it does, and **More** when there's more to say. Its switch, list or box is beside it, or under it when it needs the width.

- **Changes apply as you make them.** There's no **Save** button, and nothing to restart, except where a setting says it applies to tabs started after the change. A box you type in takes effect when you leave it.
- **A changed setting** has a short colored bar at its left. Point at it, or move to it with Tab, for its **Reset**, which puts just that setting back. **Reset** appears in a space kept for it beside the setting's switch or list, so nothing moves as it does.
- **This computer** by a setting means [settings sync](../history/settings-sync.md) leaves it as it is on each machine. When a whole section or page stays on this machine, it says so once, by the section's heading or the page's title.
- **1 tab has its own** by a setting means a tab changed it for itself in its [Tab settings…](../tabs/tab-settings.md). Point at it for which tabs.

## Searching

{% include shot.html name="guide/settings/search" alt="Settings searched for font: the four font settings and others that mention fonts, each with its page and section under it, and Appearance's Text section on the right" %}

Type in **Search settings** above the pages. The list shows each setting and section that matches, with where it is under it: *Appearance → Text*. It finds a setting by its name, by what it does, and by other words for it, so "dark mode" finds **Theme** and "caffeinate" finds **Keep this computer awake**. Pick one to open its page there: Settings scrolls to it and picks it out for a moment. If nothing matches, the list says "No settings match."

The search also finds the selected tab's project pages (below), named with the project: "NightOwl → Links". It matches what's on them, such as "web links", "add an action", "engine" or "editor configuration", and the project's name.

## Reset to defaults

Each section has **Reset to defaults** by its heading, which puts that section's settings back and nothing else. It's only there to click while something in the section has changed. A few things are left alone, because changing them moves where your data lives or because they're yours rather than settings:

- **Sessions** leaves the library's **Folder** and **Sync Claudette's settings through the library**. Use **Use the default folder**, or turn the switch off, when you mean to change them.
- **Usage** leaves **Keep usage history**.
- **Tabs** leaves your favorite and recent folders.
- **Accounts** only puts **New folders use** back to Default. Your accounts and their keys stay.
- **Perforce** leaves its stored password and its per-folder servers and users.
- **Keyboard**'s **Quick suffixes** puts the five built-in suffixes back, which deletes your own, so it asks first.

No **Reset to defaults** touches a project's files or the choices Claudette remembers for a project, and the project's pages have none.

## The project's pages

At the foot of the sidebar, above the version, is a group for the project of the tab selected in the main window, headed **Selected project**. It stays there however far you scroll the pages above it. Under the heading is the project's name when Claudette found one ("NightOwl"), otherwise the folder's name, after a dot in the color of the tab's group in the main window. Hover the name for the folder's path. Select another tab while Settings is open and the group follows it. With no tab open, the group isn't there, and while you search it's hidden with the pages. Each page begins with a line saying which tab it's for.

- **Links** lists the project's web links, from `claudette.json` and `claudette.local.json`, with **Add…**, **Edit…**, **Remove**, **Move up** and **Move down**.
- **Actions** is the same for the project's own actions, with **Open file**.
- **Tools** holds this machine's choices for the project: which project file when the folder has several, its editor configuration or code optimization, and the engine folder, Unity editor or Godot executable you picked, each with **Clear** to forget the pick. Without a project it says "No Unreal, Unity or Godot project in this folder". The defaults every project starts with are on its kind's page in Project tools, which **Open Unreal Engine** (or Unity, or Godot) at the foot of the page shows; without a project, **Open All tools**.

Each change is saved as you make it, to the file it belongs to, and the project's menu follows at once. Saving rewrites the file, so comments in it are dropped. A file that isn't valid JSON is never rewritten: the page shows why, and you fix the file by hand. [Actions and links](../project-tools/actions-and-links.md) explains what they do. The project's menu has **Add a link…** and **Add an action…**, which open Settings on these pages with a new entry started.

## The version and Help

The foot of the sidebar shows which Claudette this is, such as "Claudette 0.6.0". A copy built from a source checkout adds the commit it was built from.

- **Click the version** to copy the versions for a bug report: Claudette's, and how it was installed, Claude Code's, your OS and the .NET runtime it runs on. It says "Copied" for two seconds. It holds only versions, never paths, names or account details, so it's safe to post.
- **Help**, the question mark beside it, has:
  - **User Guide**, which opens this guide in your browser.
  - **Ask on Discord**, which opens dotBunny's Discord in your browser, where you can ask questions about Claudette.
  - **Report an issue**, which opens a new issue on Claudette's GitHub page in your browser, with an outline to fill in and the same versions already there. Nothing is sent until you submit it. [Reporting an issue](../troubleshooting/reporting-an-issue.md) has more.
  - **About Claudette**, which opens [About](about.md): the version, the trademarks Claudette names, and the licenses of what it's built with.

## Where the settings are kept

Claudette keeps its settings in its own file, separate from Claude Code's. Claudette doesn't change Claude Code's settings except where a page says it does. [Where Claudette keeps things](../privacy/where-claudette-keeps-things.md) says where the file is, and [Settings back to their defaults](../troubleshooting/settings-back-to-their-defaults.md) says what happens if it can't be read.

To keep the same settings on every machine that shares your session library, turn on **Sync Claudette's settings through the library** in Sessions. [Settings sync](../history/settings-sync.md) explains it, and [Every setting](every-setting.md) says which settings it carries.

## Shortcuts

| Action | Shortcut |
|---|---|
| Open Settings | `Ctrl/Cmd+,` |
| Search settings, while Settings is open | `Ctrl/Cmd+F` |
| Close Settings | Esc, or `Ctrl/Cmd+W` |

You can change the first in Settings → [Keyboard](keyboard.md).

## See also

- [Every setting](every-setting.md), a table for each page.
- [Tab settings](../tabs/tab-settings.md), the few settings one tab can change for itself.
