# Local engine protocol

`vp --stdio` speaks JSON Lines v1 over redirected standard input/output. There is no TCP listener, HTTP service, shell-command endpoint, or automatic USB startup. Stdout contains one response per request; errors outside the protocol go to stderr.

```json
{"version":1,"id":"1","method":"hello"}
{"version":1,"id":"2","method":"state","parameters":{"profileId":"wide-1280x480","scene":"Music","includeFrame":true}}
{"version":1,"id":"3","method":"levels"}
```

Responses have `version`, matching `id`, `ok`, `result`, and `error`. Binary preview JPEGs are base64 JSON values. Request lines are limited to 16 KiB; the desktop client bounds responses at 2 million characters. Unknown protocol versions and methods are rejected. JSON is UTF-8; the human CLI banner is disabled in this mode.

`state` reads local media, audio levels, metrics, and device enumeration, without probing/writing a panel. Its opaque device IDs are local hashes, not USB paths. `levels` samples cached playback-session meters; it never opens a microphone.

Preview parameters also accept `light` and `ambientDrift`, both false by default. They affect the local raster only. The viewer resolves automatic day/night mode using local PC time; it never opens USB to change themes.

Only `start` (`deviceId`, `profileId`, optional fixed `scene`) starts a physical display session. An absent scene selects the five-screen rotation. `stop` cancels that session. EOF also stops it; a removed device is never silently replaced. A successful start response means the request was accepted, not that an image was physically observed.

`media` accepts an `action`: `toggle`, `play`, `pause`, `next`, or `previous`. There is deliberately no volume-write method.

The OpenSilver presentation depends on the portable Contracts project. The current native engine is Windows-only; replacing the UI does not port the HID/audio/media providers to another OS. No hook installation or Codex configuration changes are performed by this protocol.
