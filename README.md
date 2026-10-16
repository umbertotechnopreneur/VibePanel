# Vibe Panel

My music. My workspace. My screen.

I'm [Umberto Giacobbi](https://umbertogiacobbi.biz). I wasn't happy with TRCC, the software that came with my little Thermalright display. So, as usual, I decided to make my own.

I started **Vibe Panel** as a week-long holiday hobby project. I build it with AI assistance, make the decisions, and keep the source open. It is an early preview, not a commercial promise: **no warranty, no guaranteed support, and no claim that every panel works**.

## What's here

- A simple **OpenSilver Modern + Photino** theme viewer: category, theme, preview, font and size. Light, dark or automatic day/night appearance.
- **10 Multimedia themes** and **10 OpenAI Codex themes**, discovered from folders. Each theme is Python drawing on an offscreen Skia canvas; the renderer does not use a browser.
- Generic Windows media sessions: title, artist, cover, position and available shuffle state. **No Spotify API, account or direct integration.** Volume and VU are read-only.
- Artwork-colored blur and a tinted 16-pixel cover shadow; wallpaper-derived palettes, cached backgrounds, and an optional slow drift.
- A separate .NET 10 engine and `vp` CLI, connected to the viewer through stdin/stdout. No local HTTP server, telemetry, startup service or sensor installation.
- An optional local Codex hook plugin. Theme packs do not install or enable it.

The viewer requests updates once a second. That is an update policy, **not a measured CPU or physical refresh-rate guarantee**. Missing data stays unavailable. I am keeping hardware monitoring secondary.

## Try it

Build the portable ZIP, extract the whole folder, then run **`VibePanel.exe`**. The Windows package includes .NET, Python and the theme dependencies. WebView2 is required; I do not install it automatically.

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
./scripts/Publish.ps1 -Architecture x64
```

Output: `artifacts/packages/VibePanel-win-x64-preview.zip`. For Windows ARM64 use `-Architecture ARM64`. These are local preview packages, not a published release.

Inside the extracted folder:

```powershell
./engine/vp.exe help
./engine/vp.exe themes
./engine/vp.exe theme media.aurora --export my-frame.jpg
./engine/vp.exe theme media.editorial --export my-light-frame.jpg --light --font "Consolas" --font-scale 1.1
```

The viewer is **preview-only**. Sending a theme to hardware is a separate, explicit command: `vp theme media.aurora --device 1 --profile auto --seconds 20`. It probes and writes to the selected display; it is not a preview command.

See [portable setup](PORTABLE.md), [theme packs](docs/theme-packs.md), [the engine contract](docs/engine-protocol.md), and [the optional Codex plugin](plugins/vibepanel-codex/README.md).

## Honest limits

My earlier labelled JPEG chart worked on one Thermalright 1280 × 480 HID panel. That does not validate these new themes, every device, or Linux/macOS hardware support. Windows has the native providers today; a portable UI build is not cross-platform USB/media support.

I record current limitations in [cave.md](cave.md), decisions in [lesson.md](lesson.md), and protocol evidence in [the protocol notes](docs/protocol-notes.md). Compilation, image rendering and a physical-device trial are different kinds of evidence.

## Contributing and license

I welcome issues and pull requests. `main` is protected, and I keep final control over what lands. Read [AGENTS.md](AGENTS.md) before changing code. Tests and hardware trials need my approval before each execution.

I publish the source under the [MIT License](LICENSE), copyright © 2026 Umberto Giacobbi, **as is and without warranty**. Third-party dependencies keep their [own notices](THIRD_PARTY_NOTICES.md). Names and visual identity are covered separately in [TRADEMARKS.md](TRADEMARKS.md).

---

<p align="right">
  <a href="https://umbertogiacobbi.biz/vibeware/manifesto">
    <img src="src/VibePanel.App/Assets/vibeware-logo.png" alt="VibeWare floppy logo" width="120">
  </a><br>
  <strong>VibeWare</strong><br>
  <em>Human intent, AI execution, and plenty of tokens</em>
</p>

### This is VibeWare · [Manifesto](https://umbertogiacobbi.biz/vibeware/manifesto)

I participate in **VibeWare**, the open initiative I created. VibeWare is the initiative, not Vibe Panel's author or publisher.

I am **Umberto Giacobbi**. I define this project's purpose, guide its implementation, and remain responsible for checking its functionality, performance and security. The badge does not replace this project's validation evidence.

[Vibe Panel on GitHub](https://github.com/umbertotechnopreneur/VibePanel) · [VibeWare on GitHub](https://github.com/umbertotechnopreneur/VibeWare) · [My website](https://umbertogiacobbi.biz)

[Terms](https://umbertogiacobbi.biz/terms/) · [Privacy](https://umbertogiacobbi.biz/privacy/)
