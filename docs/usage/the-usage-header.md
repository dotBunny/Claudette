---
title: The usage header
parent: Usage and limits
nav_order: 1
summary: "The strip across the top of the window shows how much of your plan's limits you've used, how fast you're using them, and when you'll hit the limit at that rate."
---

# The usage header

A small chip at the right of the window's top strip shows how much of your plan's limits you've used, and a click on it says how fast you're using them and when you'll hit the limit at that rate. It's there whichever tab is selected, and it keeps up even when no tab is working.

{% include shot.html name="standard" alt="Claudette's window with the usage chip at the right of the top strip: a short label, a thin bar and the percent for the session, the weekly limit and a model" %}

## What's in it

The chip is one line. Each meter in it has a short label, a thin bar and the percent used:

- **S** is the session, the most important: a 5-hour window.
- **W** is the weekly limit across all models.
- A model's own weekly limit, if your plan has one, takes the model's first letter, such as **F** for Fable (more letters if another meter has that one).

In a narrow window the chip drops the bars and keeps the labels and percents, so it never crowds the logo or the menu. A screen reader reads each meter with its reset, such as *Session 9%, resets in 3h 18m*.

## The popover

Click the chip, or press `Enter` while it has the focus, to open a popover under it. `Esc` closes it.

- **Each meter** on its own row: its full name, the percent used, a full-size bar, and when it resets: the session's as a countdown (*resets in 2h 14m*), the weekly ones' as a day and time (*resets Thu 9:00 AM*).
- **The trendline** is a small chart of this window's usage so far. A dotted line carries the current rate forward to show where the window is heading, and faint dashed lines mark the warning and alert levels.
- **The line under it** says in words where that's heading. See below.
- **Open the Usage panel** opens [the Usage window](the-usage-window.md).
- **Model meters** are the rows after **Weekly**: one for each weekly limit your plan has for a particular model, named for the model.

## Colors and warning levels

A meter's bar is blue, turns amber at the warning level, 75% by default, and red at the alert level, 90% by default. The percent beside it takes the color too, in the chip and in the popover. The same two levels apply to every meter, and to the [alerts](alerts.md).

## The line under the trendline

It's one of these:

- *At this rate you'll hit the limit in 1h 05m, 1h 09m before it resets.* In amber or red. At the pace of the last half hour, you'd reach 100% before the window resets.
- *On track: about 70% used when the session resets.* At that pace, you'd get through the window.
- *No recent usage.* Nothing has been used lately, so there's no pace to project. Claudette says so instead of carrying on a stale rate.
- *Not enough data yet.* Claudette needs a few minutes of readings to measure a pace.
- *You've hit the limit.* See [When a limit stops a task](when-a-limit-stops-a-task.md).

The pace is measured over the last 30 minutes by default, so one big turn doesn't swing the projection. Change the window with **Burn rate window (minutes)**.

## When usage credits pay

If your plan has usage credits, they pay for Claude once a limit is used up, up to the cap set on claude.ai. While they do, a notice runs across the window straight under the header:

> **Using usage credits.** *Your session limit is used up, so your usage credits pay for Claude until it resets at 15:45. $12.40 of $50.00 spent this month.*

- It names the limit that's used up (the session, the week or a model's) and when it resets, then what's been spent this month, against your cap when the account has one of its own.
- **Manage on claude.ai** opens your plan's usage on claude.ai, where you turn usage credits off or change the cap.
- It has no **Dismiss**. It goes by itself when credits stop paying: the limit resets, credits are turned off or reach the cap, or Claude Code says they aren't paying.
- It shows whichever tab is selected, one on an [API-key account](../accounts/api-key-accounts.md) too, since your plan's other tabs are still spending them, apart from [those that don't use them](tabs-that-dont-use-usage-credits.md).
- Just after Claudette starts, it waits for the first reading or the next turn.

An OS notification tells you when credits start paying: see [Alerts](alerts.md).

## In the sidebar

With **Show usage in the sidebar** on (Settings → Usage, on by default), the meters are a card at the top of the sidebar instead of the chip. Each limit has its own row: its name, the percent used and when it resets (*9% · resets in 3h 18m*), over a bar that turns amber and red as the chip's do. Under them is the line that says where the session is heading. Click the card for [the Usage window](the-usage-window.md).

The card shows only while the sidebar is open. Collapse the sidebar and the chip comes back to the header; open it again and the card takes over. Turn the setting off to keep the chip in the header.

## Model meters

**Show model-specific weekly limits** (Settings → Usage, on by default) shows the model meters. Claude Code reports them. If a version of Claude Code stops reporting them, the model meters go away and the rest of the header carries on, unless **If Claude Code stops reporting them, read them from /usage** is on. Then Claudette reads the text Claude Code prints for `/usage` instead. That costs nothing, but it can break when Claude Code changes the wording, so it's off by default.

## How current the numbers are

Claudette reads your usage when it starts, after each turn (at most once a minute) and every 5 minutes otherwise, and the meters also move as your tabs work. When the numbers are more than 10 minutes old, for example just after launch before the first reading, the popover adds *as of 14:02* under the line that says where the session is heading.

Within one window a meter only goes up. A reading lower than the one shown, for the same window, is out of date, so Claudette keeps the higher one.

## When there are no meters

A sign-in with no plan limits, such as an API key, has no chip, and the header just says **Claudette** (for one of Claudette's [API-key accounts](../accounts/api-key-accounts.md), it shows the account's spend). If Claudette can't start tracking usage, the header says **Usage unavailable** and why, and your tabs keep working.

## Opening the details

**Open the Usage panel** in the popover opens [the Usage window](the-usage-window.md). The chevron at the header's right draws the header taller, with charts: see [The detailed header](the-detailed-header.md).

## Settings

All in Settings → Usage.

| Setting | Default |
|---|---|
| **Warn at (% of the session used)** | 75 |
| **Alert at (% of the session used)** | 90 |
| **Burn rate window (minutes)** | 30 (from 5 to 300) |
| **Show model-specific weekly limits** | On |
| **Show usage in the sidebar** | On |
| **If Claude Code stops reporting them, read them from /usage** | Off |

## See also

- [The detailed header](the-detailed-header.md) for the charts.
- [Your Claude sign-in](../accounts/your-claude-sign-in.md) for the plan beside your email, which opens your plan's usage on claude.ai.
