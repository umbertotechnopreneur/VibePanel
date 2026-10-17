# Use Vibe Panel

1. Unzip the file. Keep its files together.
2. Open `VibePanel.exe`.
3. Choose **Music**, **Codex** or **System**.
4. Choose a theme. Look at the preview.
5. Choose your USB screen. Press **Show on display**. Press **Stop** to stop.

Only one window opens. Opening the app again brings it back to the center.

On Windows, a separate guard checks the app and its child processes every 500 ms. If their private committed memory goes above 1 GB (1,000,000,000 bytes), it shows an alert and forces them to stop after two seconds, or sooner if you dismiss it. The alert stays open until dismissed. The app refuses to open if the guard cannot start. This guard remains a last limit after the focused memory fixes. [Checks and limits](cave.md).

**Close app** stops it. **Cancel** keeps it open. **Minimize to tray** keeps it running by the clock. Right-click its icon for Open, About, manifesto, website, GitHub and Exit.

## Install and start with Windows

From the extracted package, open PowerShell in its folder and run:

```powershell
.\Install-VibePanel.ps1
```

Find **Vibe Panel** in Start. Both Start and Windows startup use one stable launcher at `%LOCALAPPDATA%\Umberto Giacobbi.biz\Apps\VibePanel\VibePanel.cmd`; package-version folders are never used directly by those entries.

Vibe Panel keeps one current setup. Closing the app saves the selected theme and all settings, including the last slider change. Opening it again restores that setup in the preview. At Windows sign-in it starts in the tray and starts the saved screen and theme, like **Show on display**.

If the saved screen appears late, startup waits and checks every ten seconds. It never chooses a different screen or theme. **Stop** cancels the wait or current output for this run. To stop future Windows startup too, disable startup below.

The device list refreshes every ten seconds without sending panel commands. A missing screen closes its session, including when the picture is still. Reconnect it and press **Show on display** again. **Stop** closes the session too. A preview error alone does not stop USB output.

To turn off startup, run `.\Install-VibePanel.ps1 -DisableStartup`.

To uninstall, first quit from the tray menu. Use Windows **Installed apps** or run `.\Uninstall-VibePanel.ps1`. App files move to a backup. Your settings, user themes and Codex plugin stay in place.

From a command line, `VibePanel.exe --start-minimized` starts the preview in the tray. `VibePanel.exe --restore-last-session` starts in the tray and starts the saved screen and theme. Windows startup uses both flags.

## Make it yours

**Screens on display** saves which screens may appear. Each keeps its own chosen theme. Auto-cycle switches between enabled screens every twenty seconds. With only one enabled screen, it cycles that screen's themes.

You can preview a disabled screen. Codex is off by default on a fresh setup and is skipped on the display when its process is closed. If no enabled screen is available, the active display receives a black frame.

**System** shows available CPU, GPU and physical RAM readings. Missing details hide themselves. There is no swap display. The Windows x64 reader uses the original Open Hardware Monitor source rebuilt for .NET 10; it may manage its WinRing0 driver and some readings may require administrator rights. Windows ARM64 sensors are unavailable. Hardware coverage still needs a live check. `vp sensors --diagnose` requests one sensor read and prints local initialization details, without audio or USB.

- **Font**: pick a style of letters. **Custom font** lets you type a name.
- **Saturation**: more or less color.
- **Hue**: change the colors.
- **Brightness**: lighter or darker.
- **Contrast**: stronger or softer light and dark.
- **Reset**: put the four color sliders back.
- **Spectrum**: show or hide the sound bars. VU meters stay visible.

Your choices are saved. On short windows, open **Colors +** to see the sliders.

Start with **1 s** updates and **High** quality. **Poor** makes rougher, smaller pictures. **25 ms** asks for very fast updates; your PC or screen may be slower.

For Codex, press **Prepare Codex plugin**. Then finish installation and review its hooks inside Codex. [Help](https://github.com/umbertotechnopreneur/VibePanel/blob/main/plugins/vibepanel-codex/README.md). The bundled files are under `engine/plugins/vibepanel-codex`.

Windows only for now. Windows needs WebView2. No music login is needed. The app does not change volume or record sound.

**Themes run code. For now, only use themes from the [official repo](https://github.com/umbertotechnopreneur/VibePanel).**

Hobby project. No warranty. [My to-do list](cave.md) · [Make a theme](docs/theme-packs.md) · [Build](docs/build.md).
