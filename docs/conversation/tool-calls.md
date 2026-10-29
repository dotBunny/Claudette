---
title: Tool calls
parent: The conversation
nav_order: 2
summary: "When Claude reads a file, edits one, runs a command or searches, that is a tool call."
---

# Tool calls
When Claude reads a file, edits one, runs a command or searches, that is a tool call. Calls that come one after another share a single **Tool calls** card, with a tile for each call.

{% include shot.html name="guide/conversation/tool-calls" alt="A Tool calls card with tiles for several calls: files read, an edit with its line counts, and a command." %}

## The card and its tiles

The card's header counts its calls: *1 call*, *4 calls · 1 running*, *4 calls · 1 failed*. Anything between two calls ends the card and the next call starts another: a reply, thinking, a hook run, a prompt, a subagent, the start of a task. Tiles go three across, then two, then one as the card narrows, and the tiles in a row are all as tall as the tallest.

A tile shows:

- a status dot (running, done or failed), the tool's icon, its name and the time of the call;
- what it worked on: a file by the end of its path (`…\Player\StarfallCharacter.cpp`, with the whole path as its tooltip), a command, or a search pattern;
- how it went: *Running…*, *120 lines · 0.3s*, *Failed · 2.1s*, with an Edit's or Write's `+added −removed` beside it;
- the first line of its output, and any Claude Doc link, git chips and images ([Images](images.md)).

A call under ten seconds shows its duration with a decimal, such as *0.4s*. The result line says what the tool found: the lines a Read read (*40 lines of 900*), what a Grep found, a Glob's files, a fetch's status and size (*200 OK · 14 KB*), whether a Write created a file or updated it.

A question or a plan gives its tile up to its own card ([Questions](../permissions/questions.md), [Approving a plan](../permissions/approving-a-plan.md)). A subagent is a card of its own ([Subagents](subagents.md)).

## Folding the calls
A long run of calls takes a lot of room. The chevron before a Tool calls card's icon folds the card to its header line, such as *Tool calls · 4 calls · 1 failed*, and a second click brings the tiles back. A call that is running or has failed still shows in the header's words.

It works as thinking does. Settings → Conversation → **Show tool calls collapsed** starts new cards folded. Right-click a Tool calls card for **Collapse all tool calls** and **Expand all tool calls**: they fold or open every card in the tab, and start the ones still to come the same way, without changing Settings. The tab remembers which you chose. Opening a call from elsewhere, as find does, opens its card.

## Folding a chain of steps
Claude's thinking and tool calls between two messages can still take a lot of room, even with each Tool calls card folded. Settings → Conversation → **Fold each chain of steps** (off by default) folds the whole run of steps, the thinking, Tool calls cards and subagents between two messages, into one line, such as *Worked for 18m*, with what the steps were beside it: *9 tool calls · 3 thinking*.

- **Open the line** with a click anywhere on it, or its chevron. It opens a list of the steps, a line each: *Thought for 7s*, *Ran check for UE4SS.pdb*, *Created analyze_dump.py*, *Edited disasm.py*, with how long a call took at the right of its line. A long list scrolls, with a scroll bar of its own.
- **Open a step** with its chevron. A thinking line opens to what Claude thought, and a call to its detail, as a tile of a Tool calls card opens: the input and the output, and **Open** for the file it worked on.
- **Fold the line again** with another click, and the list is gone.
- **While Claude is working**, the line says *Working · 6 earlier steps*, and the last step stays under it: the one Claude is on. Once Claude has moved on, the whole run is in the line.
- A single step, such as a lone *Thinking* row before a reply, folds the same way once Claude has moved on from it. If a step's time isn't known, or the run took under a second, the line counts the steps instead of saying how long they took.

Find and **Export conversation…** still reach a folded step, and going to one opens the list at its line. **Show every step** in the line's right-click menu turns the setting off.

## Opening a call

Click a tile to open its detail under the tiles. One tile is open at a time, so opening another closes it, and the × at the detail's right closes it. The detail shows the call in full:

- **Edit and Write.** An inline diff of the change. **Open diff** opens the file in the [diff view](../changes/the-diff-view.md): from before Claude's first change to it in this session to the file as it is now, the way Changed files shows it.
- **Bash.** The command, and its output in a monospace font. What git did shows as chips under the tile: *Committed 1a2b3c4 on main*, *Pushed main*, *Rebased onto origin/main*, *Opened PR #42* (a link to the pull request). A command that reached its time limit says so, and that it carries on in the background; one in the background says *Running in the background* or *Moved to the background*.
- **Read, Grep, Glob and others.** What the tool was given and what it returned.
- **Open.** For a call that worked on a file, **Open** opens the file in Claudette's editor or the OS's app ([Opening files from the conversation](../project-files/opening-files-from-the-conversation.md)).

An output over 20,000 characters shows its start, with **Show all (n KB)** to show the rest. A call with nothing more to say reads *Nothing more to show for this call.*

[Find](finding-your-way.md#find) opens the tile a match is in.

## A call in a window

**Open in a window**, the button in a call's detail, opens the call in a window of its own, which follows the call: its title and state, and its running time, ticking each second. It has two tabs, **Call** and **Details**.

**Call** depends on the tool:

- **Bash.** The command and its output. Claude Code sends a command's output only when it ends, so a running command shows its time, not its output.
- **Read.** The file as Claude read it, numbered from where it started, in the diff view's colors.
- **Edit and Write.** This call's own change, with **Open diff** for the whole session's.
- **Grep and Glob.** What they found, file by file, with a Grep's matching lines.
- **Anything else.** Its arguments and its result.

**Details** has the arguments and the result as the tool got and gave them. There is one window per call: opening it again brings it to the front. `Esc` closes it.

## Settings

**Show tool calls collapsed** (Settings → Conversation, off by default) starts new Tool calls cards folded to their header line. **Fold each chain of steps** (off by default) folds a run of steps between two messages into one line, as [Folding a chain of steps](#folding-a-chain-of-steps) says.

Tool calls are one of the in-between steps that **Quieter in-between steps** (Settings → Conversation, on by default) draws without a tint and outline until you point at them. See [Reading the conversation](reading-the-conversation.md#quieter-in-between-steps).

## See also

- [The working line and running tasks](the-working-line-and-running-tasks.md) for what Claude is doing right now.
- [Changed files](../changes/changed-files.md) for everything Claude has changed since the session started.
