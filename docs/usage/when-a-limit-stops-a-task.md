---
title: When a limit stops a task
parent: Usage and limits
nav_order: 8
summary: "When a plan limit stops a task, the tab waits for the reset and carries on by itself."
---

# When a limit stops a task

When a plan usage limit stops Claude in the middle of a task, the tab waits for the limit to reset and then carries on, so work left running overnight or over lunch picks up again in the next window. It's on by default.

{% include shot.html name="guide/usage/limit" alt="A tab stopped at its session limit: a bar over the composer says the task continues by itself when the limit resets, with Don't continue beside it" %}

## While it waits

- **A bar over the composer** says so: *You've hit your session limit. The task continues by itself when it resets, at 15:45.* Beside it is **Don't continue**. A reset on another day reads *on Mon at 09:00*.
- **The tab's row** says *Usage limit · continues at 15:45* in place of the model, and the tab's info card has a **Usage limit** row with the bar's text.
- **The Dock or taskbar icon** shows an hourglass until the limit resets: see [The Dock and taskbar icon](../away/the-dock-and-taskbar-icon.md).

A computer that sleeps through the reset misses it, so turn on **Keep this computer awake** (Settings → General, or the cup in the header) to hold sleep off while a tab waits: see [Keeping the computer awake](../away/keeping-the-computer-awake.md).

## Continuing

A minute after the reset, the tab sends *Continue from where you left off.* It appears in the conversation as a message labeled *Automatic continue after the usage limit reset*. It doesn't send your last message again. The continued turn is like any other: it still asks for permissions, so it can stop on a prompt while you're away.

Only a limit that waiting fixes starts a wait. A rejection that needs usage credits, a turn you stopped, running out of turns or budget, or a server error doesn't.

A tab that doesn't use usage credits waits the same way when they'd pay for it: see [Tabs that don't use usage credits](tabs-that-dont-use-usage-credits.md).

## When it won't continue by itself

The bar says why, and offers **Continue when it resets** (or **Continue**, once the limit has reset) and a close button.

- You turned it off, in Settings or for the tab: *You've hit your session limit. It resets at 15:45.*
- You chose **Don't continue**. That's for this reset only: hitting the limit again before it resets doesn't start another wait, and the next window starts fresh.
- The reset is more than a day away, as a weekly limit's can be.
- Three continues in a row each ran straight into the limit again. Like Claude Code, it waits again at most twice, then stops. A continue that gets going, or a message from you, starts the count again.
- The limit reset more than 30 minutes before Claudette noticed, because the computer was asleep or Claudette wasn't running: *Your session limit reset at 15:45 while this computer was asleep or Claudette was closed, so the task didn't continue by itself.*

## Ending the wait

Any turn that starts ends it, whether you sent a message or something else started one. Turning the setting off or on while a tab waits changes that wait straight away.

The wait is saved with the tab, so a restart, including one into a new build of Claudette, keeps waiting.

## Turning it off

- Everywhere: Settings → Tabs → **Continue tasks when a usage limit resets**.
- For one tab: **Tab settings…** in the tab's menu, **Continue tasks when a usage limit resets**, set to **On** or **Off**. The default choice reads *Default (on)* or *Default (off)*, following the setting above. See [Tab settings](../tabs/tab-settings.md).

## Play a game while you wait

While a limit holds a task, the bar has **Play a game while you wait** under its text, with a menu: **Tic-tac-toe**, **Connect Four**, **Hangman** and **Minesweeper**. The game opens in the Games tool window. See [Games with Claudette](../claudette/games-with-claudette.md).

## Settings

| Setting | Default | Where |
|---|---|---|
| **Continue tasks when a usage limit resets** | On | Settings → Tabs → At a usage limit |
| **Continue tasks when a usage limit resets** | Default | **Tab settings…** |

## See also

- [Alerts](alerts.md), which warn you before you get here.
- [Tabs that don't use usage credits](tabs-that-dont-use-usage-credits.md), which wait here rather than spend them.
- [Check-ins on long turns](../composer/check-ins-on-long-turns.md), the other message Claudette sends on its own.
