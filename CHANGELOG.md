# Changelog

What's changed in each version of Claudette, for the people using it.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Until 1.0.0, a new feature or a change you'd notice raises the second number (0.2.0 to 0.3.0), and fixes alone the third (0.3.0 to 0.3.1).

## [Unreleased]

### Fixed

- **Start Claudette when I log in** now starts a build of Claudette on macOS and Linux that runs on a .NET in your home folder, such as the one Microsoft's install script puts in `~/.dotnet`. It used to start the build's own program, which looks for .NET only in the usual places, so nothing started at login. If you turned it on before and Claudette didn't start, turn it off and on again.
- A tab whose Claude Code stopped as it started, because it isn't signed in say, could say *Couldn't start Claude Code: Broken pipe*. It now says why Claude Code stopped, and shows the sign-in screen when signing in is what it needs.

## [0.6.0] - 2026-10-24

### Added

- **Read a Markdown file rendered.** In a Files tab, a Markdown file's bar has **Edit**, **Preview** and **Edit and preview**. **Preview** shows the file as Claude's replies show Markdown, with its headings, lists, tables, code blocks and images, and **Edit and preview** puts the editor and the preview side by side: the preview follows your typing, and each scrolls with the other. A link to a heading scrolls to it, a link to another file opens it, and each file keeps your choice. See [Markdown files](https://claudette.dotbunny.com/project-files/markdown-files/).
- **Install Claude Code from Claudette.** When Claude Code isn't installed, the first screen now offers **Install Claude Code**. It shows the command from Claude Code's own setup page for your system, runs it there and then, shows what it prints, and carries on to signing in once Claude Code is installed. Nothing asks for an administrator's password, and no terminal is needed.
- **See how much a synced session takes.** A long session can reach hundreds of megabytes, all of which your sync client uploads and keeps. A tab that syncs to the session library now says how much its session takes there: hover its sync icon (*Synced to the session library · 312 MB*), or see **Sync size** on its info card and the line under **Sync to other machines** in **Tab settings…**. It's measured each time the session is copied to the library. History shows how much each session in the library takes, and Settings → Sessions how much the whole library holds.
- **Ask questions on Discord.** **Ask on Discord**, in the **Help** menu beside the version at the foot of the Settings sidebar, opens dotBunny's Discord in your browser, where you can ask questions about Claudette. Problems still go to GitHub, through **Report an issue**. The command palette now has **User Guide**, **Ask on Discord** and **Report an issue** under *Help*, and on macOS the menu bar has a **Help** menu with all three.
- **Turn a kind of project off.** Settings → Project tools has a page for each kind of project Claudette knows, Unreal Engine, Unity and Godot, each starting with **Recognize … projects**. Turn it off and Claudette treats those projects as plain folders: no project actions or editor logs for them and nothing said to Claude about them, and its page says *Off* beside its name in the sidebar. A folder's own actions and links still work. Each page also has **Tell Claude about … projects**, and the defaults its projects start with.
- **Settings from where you use them.** Claudette's right-click menu has **Claudette settings…**, the header's cup **Keep-awake settings…**, and a changed file's menu **Diff tool settings…**. Each opens Settings at those settings and picks them out for a moment.
- **Wrap lines in the diff view.** The diff window has **Wrap lines** beside **Whole file**, and so does a Files tab's bar in **Inline changes** and **Side by side**. Turn it on and long lines wrap at the width shown, each side in its half when they're side by side, instead of scrolling sideways. The next diff you open starts the way you left the last one.
- **See what changed in a file since Claude did.** When a file in Changed files has changed on disk since this session's last change to it, its row says so under its folder: *Changed since: +3 −1*, with *by Claude in* and the tab's name when another tab's Claude did it, or *Deleted since*. The diff view then shows this session's changes, and **With later changes** shows the file as it is now, with **Revert**.
- **Changed files follows Claude into worktrees.** When Claude moves a session into a worktree partway through, and back out, the tab moves with it, as a worktree tab does, and says so in the conversation. Files Claude changed in other checkouts of the repository, such as a worktree, one a subagent works in or one made with `git worktree add`, are listed in a group for each, headed by its folder and branch, and **Working tree vs HEAD** lists each of them too. A removed worktree's files stay in their group, marked removed, and cleaning up worktrees leaves a worktree an open tab's session changed files in.
- **See the changes a diff can't show.** A file in Changed files whose only change is its line endings, its byte order mark, the newline at its end or whitespace now says so under its folder, such as *Only its line endings changed: CRLF to LF*, instead of showing +0 −0 or *back to how it was* with an empty diff, and one with other changes too says *Its line endings changed too*. The diff view says the same, and on a line that changed only in its whitespace, spaces show as · and tabs as →. This catches Claude rewriting a Windows file with Unix line endings, which its Write does. **Working tree vs HEAD** says when only a file's line endings or its mode changed, and its counts leave out lines that changed only in their line endings, as the diff does. A turn's **Modified files** tiles show what changed in place of +0 −0.

### Changed

- **A new Settings window.** Its pages are in groups down the left, each with an icon: Claudette (General, Appearance, Conversation, Notifications, Keyboard), Claude (Claude Code, Accounts, Tabs, Usage, Advanced), Your work (Sessions, Files and diffs) and Project tools (All tools, a page each for Unreal Engine, Unity and Godot, and Perforce). All tools has how solutions open and **Show editor logs under the tab**. Each page is in sections, and each setting has a line saying what it does, with **More** for the rest. Settings that are on or off are switches. A setting you've changed has a bar at its left and a **Reset** of its own, and each section has its own **Reset to defaults** in place of one for the whole page. What settings sync leaves on each machine says **This computer**, and a setting that a tab has changed for itself says how many tabs have.
- **Settings stays open.** It's a window beside the main one now, so you can keep it open while you work and see what a change does. Its project pages follow the tab you select, and it opens on the page you left.
- **Some settings moved.** Conversation is a new page, with what was in Appearance about the conversation, the working line and the tab rows, and **Messages sent while Claude works**, **Show every hook run** and **Suggest my next prompt**. Tabs has what new tabs start with (it was New tabs), **Subagents**, what happens at a usage limit, the check-ins and the process monitor (which were pages of their own), and the new tab picker's folders. Quick suffixes are on Keyboard, and the diff tool and the Project Files tab's settings on Files and diffs. **Also restore unpinned tabs when Claudette starts** is on General, with **Also while tabs are connected to the Claude app**; **Name each tab's session in Claude Code after the tab** is on Claude Code, and **Detailed usage header** on Usage. Search finds any of them by name, by what it does, or by other words for it, such as "dark mode".
- **Each kind of project has its own icon**, on its page in Settings and on the project's row at the foot of the sidebar: a gear for Unreal Engine, a cube for Unity and a robot's head for Godot. A folder with no project Claudette knows shows a folder there.
- **Appearance shows a preview** of your choices as you make them: a message, thinking and a reply in your style, density, fonts and zoom, with Claudette at her size.
- **Tab settings… applies as you go.** Each change applies as you make it, as in Settings, each section has its own **Use defaults**, and **Done** closes it.
- **Old sessions leave the session library after 15 days.** Settings → Sessions has **Delete sessions not used for a while**, which is on, with how many days under it, 15 to start. Once no machine has used or opened a session for that long, it's deleted from the library, and from History on every machine that shares the library. Opening a session from History counts as using it, even if you don't send anything. Claudette checks when it starts, then once a day while it's open. This replaces **Keep library sessions for**. If you'd chosen 1 month or 1 year, that's now 31 or 366 days. Forever, which was the default, becomes 15 days, so the first launch after you update deletes the library sessions no machine has used or opened in 15 days. Turn the switch off to keep sessions for good from then on.
- **A tab's collapse chevron sits on its group's line.** The chevron that folds away what's under a tab, such as a thread's sub-threads or its subagents, used to push the tab's name over, so the tab looked as if it were under the one above it. It's now on the folder group's colored line, in the group's color and level with the name, so every name in a group starts in the same place and only sub-threads are indented.
- **Changed files counts only the tab's own changes.** With several tabs working in one folder, each tab's Changed files, its diffs and its turns' **Modified files** cards count what its own Claude changed, from before its first change to how its last one left the file, rather than the file as it is now, so another tab's edits, yours and a command's no longer count as its own.
- **Changed files keeps up by itself.** While a tab is on screen, a file Claude changed is looked at again a moment after anything changes it on disk, and **Working tree vs HEAD** again when you stage, commit or check out. Both also catch up when a turn ends, when another tab's Claude changes a file, and when you come back to Claudette, so **Refresh** is rarely needed.
- **One name for each command.** The command palette, Settings → Keyboard and the menus now agree: **Go to file…** (it was **Find a file…** on a Files tab, and *Go to a file in the project* in Keyboard), **Go to the first message** and **Go to the latest message**, **Open from History…**, and **Reveal in file manager** wherever a file can be shown in its folder. The project menu's **Kill all Unreal editors…** (and Unity's and Godot's) is now **End all Unreal editors…**, as its confirmation says.

### Fixed

- A file open in a Files tab came back at its top when Claudette started again, rather than where you'd scrolled to. It now comes back where you left it.
- The macOS app wasn't signed, so macOS refused to open it, or opened it only once you'd allowed it in System Settings → Privacy & Security. It's now signed by dotBunny and notarized by Apple, so it opens like any other app.
- A running task with a long name pushed **Stop** out of the composer's list of running tasks, which opened cut off at the right with a scroll bar under it. The list now has room for it, and a name longer still ends in "…". Other lists and cards that open from a button, such as the tab's info card and the stash, could be cut off the same way, and no longer are.
- With the Claude desktop app installed, Claudette said Claude Code wasn't installed without saying why the desktop app didn't count. The desktop app isn't Claude Code: it keeps a copy of Claude Code for its own use, which it replaces as it updates. The first screen now says so, and offers to install Claude Code beside it. Pointing **Path to claude** or **Choose claude…** at the desktop app, or at its copy, now says whose it is, rather than trying to run it.
- In a long session, Claude's task list could lose track of the tasks: it showed 61 while Claude had 5, kept tasks Claude had finished as still to do, and ticked off old ones instead. This happened when Claude Code started its list over and numbered new tasks from #1 again. The to-do list and the Tasks page now show the list as Claude Code keeps it, which is the list Claude sees: they follow it as Claude works, when another session that shares the list (`CLAUDE_CODE_TASK_LIST_ID`) changes it, and when a tab is restored.
- A pinned tab that syncs to the session library could carry on from this machine's older copy of its session after you'd gone on with the session on another machine, and its next turn then wrote over the other machine's turns in the library. It now picks up where the other machine left off (*Picked up where LAPTOP-02 left off at 12:04.*), and keeps looking while it's open, since your sync client may bring those turns only after Claudette starts. A message you send before it notices stays in the composer. If the session went on in both places, nothing is written over: this machine's turns are kept in the library as a copy of their own, which History lists beside the other machine's, and the tab becomes read-only. See [Coming back to the first machine](https://claudette.dotbunny.com/history/picking-up-on-another-machine/#coming-back-to-the-first-machine).
- `/clear` left the files from the old conversation in Changed files, ticks and all, until Claudette restarted. They're now cleared with it.
- A file a subagent changed showed in Changed files as modified with no counts, saying Claude Code didn't report its earlier content. Claudette now reads the file just before the subagent changes it, and keeps that for when the tab is restored.
- Moving a tab out of a git repository with **Working tree vs HEAD** on left Changed files empty, with the box hidden so it couldn't be turned off. The list goes back to Claude's changes.
- A file in a folder whose name starts with two dots, such as `..data`, showed its full path in Changed files and on a turn's **Modified files** card.
- After a restart, a project's Files tab could compare a file with the wrong tab's earlier contents, and named the time of the restart as when Claude changed it.
- The Usage window ended each past session's line with a "P" or an "A" where its time should be (*Session ending Mon Oct 12, P*), and the week's reset showed a 12-hour clock even where the system uses 24 hours.
- A restored tab that hadn't started yet said *Default · Default* on its row, whatever model and effort it was set to.
- A hook's row in the conversation named its event twice (*PreToolUse hook (PreToolUse:Bash)*). It now reads *PreToolUse hook (Bash) · failed (exit code 2)*.
- With auto-compact off, the context breakdown said Claude Code would compact the conversation on its own once it was nearly full. It now says to compact it.
- The **Stop** button's tooltip, and the arrows in a Files tab's change peek, kept showing their usual keys after you'd changed them in Settings → Keyboard.
- Search in Settings listed **Quick suffixes**, **Diff tool** and **Add an API key** twice, and a project's Links and Actions pages were marked *This computer*, though `claudette.json` goes with the project.
- A screen reader read the rows of Changed files, the command palette, the new tab picker, a Files tab's explorer and file finder, the diff view and a project's links and actions by Claudette's internal names for them. It now reads each by what it shows, such as a file's name and how it changed.
- The group menu in the collapsed sidebar didn't have **Open project files**.
- Messages that only tell you something, such as a worktree that couldn't be removed, offered **Cancel** beside **OK**.
- Many smaller slips in Claudette's wording: plurals such as *1 turns*, *From the session another session*, a turn that took *1m 60s*, the header's cup saying twice that it can't keep the computer awake, exit codes written three ways, and settings named by labels they no longer have.

## [0.5.1] - 2026-10-23

### Added

- **A source build keeps up with its branch.** Claudette built from a checkout, such as one of `main`, now checks the branch it tracks for new commits at launch and every hour, as an installed Claudette checks for releases. A badge at the foot of the sidebar says *3 new commits on origin/main* and lists them; **Pull, build and restart** pulls them, builds Claudette and restarts into it with every tab as it was, and **Update when idle** waits for your tabs to finish first. It only ever fast-forwards: with changes or commits of your own in the checkout, it tells you why it won't pull and leaves the checkout alone.
- **Claudette from the Microsoft Store.** A copy installed from the Store is kept up to date by the Store, so it doesn't look for releases on GitHub, and Settings → General says so.

### Changed

- Tested with Claude Code 2.1.308.

### Fixed

- **Find in the conversation** brought the message into view but didn't show where in it the text was. The text you searched for is now highlighted in the message, whether it's in a reply, a code block, your prompt or a tool's output.
- **Files** under the composer opened the side panel on whichever page it last showed, and closed it when it showed another. It now always opens it on Changed files, and closes it only from there.
- The Windows package wasn't signed, so Windows wouldn't install it. It's now signed by dotBunny.
- A long to-do list at the top of the conversation could fill a short window and push the conversation out of sight. Now it takes at most two fifths of the conversation's height and scrolls past that.

## [0.5.0] - 2026-10-22

### Added

- **Editor logs under the tab.** While the Unreal editor, a commandlet or a game run has the tab's project open, its log is a row under the tab, and a click shows it in the main area as a project run's log is, followed as it's written, with its errors and warnings colored and counted on the row. Unreal's lines show in columns: the time in your computer's clock, the log category, the verbosity and the message. A filter over the log hides levels with a click on their chips, each with its count, Unreal's categories from a menu, or every line without some text; the levels and categories you hide are remembered for the project. Unity's editor and batch runs, and Godot's editor and game, get theirs too. The row stays after the editor closes, says *Crashed* when its log ends in a crash, and comes back with the new log when the editor starts again. Claudette reads the file without keeping it open, so the editor can rename or delete it as it always does. **Show editor logs under the tab** in Settings → Project tools turns it off.
- **Collapse a tab in the sidebar.** A tab with rows under it, a thread's sub-threads, subagents or a project action's runs, has a chevron beside its name that folds them away, as a project group's does; **Collapse tab** in its menu or the command palette does the same. The collapsed row says how many it hides and shows the most urgent of them, such as a sub-thread waiting on you or a failed build. It opens again when you go to one of them, when a sub-thread joins, or when one starts waiting on you, and it stays collapsed when Claudette restarts.
- **Zoom in the image window.** Turn the mouse wheel over an image to zoom in and out about the pointer, or use **+** and **−** in the window's header or the `+`, `-` and `0` keys; **Fit** shows the whole image again. It goes from 10% to 1600%, and once it's larger than the window you can drag it around with the left mouse button, holding `Space` or not, or the middle one.

### Changed

- **Close the to-do list at the top of the conversation.** Its line has a chevron, and once you close it the list stays closed while Claude works through it, and when Claudette restarts, rather than opening again each time Claude changes it. Closed, the line names the task in progress, so a long list takes one line. It opens by itself when Claude starts a new list.
- **Stay current and Wrap lines over a project action's log.** **Stay current** shows whether the log is following its newest line: scroll up and it turns off, so the log holds still while you read; scroll back to the end, or click it, and it follows again. Turn **Wrap lines** off and long lines stay on one line, scrolling sideways. Each new log starts as you left the last one.

### Fixed

- When a long conversation opened, such as a resumed session's, its newest message shook up and down by a pixel for a few seconds while Claudette laid out the messages above it, and could drop down for a moment as it started.

## [0.4.0] - 2026-10-18

### Added

- **`@` completes any path.** Type a folder after `@` and the list shows what's in it, from `C:\`, `/`, `~/` or `../` too, folder by folder, and picks a file outside the project in full. A folder in the project shows the files git ignores as well, dimmed, and the tab's extra folders' files are in the list with the project's.
- **Subagents under their tab.** Each subagent Claude starts has a row of its own under its tab, nested under the one that started it and dimmed once it's finished and from an earlier turn. Select one to see its work in the tab, with the prompt it was given and what it handed back; the conversation keeps a one-line card for it. You can't write to a subagent, so **Message it through Claude** names it in your next message instead, and a permission prompt it waits on is answered right there. The Agents page opens a subagent's work the same way. To open subagents' cards in the conversation as before, set Settings → Appearance → **Show subagents' work** to **In the conversation**.
- **Images open in Claudette.** Click an image file Claude names (a PNG, JPEG, GIF, WebP or BMP), its **Open** on a tool card or in Changed files, or a link to an image on the web, and it opens in Claudette's image window rather than another app. In Project Files, double-click an image, or use **Open in Claudette's image viewer** where a file says it isn't text. To open them in your system's app as before, set Settings → Editor → **Open images in** to **The app the OS uses for the file**.
- **Animated GIFs play.** An animated GIF or WebP plays in the image window, with **Pause** and **Play**. With motion reduced, it opens still until you press **Play**.
- **Discourage subagents, or turn them off.** Settings → Claude Code → **Subagents**, or one tab's **Tab settings…**: **Discourage them** asks Claude to do the work itself unless you ask for a subagent, and **Don't allow them** turns down any it starts, in tabs already open too. Claude hears about a change with your next message.

### Changed

- **A project action's log shows where the conversation is.** Clicking an action's entry under its tab shows its log in the main area, with room to read a build's errors, and the tab's own row takes you back to the conversation. **Open beside** in the entry's menu, or dragging it onto the conversation, puts the log beside it. Web addresses in the log open in your browser, and the files and folders it names open with a click, in Claudette's editor at the line an error points to. Select and copy any of it, and `Ctrl/Cmd+F` finds in it. **Show output…** in the project's menu and the *action finished* notification open it there too, and the side panel's **Project** page keeps the project's details.

### Fixed

- Just as Claude finished a turn, turning Remote Control on or off, moving the tab to another account, changing its extra folders, ticking **Sync to other machines**, or sending a message with *Queue them* chosen could wait for a turn that had already ended. A queued message, or a sub-thread's report, that went as the turn ended could leave the tab saying Claude was idle while it worked.
- When Claude Code started a turn by itself, such as for a message from the Claude app, a message sent with *Queue them* chosen, or **Auto-continue**'s message once a usage limit reset, could go in that moment as if Claude were idle.
- As the working line's glyph twinkled, the line could nudge up and down, taking the conversation above it along. On Windows, one of its stars also came out smaller and thinner than the rest.
- After Claudette restarted, such as for an update, a subagent Claude ran in the background could read *Didn't finish* when it had finished while Claude was still working.

## [0.3.0] - 2026-10-16

### Added

- **Project Files**: a tab for each project with an explorer and a code editor, opened with the file button beside a group's **+**, or **Go to file…** (`Ctrl/Cmd+P`). Find a file by name, or make a new file or folder from the explorer's menus, then edit it and save it; unsaved edits are kept when Claudette closes. Claude's changes are marked in the gutter, with a peek to see what was there and **Revert** it. A file named in the conversation opens in it at the line, beside the conversation, which gets its width back when you close the file. The diff window's **Open** brings it in front of the diff, which closes. Settings → Editor sets it up, or sends files to the OS's app instead, and **Open in the diff window** can open the file in the OS's app, or the changes in your diff tool.
- **API-key accounts.** Add a key in Settings → Accounts → **Add an API key**; it's kept only in the OS's credential store. A project group's **Account ▸** menu picks the account its new tabs use, and a tab's moves it to another, saying first what the move costs. While one of its tabs is selected, the header shows the account's estimated spend today, this week and this month, and Settings → Accounts can warn when a day or a month passes an amount.
- **Files named in Claude's replies open.** A Markdown link to a file, or a file's name in code (`Player.cpp:42`), opens the file with a click. Several files with that name are offered in a menu.
- **Tabs side by side.** **Open beside** in a tab's menu, or drag its row onto the conversation: up to three tabs at once, each with its own conversation and composer. Drag the line between two to share the width differently. A thread's **Thread ▸** → **Show with its sub-threads** shows it beside its sub-threads.
- **Conversation cards.** Each step is a card in its kind's color. A run of tool calls is one card of tiles, each opening its detail or a window of its own, and a turn ends with its **Modified files** and a **Turn** card. Marks beside the scroll bar show your messages, waiting prompts and errors; `F3` and `F4` go to the previous and next message, and `Ctrl/Cmd+F3` and `Ctrl/Cmd+F4` to the first and the latest.
- **Quieter in-between steps.** Tool calls, thinking, subagents and the other steps on the way to an answer lose their card's tint and outline, keeping the stripe, until you point at one, so your messages and Claude's answers stand out. Settings → Appearance → **Quieter in-between steps** turns it off.
- **Give Claude a task list.** Newer models keep track of their work without a written list, so the Tasks page showed only the plan. Settings → Claude Code → **Give Claude a task list**, on unless you turn it off, gives every model one, at the cost of a little room in the context. With no tasks, the status line under the composer says *Following the plan* once a plan is approved.
- **Renaming a folder group**, for folders with the same name in different places: **Rename…** in the group's menu, and **Reset name** to go back to the folder's name.
- **Using usage credits.** Once a limit is used up and your plan's usage credits pay for Claude, a notice under the header says which limit, when it resets, and what you've spent this month, with **Manage on claude.ai**. It stays until they stop paying, and an OS notification tells you when they start.
- **Tabs that don't use usage credits.** Untick **Use usage credits** in a tab's menu, or turn off Settings → Usage → **Use usage credits once a limit is used up** for every tab, and once a limit is used up the tab stops and waits for it to reset instead of spending them, so credits go only to the work that matters. A message you send meanwhile waits for the reset, with **Send now** to send it anyway.
- **The prompt cache's timer.** *Cache 52m* beside the context says how long your next message can read the conversation from Claude's cache, and turns amber in its last three minutes. Settings → Appearance → **Show when the prompt cache expires** turns it off.
- **Games with Claudette.** Tic-tac-toe, Connect Four and hangman, from her right-click menu, the command palette, or **Play a game while you wait** when a usage limit stops a task. Settings → Appearance → **Games with Claudette** turns them off.
- **Clear usage history on every machine…** in Settings → Usage, while **Share usage with my other machines** is on.
- **User guide**, under **Settings** at the foot of the sidebar and at the foot of the Settings sidebar, opens Claudette's guide in your browser.
- A turn's changes to a file open in your diff tool too, from the diff window's **Open in diff tool**, and its menu has **Open in the OS's app**.

### Changed

- **Also restore unpinned tabs** (Settings → Sessions) is on for new installs, so every tab comes back at launch. Settings saved by 0.2.0 or earlier keep it off until it's turned on, or Sessions' **Reset to defaults** is used.
- **Share usage with my other machines** shares the turns of tabs that sync to the session library too. The Usage window's **Projects** adds up what every machine spent on a project, its tabs and the busiest tabs list another machine's as *Fix the loader · Laptop*, and an API-key account's spend and its warnings count its turns on your other machines under an account of the same name.
- The Usage window's **Projects** counts a project as one row in any of its folders, such as a worktree or another clone.
- Tested with Claude Code 2.1.300. A tab Claude Code refuses to start because of an organization's configuration says so in words.
- The license's copyright holder is dotBunny Inc.

### Fixed

- A tab's tokens and estimated cost, and the Usage window's, no longer count every earlier turn of the session again with each turn, which made a long session look far bigger than it was. Turns recorded by 0.2.0 or earlier keep the counts they had.
- **On pace to hit the session limit** under the header keeps up with the projection, and goes once you're no longer on pace to hit it, rather than repeating the time it was first worked out from.
- Right-click menus no longer run off the bottom of some displays, and Claudette's own menu stays inside the window.
- The folder trust question fits in the window when a folder's configuration lists a lot.
- The spacing of thinking and the height of lines in the conversation.
- In a long conversation, the scroll bar keeps its size and place as you scroll, rather than changing as each message comes into view.
- The tab info card's **Project** row no longer ends a sentence with two full stops when the project's engine wasn't found.
- In a narrow conversation, as beside the side panel, the line saying what Claude is doing no longer runs over the side panel: a long command trims to fit. The composer's choices wrap onto another line, and the folder and status under it trim, rather than running past its edge.
- **New tab** (`Ctrl/Cmd+T`) puts the cursor in its search box, so you can type a folder's name straight away.

## [0.2.0] - 2026-10-14

### Added

- **Threads.** Make a tab a thread from its **Thread ▸** menu, and its Claude hands work to the other tabs in its project, its sub-threads, and gets one report back when they've finished. You approve each message first, unless **Ask before sending to sub-threads** is off, and answer the sub-threads' prompts from a strip over the thread's composer.
- **Following a plan.** The Tasks page shows Claude's plan while it writes it in Plan mode, keeps every version with **Changes from** the one before, and can take a reply as the plan. Each task says who worked on it, how long it took, and the files and tokens it used. A tab's row shows how far its tasks have got (`3/7`), and a turn that ends with tasks left offers **Continue**.
- **Sessions that know each other.** Each tab's session goes by the tab's name in Claude Code (Settings → General → **Name each tab's session in Claude Code after the tab**), so other sessions can message it by name. `@` in the composer names another tab, another Claude Code session on this machine, or one of the tab's subagents. The tab's menu has **Copy session ID**, and its info card **Reached as**.
- **Claudette on the composer**, who climbs onto the message box and reacts to what the tab is doing. Settings → Appearance → **Show Claudette on the composer** turns her off.
- **Claude Docs.** A doc Claude writes on claude.ai with the Docs connector gets a link on its card, a row with **Open** at the end of the turn, and a place on the Tasks page.
- **Images from Claude's tools** show under their cards: an image Claude read, a PDF's pages, a notebook's figures, an MCP tool's screenshot. A click opens one full size, with **Copy image**, **Save as…** and **Open**.
- **Prompt suggestions.** Settings → Claude Code → **Suggest my next prompt** shows what Claude Code thinks you'll send next in the empty composer; `Tab` takes it.
- **Keep this computer awake** in Settings → General, while Claude has work to finish or whenever Claudette is running, and the cup in the header that turns it on and off.
- The button at the header's right shows and hides the side panel, and its pages can be dragged into the order you like (or **Move left**, **Move right** and **Reset order**).
- History has a chip for each recent project, to list only its sessions, and a group's menu has **History for this folder**.
- The Processes page collapses a process's children, counting them and their CPU and memory on its row, with **Expand all** and **Collapse all**.

### Changed

- Claudette moved to [dotBunny/Claudette](https://github.com/dotBunny/Claudette), and its packages are dotBunny's. 0.1.0 announces 0.2.0 but can't install it, so install 0.2.0 by hand; on Windows it installs beside 0.1.0.

### Fixed

- A link clicked in a reply opens again.
- When the last tab in a worktree reached through a symbolic link closes, Claudette offers to remove the worktree.

### Security

- A reply's images no longer load by themselves. An image from the web, or a file outside the tab's folder, waits for **Load image**, so a reply can't send anything out in an image's address, and an image on a network path never loads.

## [0.1.0] - 2026-10-08

The first public release.

### Added

- **Tabs**, each a Claude Code session running the real `claude` with your settings, `CLAUDE.md`, MCP servers and hooks, grouped by folder in a sidebar with each group's color. Status icons, pins, marks and the reviewed badge, tabs restored at launch, the command palette (`Ctrl/Cmd+Shift+P`) and the next tab waiting for you (`Ctrl/Cmd+J`).
- **Worktree tabs**, each in a git worktree and branch of its own, with setup actions for each new worktree and cleaning up the ones you've merged; and extra folders a tab's Claude may also use.
- **The conversation**: replies in Markdown, tool calls with their diffs and output, subagents, thinking, hook runs, find (`Ctrl/Cmd+F`), exporting to Markdown or a web page, and going back to an earlier message with **Edit and resend…**, **Branch from here** and **Restore files to before this…**. The working line says what Claude is doing, and a chip counts the tasks it keeps running.
- **The composer**: `/` commands and `@` files, attachments and large pastes, earlier prompts, drafts and the stash (`Ctrl/Cmd+S`), messages queued while Claude works, quick suffixes, the model, effort and permission mode, **Ultracode** and output styles, and check-ins on long turns.
- **Permission prompts** as cards, with **Always allow** rules you can edit, questions, plans to approve, MCP servers' forms, and a question before using a new folder's own configuration.
- **Changes to review**: the Changed files page, a diff view, ticking files reviewed, reverting a change or a whole file, and your own diff tool.
- **Usage**: the session and weekly meters with a trendline and a projection, a detailed header with charts, alerts, a Usage window with each project's tokens and cost, the context window and **Compact now**, continuing a task when a usage limit resets, and usage shared between your machines.
- **History** of every session, and a session library in a synced folder that lets another machine continue the tabs you choose, with Claudette's settings synced too.
- The **agent map** of Claude's subagents, the **Tasks** page, the **MCP** page, the **Processes** page and a **scratch pad** for each project.
- **Project tools** for Unreal Engine, Unity and Godot, your own actions and links in `claudette.json`, and Perforce logins kept fresh, with each tab's changelist.
- **The Claude app**: connect a tab to use it from your phone or claude.ai. Notifications, the Dock or taskbar badge, starting at login, and Claude's service status in the header.
- **Updates** to Claude Code, and to Claudette itself.
- The Standard and Claude styles, in light and dark, with compact density, zoom, reduced motion and screen readers' announcements.
- Packages for Windows (MSIX) and macOS (`.dmg`), for x64 and Arm.

[Unreleased]: https://github.com/dotBunny/Claudette/compare/v0.6.0...HEAD
[0.6.0]: https://github.com/dotBunny/Claudette/compare/v0.5.1...v0.6.0
[0.5.1]: https://github.com/dotBunny/Claudette/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/dotBunny/Claudette/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/dotBunny/Claudette/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/dotBunny/Claudette/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/dotBunny/Claudette/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/dotBunny/Claudette/releases/tag/v0.1.0
