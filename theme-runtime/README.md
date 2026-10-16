# Vibe Panel Python themes

I use a persistent Python worker and Skia's offscreen raster canvas. The engine sends a snapshot once per requested update; a theme draws it and returns one JPEG. No browser, HTTP server, hardware write, media control, or audio recording is part of this renderer.

The two independent packs in `themes/media` and `themes/codex` contain ten themes each. A pack is a folder with `pack.json`; each theme has its own `theme.json` and `render.py`. A multimedia pack does not need Codex or its hooks. Categories come from the manifest's `category`, not translated folder names. Installing a theme never installs or enables a data provider.

## Runtime

Use CPython 3.11–3.14 with the exact package versions in `requirements.txt`. The desktop host locates the configured Python and starts `worker.py` with `-u`. A portable distribution can bundle that interpreter and the native wheels for its own platform. The dependency versions are pinned; this file is not a wheel-hash lock.

The upstream [Skia package](https://pypi.org/project/skia-python/144.0.post2/) publishes Windows x64/ARM64, Linux x64/ARM64 and macOS x64/ARM64 wheels. Linux also needs the native OpenGL/EGL/fontconfig libraries listed upstream, even though this worker uses a CPU raster surface. Those renderer packages do not implement Linux/macOS media or USB backends.

## Protocol 1

One UTF-8 JSON object per line, request and response IDs preserved:

```json
{"version":1,"id":"frame-1","method":"render","themePath":"/themes/media/themes/aurora","width":1280,"height":480,"light":false,"fontFamily":"Cascadia Mono","fontScale":1.0,"ambientDrift":false,"state":{"media":{"title":"","artist":"","album":"","source":"","playing":false,"positionSeconds":0,"durationSeconds":0,"coverKey":"","coverBase64":null},"audio":{"volumePercent":null,"peak":null,"sessions":[]},"clock":{"iso":"2026-10-16T21:42:00+07:00","locale":"en-US"},"desktop":{"palette":["#6677CC","#A575BC","#4AABB1","#C8A48C"]},"codex":{"status":"Waiting for Codex","isLive":false,"events":[]}}}
```

```json
{"version":1,"id":"frame-1","ok":true,"jpegBase64":"..."}
```

Errors are `{ "version": 1, "id": "frame-1", "ok": false, "error": "..." }`. All diagnostics and accidental theme prints go to stderr. A missing Python dependency is reported for each request rather than corrupting stdout. A malformed or unsupported theme produces an error, never a fabricated scene.

`coverKey` stays in each media snapshot. Send `coverBase64` when that key changes or when the worker starts. Omit it on later frames to reuse the cached image; explicitly send `null` to clear it. Media metadata, elapsed position, volume and peak are actual provider values. `null` is unavailable, not zero. Audio peak is an instantaneous linear amplitude, not spectrum data.

Codex accepts `chatTitle`, `status`, `event`, `phase`, `tool`, `model`, `isLive`, `stale`, `atUtc`, and `events`. Missing events show a waiting state. A permission prompt highlights the screen only when the actual latest event is `PermissionRequest`, is live and is not stale. No theme sends approvals. History uses only received events. Set `state.preview` for explicitly illustrative snapshots; the frame identifies demo data.

## Drawing a theme

```python
def render(canvas, context):
    if canvas.compact:
        canvas.compact_media("aurora")
        return
    canvas.backdrop("aurora", artwork=True)
    canvas.header("MEDIA")
    canvas.cover(48, 80, 280, radius=18)
    canvas.media_text(376, 173, 820, 48)
    canvas.progress(48, 401, 1180, 7)
    canvas.vu(930, 428, 225, 16)
    canvas.footer()
```

Each script owns its composition. `Canvas` supplies text with width-aware ellipsis, rectangles, gradients, polygons, lines, rings, artwork clipping, colored shadows, cached blurred backgrounds, progress, read-only VU and clock. Its `native` attribute gives access to the underlying Skia canvas when a theme needs more. `context.theme_directory` identifies the theme folder for its own bundled assets.

All twenty scripts have separate wide compositions. At aspect ratios below 2:1 they explicitly switch to a shared 480×480 compact composition, preserving each theme's motif. Other aspect ratios are centered with uniform scaling and background fill; artwork and typography are never stretched. The JPEG always has exactly the requested physical dimensions. This fallback is intentionally documented rather than claiming twenty individually designed square compositions.

The 16-pixel artwork shadow uses the average of the most frequent quantized color cluster. Four quadrant averages supply the background palette. Both are calculated once when artwork changes using a 16×16 native resize and NumPy. Backgrounds, curves, glow and shadows are cached; optional drift moves an existing layer at one logical pixel per second in a ping-pong motion. The worker retains up to four decoded covers, twelve static layers and ninety-six fonts. It never loops over frame pixels in Python.

Fonts use the selected installed family with the platform monospace fallback. A pack does not ship proprietary fonts or artwork. Light/dark and font scale 0.65–1.6 apply to every theme. The helper reserves fixed slots and truncates long labels; elaborate scripts can use other layouts or assets. A read-only shuffle label appears only when the provider supplies a non-null boolean value; quality is omitted while unavailable.

JPEG quality is reduced adaptively to stay at or below 450,000 bytes without changing physical resolution. If even the lowest quality exceeds the cap, the worker reports it. This encoding limit does not guarantee a refresh rate or validate any USB device.

## Sources and validation boundary

The code is original MIT source based on the owner's generated visual concepts. Package/API reference: [Skia Python](https://github.com/kyamagu/skia-python), [NumPy 2.4.3](https://pypi.org/project/numpy/2.4.3/), and [pybind11 3.0.2](https://pypi.org/project/pybind11/3.0.2/). No generated proposal image or external album artwork is bundled.

Rendering and physical-device trials require the owner's separate approval under the repository rules. Source implementation is not a claim of visual or hardware validation.
