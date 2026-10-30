---
title: Images
parent: The conversation
nav_order: 11
summary: "The images Claude's tools hand it, shown under their cards, the images in its replies, and the image files and links it names."
---

# Images

Claude doesn't make images, but it does see them, and the conversation shows them in two places: under the cards of tools that handed Claude an image, and inside a reply's Markdown. An image file or a link to an image that Claude names opens in Claudette too. Images you attach to your own messages are in [Attachments and large pastes](../composer/attachments-and-large-pastes.md).

## Images from Claude's tools

A tool's result can hold images: a Read of an image file (PNG, JPEG, GIF or WebP), a PDF's pages read with `pages`, a notebook's figures, a Bash command whose output is an image, and an MCP tool's image, such as a screenshot from a browser tool. They show under the tool's tile without opening it, each at most 120 pixels high and 320 wide, never larger than it really is. Several images, such as a PDF's pages, wrap onto more lines. A restored tab shows them too.

Each image is named for what it is, as its tooltip: a Read of an image by its file's name, a PDF's page as *report.pdf, page 3*, a notebook's figure as *analysis.ipynb, figure 2*, anything else *Image* (or *Image 2* of several).

A whole PDF read without `pages` isn't images: the card says what was read.

### Showing one full size

Click an image to open it in a window of its own beside the conversation, titled with its name. It opens at its own size, up to 1400 by 900 pixels and 90% of the screen, and from then on any size you drag it to, shrinking to fit but never growing past its own size until you zoom in. `Esc` closes it.

Under the title are the tool, the type and size (*Read · PNG 245 KB*), and, when Claude Code shrank the image before sending it, *scaled down from 3000 × 1000 to send*, since what you see is what Claude saw.

- **Copy path**, for an image from a file, puts the file's full path on the clipboard, to paste into a terminal or another app, and says *Copied* for a moment. An image that isn't from a file, such as a screenshot from a tool or a web image, doesn't have it.
- **Copy image** puts it on the clipboard and says *Copied* for a moment.
- **Save as…** saves it under its name, with its type's extension. Claude Code may have sent a large PNG on as a JPEG, which then saves as `.jpg`. A save that fails says why under the title.
- **Open**, for an image file Claude read, opens the file in the app your OS uses for it. A web image has **Open in browser** instead.

### Zooming in

The window opens with the whole image in it. To look closer:

- **The mouse wheel** over the image zooms in a step as you turn it up, and out as you turn it down, keeping the spot under the pointer where it is.
- **+** and **−**, under the buttons on either side of the percentage the image is shown at, zoom about the middle of the window, as do the `+` and `-` keys, with `Ctrl/Cmd` or without.
- **Fit**, or `0`, shows the whole image again.

It goes from 10% to 1600% of the image's own size, in steps. Once it's larger than the window, drag it around with the left mouse button, holding `Space` or not, or with the middle button; the scroll bars move it too. Each image opens fitting the window.

### Animated images

An animated GIF or WebP plays in the window, and only there: under a tool's tile and in a reply it stays on its first frame. Each frame shows for as long as the image says, and it plays as many times as the image says before stopping on its last frame. Under the title, the details count its frames (*loop.gif · GIF 2.4 MB · 48 frames*).

- **Pause** stops it on the frame showing, and **Play** goes on from there, or from the beginning once it has stopped.
- **Copy image** copies the frame showing, since the clipboard holds a still image. **Save as…** saves the image as it came, still animated.

With motion reduced (Settings → Appearance → **Motion**), an animated image opens still on its first frame until you click **Play**.

## Images in Claude's replies

A reply's Markdown can include an image. A reply can be steered by anything Claude read, such as a web page or a file in the project, and an image that loaded by itself could send data out in its web address. So Claudette loads only what can't reach another machine, and asks before the rest.

- **Right away.** A file in the tab's folder (the worktree, for a tab in one), as long as nothing on the way down from the folder is a link, which could lead anywhere; and an image embedded in the reply itself. A relative path is read from the folder, so a chart Claude just made shows up.
- **When you click.** An image from the web, or a file anywhere else, shows a placeholder: *Image from example.com, which loads only when you ask.* or *Image at /tmp/chart.png, outside the tab's folder.* Its button is **Load image** for the web and **Show image** for a file. The address is the placeholder's tooltip.
- **Never.** An image on a network path (`\\server\share\…`, `//server/…`), which would hand your credentials to that server, and any address that isn't a web address or a file. The placeholder says why, with no button.

An image comes through if it arrives within 30 seconds, is at most 20 MB and really is a PNG, JPEG, GIF, WebP or BMP, whatever its name says. If it fails, the placeholder says why (*example.com answered 404 Not Found.*) and the button stays, so you can try again.

An address you loaded once loads by itself in that tab from then on, until you close it. A loaded image is shown at most 480 pixels high and as wide as the conversation, never larger than it is. Click it to open it full size, in the same window as above.

This applies wherever Markdown shows: replies, plans, the Tasks page, and a subagent's prompt and report.

## Opening an image Claude names

An image file Claude names, and a link to an image on the web, open in the same window as above, rather than in another app. That's a PNG, JPEG, GIF, WebP or BMP of at most 20 MB. Anything else, such as an SVG or a PSD, opens in the app your OS uses for it, as before.

- **A file named in a reply.** Click a link to the file or its name in code, as in [Files and links in replies](files-and-links-in-replies.md). A file named like an image that isn't one, such as a text file called `notes.png`, opens as any other file does.
- **Open buttons.** **Open** on a tool card, and **Open** in Changed files, the diff window and a project action's log.
- **In Project Files.** Double-click an image in the explorer, or select it and press `Enter`. A single click shows it in the preview tab, which says the file isn't text, with **Open in Claudette's image viewer** and **Open in the OS's app**, so you can go down a folder of images without opening a window for each. That button works whatever the setting says.
- **A link to an image on the web.** A web address ending in an image's name (`…/menu.png`) loads as a reply's web image does, through Claudette itself and within 30 seconds, and then opens in the window, whose **Open in browser** takes you to the address. If what's there isn't an image after all, such as a page or an error, the link opens in your browser. Other web links open in the browser as they always have.

Under the window's title is where the image is: its path in the project (`docs/menu.png`), its whole path elsewhere, or the website. Every menu that has **Open in the OS's app** keeps it.

An image under a tool's tile or shown in a reply opens in the window whatever the setting says, since many of them aren't files.

## Settings

- Settings → Files and diffs → **Open images in**: **Claudette's image viewer** (the default) opens the images above in Claudette's window. **The app the OS uses for the file** opens image files there, and links to images in the browser, as before. See [Every setting](../settings/every-setting.md#files-and-diffs).
- Settings → Appearance → **Motion**: with motion reduced, an animated image opens still.

## Not in find or an export

Find and [Exporting](exporting.md) skip images, since they have no text to find. An export's line for a tool call keeps its summary.

## See also

- [Tool calls](tool-calls.md) for the tiles the images sit under.
- [Files and links in replies](files-and-links-in-replies.md) for clicking a file Claude names.
- [Opening files from the conversation](../project-files/opening-files-from-the-conversation.md) for the files that open in Claudette's editor.
