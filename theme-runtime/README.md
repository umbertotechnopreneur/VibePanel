# Vibe Panel Python themes

I use a persistent Python worker and Skia's offscreen raster canvas. The engine sends a snapshot once per requested update; a theme draws it and returns one JPEG. No browser, HTTP server, hardware write, media control, or audio recording is part of this renderer.

The two independent packs in `themes/media` and `themes/codex` contain ten themes each. A pack is a folder with `pack.json`; each theme has its own `theme.json` and `render.py`. A multimedia pack does not need Codex or its hooks. Categories come from the manifest's `category`, not translated folder names. Installing a theme never installs or enables a data provider.

## Runtime

Use CPython 3.11–3.14 with the exact package versions in `requirements.txt`. The desktop host locates the configured Python and starts `worker.py` with `-u`. A portable distribution can bundle that interpreter and the native wheels for its own platform. The dependency versions are pinned; this file is not a wheel-hash lock.

The upstream [Skia package](https://pypi.org/project/skia-python/144.0.post2/) publishes Windows x64/ARM64, Linux x64/ARM64 and macOS x64/ARM64 wheels. Linux also needs the native OpenGL/EGL/fontconfig libraries listed upstream, even though this worker uses a CPU raster surface. Those renderer packages do not implement Linux/macOS media or USB backends.

## Protocol 1

One UTF-8 JSON object per line, request and response IDs preserved:

```json
{"version":1,"id":"frame-1","method":"render","themePath":"/themes/media/themes/aurora","width":1280,"height":480,"light":false,"fontFamily":"Cascadia Mono","fontScale":1.0,"ambientDrift":false,"jpegQuality":"high","state":{"media":{"title":"","artist":"","album":"","source":"","playing":false,"positionSeconds":0,"durationSeconds":0,"coverKey":"","coverBase64":null},"audio":{"volumePercent":null,"peak":null,"sessions":[]},"clock":{"iso":"2026-10-16T21:42:00+07:00","locale":"en-US"},"desktop":{"palette":["#6677CC","#A575BC","#4AABB1","#C8A48C"]},"codex":{"status":"Waiting for Codex","isLive":false,"events":[]}}}
```

```json
{"version":1,"id":"frame-1","ok":true,"jpegBase64":"...","accentColor":"#88AACC"}
```

Errors are `{ "version": 1, "id": "frame-1", "ok": false, "error": "..." }`. All diagnostics and accidental theme prints go to stderr. A missing Python dependency is reported for each request rather than corrupting stdout. A malformed or unsupported theme produces an error, never a fabricated scene.

Render requests also accept `saturationPercent` (0–200, default 100), `hueDegrees` (-180–180, default 0), `brightnessPercent` (-100–100, default 0), `contrastPercent` (0–200, default 100) and `showSpectrum` (boolean, default true). Numeric settings must be finite. The four color settings affect the completed frame before JPEG encoding; defaults 100/0/0/100 return the original snapshot without an extra raster pass. Other values use one native Skia matrix pass. Grays stay neutral, alpha is unchanged, and out-of-gamut RGB is clamped. Contrast pivots at 50% gray; brightness is an additive offset after contrast, not a hardware backlight command. In normalized channel units the tone step is `rgb * (contrast / 100) + 0.5 * (1 - contrast / 100) + brightness / 100`.

Filter coefficients are cached for up to 64 setting combinations; Python never loops over frame pixels. Saturation and hue follow the [W3C matrices](https://www.w3.org/TR/filter-effects-1/#feColorMatrixElement), applied through [Skia ColorFilters](https://skia-python.github.io/skia-python/reference/skia.ColorFilters.html). The [native matrix filter](https://github.com/google/skia/blob/main/src/effects/colorfilters/SkMatrixColorFilter.cpp) applies a float matrix and then clamps to 0–1, so its bias is not multiplied by 255.

### Color settings in a theme

`context.colors` is a fresh, mutable `ColorAdjustments` object for each frame, initialized from the UI/CLI request. A theme can read or override any of its four fields inside `render`; untouched fields retain the requested values. The worker reads the final values after drawing and validates them again before applying the filter. Invalid types, non-finite numbers or out-of-range overrides return a render error rather than being silently clamped.

```python
def render(c, s):
    requested_saturation = s.colors.saturation_percent  # Read the UI/CLI value.
    s.colors.saturation_percent = min(200, requested_saturation + 10)
    s.colors.hue_degrees = -12
    s.colors.brightness_percent = 6
    s.colors.contrast_percent = 108
    c.backdrop("aurora", artwork=True)
    c.header("MY MEDIA")
    # Draw the rest of the theme as usual. The worker applies colors at the end.
```

`ColorAdjustments(...).validate()` is also available from `color_adjustments`; it validates all four fields before normalizing them to floats and returns the same object. Do not save `s.colors` in module globals or mutate the request. These overrides apply only to that rendered frame: they do not change saved UI settings, shared backdrop caches or the next frame. The twenty bundled themes currently leave all four values untouched.

`s.color_filters_enabled` follows the studio's **Color filters** switch. Off skips the final color pass, including theme overrides and fades. The studio saves the values and starts with filters off.

For a fade, call `s.colors.fade(amount)` after the other color overrides. Use 0 for black and 1 for the current correction. Intermediate values reduce contrast and brightness together, preserving hue, saturation and alpha. The amount must be a finite number from 0 to 1. The final filter still uses one native pass; this is a fade through black, not a blend with the previous theme.

```python
def render(c, s):
    c.backdrop("aurora", artwork=True)
    c.header("MY MEDIA")
    # Draw the rest of the theme here.
    progress = min(1, s.elapsed_seconds / 0.5)
    s.colors.fade(progress)  # Half-second fade in. Use 1 - progress for fade out.
```

`s.elapsed_seconds` uses the worker's monotonic clock, starting at the first frame of the active theme. It resets when category, theme ID or theme folder changes. It does not depend on the wall clock or requested refresh rate. Only the current theme's timer is kept; a new worker starts it again. The time advances between requests too. No bundled theme applies this fade automatically.

`showSpectrum: false` omits only FFT drawing, including its labels and unavailable placeholder. It does not hide stereo VU or dBFS values and does not stop audio analysis. All ten Multimedia manifests declare this optional setting because their compact composition includes a spectrum; the ten Codex manifests do not. Existing positions and sizes stay unchanged when the spectrum is hidden.

The worker's `fonts` method returns `{ "version": 1, "id": "fonts-1", "ok": true, "families": [], "notice": null }`, with the array populated from the renderer's installed-font backend. It uses [FontMgr and native style sets](https://skia-python.github.io/skia-python/reference/skia.FontMgr.html), accepting only families whose normal and bold matches report [isFixedPitch](https://skia-python.github.io/skia-python/reference/skia.Typeface.html#skia.Typeface.isFixedPitch). Names are not guessed from “Mono,” and no fonts are installed or directories scanned. Discovery is cached once per worker: restart it after installing fonts. At most 4,096 families are inspected, 512 names returned, and 32 backend errors tolerated; limits, missing fonts and partial results have an explicit notice. Names are sorted, case-insensitively deduplicated and limited to 128 printable characters. Fixed-pitch metadata is not a guarantee that every glyph has identical advance width.

`coverKey` stays in each media snapshot. Send `coverBase64` when that key changes or when the worker starts. Omit it on later frames to reuse the cached image; explicitly send `null` to clear it. Media metadata, elapsed position, volume and peak are actual provider values. `null` is unavailable, not zero. `audio.volumePercent` is the media application's volume setting, labeled **APP VOLUME**, not the level of the complete output mix. The legacy `audio.peak` is an instantaneous app-session linear amplitude, not spectrum data.

### Output spectrum and stereo meters

The optional `state.audio.analysis` object supplies measured playback analysis. The renderer does not open an audio device, capture a microphone or create an FFT from a volume percentage. The current Windows provider uses output loopback; other platforms may leave this object unavailable.

```json
{
  "available": false,
  "source": "system-output",
  "sampleRate": null,
  "fftSize": null,
  "channels": null,
  "bandDbfs": [],
  "frequenciesHz": [],
  "leftPeakDbfs": null,
  "rightPeakDbfs": null,
  "leftRmsDbfs": null,
  "rightRmsDbfs": null,
  "leftPeakHoldDbfs": null,
  "rightPeakHoldDbfs": null,
  "updatedAtUtc": null,
  "status": "Audio analysis unavailable"
}
```

When available, the frequency and amplitude arrays contain exactly 32 finite values with increasing positive frequency centers. Bands are measured amplitude dBFS, not power percentages. The native provider supplies 4096-point Hann-window FFT analysis, the strongest measured bin in each log-frequency band, and channel-power averaging. Its bands cover 20 Hz through the lesser of 20 kHz and Nyquist, with a -90 dBFS floor. `updatedAtUtc` identifies an actual analysis, not a UI refresh. The provider reports stale capture explicitly and supplies floor values instead of invented movement.

The canvas labels this source **SYSTEM OUTPUT**. Spectrum bars use the actual log-frequency centers and a -60–0 dBFS visible scale, with artwork-colored gradients. Stereo strips use RMS fill, a peak line and a separate native peak-hold marker; numerical RMS / PK / HOLD readouts remain the raw measurements. Mono leaves R unavailable. With more than two channels the VU identifies FRONT L/R while the spectrum represents the overall output mix. A missing or invalid spectrum draws no bars. Missing levels remain em dashes, not zero.

Only visual ballistics are applied: elapsed-time exponential attack/release follows received data. Spectrum uses 25/320 ms, RMS 55/360 ms and peak 8/450 ms; native peak-hold values are not synthesized. The bounded history retains at most four format keys and resets on missing input or gaps of two seconds. These time constants are display styling, not measured capture latency or a promise of the requested refresh rate.

Codex accepts `chatTitle`, `status`, `event`, `phase`, `tool`, `model`, `isLive`, `stale`, `atUtc`, and `events`. Missing events show a waiting state. A permission prompt highlights the screen amber only when the actual latest event is `PermissionRequest`, is live and is not stale. A received `Stop` event has a green ready signal. Stale data, interruption and session closure are shown explicitly. No theme sends approvals. History uses only received events; empty Flow slots say “No event yet.” Set `state.preview` for illustrative snapshots; the footer identifies demo data while keeping the semantic status visible.

## Drawing a theme

```python
def render(canvas, context):
    if canvas.compact:
        canvas.compact_media("aurora")
        return
    canvas.backdrop("aurora", artwork=True)
    canvas.header("MEDIA")
    canvas.cover(48, 80, 280, radius=18)
    canvas.media_text(376, 123, 620, 38)
    canvas.playback_indicators(1060, 80, 38)
    canvas.spectrum(376, 255, 450, 145)
    canvas.stereo_vu(860, 255, 350, 145)
    canvas.app_volume(48, 399, 260)
    canvas.progress(48, 431, 1180, 5, labels=False)
    canvas.footer()
```

Each script owns its composition. `Canvas` supplies text with width-aware ellipsis, rectangles, gradients, polygons, lines, rings, artwork clipping, colored shadows, cached blurred backgrounds, progress and clock. `spectrum(x, y, width, height)` and `stereo_vu(x, y, width, height)` draw the native system-output measurements. `app_volume(...)` keeps the app's gain distinct. `playback_indicators(...)` draws large play/pause and shuffle glyphs from vectors; these are read-only states, not buttons. Its `native` attribute gives access to the underlying Skia canvas when a theme needs more. `context.theme_directory` identifies the theme folder for its own bundled assets.

Orbit, Signal, Halo, Aurora, Cinematic and Ribbon show spectrum plus stereo VU. Editorial and Nocturne use the stereo strips; Prism and Duotone use the spectrum. All ten include playback/shuffle indicators and app volume. The shared compact layout includes both analysis views, bounded to the same fixed composition.

All twenty scripts have separate wide compositions. At aspect ratios below 2:1 they explicitly switch to a shared 480×480 compact composition, preserving each theme's motif. Other aspect ratios are centered with uniform scaling and background fill; artwork and typography are never stretched. The JPEG always has exactly the requested physical dimensions. This fallback is intentionally documented rather than claiming twenty individually designed square compositions.

The 16-pixel artwork shadow uses the average of the most frequent quantized color cluster. Four quadrant averages supply the background palette. Both are calculated once when artwork changes using a 16×16 native resize and NumPy. Full-bleed artwork is aspect-filled at full opacity before a light/dark overlay; it is not stretched or hidden under multiple opaque layers. Aurora uses a broad blur, Cinematic keeps more image detail, and Orbit has a dominant-color glow. Only cached ambience is drawn at half resolution. Covers and type remain sharp at the output size.

Each Codex theme has its own color anchors, tinted 22% by the desktop samples: blue Beacon, peach Timeline, green Terminal, rose Focus, cyan Pulse, plum Approval, cream/coral/purple Flow, mint Quiet, petrol/copper Dispatch and warm Chronicle. Amber/green state glows are layered over those identities only when the corresponding real event is present.

Backgrounds, curves, glow and shadows are cached; optional drift moves an existing layer at one logical pixel per second in a ping-pong motion. The worker retains up to four decoded covers, twelve static layers and ninety-six fonts. It never loops over frame pixels in Python. Background rebuilds depend on artwork, theme, appearance, palette or the semantic status color, not on every clock tick.

Fonts use the selected installed family with the platform monospace fallback. A pack does not ship proprietary fonts or artwork. Light/dark and font scale 0.65–1.6 apply to every theme. Read-only shuffle is ON/OFF only for a provider boolean and an em dash when unknown. Playback shows a triangle while playing and pause bars only when optional `media.playbackStatus` explicitly says `Paused`. Stopped, closed, missing sessions and an unspecified non-playing state use a neutral symbol with the corresponding label. Source quality is omitted while unavailable.

### Readable small type

All twenty themes share three secondary type roles in logical canvas pixels:

| Role | Default | Range with font scale |
| --- | ---: | ---: |
| Instrument: FFT scales, dBFS, RMS/PK/HOLD, state captions | 16 | 16–18 |
| Caption: source, album, date, footer, event details | 18 | 16–22 |
| Body: supporting text | 20 | 18–26 |

`canvas.caption(value, x, y, tint=None, width=None, align="left")` uses the caption role. `canvas.text_size("instrument" | "caption" | "body")` supplies a compensated size for `canvas.text(...)`; it must not be multiplied by the user scale again. The floors protect small labels when the user reduces font scale, while caps reserve predictable vertical space. Titles keep their own separate size and cap instead of inheriting a global enlargement.

The spectrum reserves 41 pixels for its dBFS labels and 27 pixels below the plot. Short plots show 0/-60 only; taller plots add -30. Frequency ticks use fewer labels when needed, never smaller text. Narrow headings keep SYSTEM OUTPUT and omit optional sample-rate/FFT metadata. Stereo meters retain all three numeric columns at the instrument size, with separate RMS/PK/HOLD headings; short strips have been given more height. SYSTEM MIX in narrow VU panels means the same full playback mix, not app gain. The compact layout keeps these same readable roles. Long custom-font labels can still use ellipsis; exact appearance and physical readability require a separately authorized visual check.

`jpegQuality` accepts `poor`, `low`, `medium` or `high`, with initial JPEG qualities 20, 50, 75 and 90. Omitting it selects `high`; unsupported values are rejected. Quality is reduced adaptively from the selected target, down to 8 if needed, to stay at or below 450,000 bytes without changing physical resolution. Fallback never raises the selected quality. If even the lowest quality exceeds the cap, the worker reports it. This controls JPEG compression, not the audio source quality, and does not guarantee a refresh rate or validate any USB device.

## Sources and validation boundary

The code is original MIT source based on the owner's generated visual concepts. Package/API reference: [Skia Python](https://github.com/kyamagu/skia-python), [NumPy 2.4.3](https://pypi.org/project/numpy/2.4.3/), and [pybind11 3.0.2](https://pypi.org/project/pybind11/3.0.2/). No generated proposal image or external album artwork is bundled.

Rendering and physical-device trials require the owner's separate approval under the repository rules. Source implementation is not a claim of visual or hardware validation.
