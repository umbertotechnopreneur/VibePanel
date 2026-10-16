# Local engine protocol

The desktop window starts one hidden `vp --stdio` process. Requests and replies are UTF-8 JSON Lines, version 1. There is no server port. Stdout contains replies only.

## Choose a theme and display

```json
{"version":1,"id":"1","method":"themes"}
{"version":1,"id":"2","method":"devices"}
{"version":1,"id":"3","method":"state","parameters":{"themeId":"media.aurora","profileId":"wide-1280x480","refreshIntervalMilliseconds":250,"jpegQuality":"medium"}}
```

`themes` reads the local manifests. `devices` enumerates interfaces and returns opaque IDs. Neither sends a USB handshake or picture. `state` renders a local preview and never writes to USB.

## Show the same picture on the panel

Use an ID returned by `devices`:

```json
{"version":1,"id":"4","method":"start","parameters":{"deviceId":"<device-id>","themeId":"media.aurora","profileId":"auto"}}
{"version":1,"id":"5","method":"present","parameters":{"themeId":"media.aurora","profileId":"wide-1280x480","refreshIntervalMilliseconds":250,"jpegQuality":"medium","fontFamily":"Cascadia Mono","fontScale":1,"light":false}}
{"version":1,"id":"6","method":"stop"}
```

`start` opens the selected panel and probes its size. Its reply includes `accepted` and the detected `profileId`. Pass that profile to `present`. An explicit profile can override detection for an unknown model.

`present` draws one frame, sends that JPEG to the panel, and returns it for the desktop preview. The CLI owns the renderer and USB session. It does not draw a second copy for the display. Changing theme, font, colors, interval or quality takes effect on the next request. Byte-identical frames are not sent again.

Only one request/frame is in flight. A slow update lowers the rate rather than creating a queue. Theme updates follow the GUI's requests; they stop when the window closes or the engine reaches EOF. `stop` closes the panel. Errors close the theme output; reconnecting requires another explicit start. An accepted submission does not prove visible pixels.

The GUI starts in preview mode each time. It never restores an active USB session automatically.

## Rate and quality

`refreshIntervalMilliseconds` defaults to 1000; valid range 50–1000. The GUI offers 1000, 500, 250, 100 and 50 ms. These are requested intervals, not guaranteed panel frame rates. Metadata and artwork are checked at most once a second; timeline and existing playback meters can update faster.

`jpegQuality` is `low`, `medium` or `high`, default high. Encoder targets are 50, 75 and 90. Encoding falls back to lower quality if needed to stay under 450,000 bytes. Rotated JPEG profiles also honor the selected target. Other pixel formats require conversion.

`PanelState` adds `frameMilliseconds` (server-side data/render/send duration) and `submittedFrames`. These are not measurements of physical display latency. `displayRunning` and `status` report the session.

## Other settings

`fontFamily` defaults to Cascadia Mono. `fontScale` is 0.65–1.6. `light` and `ambientDrift` default to false. Python keeps decoded artwork and static backgrounds cached. A failed preview retries after five seconds, except when settings change.

Multimedia themes read generic Windows media/audio data. Codex themes skip those sources and read optional local hook activity. Missing data is not invented. The latest twelve observed Codex events are kept in memory; events between polls and real chat titles may be unavailable. No plugin is installed by this protocol.

The engine resolves Python from `VIBEPANEL_PYTHON`, bundled `python/python.exe`, then PATH. It uses a persistent worker with a twelve-second render deadline. Themes are executable Python, not sandboxed documents.

## CLI

```powershell
vp gui
vp themes
vp theme media.aurora --export frame.jpg --quality high
vp theme media.aurora --device 1 --interval-ms 250 --quality medium --seconds 20
```

Export is local and never overwrites a file. Device playback stops on Ctrl+C, its optional duration or a failure. It does not reconnect automatically.

Legacy `start` requests without `themeId` retain the C# scene/rotation behavior. `levels` reads cached playback meters. `media` controls the current OS media session when explicitly requested; there is no volume-write method.

Request lines are limited to 16 KiB; replies to two million characters. Reply fields are `version`, matching `id`, `ok`, `result` and `error`. Native providers currently support Windows.
