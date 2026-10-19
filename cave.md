# Still to do

On 19 October, the old MSI and stale script registration were removed before a
clean Windows x64 Release build and visible installation of MSI 0.1.8. Installer
exit status was 0. All 1,624 installed payload files matched the new staging
files by SHA-256. One MSI registration remained, with no legacy script entry.
The installed sensor library is LibreHardwareMonitorLib 0.9.6.0; the old Open
Hardware Monitor DLL is absent. [Dependency scan evidence](docs/dependency-scan-2026-10-19.md).

The new reinstall command increments the MSI version, removes the old package
and generated output, builds and installs the new package, verifies its files,
then keeps only the final MSI. Initial final cleanup hit an OpenSilver DLL held
by an MSBuild server. Stopping that server allowed cleanup to finish; publishing
now disables build-server/node reuse. Only the final MSI remains in artifacts.

MSI upgrade ordering and the legacy cleanup helper were updated and compiled.
The package table places old-product removal before new file installation.
The clean installation above does not prove upgrade, repair, rollback, cancellation
or duplicate-registration regression scenarios. No tests, sensor diagnostic run,
driver installation or USB output was performed. Thermal RPM plumbing was inspected
in source; actual RPM availability on this hardware remains unverified.

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

On 19 October, Windows Defender recorded `VulnerableDriver:WinNT/Winring0` and quarantined the old installed `OpenHardwareMonitorLib.sys`. The source now uses the official precompiled LibreHardwareMonitorLib 0.9.6 package. The old source project, preparation script and payload source archive are removed. Current builds contain the library and required runtime dependencies, without a monitoring GUI or driver installer. Installer cleanup still recognizes old Vibe Panel driver names solely for removal during upgrades/uninstall.

The Windows adapter reads named motherboard channels without assigning generic readings to CPU. Physical RAM uses `GlobalMemoryStatusEx`; LibreHardwareMonitor's memory/SPD group remains disabled to avoid its driver-loading path. PawnIO registration and its service path are read without installing or opening a driver. The System sidebar displays this status and can open the existing driver folder. Registration alone does not verify access. The two thermal layouts separate names, temperatures, load and RPM, with explicit unavailable labels and readable notices.

Windows x64 restore from official NuGet, format verification and Release compilation passed. With explicit approval, 110 offline checks, 35 fake-client studio checks and 24 Python theme checks passed. Theme checks included 180 synthetic JPEG combinations; 24 additional Thermal Atlas/Airflow fixtures were inspected at font scale 1.6 across wide, square, portrait, light/dark and complete/missing sensor inputs. This does not verify physical sensors, the installed app, USB output or a new installer. No driver, monitoring app or MSI was installed, and Defender settings were not changed.

After verification, 5,800 generated build/staging/package files (about 1.60 GB) and nine generated web files were deleted, including the old sensor build and MSI/ZIP payloads. Automatic approval rejected directory deletion, including the empty-directory cleanup; generated directory shells remain. Installed app files and settings were preserved.

On 18 October, I requested a real Windows x64 MSI. The first MSI installation returned Windows Installer status 0. I then requested a clean rebuild and removal of generated output, keeping only the final MSI. No tests or MSI validation were run for this packaging change. Upgrade, repair and uninstall behavior have not been tested.

Later on 18 October, Start opened an old studio from an uninstall backup. After the app closed, its WinRing0 driver was still running from the old install path. Normal process and read-access checks missed the loaded driver. A separate administrator stop and service deletion succeeded, and the service query then reported that it no longer existed.

MSI 0.1.2 adds visible setup/progress and scoped process/driver cleanup before files change, including when upgrading an older MSI. Cleanup checks service removal and generated driver-file deletion; a busy or pending deletion is an error. Installer execution, elevation/cancellation, rollback and physical-device behavior for this new path have not been tested.

Windows x64 restore, C# format verification, Release publishing and native setup-helper compilation passed for this change. I requested no test execution. No new installer or cleanup-helper execution, sensor read, USB trial or ARM64 build was performed.

PR #2 CI later failed ARM64 publishing because the Visual Studio 2026 runner lacked the forced v143 toolset. Photino now explicitly selects the installed default after importing C++ defaults, avoiding x64's legacy v100 fallback. Targeted native x64 and ARM64 builds passed locally with v145. This is compilation evidence only; no application, sensor or USB trial ran for this fix.

The owner reported frozen display updates when the studio was hidden or minimized. The installed engine log recorded successful presents but did not record window visibility or preview waits, so it could not establish the timing of that freeze. The presentation loop awaited the browser's JPEG acknowledgement before releasing its refresh gate. I separated that bounded preview task from engine refresh and skip hidden previews using native window visibility. Windows x64 restore, format verification and Release compilation passed. The isolated background-output fixture is prepared; execution needs separate owner approval. No installed-app or USB trial has run for this change.

The next refactor moves settings, saved startup, discovery, rotation and output into `VibePanel.Studio`, with no UI framework dependency. The view reads a single latest snapshot. Shutdown drains the worker before closing engine stdin; the single-instance lock remains held through engine cleanup. MSI setup uses a transaction-scoped launch gate, released on commit/rollback or custom-action-host exit. The five semantic palette roles support manifest defaults and saved per-theme light/dark overrides.

With owner approval, 17 model checks, 14 hidden-window startup checks, the blocked-dispatcher/background-window fixture and the native setup-gate fixture passed. The gate checks covered explicit release and owner-process exit, not an actual MSI rollback. Three palette checks passed, including 60 offscreen renders across all 30 themes and both appearances. Eighteen System checks passed, including 180 synthetic JPEG renders. The startup fixture was updated to allow a hidden preview to remain idle. No test opened sensors, audio or USB. These fixtures do not prove real Windows sign-in, physical display behavior or every installer failure path.

MSI 0.1.4 was installed through its visible wizard after removing 0.1.2. Both returned status 0. The old installer used its termination fallback for the running elevated app, then verified driver stop, service removal and runtime-file deletion. The 0.1.4 package was also removed and reinstalled successfully with the app closed. Installed model, UI, engine and palette files match the published payload; settings remain present. The app was left closed. The offline background-check Start shortcut is removed. One obsolete pre-MSI uninstall registration remains despite MSI's registry-removal action; its cleanup is unresolved. Real sign-in, in-place upgrade, repair, cancellation and rollback were not exercised.

Thermal Atlas and Airflow replace two System layouts while keeping their theme IDs. The Windows reader now asks the same Open Hardware Monitor instance for named motherboard temperatures and fan RPM from Super I/O, plus GPU fan RPM when exposed. Preview JPEGs use labeled synthetic data; they are not proof that this computer exposes those sensors. Windows x64 restore, format verification and Release build passed. Automated tests and physical sensor or USB trials were not run for this change. The installed 0.1.5 MSI predates these themes.

Both thermal layouts now use cached, blurred CPU, graphics-card and motherboard photos. These are decorative assets, not pictures of the installed hardware. Dark, light and compact preview JPEGs rendered with synthetic readings; no live sensor, USB or installer check was run for this visual update.

Photino's default toast registration created a second Start shortcut named after the window title. Toast registration is now disabled and the title is simply Vibe Panel. Windows x64 restore, format verification and Release build passed. No runtime or hardware tests were run for this naming change.

The next change adds transactional preferences/screens/palettes, versioned settings with backup recovery, typed ViewModel commands, and the portable `VibePanel.Data` provider boundary. Windows x64 restore, format verification and Release build passed. With owner approval, 35 offline model/persistence/provider checks, 14 hidden-window startup checks and the blocked-dispatcher/minimized/hidden-window fixture passed. No hardware provider ran. Linux CI now runs the same portable checks; local Windows results do not prove Linux or macOS runtime support. The engine host and physical providers remain Windows-only. No MSI was rebuilt or installed for this change.

## 19 October 2026: full sensor readout and System themes

An elevated read-only acquisition returned 43 devices and 448 sensors (433 finite readings). All 232 unique identifiers from the supplied report sensor tree were present; this is coverage evidence, not simultaneous value agreement. The supplied RTX 4060 report and acquired tree contain no GPU Power sensors. CPU Package power is present. All ten System layouts were rendered as local previews in light/dark and wide/compact sizes using acquired hardware readings. Preview inputs combined acquisitions from different times; they are not a synchronized hardware measurement. Restore, format verification and Release compilation completed. No automated tests or USB validation were run for these changes.
