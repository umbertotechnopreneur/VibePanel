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

The ten new System themes and screen switches build in Release Windows x64. Missing values and sections are hidden; swap is excluded. Screens can be disabled independently of their preview. Closed Codex processes are skipped on the device; no eligible screen sends a black frame.

On 17 October, all 96 approved offline C# checks and all 18 Python tests passed. The Python run encoded and decoded 180 JPEGs across the ten System themes, light/dark, wide/square/portrait and complete/partial/missing synthetic data. No live sensor or USB check ran. The ImageGen proposals use demo values and are design references, not captures of the renderer.

The original Open Hardware Monitor 0.9.6 is the only Windows sensor source. The stock .NET Framework 4.5 binary failed the approved diagnostic read with a missing mutex API before any data was returned. I now rebuild its pinned source for .NET 10 with four API compatibility changes. Sensor algorithms and driver bytes stay unchanged. The approved Windows x64 read returned CPU usage, GPU usage/clock/temperature, and used/total RAM. CPU clock and temperature were unavailable. This does not prove other hardware works. Windows ARM64 sensors are unavailable. GPU support covers detected NVIDIA/AMD devices, not every GPU.

I approved the DLL's WinRing0 driver lifecycle on 17 October 2026. Reading a System screen may install/load the driver internally; closing the reader may remove it. The first approved diagnostic attempt found the binary incompatibility. A separately approved read of the rebuilt library returned live data. Linux and macOS sensor readers remain unimplemented.

All 99 approved offline C# checks passed, including the .NET 10 library target and the exact hashes of both embedded upstream drivers. All 18 Python tests passed again, including 180 synthetic System-theme images. These offline checks did not access sensors, audio or USB; the live sensor read was separate.

After approval, I installed the compatibility build and reopened the saved System/Ledger screen in local preview. The visible studio showed live CPU usage, GPU usage/clock/temperature, and used/total RAM. The installed sensor DLL matched the packaged DLL. Two diagnostic reads succeeded; no USB output or audio command was issued. CPU clock and temperature remained hidden because the library returned no values.

On 18 October, a separately approved diagnostic with verified administrator privileges also returned CPU clock and package temperature. Windows recorded WinRing0 installation during the read; its service was absent after the reader closed. The existing studio and display session were left running without elevation. This confirms that the missing CPU details on this machine were caused by insufficient privileges. It does not prove the same driver works on other Windows configurations.

The System administrator-restart button builds in Windows x64. It saves choices, waits for Windows to accept elevation, then closes the old studio. The replacement waits for that process to exit and starts in preview. The existing memory guard launches the replacement outside the old process-family job. No extra service is added, and memory protection stays enabled.

On 18 October, all 99 baseline checks and nine offline administrator-button checks passed. The separate elevated fixture survived the old guard's cleanup and started its own guard. The three restore-visibility checks passed again. After approval, I updated the installed app and clicked its real administrator-restart button. One new studio reopened with the same System theme and settings, in local preview, showing CPU usage/clock/package temperature, GPU details and RAM. A normal launch then restored that elevated instance and exited successfully. Cancellation and launch failure were checked with a fake host; no real Windows permission dialog was canceled. No USB output command was sent during these checks.

The white-window report was traced read-only to a running installed studio whose WebView child windows were hidden while the UI and engine kept working. The pinned Photino bridge now synchronizes WebView2 controller visibility on host show/hide, minimize/restore and controller creation. The approved isolated startup check passed all three restore paths. Native child-window visibility, DOM content and three captured WebView images showed the studio after hidden startup, hide/show and minimize/restore. This used a fake client and isolated settings, with no engine, sensor or USB backend. The installed copy was left untouched during that check.

After separate approval, I updated the installed package and opened it in preview without startup-output arguments. Read-only inspection confirmed one studio instance with visible host, browser and renderer windows. Its native bridge matched the built DLL. This was not a sensor or USB trial, or a real Windows sign-in check.

This is my hobby project. No warranty.

On 18 October, I requested a real Windows x64 MSI. The first MSI installation returned Windows Installer status 0. I then requested a clean rebuild and removal of generated output, keeping only the final MSI. No tests or MSI validation were run for this packaging change. Upgrade, repair and uninstall behavior have not been tested.

Later on 18 October, Start opened an old studio from an uninstall backup. After the app closed, its WinRing0 driver was still running from the old install path. Normal process and read-access checks missed the loaded driver. A separate administrator stop and service deletion succeeded, and the service query then reported that it no longer existed.

MSI 0.1.2 adds visible setup/progress and scoped process/driver cleanup before files change, including when upgrading an older MSI. Cleanup checks service removal and generated driver-file deletion; a busy or pending deletion is an error. Installer execution, elevation/cancellation, rollback and physical-device behavior for this new path have not been tested.

Windows x64 restore, C# format verification, Release publishing and native setup-helper compilation passed for this change. I requested no test execution. No new installer or cleanup-helper execution, sensor read, USB trial or ARM64 build was performed.
