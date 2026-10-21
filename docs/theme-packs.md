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

Put the pack in `%LOCALAPPDATA%\ThermalrightVibeWare\themes`. Press **Reload** in the app.

## Pages

Declare pages and their interval in the manifest:

```json
"pages": [{"id":"overview","title":"At a glance"},{"id":"activity","title":"Live activity"}],
"pageSeconds": 8
```

Inside `render`, draw the page named by `s.page_id`. The worker also supplies `page_title`, `page_index`, `page_count`, `page_cycle` and `page_progress`. It owns one monotonic timer and a two-second crossfade. Page fades work with color filters off. A manifest may declare one to eight unique page IDs and an integer interval from 4 to 120 seconds. Omitting pages gives one static page.

The studio shows separate theme and page navigators. Choosing a page restarts that page's own interval, then normal manifest rotation continues from it. Manual page selection is kept only for the running studio session; changing theme starts its first available page.

An individual page can set `"seconds": 15` to override `pageSeconds`, using the same 4–120 second range. Skipped pages contribute no time to the cycle.

System themes default to eight seconds per page, skipping detail pages without readings. Thermal Atlas omits Power & cooling, starts with fifteen seconds of Live activity, then shows Temperature channels for twenty seconds. Its later pages use eight seconds each. All ten System themes add a core power/load heatmap when at least eight cores have both measurements. It averages SMT load by physical core and shows at most 32 cores. Power, voltage, current and aggregate clocks share one hardware page; RAM identity and SPD timing details share the memory page. Music themes use playback and an optional [artist page](artist-pages.md) at twelve seconds each. This page rotation is separate from the studio's twenty-second theme cycling.

## Color adjustments

The theme starts with the colors chosen in the app. It can change them:

Turn on **Color filters** to see these changes. Turning them off keeps the values but skips color adjustments, including fades through black. Page and track crossfades still work.

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

System themes receive CPU, GPU, motherboard and physical RAM in `s.sensors`. CPU/GPU/board `readings` retain sensor names, types and units. `fans` contain measured RPM; generic board channels keep their names. Preserve measured zeroes and omit absent fields. `system_dashboard.py` draws gauges, timestamped history, aggregate clocks, memory occupancy, module identities and named electrical/thermal/RPM channels. `system_details.py` groups values and omits individual CPU cores. Power channels keep separate names and are never added together. `hardware.py` retains at most 60 observed samples per series; repeated timestamps add nothing. There is no simulated history or swap field. [Snapshot format](../theme-runtime/README.md#system-snapshots).

```powershell
trv fonts
trv themes
trv theme media.aurora --export new-picture.jpg --saturation 120
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

## Progress colors

Every circular and linear progress indicator must move from the theme's accent
through amber to red as its value rises. This rule applies to all current and
future themes, in every category, layout and light/dark appearance.

The shared progress, ring and audio drawing methods already apply this scale.
For custom meters, use the same helper with the displayed value divided by its
maximum:

```python
from canvas import progress_color

tint = progress_color(s.accent, value / maximum, s.light)
```

Use a positive maximum and keep missing measurements absent. Colors follow the
displayed range; they are not hardware alarm thresholds. Decorative rings and
status symbols keep their own meaning.

[Full drawing API](https://github.com/umbertotechnopreneur/thermalright-vibeware/blob/codex/usb-transport/theme-runtime/README.md) · [Engine commands](engine-protocol.md). In the ZIP, the full API guide is in `engine/theme-runtime/README.md`.
