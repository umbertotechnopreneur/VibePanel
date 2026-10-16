# Local engine protocol

`vp --stdio` speaks JSON Lines v1 over redirected standard input/output. There is no TCP listener, HTTP service, shell-command endpoint, or automatic USB startup. Stdout contains one response per request; errors outside the protocol go to stderr.

```json
{"version":1,"id":"1","method":"hello"}
{"version":1,"id":"2","method":"state","parameters":{"profileId":"wide-1280x480","scene":"Music","includeFrame":true}}
{"version":1,"id":"3","method":"levels"}
{"version":1,"id":"4","method":"themes"}
{"version":1,"id":"5","method":"state","parameters":{"themeId":"media.aurora","profileId":"wide-1280x480","fontFamily":"Cascadia Mono","fontScale":1,"light":false}}
```

Responses have `version`, matching `id`, `ok`, `result`, and `error`. Binary preview JPEGs are base64 JSON values. Request lines are limited to 16 KiB; the desktop client bounds responses at 2 million characters. Unknown protocol versions and methods are rejected. JSON is UTF-8; the human CLI banner is disabled in this mode.

`state` reads local media and audio levels without touching USB. Legacy scene previews also sample metrics. A request with `themeId` does not start hardware sensor sampling. Multimedia themes read media/audio, while Codex themes skip both providers and use only the desktop palette, clock and optional local hook activity. Theme frames and data refresh at most once per second for an unchanged configuration. Set `includeDevices: true` to enumerate panel interfaces deliberately (cached for five seconds); this still does not send a handshake or picture. Its opaque device IDs are local hashes, not USB paths. `levels` samples cached playback-session meters; it never opens a microphone.

Media position advances from the Windows timeline's `LastUpdatedTime` only while playing and when a positive playback rate is available. It is bounded by the reported track duration; unavailable timing is not inferred from a service-specific API.

Preview parameters also accept `light` and `ambientDrift`, both false by default. They affect the local raster only. The viewer resolves automatic day/night mode using local PC time; it never opens USB to change themes.

`themes` refreshes the filesystem catalog and returns `categories`, `themes`, and `notices`. Stable categories are `multimedia` (Multimedia) and `codex` (OpenAI Codex). A theme has `id`, `name`, `category`, `description`, and `packId`. Themes are read from `themes/<pack>/themes/<theme>/theme.json` beside the engine (or the repository when developing), followed by `%LOCALAPPDATA%/VibePanel/themes`. Duplicate IDs keep the first definition and produce a notice. Listing never executes Python. There are limits of 256 themes and 64 KiB per manifest.

Set `themeId` on `state` to render that theme instead of a legacy `scene`. `fontFamily` defaults to `Cascadia Mono`; `fontScale` is 0.65–1.6, default 1. Python/Skia uses a persistent worker with a 12-second deadline; an error is returned in `notices`, and the last valid frame for exactly that theme/font/size/color configuration may remain visible. Configuration changes clear the previous frame. Failed rendering retries no faster than every five seconds unless the configuration changes. The existing RPC physical `start` command still uses legacy scenes; theme output has a separate explicit CLI command.

Python is resolved from `VIBEPANEL_PYTHON`, then bundled `python/python.exe`, then `python` on PATH (`python3` on Unix). It requires Python 3.11+ with the dependency declared in `theme-runtime/requirements.txt`; the engine never installs it. Cover bytes are sent only when their hash changes or a worker restarts. Wallpaper colors are cached, with the wallpaper identity checked every 30 seconds. The Codex page reads the optional `%LOCALAPPDATA%/VibePanel/codex/current.json` hook state. Absent activity shows a waiting state; data older than five minutes is marked non-live. The renderer keeps the latest 12 distinct observed events in memory for timeline themes; it cannot recover events between polls or before this process started. No chat titles are guessed or transcripts read.

The same catalog is available with `vp themes`. `vp theme <id> --export <new.jpg> [--font <family>] [--font-scale 1] [--light]` renders one real-data preview to a new file. Export never starts USB and never overwrites an existing file.

`vp theme <id> --device <number> [--profile auto] [--seconds N] [--font <family>] [--font-scale 1] [--light]` explicitly probes and streams to the device chosen from `vp discover`. It uses the detected geometry by default, targets one frame per second, and stops on Ctrl+C, the optional duration (1–86400 seconds), or a failure. It never reconnects to another device. `--device` and `--export` are mutually exclusive. Progress reports frame submission, not confirmation of physical pixels; actual frame rate depends on rendering and USB transfer time.

Only `start` (`deviceId`, `profileId`, optional fixed `scene`) starts a physical display session. An absent scene selects the five-screen rotation. `stop` cancels that session. EOF also stops it; a removed device is never silently replaced. A successful start response means the request was accepted, not that an image was physically observed.

`media` accepts an `action`: `toggle`, `play`, `pause`, `next`, or `previous`. There is deliberately no volume-write method.

The OpenSilver presentation depends on the portable Contracts project. The current native engine is Windows-only; replacing the UI does not port the HID/audio/media providers to another OS. No hook installation or Codex configuration changes are performed by this protocol.
