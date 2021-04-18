# Discord-Overlay
A DirectX 11 window to host Discord's Overlay in order to capture and display it with OBS.
Based on the idea of [Discord Overlay Host](https://obsproject.com/forum/resources/discord-overlay-host.371/) I made an updated version as it wasn't updated in 5 years and accumulated a lot of issues.

## Compatibility
You should only need to have a DirectX 11 capable GPU to run the program.

## Why not use Discord Streamkit ?
The people behind Streamkit clearly never really used it. The link it generates works only for the specific room of the specific server you targeted. It means that if you move to another room/server you need to recreate another link.

## Why is it greyish ?
I get that it's not easy to chroma key that color but if there was a green background it would look awful as the Discord's overlay is partially transparent. You'd have a disgusting green under the overlay that you couldn't make disappear.

The current RGB color of the background is 46, 49, 54.

## Why can't I resize the window ?
The way Discord detects if it will display its overlay on a window depends on 2 factors :
- Does it uses the GPU ?
- Does it have dimensions of at least 768 * 432 pixels ?

So the window needs to be at least 768 * 432 and take into account the scale factor of each display. So in order to avoid any confusion about if my program works or if the window is too small I forced the size of its window. I doubt that you will need to have a bigger window for the overlay anyway.
