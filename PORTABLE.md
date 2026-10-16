# Use Vibe Panel

1. Unzip the file. Keep its files together.
2. Open `VibePanel.exe`.
3. Choose **Multimedia** for music or **OpenAI Codex** for Codex.
4. Choose a theme. Look at the preview.
5. Choose your USB screen. Press **Show on display**. Press **Stop** to stop.

## Start when you sign in

From the extracted package, open PowerShell in its folder and run:

```powershell
.\Install-VibePanel.ps1
```

It installs that exact package for the current user in `%LOCALAPPDATA%\Umberto Giacobbi.biz\Apps\VibePanel` and creates one managed Windows Startup shortcut. At the next sign-in it opens the VibePanel studio. It does **not** connect to a USB screen or send content until you choose a screen and press **Show on display**.

To remove only the automatic startup, run `./Install-VibePanel.ps1 -DisableStartup` from the same package. The installed files are kept so you can still open VibePanel manually.

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

For Codex, press **Prepare Codex plugin**. Then finish installation and review its hooks inside Codex. [Help](plugins/vibepanel-codex/README.md).

Windows only for now. Windows needs WebView2. No music login is needed. The app does not change volume or record sound.

**Themes run code. For now, only use themes from the [official repo](https://github.com/umbertotechnopreneur/VibePanel).**

Hobby project. No warranty. [My to-do list](cave.md) · [Make a theme](docs/theme-packs.md) · [Build](docs/build.md).
