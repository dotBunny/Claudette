---
title: A tab's tokens
parent: Usage and limits
nav_order: 4
summary: "Each tab counts the tokens it has used."
---

# A tab's tokens

Each tab counts the tokens it has used. The count sits in the composer bar, at its right, in short form such as `1.2M tok`. It moves while Claude works, and it's saved with the tab, so it survives restarts and resumed sessions.

## The breakdown

Click the count (its tip says **Tokens used by this tab**) to open a breakdown:

- A table with a row for each model the tab used: **Input**, **Output**, **Cache write**, **Cache read** and **Cost**.
- A summary line: *12 turns · 345,000 tokens · about $1.20 at list price (an estimate, not your bill)*. Claude Code works out the cost at list price, so it's an estimate, not what you're billed.
- *This session window* and a number: the tokens this tab has used since the current 5-hour window started. That's the part that counts against the session limit.
- **Tokens per turn**, a small chart of the tab's last turns from the past week.

Each turn adds what it used, subagents and compaction included, so a long session isn't counted again with every turn.

The tab's info card (hover over its row, or the **ⓘ** button at the end of the line under the message box) has a **Tokens** row with the same count and the number of turns.

## Where else the numbers show

- The working line above the composer shows the tokens of the turn in progress: see [The working line and running tasks](../conversation/the-working-line-and-running-tasks.md).
- [The Usage window](the-usage-window.md) and [the detailed header](the-detailed-header.md) rank your tabs by what they used in the session window.
- The count isn't the same as how full the tab's context is: see [Context and compacting](context-and-compacting.md).

## Resetting the counts

**Clear usage history…** in Settings → Usage can also reset every tab's token totals, with **Also reset per-tab token totals**. Otherwise the totals stay with the tab: see [Usage history](usage-history.md).

## See also

- [The composer bar](../composer/the-composer-bar.md) for everything else in it.
- [The prompt cache](the-prompt-cache.md), which is why *cache read* tokens cost so much less.
