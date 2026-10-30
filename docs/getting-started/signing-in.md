---
title: Signing in
parent: Getting started
nav_order: 5
summary: "Claude Code keeps its own sign-in."
---

# Signing in

Claude Code keeps its own sign-in. Claudette never reads or stores your credentials: it only notices when Claude Code needs you to sign in, and runs Claude Code's own sign-in for you. Signing in from Claudette signs in Claude Code in your terminal too, and the other way round.

{% include shot.html name="guide/getting-started/sign-in" alt="Claudette's sign-in screen, with Sign in, More options and I've already signed in buttons" %}

## Signing in

At launch, if Claude Code isn't signed in, Claudette shows **Sign in to Claude** instead of the tabs.

1. Choose **Sign in**. Claudette opens your browser at Claude's sign-in page and shows *Finish signing in in your browser.*
2. Sign in there. When the browser finishes, Claudette notices and shows your account.
3. Your tabs start. A tab that couldn't start earlier is started again on its session.

Messages you sent while signed out aren't lost. Claudette keeps them, with their images, and delivers them in order once you're signed in.

## When the browser can't finish

While Claudette waits you can:

- **Open browser again**, if the browser didn't open or you closed the tab.
- **Enter a code instead**, if the browser is on another device or something blocks the page from reaching Claudette. The page shows a code. Paste all of it into the field (**Paste the code from the browser**) and choose **Submit code**.
- **Cancel**, which ends the sign-in.

## Another kind of account

**More options** offers other ways to sign in:

- **Sign in with an Anthropic Console account (API usage billing)**, for an account billed for API usage.
- **Sign in with SSO**, for single sign-on.
- **Sign in with a Claude subscription instead**, while one of the others is under way.

If your organization's settings limit the kind of account that may sign in, Claude Code refuses the others. Use the kind it allows.

## Signed in somewhere else

If you signed in from a terminal, choose **I've already signed in**. Claudette asks Claude Code again. If Claude Code still says you're not signed in, Claudette says so.

## Without an account

**Try the demo** opens Claudette on made-up projects, with a stand-in for Claude, in a window of its own: see [Trying the demo](trying-the-demo.md).

## In the middle of a session

If Claude Code loses its sign-in while Claudette is running, a banner runs across the top of every tab: *Claude Code needs you to sign in.* The tabs stay open. **Sign in** on the banner opens the same screen as a window over the main one, which you can close. If Claudette isn't in front, you also get one notification, whatever the number of tabs.

Your account, its plan and **Sign out…** are in the header's account menu: see [Your Claude sign-in](../accounts/your-claude-sign-in.md). Settings → Claude Code has **Sign in** and **Sign out…** too.

## When it goes wrong

A sign-in that fails shows Claude Code's own message and **Try again**, which repeats the same kind of sign-in. [Troubleshooting: signing in](../troubleshooting/signing-in.md) lists the causes.
