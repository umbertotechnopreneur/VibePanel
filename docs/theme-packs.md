# Make a theme

A theme is a small Python file that draws a screen. I include 10 music themes and 10 Codex themes. A music pack does not need Codex.

Start by copying a theme from `themes/media` or `themes/codex`. Give it a new ID and name in `theme.json`.

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

```powershell
vp fonts
vp themes
vp theme media.aurora --export new-picture.jpg --saturation 120
```

Export makes a new picture on the PC. It does not open USB. Only use `--device N` when you want to send pictures to a screen.

**Themes run code on your PC. For now, only get them from the official repo.**

[Full drawing API](https://github.com/umbertotechnopreneur/VibePanel/blob/codex/usb-transport/theme-runtime/README.md) · [Engine commands](engine-protocol.md). In the ZIP, the full API guide is in `engine/theme-runtime/README.md`.
