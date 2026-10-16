# Vibe Panel theme packs

I keep the product name **Vibe Panel**; `VibePanel` is the repository/code identifier. Themes are Python scripts drawing onto an offscreen Skia canvas, not HTML pages.

## Categories and folders

The first categories are stable internal IDs: `multimedia` → Multimedia and `codex` → OpenAI Codex. Each theme declares its category in a manifest. Folder names are not translated category names.

A pack may contain one category or both. A Multimedia-only pack does not need Codex, and installing a theme never installs or enables a data provider.

```text
themes/
  my-pack/
    pack.json
    themes/
      my-theme/
        theme.json
        render.py
```

`pack.json`:

```json
{ "schema": 1, "id": "my-pack", "name": "My themes" }
```

`theme.json`:

```json
{
  "schema": 1,
  "id": "my-pack.media",
  "name": "My media screen",
  "category": "multimedia",
  "description": "My own layout.",
  "entry": "render.py",
  "author": "Your name",
  "license": "MIT"
}
```

Copy a pack into `%LOCALAPPDATA%\VibePanel\themes` and click **Reload**. In a portable build, bundled packs are in `engine/themes`; in the repository, `themes`. The engine sends the same catalog to UI and CLI. Listing manifests does not execute Python. Duplicate IDs are reported and the first definition wins; bundled packs precede personal packs. Give custom themes unique IDs.

There is no ZIP marketplace installer yet. The current install workflow is local-folder copy; no dependency or provider is installed behind it.

## Included themes

| Multimedia | OpenAI Codex |
| --- | --- |
| Aurora Glass | Beacon |
| Editorial | Timeline |
| Cinematic | Terminal |
| Signal | Focus |
| Orbit | Pulse |
| Prism | Approval |
| Ribbon | Flow |
| Nocturne | Quiet |
| Halo | Dispatch |
| Duotone | Chronicle |

These are twenty separate wide compositions. Narrow/square panels share a compact fallback with each theme's motif, without stretching typography or cover art.

## CLI and shared settings

```powershell
vp themes
vp gui
vp theme media.aurora --export frame.jpg
vp theme media.aurora --device 1 --interval-ms 250 --quality medium
vp theme codex.beacon --export activity.jpg --light --font "Consolas" --font-scale 1.1
```

Export never opens USB and refuses to overwrite a file. `--profile` selects geometry. `--font` and `--font-scale` affect the screen; the supported scale range is 0.65–1.6. The UI offers 70–150% in convenient 10% steps.

In the GUI, choose a USB display and press **Show on display**. Theme, font, quality and rate changes then reach the panel. **Stop** ends the session. Opening the window alone never starts USB output.

The update choices are 1 second, 500, 250, 100 and 50 ms. Low/Medium/High JPEG targets are 50/75/90, reduced if needed to fit the frame limit. Actual speed depends on rendering and USB; frames do not queue up.

In the CLI, only an explicit `--device N` starts physical playback, for example `vp theme media.aurora --device 1 --seconds 20`. It cannot be combined with export. Never use it as an automatic renderer check.

## Rendering contract

Each script defines `render(canvas, context)`. It receives generic `media`, `audio`, `clock`, `desktop` and `codex` snapshots. The engine owns native providers and USB. Themes do not query Spotify or control media volume.

A persistent worker keeps cover decoding, palette extraction, backgrounds and fonts cached. Artwork supplies the dominant-colored blur/shadow; wallpaper supplies four sampled colors. Light/dark and font settings are shared. Missing data stays missing; a VU peak is not a spectrum.

Full API, caching and JSONL details: [Python runtime source documentation](https://github.com/umbertotechnopreneur/VibePanel/blob/codex/usb-transport/theme-runtime/README.md), also included at `engine/theme-runtime/README.md` in the portable folder. The worker is not a Python sandbox: load only trusted themes.
