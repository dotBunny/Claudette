# Claudette User Guide

The plan for Claudette's user guide, the website at https://claudette.dotbunny.com that shows people how to use every part of Claudette. [DESIGN.md](DESIGN.md) says what Claudette does; this file says how the guide is built, how a page is written, how its pictures are taken, which page describes each part of Claudette, and how `CHANGELOG.md` is kept. `§n` still means a section of DESIGN.md, whose [User Guide](DESIGN.md#user-guide) section has the decisions behind the site.

Planned 2026-10-15. Built 2026-10-16: the site, every chapter, and the pictures the demo can show ([Steps](#8-steps)).

## 1. The site

- **Where it lives.** In `docs/` on `main`. GitHub Pages builds it from there itself (the repository's Settings → Pages → Source: **Deploy from a branch**, `main`, `/docs`) and publishes it at the domain.
  - `docs/CNAME` holds the domain, `claudette.dotbunny.com`. GitHub wrote it when the domain was set, and deleting it unsets the domain. The domain's DNS points at GitHub Pages, and the site answers over HTTPS.
  - Everything in `docs/` is published, the README's screenshots included. Nothing outside it is, so this file isn't, and neither is `CHANGELOG.md`: the sidebar's **Changelog** links to it on GitHub.
- **How it's built.** With Jekyll and the plugins GitHub Pages has ([its versions](https://pages.github.com/versions/)); `docs/Gemfile` names the `github-pages` gem, which pins the same versions for previewing. The theme is [Just the Docs](https://just-the-docs.com), as a `remote_theme` pinned to a release's tag. It gives a sidebar from each page's front matter (`nav_order`, `parent`, `has_children`), search built with the site, callouts, and an **Edit this page on GitHub** link.
  - **Why GitHub Pages' own build.** It needs no workflow to publish, and nothing of ours runs to build the site. The price is GitHub Pages' limits: only its own plugins, and nothing outside `docs/`. So the changelog is a link to `CHANGELOG.md` rather than a page made from it, and the Downloads page reads the releases through `jekyll-github-metadata`, one of those plugins ([Downloads and the changelog](#5-downloads-and-the-changelog)). A workflow of ours that built and published the site could do more, at the cost of keeping it.
  - `docs/_config.yml`: the title; `url: https://claudette.dotbunny.com` and no `baseurl`, since the site is the domain's root; the theme; the plugins (`jekyll-remote-theme`, `jekyll-seo-tag`, `jekyll-sitemap`, `jekyll-github-metadata` and `jekyll-redirect-from`); `repository`, for the releases; search; a link to the repository; the **Changelog** link; and the edit link, to `docs/` on `main`. GitHub Pages also turns on `jekyll-relative-links`, so a page links to another by its `.md` file, which reads on GitHub too.
  - **Addresses.** `permalink: pretty` makes a page's address a folder: `tabs/opening-tabs.md` is at `/tabs/opening-tabs/` (and `/tabs/opening-tabs`, which GitHub Pages sends there), and a chapter's `index.md` at its folder, `/tabs/`. A page's `.md` links follow its address, so they need nothing. Anything else that names a page, such as the landing page's links or `_config.yml`'s, writes its folder, `/downloads/`. The addresses used to end in `.html`, and those didn't carry over, except one that somewhere outside the site names: the page keeps it with `redirect_from`, which leaves a page at the old address that sends the reader on, as the privacy policy does ([Privacy policy](#privacy-policy-docsprivacy-policymd)).
  - **Light and dark.** The site follows the reader's system, as Claudette does by default. Just the Docs switches schemes but doesn't follow the system, so `_includes/head_custom.html` calls `jtd.setTheme` with Claudette's light or dark scheme from `prefers-color-scheme`, and again when that changes. The page has no theme switch of its own, so the pictures can follow the same setting ([Pictures](#3-pictures)).
  - **The reading width.** A page keeps Just the Docs' width, which keeps lines short enough to read easily. **Full width**, in the header before **Home**, spreads the page across the window beside the sidebar, for the wide tables and pictures, and **Reading width** puts it back. `_includes/header_custom.html` is the button, `_includes/head_custom.html` remembers the choice in the browser's local storage and applies it before the page is drawn, so it doesn't jump, and `_sass/custom/custom.scss` lays it out. The button shows only where the window is wider than the sidebar and the page together (Just the Docs' `lg`): a narrower window is filled already.
  - **The icon, favicon and wordmark:** `packaging/icon/build-icons.mjs` writes them to `docs/assets/` too, so they stay the app's ([Packaging and signing](DESIGN.md#packaging-and-signing)). The wordmark is her name in the icon's pixels.
  - **The social card**, `docs/assets/social-card.png`: the picture a link to the site shows where it's shared, 1280 by 640. It's the landing page's hero in dark: her and her name, its title, and the README's main window running off the edge. `_config.yml` gives it to every page as its `image`, which `jekyll-seo-tag` writes as `og:image` and `twitter:image`. `tools/Claudette.Demo/social-card.html` lays it out and `social-card.ps1` draws it with a headless Edge or Chrome; `screenshots.ps1` runs it last, so it keeps the window's latest picture. It's GitHub's size for a repository's social preview too.
  - **Its look.** Claudette's colors, from her icon: Claude's coral, her hair's rust and her hair tie's raspberry, over Anthropic's ivory or charcoal. `_sass/custom/setup.scss` holds them, `_sass/color_schemes/claudette-light.scss` and `claudette-dark.scss` are the schemes (with `assets/css/just-the-docs-claudette-*.scss` to switch to them), and `_sass/custom/custom.scss` the shapes: her colors across the top, rounder corners, pictures framed with a glow, and the *New in the next release* label as a pill. `_includes/title.html` puts her icon and name at the top of the sidebar, and `_includes/components/children_nav.html` (Just the Docs' own, changed) lists a section's chapters and a chapter's pages as cards, each with its `summary`.
- **The footers.** The landing page's and the guide's (`_includes/footer_custom.html`, Just the Docs' own replaced) say who makes Claudette, its license and where the privacy policy is, then the trademarks the site names, each with its owner's notice, and that none of them sponsors or endorses Claudette (`_includes/trademarks.html`). It's the list Settings → About has ([About](DESIGN.md#about)): a name added to one is added to the other.
- **The check**, `.github/workflows/docs.yml`. On a pull request or a push to `main` that changes `docs/`, it builds the site with `docs/Gemfile`'s gems and runs html-proofer over it, for internal links only. A broken link, a missing picture or a picture with no alt text fails the run instead of reaching the site. It also checks that `/privacy-policy.html` still sends the reader to the policy: nothing on the site links there, so html-proofer wouldn't notice it gone. It doesn't publish anything.
- **Previewing it.** With Ruby 3 installed, `host-docs.bat` (Windows) or `./host-docs.sh` (macOS and Linux) at the repository's root serves the site at http://127.0.0.1:4000 and builds it again as pages change: it runs `bundle install` the first time, then `bundle exec jekyll serve` in `docs/`, passing on what follows its name (`--livereload`, `--port 4001`). Each script's header says which Ruby to install. The Downloads page reads the releases from GitHub's API; without a token (`JEKYLL_GITHUB_TOKEN`) it still can, a few times an hour.
- **The landing page**, `docs/index.html`, is the site's front page, like a product's home page rather than a page of the guide: a page of its own outside Just the Docs (`_layouts/landing.html`, `assets/css/landing.css`, `assets/js/landing.js`), with a header for **Home**, **Docs**, **Downloads**, the changelog and GitHub. It has her and her name, the latest release's packages (from `site.github`), what it does for game developers (a card for each engine, then editor logs, Perforce and your own actions), a tour of ten features with the README's and the guide's pictures, the two styles, Claudette herself, cards for the rest, a card for each system it runs on (what you get on Windows, macOS and Linux, and how to install it) with the commands that build it on Linux, and a last call to download. Linux has its own button beside the packages', to **Installing on Linux**, and the hero's *Windows, macOS and Linux* goes to the systems' cards. Its pictures follow the reader's theme through `_includes/picture.html`, its icons are drawn inline (`_includes/icon.html`, from Lucide, with Godot's logo on its card), so it loads nothing from elsewhere, and its motion (pictures rising into view, the tour's index following along) stops with reduced motion, when her animation gives way to her still picture too. It reads as well on a phone as on a desktop: nothing reaches past the sides of the screen, and the tour's index scrolls sideways inside its pill, keeping the step in view in sight. What a release doesn't have yet is marked *Next release*.
- **The guide** starts at `/guide/`, the **Overview** (`docs/guide/index.md`), which **Docs** opens. Its chapters are grouped into sections in the sidebar, each a page in `docs/guide/` ([The chapters](#4-the-chapters)), so the sidebar has ten entries rather than twenty-three; **Getting started** and **Downloads** stand on their own.
- **Chapters and pages.** Each chapter in [The chapters](#4-the-chapters) is a folder of pages with an `index.md`, which says what the chapter covers and has its section as its `parent`; the theme lists its pages as cards. A page's file is its title in lowercase with hyphens: **Worktree tabs** is `docs/tabs/worktree-tabs.md`. Every chapter and page has a `summary` in its front matter, a line saying what it's for, which its card shows. Chapters sit in the sidebar in the map's order.

## 2. Writing a page

- **Who it's for.** People using Claudette, not working on it: what the part is for, how to do each thing (in numbered steps where there are steps), the settings that change it and its shortcuts. No class names, protocol or other internals.
- **The app's own words.** In bold, exactly as the app shows them: Settings → Appearance → **Style**, **Always allow**. Where DESIGN.md and the app disagree, the app wins, and DESIGN.md is corrected in the same change. Shortcuts are written as `Ctrl/Cmd+J`; the Reference chapter lists both.
- **One home for each part.** Each part of Claudette is explained on one page, the one the map names. Other pages say what it is in a sentence and link to it. A setting is explained on its feature's page, and Settings → **Every setting** links there.
- **The shape of a page.** Its front matter has its `title`, its chapter as its `parent`, its `nav_order`, and a `summary`: what the part is for, in a line.
  1. What the part is for, in a sentence or two, and a picture of it.
  2. How to do each thing with it, a heading each.
  3. **Settings**: each setting that changes it, its default, and a link to its Settings page.
  4. **Shortcuts**, if it has any.
  5. **See also**, for the pages it's easily confused with.
- **Windows, macOS and Linux.** A difference is written where it applies: "On macOS, …". Reference → **Windows, macOS and Linux** collects them.
- **Claude Code's part.** What Claude Code itself does (permission modes, hooks, MCP servers, `CLAUDE.md`, Plan mode) links to [Claude Code's docs](https://code.claude.com/docs) rather than being explained again. The page says what Claudette shows of it.
- **Only what's built.** Only what's built (✅ in DESIGN.md) has a page. A feature that's only designed gets its page when it's built, and a page that mentions one doesn't. Something built but still to verify on a real machine is covered like the rest.
- **Ahead of a release.** The site is published from `main`, so it can describe things no release has yet. A section about one starts with a *New in the next release* note (`{% include unreleased.html %}`), which publishing the release removes ([Downloads and the changelog](#5-downloads-and-the-changelog), "Releasing"). The change is under **Unreleased** in `CHANGELOG.md` meanwhile.

## 3. Pictures

- They're of the real app running the demo (`tools/Claudette.Demo`), never anyone's account or projects. The UI tests' headless renders won't do: they draw text about twice as wide as a screen does.
- `tools/Claudette.Demo/screenshots.ps1` takes the README's pictures, finding controls through UI Automation, driving them through Claudette's command channel so a run never takes your focus ([DESIGN.md §9](DESIGN.md#working-on-claudette), "Taking screenshots without the focus"), and capturing with `PrintWindow`, into `docs/screenshots/`. The guide's come from a sibling, `guide-screenshots.ps1` (`-Only` takes a shot's name or a chapter's), with the helpers both use in `Screenshots.psm1`. Each shot is a name, the steps to it, and the element to crop to, from its UI Automation bounds with a margin: a picture of the part a page describes reads better than the whole window. They go in `docs/screenshots/guide/<chapter>/<name>-<light|dark>.png`.
- In the Standard style, light and dark. The Claude style shows only on the pages about it: every picture in all four looks would double what the pictures add to the repository, for little (the README's 37 are 8.0 MB).
- A page shows the picture that matches the reader's theme, through an include, `{% include shot.html name="…" alt="…" %}`, which writes a `<picture>` with a `prefers-color-scheme: dark` source. The name is the picture's path under `docs/screenshots/` without `-light.png` or `-dark.png`: `guide/tabs/sidebar` for one of the guide's, `tasks-standard` for one of the README's, which pages use where they fit. Every picture has alt text that says what it shows, as the README's do.
- **What the demo doesn't show yet.** The demo grows what a chapter needs, or the script makes it happen with `fake-claude`'s prompts: a waiting permission (`ASK_PERMISSION`), a usage limit (`LIMIT`), usage credits paying (`FAKE_CLAUDE_USAGE=credits`, which the script doesn't take yet, so the notice is described without a picture), a process for the process monitor (`SPAWN`), a prompt suggestion (`SUGGEST`), a Claude Doc (`DOC`), and the sign-in screen (`FAKE_CLAUDE_LOGGED_IN=0`). A question, a plan to approve and an MCP server's form need `fake-claude` to learn them, as the mock API knows `ASK_QUESTION` and `EXIT_PLAN`. Perforce has no fake outside the tests, so its page can do without pictures.
- The script runs on Windows. What only macOS or Linux shows, such as the menu bar and the Dock icon's badge and animation, is captured by hand and named `-macos`, or described without a picture.

## 4. The chapters

Each chapter's table lists its pages in order, what each covers, and the DESIGN.md sections it's written from. A page is written from those sections, then checked against the running app.

The sidebar groups the chapters into sections (`docs/guide/<section>.md`), in this order:

| Section | Chapters |
|---|---|
| Overview | The guide's start (`docs/guide/index.md`): where to start, and what's in each section. |
| Downloads | Its own page. |
| Getting started | Its own chapter. |
| Sessions | Tabs and sessions, The conversation, The composer, Permissions and prompts, History and syncing. |
| Agents and threads | Plans, tasks and agents, Threads. |
| Code and changes | Reviewing changes, Project files, Project tools. |
| Usage and accounts | Usage and limits, Accounts. |
| Make it yours | Settings, Away from your desk, Claudette. |
| Help | Updates, Troubleshooting, Privacy and your data, Reference. |

### Home (`docs/index.html`)

The landing page ([The site](#1-the-site)): what Claudette is, the latest release's packages and building it on Linux, what it does for game developers, a tour of its main features with pictures, its two styles, Claudette herself, the systems it runs on, and where to start. From [§1](DESIGN.md#1-goals) and the README.

### Downloads (`docs/downloads.md`)

One page, second in the sidebar: the latest release's packages, the next version when a pre-release is newer, and every earlier release. Made from the releases ([Downloads and the changelog](#5-downloads-and-the-changelog)).

### Privacy policy (`docs/privacy-policy.md`)

One page, out of the sidebar and linked from both footers and the privacy chapter, at `/privacy-policy/`. The Microsoft Store listing names its old address, `/privacy-policy.html`, which the page keeps with `redirect_from` and `docs.yml` checks, so the listing doesn't need to change. In dotBunny's way (dotbunny.com/legal): short numbered sections in plain English, its date at the top. It says Claudette and the site collect nothing, and sums up what the privacy chapter's pages say in detail, so when one of them changes what leaves the computer, the policy and its date change with it. From [§2](DESIGN.md#2-platform--tech-stack) and the privacy chapter.

### Getting started (`getting-started/`)

| Page | Covers | From |
|---|---|---|
| What you need | Windows (x64 and arm64), macOS 12 or later, Linux on x64 from source (built and tested with every change, but no package); Claude Code installed first, at its minimum version, which the Claude desktop app isn't. | [§1](DESIGN.md#1-goals), [§2](DESIGN.md#2-platform--tech-stack) |
| Installing | Claude Code first. Which package to take from the Downloads page. The MSIX on Windows, which shares Claude Code's configuration with the terminal's `claude`. The `.dmg` on macOS, moving Claudette to Applications, and the privacy prompts that name Claudette when Claude reads a project in Documents or Downloads. | [Packaging and signing](DESIGN.md#packaging-and-signing) |
| Installing on Linux | Building Claudette from source on Linux, start to finish: what it needs (x64, X11 or Wayland through XWayland, the programs some features use and each distribution's packages for them), Claude Code's installer, cloning `main` or a release's tag, the .NET SDK from Microsoft's script and why a distribution's own won't do, `dotnet run`, a launcher in the app menu (a desktop entry that starts the build with `~/.dotnet`'s `dotnet`, since a launcher wouldn't find that .NET by itself), starting at login, keeping current with **Pull, build and restart**, and what to do when it doesn't start. | [§2](DESIGN.md#2-platform--tech-stack), [Working on Claudette](DESIGN.md#working-on-claudette), [Starting at login](DESIGN.md#starting-at-login) |
| The first launch | *Looking for Claude Code…*, the setup screen (**Update now**, **Install Claude Code**, **Check again**, **Choose claude…**, **Install instructions**), its note that the Claude desktop app isn't Claude Code, Claude Code's minimum version. | [§2](DESIGN.md#2-platform--tech-stack), [§12](DESIGN.md#12-claude-code-updates), [Installing Claude Code](DESIGN.md#installing-claude-code) |
| Signing in | The sign-in screen, finishing in the browser, **Enter a code instead**, **More options** (a Console account, SSO), **I've already signed in**. What can go wrong is in Troubleshooting. | [§11](DESIGN.md#11-sign-in) |
| Trying the demo | **Try the demo** on the sign-in screen and the setup screen: a Claudette of its own on made-up projects, offline, with a stand-in for Claude; what's in it (no project files), the stand-in's example turns, the banner and **Leave demo**, which takes what it made with it. | [Demo mode](DESIGN.md#demo-mode) |
| Your first tab | The new tab picker, choosing a folder, the folder trust question, sending a message, **Stop**. | [Opening a tab](DESIGN.md#opening-a-tab), [Folder trust](DESIGN.md#folder-trust), [Composer](DESIGN.md#composer) |
| A tour of the window | The header as the window's title bar (the logo, the menu with **User Guide**, the usage meters, the cup, the service status dot, the account, **Settings**, the window's buttons, and the system's title bar instead), the sidebar, the conversation, the tool windows and the composer; on macOS the menu bar. Each part links to its chapter. | [§3](DESIGN.md#3-main-window) |
| How Claudette uses Claude Code | Each tab runs the real `claude`, so your `CLAUDE.md`, settings, hooks, MCP servers, skills and plugins load as in a terminal; a folder's own configuration once it's trusted; where Claude Code keeps sessions; starting Claudette from a terminal inside Claude Code; the helper `claude` in the process list. | [§13](DESIGN.md#13-architecture), [Integration with Claude Code](DESIGN.md#integration-with-claude-code) |
| Running from source | Building and running Claudette from a checkout, running from a copy, new commits on the branch it tracks and pulling and building them, **New build ready** and restarting into new builds, `CLAUDETTE_RUN_IN_PLACE`, the commit in the version, and trying Claudette on the demo. | [Working on Claudette](DESIGN.md#working-on-claudette), [Version](DESIGN.md#version) |

### Tabs and sessions (`tabs/`)

| Page | Covers | From |
|---|---|---|
| Opening tabs | The new tab picker (search, favorites then recent folders in one list, **Browse…**, **Open from History…**, `1`–`9`, its right-click menu), a group's **+**, dropping a folder on the sidebar, **File → Open Recent** and the Dock menu on macOS, the jump list on Windows, `--folder`, one Claudette at a time. | [Opening a tab](DESIGN.md#opening-a-tab) |
| The sidebar | A tab's row: the status icons, the second line (*Needs your input*, *Possibly stuck*, the task in progress), the badges (pin, sync, phone, gear, running tasks, task progress, branch, changelist, account, context ring), the info card on hover, the rows under it (sub-threads, subagents, editor logs, project runs), collapsing a tab (the chevron, the badge, what opens it again), resizing, the rail and narrow windows. The top and the foot, each set apart by a line. | [Sidebar](DESIGN.md#sidebar), [§4](DESIGN.md#4-tabs--sessions) |
| Folder groups | Groups and their colors, **Rename…** and **Change color…**, collapsing, dragging tabs and groups, the group's menu (**New tab here**, **History for this folder**, **Account ▸**, **Close group**), Claude Code's projects (the setting, the offer, past sessions under a group). | [§4](DESIGN.md#4-tabs--sessions), [Claude Code's projects](DESIGN.md#sidebar) |
| Names, marks and pins | Claude Code's titles and the folder name before one, **Rename** and **Reset name**, **Name each tab's session in Claude Code after the tab**, **Mark ▸** and **Clear mark**, the reviewed icon over a mark, **Pin tab**. | [§4](DESIGN.md#4-tabs--sessions), [§13](DESIGN.md#13-architecture) |
| The tab's menu | Every item, each with a line and a link to its page; **Copy session ID**, **Duplicate tab**, **Move up** and **Move down** are explained here. | [Sidebar](DESIGN.md#sidebar) |
| The tab info card | Hovering a row or clicking **ⓘ**, and each row the card can show: folder and branch, model, session and **Reached as**, sync size, tokens, context, Perforce, project, Claude app, agents, running tasks, tasks, usage limit, worktree, account, extra folders. | [§4](DESIGN.md#4-tabs--sessions) |
| Tab settings | **Tab settings…**: the overrides in their sections (model, effort, permission mode, **Subagents**, **Continue tasks when the limit resets**, **Use usage credits**, check-ins, process monitor), each change applying as it's made, each section's **Use defaults**, **Done**, the dot beside it; and **Sync to other machines** (with the session's size in the library), **Connect to the Claude app** and **Extra folders**, which aren't overrides. | [Per-tab overrides](DESIGN.md#per-tab-overrides), [§4](DESIGN.md#4-tabs--sessions) |
| Tool windows | What they show and which tab, each window and when it has an icon (the dots, the tips), the strip's three groups, showing and hiding by an icon and **−**, the ways to each window, **Dock Pinned** and **Dock Unpinned**, moving them along the strip and with **Move to**, resizing by the gaps, what's remembered. | [Tool windows](DESIGN.md#tool-windows) |
| Split panes | **Open beside**, dragging a row onto a pane, the focused pane and what follows it, resizing and closing panes. | [Split panes](DESIGN.md#split-panes) |
| The command palette | `Ctrl/Cmd+Shift+P`, its sections and filtering, and **Go to the next tab waiting for you** (`Ctrl/Cmd+J`). | [Command palette](DESIGN.md#command-palette) |
| Worktree tabs | **New tab in a worktree**, how a worktree tab shows, Claude moving a tab into a worktree and out again, duplicating and branching in one, closing its last tab (**Remove**, **Keep**, **Remove and discard**), Settings → General → **Worktrees** and **Clean up now**. Setup actions for a new worktree are in Project tools. | [Worktree tabs](DESIGN.md#worktree-tabs), [Cleaning up worktrees](DESIGN.md#cleaning-up-worktrees) |
| Processes | The process monitor: turning it on, the composer bar's summary, the header's total, the **Processes** page and its menu, stopping a process. | [Process monitor](DESIGN.md#process-monitor) |
| MCP servers | The **MCP** page: each server's status, **Reconnect**, **Turn off**, **Sign in** and **Sign out**. A server's form is in Permissions and prompts. | [MCP servers](DESIGN.md#mcp-servers) |
| Closing and restoring | Closing a working tab and its confirmations (**Close and stop them**, **Close, leave running**), pinned tabs, restoring at launch and **Also restore unpinned tabs when Claudette starts**, tabs that start when selected, a folder that's gone, a session Claude Code deleted, **Restart** after `claude` stops. | [§4](DESIGN.md#4-tabs--sessions), [Restore on launch](DESIGN.md#restore-on-launch), [§13](DESIGN.md#13-architecture) |

### The conversation (`conversation/`)

| Page | Covers | From |
|---|---|---|
| Reading the conversation | The cards and what their colors mean in each style, times, following new output and **Jump to latest**, **Quieter in-between steps**, **Indent your messages** and **Indent Claude's steps**, **Full-width conversation**. | [§5](DESIGN.md#5-conversation-view), [Conversation cards](DESIGN.md#conversation-cards), [Copy and times](DESIGN.md#copy-and-times) |
| Tool calls | The **Tool calls** card and its tiles, a tile's detail and **Show all**, Edit and Write diffs, Bash's output and git chips, a call in a window of its own. | [Conversation cards](DESIGN.md#conversation-cards), [§5](DESIGN.md#5-conversation-view) |
| Thinking, notes and hooks | Thinking (**Show thinking expanded**, **Collapse all thinking**), notes, warnings and errors, API retries, hook runs and **Show every hook run in the conversation**. | [§5](DESIGN.md#5-conversation-view), [Hook runs](DESIGN.md#hook-runs), [Notices](DESIGN.md#notices) |
| Subagents | A subagent's card, its own cards and report, subagents in the background. **Show subagents' work** under the tab: a subagent's row, its work in the tab, **Message it through Claude**, rows from earlier turns and dismissing them. Settings → Tabs → **Subagents**. The agent map is in Plans, tasks and agents. | [§5](DESIGN.md#5-conversation-view), [Subagents](DESIGN.md#subagents) |
| The end of a turn | The **Modified files** and **Turn** cards, the **Tasks** card, the Claude Docs a turn wrote. | [Conversation cards](DESIGN.md#conversation-cards), [Changes per turn](DESIGN.md#changes-per-turn) |
| Finding your way | The scroll bar's marks, `F3` and `F4`, `Ctrl/Cmd+F3` and `Ctrl/Cmd+F4`, find (`Ctrl/Cmd+F`). | [Conversation cards](DESIGN.md#conversation-cards), [Find](DESIGN.md#find) |
| Copying and quoting | **Copy**, **Copy message**, **Quote in reply**, **Quote message**, a code block's header (**Wrap lines**, **Copy**). | [Copy and times](DESIGN.md#copy-and-times) |
| The scratch pad | One pad per project, **Add to scratch pad**, **Copy all**, **Clear…**, how it syncs and its conflicts (**Keep both**, **Use theirs**, **Keep mine**). | [Scratch pad](DESIGN.md#scratch-pad) |
| Exporting | **Export conversation…** to Markdown or a web page, and what's left out. | [Export](DESIGN.md#export) |
| Rewind and branch | **Edit and resend…**, **Branch from here**, **Restore files to before this…** and **Keep copies of files Claude changes**, **Duplicate tab**. | [Rewind and branch](DESIGN.md#rewind-and-branch) |
| Images | Images from Claude's tools, images in its replies and **Load image**, the image window (**Copy image**, **Save as…**, **Open**, **Pause** and **Play**, and zooming with the wheel, the keys and **Fit**); image files and links to images opening there, and Settings → Files and diffs → **Open images in**. | [Images from Claude's tools](DESIGN.md#images-from-claudes-tools), [Images in Claude's replies](DESIGN.md#images-in-claudes-replies), [Opening images](DESIGN.md#opening-images) |
| Files and links in replies | Clicking a file, a line, a folder or a web link; several files with one name; a file that isn't there; what's never opened. | [Files named in Claude's replies](DESIGN.md#files-named-in-claudes-replies) |
| The working line and running tasks | The line above the composer, **Show fun words while Claude works** and **Show what Claude is doing while it works**; the running tasks chip, its list and **Stop**. | [Working line](DESIGN.md#working-line), [Running tasks](DESIGN.md#running-tasks) |

### The composer (`composer/`)

| Page | Covers | From |
|---|---|---|
| Sending and stopping | **Send with** and the chip that shows it, **Stop** and `Esc`, messages sent while Claude works: into the turn, or queued with **Send now** and **Cancel**. | [Composer](DESIGN.md#composer), [Queued messages](DESIGN.md#queued-messages) |
| The composer bar | The attach button, **Suffixes ▾**, **Stashed ▾**, model, effort and mode on the left; processes, **Agents**, running tasks, **Files**, context, the prompt cache, tokens and **ⓘ** on the right; the send key chip before **Send**, which switches **Send with**; the status line under the box, with the folder. Each links to its page. | [Composer](DESIGN.md#composer) |
| Slash commands and mentions | `/` commands; `@` files and folders, the extra folders' among them; typing a path, anywhere on the computer or into a folder of the project, with ignored files dimmed; `@` sessions, tabs and agents, which Threads explains. | [Autocomplete](DESIGN.md#autocomplete) |
| Attachments and large pastes | Dropping, pasting and the paper clip; images and their limits; files and folders as `@` mentions; large pastes as a chip, and `Ctrl/Cmd+Shift+V`; long messages shown short. | [Attachments](DESIGN.md#attachments) |
| Earlier prompts, drafts and the stash | `Up` and `Down`, drafts kept when Claudette closes, **Stash the message** (`Ctrl/Cmd+S`) and **Stashed ▾**. | [Composer](DESIGN.md#composer), [Drafts and the stash](DESIGN.md#drafts-and-the-stash) |
| Quick suffixes | **Suffixes ▾** (`Ctrl/Cmd+Shift+S`), the chips, **Keep on this tab**, each suffix's shortcut, Settings → Keyboard → **Quick suffixes** and the built-in suffixes. | [Quick suffixes](DESIGN.md#quick-suffixes) |
| Model, effort and output style | Switching the model and its confirmation, effort, **Ultracode**, **Output style**, **Fallback model**. The permission mode is in Permissions and prompts. | [Model & effort](DESIGN.md#model--effort) |
| Prompt suggestions | **Suggest my next prompt**, and taking one with `Tab` or `→`. | [Prompt suggestions](DESIGN.md#prompt-suggestions) |
| Check-ins on long turns | When a check-in is sent, its countdown with **Send now** and **Don't check in**, *Possibly stuck*, Settings → Tabs → **Check-ins on long turns** and a tab's own. | [Check-ins on long turns](DESIGN.md#check-ins-on-long-turns) |

### Permissions and prompts (`permissions/`)

| Page | Covers | From |
|---|---|---|
| Permission cards | What a card shows, **Allow**, **Always allow…** and editing its rule, **Allow for this session only**, **Allow all edits this session**, **Deny** and **Deny with a message…**, several prompts waiting, a subagent's prompt, answering from the keyboard. | [§7](DESIGN.md#7-permission-prompts) |
| Permission modes | The mode picker, when Auto isn't offered, Bypass permissions and its warning, the mode a tab starts in. | [§7](DESIGN.md#7-permission-prompts), [Starting mode](DESIGN.md#starting-mode) |
| Questions | The question card, **Send answers** and **Dismiss**. | [§7](DESIGN.md#7-permission-prompts) |
| Approving a plan | The plan card, **Approve, auto mode** and the other approvals, **Keep planning…**, versions and **Changes from v1**. Following the plan is in Plans, tasks and agents. | [§7](DESIGN.md#7-permission-prompts), [Tasks](DESIGN.md#tasks) |
| MCP servers asking for input | A server's form, **Send**, **Open in browser** and **Done**, **Decline**. | [§7](DESIGN.md#7-permission-prompts) |
| Folder trust | **Trust this folder?** and what it lists, **Start without it**, the tab menu's **Trust this folder**, **Ask before using a new folder's own configuration**. | [Folder trust](DESIGN.md#folder-trust) |

### Plans, tasks and agents (`plans/`)

| Page | Covers | From |
|---|---|---|
| The Tasks page | The task list, kept as Claude Code keeps it (a list shared with `CLAUDE_CODE_TASK_LIST_ID` too), the plan and its versions, **Use as the plan** and **Show as the plan**, task rows in the conversation, **Continue**, progress on the row and the status line, **Give Claude a task list**. | [Tasks](DESIGN.md#tasks) |
| Claude Docs | Docs Claude writes with claude.ai's Docs connector: the link on the tile, the turn's rows, the Tasks page's **Docs**. | [Claude Docs](DESIGN.md#claude-docs) |
| The agent map | The **Agents** page and its window, a subagent's details, stopping one, **Summarize subagents' progress**. | [Agent map](DESIGN.md#agent-map) |

### Threads (`threads/`)

| Page | Covers | From |
|---|---|---|
| What threads are | A thread hands work to sub-threads in its folder group; what Claude is told; prompts stay yours; what threads don't do. | [Threads](DESIGN.md#threads) |
| Making a thread | The **Thread** submenu (**Make this tab a thread**, **New sub-thread** and **New sub-thread in a worktree**, **Assign to thread**, **Detach from thread**, **Stop being a thread**), threads in the sidebar and collapsing one, **Show with its sub-threads**. | [Threads](DESIGN.md#threads), [Split panes](DESIGN.md#split-panes) |
| Sending work and getting reports | **Ask before sending to sub-threads** and its question, *From the thread*, the *From the sub-threads* report and how a sub-thread's work can end. | [Threads](DESIGN.md#threads) |
| Sub-threads waiting on you | The strip over the thread's composer: **Allow**, **Deny**, **Go to tab**. | [Threads](DESIGN.md#threads) |
| Reaching other sessions | A session's name in Claude Code, **Reached as**, `@` sessions and tabs, **Copy session ID**, *From the session*. | [§13](DESIGN.md#13-architecture), [Autocomplete](DESIGN.md#autocomplete) |

### Reviewing changes (`changes/`)

| Page | Covers | From |
|---|---|---|
| Changed files | The **Changed files** page and the **Files** count, the line counts as this session's own, *Changed since* and what it's by, the line for what the lines don't show (line endings, byte order mark, newline at the end, whitespace, a file's mode), keeping up with the disk, the groups for several working trees, **Working tree vs HEAD**, a file whose earlier contents aren't known (subagents' included). | [§8](DESIGN.md#8-file-changes--diff-view), [This session's changes](DESIGN.md#this-sessions-changes), [What the lines don't show](DESIGN.md#what-the-lines-dont-show), [Working trees](DESIGN.md#working-trees), [Subagents' changes](DESIGN.md#subagents-changes) |
| The diff view | Side by side or inline, **Whole file**, **Wrap lines**, **With later changes**, what changed that no line shows and whitespace shown as · and →, the file's actions, **Open** and **Open in the OS's app**. | [§8](DESIGN.md#8-file-changes--diff-view), [What the lines don't show](DESIGN.md#what-the-lines-dont-show) |
| Reviewed files | Ticking files, what unticks them, the diff view's **Reviewed**, the row's reviewed icon. | [§8](DESIGN.md#8-file-changes--diff-view) |
| Reverting | **Revert** on a change, **Revert file**, and what can't be reverted. | [Reverting](DESIGN.md#reverting) |
| Changes per turn | A turn's **Modified files** and its read-only diffs. | [Changes per turn](DESIGN.md#changes-per-turn) |
| External diff tools | Settings → Files and diffs → **Diff tool**: **Built-in**, the presets, a custom command and its placeholders, **Test**. | [External diff tool](DESIGN.md#external-diff-tool) |

### Project files (`project-files/`)

| Page | Covers | From |
|---|---|---|
| Opening project files | The **Project files** button, **Open project files**, **Go to file…** (`Ctrl/Cmd+P`), its sidebar row, panes, and what it doesn't do (no language features, terminal or source control). | [Project files](DESIGN.md#project-files) |
| The explorer | The tree, dimmed and linked entries, following the disk, new files and folders, renaming and deleting, its menus, **Show only changed files**, its side and width, hiding it. | [Project files](DESIGN.md#project-files) |
| Finding a file by name | The find box, `name:42`, its keys and messages. | [Project files](DESIGN.md#project-files) |
| Finding in files | **Find in files** and **Replace in files** (`Ctrl/Cmd+Shift+F`, `Ctrl/Cmd+Shift+R`), the palette and **Find in folder…**; where a Files tab offers it (with nothing open, Go to file's link, the editor's find box's button, the explorer's menus); the popup: the chevron between finding and replacing, the box and its options, **File mask**, **In project** and **Directory**, the matches and the preview, opening one; **Replace** and **Replace all**, open files changed unsaved, the rest on disk; **Open in Find window**. | [Project files](DESIGN.md#project-files) ("Finding in files") |
| File tabs | Preview and kept tabs, **Pin**, the **Close…** commands, moving between files, a file in a window of its own (**Open in a window**, the row's button, dragging a tab out, **Put back**). | [Project files](DESIGN.md#project-files) |
| Editing | The editor, **Wrap long lines**, languages and colors, the chip (line endings, indentation, encoding), files that open read-only or not at all. | [Project files](DESIGN.md#project-files) |
| Markdown files | **Edit**, **Preview** and **Edit and preview**, what the preview shows, scrolling together, links and images in it, files too large to preview. | [Markdown preview](DESIGN.md#markdown-preview) |
| Saving | Saving and **Save all**, unsaved edits kept on disk, the questions on closing, a file that changes on disk and its banners. | [Project files](DESIGN.md#project-files) |
| Claude's changes in the editor | The marks in the explorer and the gutter, the peek and **Revert**, `F3` and `F4`, the mode bar, **Reviewed**, **Against git HEAD**. | [Project files](DESIGN.md#project-files) |
| Opening files from the conversation | Which files open where, at the line named, in which Files tab, and **Show a file clicked in a conversation**. | [Project files](DESIGN.md#project-files), [Files named in Claude's replies](DESIGN.md#files-named-in-claudes-replies) |

### Usage and limits (`usage/`)

| Page | Covers | From |
|---|---|---|
| The usage header | The session and weekly meters, their colors and the warning levels, the trendline and the line under it, the notice while usage credits pay (**Manage on claude.ai**), the model meters and reading them from `/usage`, a reading's "as of" time. | [Header meters](DESIGN.md#header-meters), [Burn trendline](DESIGN.md#burn-trendline), [Usage credits](DESIGN.md#usage-credits) |
| The detailed header | **Detailed usage header**: the charts, **Burn rate**, **Time to limit**, **Busiest tabs**. | [Detailed header](DESIGN.md#detailed-header) |
| The Usage window | Tabs in this session window, **Projects** and its period, **Accounts**, past sessions and weeks. | [Burn trendline](DESIGN.md#burn-trendline), [Usage history](DESIGN.md#usage-history) |
| A tab's tokens | The composer bar's token count and its breakdown. | [§4](DESIGN.md#4-tabs--sessions) |
| Context and compacting | The context indicator, the row's ring, the breakdown, **Compact now**, *Conversation compacted*. | [Per-tab context](DESIGN.md#per-tab-context) |
| The prompt cache | The **Cache** chip, its tooltip, **Show when the prompt cache expires**. | [Prompt cache](DESIGN.md#prompt-cache) |
| Alerts | When alerts come (usage credits starting to pay too), where they show, turning them off. | [Alerts](DESIGN.md#alerts), [Usage credits](DESIGN.md#usage-credits) |
| When a limit stops a task | The limit bar, continuing when the limit resets, **Don't continue**, **Continue when it resets**, why it won't continue by itself, a tab's **Auto-continue**, **Play a game while you wait**. | [Continuing after a limit resets](DESIGN.md#continuing-after-a-limit-resets) |
| Tabs that don't use usage credits | **Use usage credits once a limit is used up**, the tab menu's **Use usage credits** and **Tab settings…**' **Usage credits**, a message held for the reset (**Send now**, **Cancel**), a turn stopped when credits begin paying, the bar's wording and its **Use usage credits**. | [Tabs that don't use usage credits](DESIGN.md#tabs-that-dont-use-usage-credits) |
| Usage history | What's kept and for how long, **Keep usage history**, **Clear usage history…**. | [Usage history](DESIGN.md#usage-history) |
| Sharing usage across machines | **Share usage with my other machines**, what it adds, **Clear usage history on every machine…**. | [Sharing across machines](DESIGN.md#sharing-across-machines) |

### Accounts (`accounts/`)

| Page | Covers | From |
|---|---|---|
| Your Claude sign-in | The **Default** account, the header's account menu, **Plan and billing** or **Console billing**, **Sign out…** and what it stops. | [§11](DESIGN.md#11-sign-in), [Accounts](DESIGN.md#accounts) |
| API-key accounts | Settings → Accounts: **Add an API key**, where the key is kept, **Change key…**, **Remove…**; on Linux, `secret-tool`. | [Accounts](DESIGN.md#accounts) |
| A folder's account | A group's **Account ▸**, **New folders use**, the account tags. | [Accounts](DESIGN.md#accounts) |
| Moving a tab to another account | A tab's **Account ▸**, and what moving it costs. | [Accounts](DESIGN.md#accounts) |
| Spend and its warnings | The header's spend, the warnings, the Usage window's **Accounts**, and what an API-key account doesn't have (plan meters, the Claude app). | [Accounts](DESIGN.md#accounts) |

### History and syncing (`history/`)

| Page | Covers | From |
|---|---|---|
| History | `Ctrl/Cmd+Shift+H`, the project chips, a row's details (with a library session's size), searching and **Search Claude's replies too**, opening a session, worktree sessions. | [History](DESIGN.md#history) |
| The session library | What it is, its folder and **Move library…**, how much it holds, a cloud-synced folder and its warning, **Name for this machine**, **Delete sessions not used for a while** and its days, keeping sessions past Claude Code's 30 days. | [Session library](DESIGN.md#session-library-sync-across-machines) |
| Syncing a tab | **Sync to other machines**, **Sync new tabs to the session library**, **Sync now** and its notes, how much a session takes in the library. | [Session library](DESIGN.md#session-library-sync-across-machines) |
| Picking up on another machine | Finding the folder, the warning when the code may differ, one machine at a time (**Open a copy**, **Take over**, a read-only tab), coming back to a session another machine carried on (picking it up, the first message held back, a session that went on in both places), conflict copies. | [Session library](DESIGN.md#session-library-sync-across-machines) |
| Settings sync | **Sync Claudette's settings through the library**, the first choice it asks, what syncs and what stays on this machine. | [Settings sync](DESIGN.md#settings-sync-optional) |

### Project tools (`project-tools/`)

| Page | Covers | From |
|---|---|---|
| Using project tools | The project's icon on its group's row and its menu, the **Project** page, runs under the tab's row and a run's log in the main area with its links, **Stay current** and **Wrap lines**, **Run the project's main action** (`Ctrl/Cmd+Shift+E`), **A project action finishes**, Settings → Project tools (**All tools**, and a page for each kind of project with its icon, **Recognize … projects**, **Tell Claude about … projects**, its defaults and *Off* in the sidebar) and the project's **Tools** page. | [Project tools](DESIGN.md#project-tools), [The project's pages](DESIGN.md#the-projects-pages) |
| Unreal Engine | Its actions, **Editor configuration**, finding the engine and **Choose engine folder…**, **Open solution** and **Open in Rider**, the ones that ask first, its page in Settings → Project tools. | [Project tools](DESIGN.md#project-tools) |
| Unity | Its actions, **Code optimization**, **Choose Unity editor…**, what's refused while Unity has the project open, its page in Settings → Project tools. | [Project tools](DESIGN.md#project-tools) |
| Godot | Its actions, the Godot executable, C# projects, its page in Settings → Project tools. | [Project tools](DESIGN.md#project-tools) |
| Editor logs | The row under the tab while the editor, a commandlet or a batch run has the project open (the icon, the counts, *Closed* and *Crashed*, its menu), the log in the main area (colored lines and entries, Unreal's columns, **Stay current**, **Wrap lines**, **Open file**, **Copy**, the 20,000 lines), filtering it (the level chips, **Categories**, **Filter lines**, what's remembered for the project), closing it and when it comes back, Unity 6.5 moving its log to the project's (*Moved to Logs/Editor.log*, the new row), which tab it's under, which logs each engine writes, **Show editor logs under the tab**. | [Project tools](DESIGN.md#project-tools) ("Editor logs"), [Sidebar](DESIGN.md#sidebar) |
| Actions and links | Your own: Settings → the project's **Actions** and **Links**, **Add an action…** and **Add a link…**, the two files, a link's placeholders, setting up each new worktree. Reference has the files' format. | [Project tools](DESIGN.md#project-tools), [The project's pages](DESIGN.md#the-projects-pages) |
| Perforce | **Keep Perforce logins fresh**, the password prompt, **Password source**, servers that need you to log in yourself, the changelist on the row, **Open in P4V**; in the Files tab, the marks and the Perforce menu; opening files in Perforce as they change, and its five switches. | [Perforce ticket handling](DESIGN.md#perforce-ticket-handling), [Perforce changelist](DESIGN.md#perforce-changelist-in-the-tab-title) |

### Away from your desk (`away/`)

| Page | Covers | From |
|---|---|---|
| Notifications | Each kind and its switch, what they say, when they're skipped, where a click goes. | [§10](DESIGN.md#10-notifications) |
| The Dock and taskbar icon | The badge, the icon's animation, the taskbar's flash. | [§10](DESIGN.md#10-notifications) |
| The Claude app | **Connect to the Claude app**, **Open in the Claude app**, the phone icon, pushes to your phone, who can't use it. | [Remote Control](DESIGN.md#remote-control-the-claude-app) |
| Keeping the computer awake | Settings → General, the header's cup, when it holds off sleep. | [Keeping the computer awake](DESIGN.md#keeping-the-computer-awake) |
| Starting at login | **Start Claudette when I log in**, which copy starts, when the switch is turned off elsewhere. | [Starting at login](DESIGN.md#starting-at-login) |

### Claudette (`claudette/`)

| Page | Covers | From |
|---|---|---|
| Claudette on the composer | What she does and why, poking and carrying her, her hats, her settings, **Hide Claudette**. | [Claudette on the composer](DESIGN.md#claudette-on-the-composer) |
| Games with Claudette | Starting a game, the four games and their levels, the score. | [Games with Claudette](DESIGN.md#games-with-claudette) |

### Settings (`settings/`)

| Page | Covers | From |
|---|---|---|
| The Settings window | Opening it, and from a feature's own menu; staying open beside the main window; the pages in their groups; a setting's row, **More**, the changed bar and **Reset**, *This computer* and the tabs that have their own; its search; changes applying at once; each section's **Reset to defaults** and what it leaves alone; the project's pages; the version and **Help** (**User Guide**, **Ask on Discord**, **Report an issue** and **About Claudette**). | [§14](DESIGN.md#14-settings), [Version](DESIGN.md#version) |
| Every setting | A table per page: each setting's label, its section, its default, whether it syncs, whether a tab can override it, and the page that explains it. | [Pages](DESIGN.md#pages), [Settings sync](DESIGN.md#settings-sync-optional) |
| Styles and themes | **Theme**, **Style** (Standard, after JetBrains Rider, and Claude, with pictures of both in light and dark), the islands both have, **Density**, the fonts (Standard's Inter and JetBrains Mono), what the tab rows show. | [Visual style](DESIGN.md#visual-style) |
| Accessibility | Screen readers and what they hear, **Zoom**, **Motion**, and what the editor can't do yet. | [Accessibility](DESIGN.md#accessibility) |
| Keyboard | **Send with**, changing a shortcut, one key for both OSes, the shortcuts it refuses. Reference lists them all. | [Keyboard shortcuts](DESIGN.md#keyboard-shortcuts) |
| Git | The first page under **Source control**: what it gathers (the branch on tab rows, worktree cleanup, the Files tab's two choices, each explained on its feature's page), and **Path to git**: what Claudette runs git for, the git and version found, and that Claude's own git commands don't use it. | [Pages](DESIGN.md#pages) |
| Advanced | **Extra arguments for every claude process**, **Log protocol traffic**, **Open log folder**, **Open data folder**, **Diagnostics**. | [§14](DESIGN.md#14-settings), [Staying tolerant at runtime](DESIGN.md#staying-tolerant-at-runtime) |
| About Claudette | A window of its own; opening it (**About Claudette** in the header's menu, in Settings' **Help** and the macOS app menu, the palette); the version, **On the web** (**Website**, **Source code**, **Changelog**, **Privacy policy**) and Claudette's **MIT License**; **Contributors** (**GitHub**, **Pull requests**); **Trademarks**, each name with its owner's notice, and Godot's logo used under its trademark policy; **Open-source licenses** (**Website**, **License**, **Notices**). | [About](DESIGN.md#about) |

### Updates (`updates/`)

| Page | Covers | From |
|---|---|---|
| Claude Code's updates | The badge, what **Update now** does for each way of installing it, **Update on next launch**, **Dismiss**, tabs keeping their version until they restart, updates turned off. | [§12](DESIGN.md#12-claude-code-updates) |
| Claudette's updates | **Check for Claudette updates automatically** and **Include pre-releases**, the badge, **Restart to update**, **Update when idle**, **Skip this version**, copies that can't update themselves, upgrading from 0.1.0; what's new in each version is in the changelog. | [Updating Claudette](DESIGN.md#updating-claudette) |
| When an update fails | What comes back, installing by hand, moving Claudette to Applications. | [Updating Claudette](DESIGN.md#updating-claudette) |

### Troubleshooting (`troubleshooting/`)

| Page | Covers | From |
|---|---|---|
| Claude Code missing or too old | The setup screen, installing Claude Code from it, the Claude desktop app taken for Claude Code, and the notes about a version newer or older than the one Claudette was tested with. | [§12](DESIGN.md#12-claude-code-updates), [Installing Claude Code](DESIGN.md#installing-claude-code), [Tested versions](DESIGN.md#tested-versions) |
| Signing in | When signing in fails, **Try again**, an organization that isn't allowed. | [§11](DESIGN.md#11-sign-in) |
| A tab that won't start | The reasons a tab gives, **Restart**, **Choose folder…**, a folder or a session that's gone. | [§4](DESIGN.md#4-tabs--sessions), [§13](DESIGN.md#13-architecture), [Restore on launch](DESIGN.md#restore-on-launch) |
| A tab that seems stuck | *Possibly stuck*, check-ins, **Stop**. | [Check-ins on long turns](DESIGN.md#check-ins-on-long-turns) |
| Claude can't find a program | On macOS and Linux, the login shell's environment, the shells it reads, **Use my login shell's environment**. | [Login shell environment](DESIGN.md#login-shell-environment) |
| Something missing on Linux | `notify-send` for notifications, `systemd-inhibit` for keeping awake, `secret-tool` for passwords and keys; and a `dotnet run` build's notifications on macOS. | [§10](DESIGN.md#10-notifications), [Keeping the computer awake](DESIGN.md#keeping-the-computer-awake), [Accounts](DESIGN.md#accounts) |
| Claude is having problems | The service status dot and banner, and **Show Claude's service status**. | [Service status](DESIGN.md#service-status) |
| Diagnostics and logs | **Diagnostics** and **Copy diagnostics**, `claudette.log`, the protocol log, the note when Claudette couldn't show part of a conversation. | [Staying tolerant at runtime](DESIGN.md#staying-tolerant-at-runtime), [§13](DESIGN.md#13-architecture) |
| Reporting an issue | Asking a question on dotBunny's Discord (**Ask on Discord**), and what goes to GitHub instead; copying the versions, **Report an issue**. | [Version](DESIGN.md#version), [User Guide](DESIGN.md#user-guide) |
| Settings back to their defaults | A settings file that isn't valid kept as `.bad`, and one that couldn't be read. | [Storage](DESIGN.md#storage) |

### Privacy and your data (`privacy/`)

| Page | Covers | From |
|---|---|---|
| Where Claudette keeps things | The data folder on each OS and `CLAUDETTE_HOME`, what's in it (settings, state, usage history, drafts, unsaved edits, logs, downloads), the session library's contents, and Claude Code's own files. | [Storage](DESIGN.md#storage), [§9](DESIGN.md#9-sessions-history-restore--sync), [Usage history](DESIGN.md#usage-history) |
| What leaves this computer | The session library and its synced folder, usage sharing, settings sync, the status page, the update check. What Claude Code sends is Claude Code's. | [Session library](DESIGN.md#session-library-sync-across-machines), [Sharing across machines](DESIGN.md#sharing-across-machines), [Service status](DESIGN.md#service-status), [Updating Claudette](DESIGN.md#updating-claudette) |
| Passwords and keys | Perforce passwords and API keys in the OS's credential store, never synced or logged. | [Perforce ticket handling](DESIGN.md#perforce-ticket-handling), [Accounts](DESIGN.md#accounts) |

### Reference (`reference/`)

| Page | Covers | From |
|---|---|---|
| Keyboard shortcuts | Every shortcut, with its default on Windows and Linux and on macOS; the fixed keys; the editor's keys. | [Keyboard shortcuts](DESIGN.md#keyboard-shortcuts), [Project files](DESIGN.md#project-files) |
| Icons and badges | Every status icon, badge and dot, what it means, and the page that explains it. | [Sidebar](DESIGN.md#sidebar), [§3](DESIGN.md#3-main-window) |
| Notifications | Every kind, its switch and what it says. | [§10](DESIGN.md#10-notifications) |
| Command line and environment | `--folder` and `--login`; `CLAUDETTE_HOME` and `CLAUDETTE_RUN_IN_PLACE`; Claude Code's own variables, which pass through to `claude`; what project actions get (`CLAUDETTE_WORKTREE`, `CLAUDETTE_MAIN_CHECKOUT`) and what a login shell sees (`CLAUDETTE_RESOLVING_ENVIRONMENT`). | [Opening a tab](DESIGN.md#opening-a-tab), [Integration with Claude Code](DESIGN.md#integration-with-claude-code), [Login shell environment](DESIGN.md#login-shell-environment), [Project tools](DESIGN.md#project-tools) |
| `claudette.json` | The format of `claudette.json` and `claudette.local.json`: actions and their fields, links and their placeholders, comments, and what's skipped. | [Project tools](DESIGN.md#project-tools) |
| Windows, macOS and Linux | What differs on each: installing, the window (X11 and XWayland on Linux), menus, notifications, the credential store, keeping awake, starting at login, what isn't tested yet. | [§2](DESIGN.md#2-platform--tech-stack), the pages' own notes |

### Changelog

Last in the sidebar: a link to `CHANGELOG.md` on GitHub, which says what changed in each version, newest first, with what's on `main` and not yet released at the top ([The changelog](#the-changelog)).

## 5. Downloads and the changelog

The Downloads page is made from the repository's releases rather than written, and the changelog is `CHANGELOG.md` itself.

### The Downloads page

- **The latest release** comes first: its version and date, a link to its release on GitHub for what's new in it (its notes are its section of the changelog), a link to the changelog, and a download for each package.
  - **Windows**: **x64 (Intel or AMD)** and **Arm64**, the MSIX files.
  - **macOS**: **Apple silicon** and **Intel**, the `.dmg` files.
  - **Linux**: no package, and a link to Getting started → **Installing on Linux**.
  - Each package shows its file's name and size, and its SHA-256 digest, for anyone who wants to check the file (`Get-FileHash` in PowerShell, `shasum -a 256` on a Mac). Claudette's own updates check it themselves ([Updating Claudette](DESIGN.md#updating-claudette)).
- **Which one you need.** On Windows, Settings → System → About → **System type**. On a Mac, Apple menu → **About This Mac** → **Chip**: an Apple M-series chip is Apple silicon.
- **The next version**, when a pre-release is newer than the latest release: in a box of its own, marked as a pre-release, saying that Claudette offers pre-releases only with Settings → General → **Include pre-releases**.
- **Earlier releases**, newest first: each with its date, its packages, and a link to its release. 0.1.0, from before the repository moved, says it can't update itself to a later version, which is installed by hand.
- Then links to Getting started → **What you need** and **Installing**, and to Updates → **Claudette's updates**: once installed, Claudette updates itself.
- **Which file is which.** `package.yml` names the packages `Claudette-<version>-<x64|arm64>.<msix|dmg>` ([Packaging and signing](DESIGN.md#packaging-and-signing)), as the update check expects. The page reads the OS from the extension and the processor from the name, and lists anything else attached to a release under **Other files**.
- **The data.** `jekyll-github-metadata`, one of GitHub Pages' own plugins, gives the page the repository's releases as `site.github.releases` and the latest as `site.github.latest_release`, with each package's name, size, SHA-256 digest and download address. `_includes/release.html` lays out one release's packages. The next version is the newest pre-release published after the latest release. Drafts never show, and pre-releases older than the latest release are left out.
- **When it changes.** The page is built with the site, by GitHub Pages, on a push to `main`, not when a release is published: the change that follows a release ([Releasing](#the-changelog), step 3) brings it up to date. Without the releases, as in a preview with no network, it links to the Releases page on GitHub instead. It needs no JavaScript.
- The home page's download button goes here, and so does the README's link, once the page is published.

### The changelog

- **`CHANGELOG.md`**, at the repository's root, says what changed in each version, for the people using Claudette. It follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/):
  - `## [Unreleased]` at the top, then `## [X.Y.Z] - YYYY-MM-DD` for each release, newest first.
  - Under each, only the headings it needs, in this order: `### Added`, `### Changed`, `### Deprecated`, `### Removed`, `### Fixed`, `### Security`.
  - At the bottom, a link for each version: Unreleased compared with the latest release, each release with the one before it, and the first release's own page.
- **What goes in.** Anything people using Claudette could notice: what they can do now, what works differently, what's going away or gone, what was fixed that they could have run into, and what affects their security. Not refactoring, tests, CI, or changes to DESIGN.md or this file.
  - A fix to something no release has had yet changes that thing's entry under **Unreleased**, rather than adding one under **Fixed**.
  - A change Claude Code made that Claudette now handles goes in when people can tell, such as the version Claudette is tested with.
- **How it's written.** As a guide page is ([Writing a page](#2-writing-a-page)): for the people using Claudette, in the app's own words in bold, saying what they can now do and where (Settings → Usage → **Keep usage history**), not how it was built. An entry is a sentence or two, starting with the thing that changed in bold when that helps the reader scan. It can link to the change's guide page by its full address once the page exists.
- **When.** In the same change as the code, as DESIGN.md is: a change people would notice adds its entry under **Unreleased**.
- **Releasing.** `packaging/release.mjs` makes the changes steps 1, 3 and 4 describe, and prints what it changed; committing, tagging and publishing are yours.
  1. One change turns `## [Unreleased]` into the version with the day's date, starts an empty `## [Unreleased]` above it, and updates the links at the bottom: `node packaging/release.mjs prepare`. The version follows Semantic Versioning: until 1.0.0, a section with **Added**, **Changed**, **Deprecated** or **Removed** raises the second number, and **Fixed** or **Security** alone the third, which the script checks. It's the version `Directory.Build.props` names ([Version](DESIGN.md#version)); for a patch release while the next version is being worked on, `--version 0.6.1` sets both.
  2. The tag `vX.Y.Z` goes on that change. `package.yml` drafts the release with the version's section as its notes, from `packaging/release-notes.sh` ([Packaging and signing](DESIGN.md#packaging-and-signing)), so the release on GitHub and Claudette's update dialog say what the changelog says. A tag whose version has no section fails before anything is built.
     - A pre-release (`v0.4.0-beta.1`) needs no section of its own: its notes are **Unreleased**'s, which stays as it is until the release.
  3. Once the release is published, a change of its own removes the guide's *New in the next release* notes for it, and brings the pages' examples of the version Claudette shows (*Claudette 0.5.1*) up to the release. Pushing it to `main` rebuilds the site, so the Downloads page lists the release.
  4. `Directory.Build.props` moves to the next version to work on, and the lock files are restored to name it.

  `node packaging/release.mjs after` does steps 3 and 4 in one change: it removes the labels, brings the examples up to the release, moves to the next second number (or `--next`) and restores the lock files. It lists what a person has to word, such as *new in the next release* inside a sentence, the landing page's *Soon*, or a page naming an earlier release.
- **On the site**, the sidebar's **Changelog** links to `CHANGELOG.md` on GitHub, which shows it as it is: GitHub Pages builds only `docs/`, so the site can't include a file from the root. The Downloads page links each release's notes, which are its section.

## 6. Coverage

The map covers every part of Claudette that DESIGN.md describes. When a chapter is written, its pages are checked against more than its DESIGN.md sections:

- The app's own lists: the Settings pages' search entries, the command palette's commands (`ShellViewModel.Palette.cs`), the shortcuts (`KeyboardShortcuts.cs`), and the menus (the tab's, the group's, the project's, the cards'). Each item in the chapter's part of the app is named on its page.
- The running app, for labels: where DESIGN.md's words differ from the app's, the page uses the app's, and DESIGN.md is corrected.

## 7. Keeping it current

- A change people can see updates its guide page and retakes its pictures in the same change, as it updates DESIGN.md (`guide-screenshots.ps1 -Only <chapter>/<picture>`). CLAUDE.md says so. The script leaves the focus, keyboard and pointer alone, so a run can go on while you work; the two pictures that need them, `tabs/info-card` and `tabs/command-palette`, are taken only with `-WithInput`, which borrows them for a few seconds each.
- A change to what the README's pictures show, which the guide's pages use too (the main window and its header, the detailed header, the diff view, Tasks and Agents, Settings, History, the Files tab), retakes them with `screenshots.ps1`, which draws the social card again from the new main window. Both scripts take `-Projects` a short folder that says nothing about you, such as `C:\Demo`.
- A new part of Claudette in DESIGN.md gets its row in the map here, and its page, in the same change.
- A change people would notice adds its entry to `CHANGELOG.md`'s **Unreleased** section in the same change ([The changelog](#the-changelog)). CLAUDE.md says so too.
- The `issues` skill's plans include the page, and what it builds includes the changelog's entry.
- The README is the short version, sending readers to the site: the site, the Downloads page, the guide and the changelog at its top, a few lines on each main part of Claudette ending in a link to its chapter, seven of its pictures and her animation, and the guide's Getting started and the Downloads page under Getting started. A new main part of Claudette gets its few lines and link there; the detail goes on its page.

## 8. Steps

All done 2026-10-16, in one change rather than a pull request a chapter.

1. ✅ **DESIGN.md in line with the app**, since the pages are written from it: what's built marked ✅ (tab marks and the reviewed icon, worktree tabs, a group's name and color, extra folders, **Ultracode**, **Output style**, **Zoom**, **Motion**, the side panel's page order, Claudette on the composer, Claude Docs, folder trust, the starting mode, keeping the computer awake, sharing usage, several prompts waiting, History's project chips, Project files), the app's labels where the two differed, the end of a turn as the **Modified files** and **Turn** cards, and the tab's whole menu.
2. ✅ **The changelog's releases.** `package.yml` drafts a release with its version's section of `CHANGELOG.md` as the notes, in place of notes generated from pull requests, and `Directory.Build.props` moved to 0.3.0.
3. ✅ **The pictures.** `guide-screenshots.ps1` and `Screenshots.psm1`, which `screenshots.ps1` uses too, take the guide's 19 in light and dark. For them the demo lists the older projects as recent folders, and `fake-claude` learned `EDIT_FILE`, for a turn that ends with **Modified files**.
4. ✅ **The site**: `_config.yml`, the theme in light and dark, the includes, the home page and the Downloads page, `docs.yml`'s check, and the README's links to the guide and to the Downloads page. GitHub Pages builds it from `docs/` on `main` (Settings → Pages → Source: **Deploy from a branch**).
5. ✅ **Every chapter**, written from its DESIGN.md sections and checked against the app's source for labels.

**Still to do:** the pictures the demo can't show yet ([Pictures](#3-pictures), "What the demo doesn't show yet"): a question, a plan to approve, an MCP server's form, a prompt suggestion, a Claude Doc and the usage credits notice; and what only macOS shows. And reading every chapter against the running app, which [Coverage](#6-coverage) asks for and the source can't replace.

## 9. To decide later

- Whether Claudette links to the guide anywhere besides **User Guide** in the header's menu and at the foot of Settings' sidebar, the command palette and the macOS Help menu, such as through `F1` or from each Settings page. It's what the user sees, so it's decided in DESIGN.md ([User Guide](DESIGN.md#user-guide)).
- A check that the guide names everything: a test that reads `docs/` and fails when a Settings search entry, a shortcut or a palette command appears on no page. It would make [Coverage](#6-coverage) automatic, at the cost of tying the guide's words to the app's.
