# Use Vibe Panel

1. Unzip the file. Keep its files together.
2. Open `VibePanel.exe`.
3. Choose **Multimedia** for music or **OpenAI Codex** for Codex.
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

Vibe Panel saves the selected screen and studio settings. At Windows sign-in it starts in the tray with USB output off. Press **Show on display** to start output each time.

The device list refreshes every ten seconds without sending panel commands. A missing screen closes its session, including when the picture is still. Reconnect it and press **Show on display** again. **Stop** closes the session too. A preview error alone does not stop USB output.

To turn off startup, run `.\Install-VibePanel.ps1 -DisableStartup`.

To uninstall, first quit from the tray menu. Use Windows **Installed apps** or run `.\Uninstall-VibePanel.ps1`. App files move to a backup. Your settings, user themes and Codex plugin stay in place.

From a command line, `VibePanel.exe --start-minimized` starts it in the tray. The old `--restore-last-session` flag now also starts in the tray with USB output off.

## Make it yours

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
