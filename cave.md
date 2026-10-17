# Still to do

I made this in a few days. It isn't as good as I want it yet.

My list:

- Check memory over longer runs.
- Check how fast it runs.
- Check security, especially themes.
- Try more displays and settings.
- Try the Codex plugin with real activity.
- Add Linux and Mac support.

I used AI to help write the code. I haven't had time to finish these checks. I'll work on them when I can, following my [manifesto](https://umbertogiacobbi.biz/vibeware/manifesto).

**Themes are programs. They run code on your PC. For now, only get them from the [official repository](https://github.com/umbertotechnopreneur/VibePanel). Security checks are still to do.**

## What I checked

An earlier picture worked on my Thermalright display.

On 16 October 2026, I built the Windows x64 app and ZIP. One approved check made all 40 theme pictures: 20 themes, light and dark. I looked at the overview images. They used made-up demo data.

On 17 October, I confirmed Aurora Glass worked on my display after a 20-second CLI trial. The new GUI controls, faster rates, other themes, live Codex activity and setup on a fresh PC still need checking.

The new playback spectrum, stereo VU and Codex setup controls are not checked in live use yet. A successful build does not check them.

The color controls, monospace font list, theme fades and 25 ms/Poor settings also need a live check.

Track and cover changes blend in the renderer for 600 ms. All 10 approved offline checks passed on 17 October: mixed pixels, timing, rapid skips and color filters. They used synthetic images and metadata, with no app, audio or USB. Real playback and USB output still need checking.

The new studio layout builds for Windows x64 with no errors or warnings. Its dropdowns, filter switch and color guides still need my visual check. CPU savings have not been measured. No tests, app or USB trial were run for this change.

The preview now has a flat, solid backing with no shadow or blur effect. The reported flicker still needs a live check.

The Windows 1 GB guard builds in Release x64. On 17 October, the old app hit about 1.16 GB and triggered the popup and stop. A frozen UI and guard failures still need checking.

I then fixed native message ownership, moved preview JPEGs outside the JavaScript eval bridge and limited NumPy's native threads. Local checks passed: 4,000 large UTF conversions, 1,317 preview frames with UI changes and error recovery, and 400 synthetic renders across all twenty themes. The UI process family peaked at 345 MB; the separate worker check peaked at 102 MB. No USB device was used. These short checks do not prove long-term memory use, speed or security.

The earlier memory check did not check the preview's on-screen size. I fixed its HTML container height, then checked WebView captures and visible canvas pixels in light/dark mode, after resizing, and with wide, square and portrait fixtures. The image is visible in those captures. Very small windows can still leave little room for it. This was an offline check, not a USB trial.

The 17 October resource audit limits PNG/JPEG/GIF/BMP wallpaper and legacy artwork to 16 megapixels. Other formats use the fallback. Legacy backgrounds stay in a 12-entry/8 MiB memory cache. Bundled scripts load only beside the engine.

USB discovery runs every ten seconds. Active output also resends an unchanged frame after ten seconds to catch stale handles. A lost session closes and never restarts itself, including in the legacy still-image player.

At my request, Windows startup now starts the saved device and theme in the tray, once that device is available. Stop cancels the wait. All 14 approved hidden-window checks passed: startup settings, the ten-second timer, a late saved device, Stop, errors and close/reopen preferences, including the last slider change. They used a fake client and no USB backend. A real Windows sign-in check is still pending.

The Windows x64 Release build has no errors or warnings. All 85 approved offline checks passed, including oversized image headers, 32 artwork changes and bundled paths. No USB handles were opened. Repeated physical unplug/replug and long-term memory use remain unchecked.

This is my hobby project. No warranty.
