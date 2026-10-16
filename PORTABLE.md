# Vibe Panel portable preview

Extract the whole ZIP and run `VibePanel.exe`, or `engine/vp.exe help` in a terminal. Keep the folder together: `engine`, `wwwroot`, Python, themes and their dependencies are not optional loose files.

## What's included

The Windows x64/ARM64 package includes .NET and an isolated Python runtime with pinned Skia/NumPy dependencies. It does not change the machine's Python installation. Windows WebView2 Evergreen is required and is not auto-installed.

No installer, drivers, services, startup entries or monitoring tools are added. The viewer never starts USB output. Physical commands remain explicit CLI operations.

## Theme studio

Choose **Multimedia** or **OpenAI Codex**, then one of ten themes in that category. Previous/next or Page Up/Page Down navigate themes; optional cycling changes the selection every 20 seconds. Auto appearance uses local time: light 07:00–19:00, dark otherwise. Manual light/dark, category, selected theme, geometry, font and text size are saved.

The preview requests a frame once a second, with a short fade when switching themes. Fonts use installed families with a monospace fallback. Source themes are editable under `engine/themes`; the shared canvas and worker live under `engine/theme-runtime`. Add personal packs under `%LOCALAPPDATA%\VibePanel\themes`, then click Reload. See [theme pack structure](docs/theme-packs.md).

The screen canvas is offscreen Python/Skia. OpenSilver/Photino uses a webview for the **desktop configuration UI**, not for rendering the panel themes.

## Data and optional plugins

Media comes from generic Windows APIs. There is no Spotify login/API dependency. Audio volume and peak meters are read-only; no microphone or audio is recorded. Unavailable quality, shuffle or volume is not fabricated.

Codex themes can be selected without installing Codex. They show a waiting state until the optional plugin supplies local events. Preparing and trusting the plugin is a separate opt-in action; it is not enabled by launching Vibe Panel. Its preparation helper currently uses system Python; the bundled renderer runtime does not automatically register Codex hooks. See [plugin instructions](plugins/vibepanel-codex/README.md).

Settings and derived-color caches stay under `%LOCALAPPDATA%\VibePanel`. Portable means no installer, not zero local state. Themes are executable Python: only load code you trust. Removing a pack does not uninstall its data provider.

## Compatibility and evidence

The native engine currently supports Windows. Linux/macOS media, audio and USB providers remain pending. A Linux UI build is not hardware compatibility.

Twenty wide layouts are individually composed for 1280 × 480. Square/narrow profiles use a shared compact fallback with each theme's visual motif. Build, local JPEG rendering and physical panel validation are reported separately in [cave.md](cave.md).

I maintain this as a hobby project, provided as is without warranty. Keep `LICENSE`, `THIRD_PARTY_NOTICES.md` and `licenses` with redistributed copies.
