---
title: The detailed header
parent: Usage and limits
nav_order: 2
summary: "The header can be drawn taller, with charts, so you can keep an eye on usage without opening the Usage window."
---

# The detailed header

The header can be drawn taller, with charts, so you can keep an eye on usage without opening [the Usage window](the-usage-window.md).

{% include shot.html name="header-standard" alt="The detailed usage header expanded: a chart of the session with its projection, a chart of the week with a model's line, the burn rate and time to the limit, and the busiest tabs" %}

## Turning it on

Click the chevron just after the meters (its tip says **Expand the usage header**), and click it again to collapse it. Settings → Usage → **Detailed usage header** is the same switch. It's off by default, and the choice is remembered on this machine, not synced.

Collapsed, the header is as described in [The usage header](the-usage-header.md). Under it, about 150 pixels high, the detailed header has the session chart, the weekly chart, two numbers and the busiest tabs, left to right. Click any of them to open the Usage window.

## This session

Usage over the current 5-hour window, from its start to its reset.

- Faint bands above the warning and alert levels, with a dashed line at each.
- The dotted projection from now, a marker for now, and the times at the start, now and the reset.
- When the projection crosses the alert level before the window resets, the chart marks that point and says when: *Hits 90% at 14:05, 1h 16m before it resets.* Past the alert level it marks the limit instead: *Hits the limit at …*.

## This week

Usage over the current 7 days, with a tick at each midnight and the days' names, and a projection to the week's reset.

The weekly projection uses the week's average pace so far, not the recent burn rate, because a week has nights and days off in it.

When **Show model-specific weekly limits** is on, each model's weekly limit is a thinner line with a legend (*All models* and the models' names). At most two models get a line, since more colors don't stay apart beside the accent color.

## Burn rate and Time to limit

- **Burn rate** is the percent of the session used per hour over the burn rate window, such as *12.4%/h*, or *Idle* when nothing was used in that time. The note under it says how long that window is.
- **Time to limit** is how long until the session hits 100% at that pace, such as *1h 05m*, with a note: *Before it resets* or *It resets first*. It says *Now* at the limit, and a dash when there's no pace to go by.

## Busiest tabs

The three tabs that used the most tokens in this session window, as *Fix the loader 41%* with a small bar. It's the same count as the Usage window's list of tabs: only turns under your plan count, and a tab you've closed keeps the last name it had. With [usage sharing](sharing-usage-across-machines.md) on, a tab from another machine reads *Fix the loader · Laptop*. With no turns yet it says *No turns yet in this window.*

## Narrow windows

The busiest tabs are left out below 960 pixels wide, then the weekly chart below 700 pixels. The session chart takes the room.

## When there are no charts

A sign-in with no plan limits gets no charts, as it gets no meters.

## Settings

| Setting | Default | Where |
|---|---|---|
| **Detailed usage header** | Off | Settings → Usage |
| **Burn rate window (minutes)** | 30 | Settings → Usage |
| **Warn at (% of the session used)** and **Alert at (% of the session used)** | 75 and 90 | Settings → Usage |

## See also

- [The usage header](the-usage-header.md) for the meters and what the colors mean.
- [The Usage window](the-usage-window.md) for the same charts larger, with past sessions and weeks.
