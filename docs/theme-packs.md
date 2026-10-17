# Make a theme

A theme is a small Python file that draws a screen. I include 10 Music themes, 10 Codex themes and 10 System themes. Music and System packs do not need Codex.

Start by copying a theme from `themes/media`, `themes/codex` or `themes/system`. Give it a new ID and name in `theme.json`. Category IDs are `multimedia`, `codex` and `system`.

Keep this folder shape:

```text
my-pack/
  pack.json
  themes/
    my-theme/
      theme.json
      render.py
```

Put the pack in `%LOCALAPPDATA%\VibePanel\themes`. Press **Reload** in the app.

## Colors and fades

The theme starts with the colors chosen in the app. It can change them:

Turn on **Color filters** to see these changes. Turning them off keeps the values but skips filters and fades.

```python
def render(c, s):
    c.backdrop("aurora", artwork=True)
    c.header("MY MUSIC")
    c.media_text(60, 150, 1100)

    s.colors.saturation_percent = 120  # More color.
    s.colors.hue_degrees = 0          # Same hues.
    s.colors.brightness_percent = 5   # A little lighter.
    s.colors.contrast_percent = 110   # Stronger contrast.
    s.colors.fade(min(1, s.elapsed_seconds / 0.5))  # Fade in.
```

For fade out, use `1 - min(1, s.elapsed_seconds / 0.5)`. Changes affect this frame. They do not change saved settings.

Saturation and contrast: 0–200. Hue: -180–180. Brightness: -100–100. Fade: 0 is black; 1 is fully visible.

Use `s.show_spectrum` to respect the sound-bars checkbox. `c.spectrum(...)` already does this. Declare `settings.showSpectrum` as `{ "type": "boolean", "default": true }` in the manifest to enable the checkbox.

## CLI

System themes receive CPU, GPU and physical RAM in `s.sensors`. Hide a missing or null field, but keep a measured zero. Hide an empty component and reuse its space. There is no swap field. The shared `hardware.py` helper implements these rules for the ten included System themes. [Snapshot format](../theme-runtime/README.md#system-snapshots).

```powershell
vp fonts
vp themes
vp theme media.aurora --export new-picture.jpg --saturation 120
```

Export makes a new picture on the PC. It does not open USB. Only use `--device N` when you want to send pictures to a screen.

**Themes run code on your PC. For now, only get them from the official repo.**

## Theme palette

Every theme receives palette v1: `s.palette.textPrimary`, `textSecondary`,
`accent`, `warning` and `info`. Existing numeric series such as `s.palette[0]`
still work. `s.fg`, `s.muted` and `s.accent` use the resolved semantic colors.
Use `s.palette.to_json()` for the five named values.

Optional manifest defaults use `paletteVersion: 1` and `palette`:

```json
{
  "paletteVersion": 1,
  "palette": {
    "accent": "#C9AEF3",
    "dark": { "textPrimary": "#F5F5FC", "warning": "#FFC16E" },
    "light": { "textPrimary": "#182133", "info": "#245C91" }
  }
}
```

Colors must be `#RRGGBB`. Omitted or null values inherit the defaults.
The order is shared defaults, manifest defaults, light/dark defaults, then
personal overrides. In the gallery, **Theme colors** edits the selected theme
and appearance. Reset removes that override. Installed theme files stay intact.
Palette colors are drawing inputs; the separate Color filters switch controls
the final whole-image saturation, hue, brightness and contrast pass.

[Full drawing API](https://github.com/umbertotechnopreneur/VibePanel/blob/codex/usb-transport/theme-runtime/README.md) · [Engine commands](engine-protocol.md). In the ZIP, the full API guide is in `engine/theme-runtime/README.md`.
