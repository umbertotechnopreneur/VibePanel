# VibePanel

I wanted to use my small Thermalright USB display without leaving TRCC and a full hardware-monitoring stack running all day. So I started VibePanel: a small Windows app and CLI that talk to the panel directly.

VibePanel is part of [VibeWare](https://umbertogiacobbi.biz/vibeware/manifesto), my personal open-source initiative. I keep it understandable, local, and modest.

## What it does

- An OpenSilver Modern-theme + Photino screen viewer and a branded `vp` CLI/engine, connected through versioned JSON Lines on stdin/stdout. No listening network port. Light, dark and automatic day/night modes; monospace typography throughout.
- Spotify track, artist and artwork through local Windows media sessions; playback controls without an API key or login. If Spotify is absent, the current Windows player is used.
- Read-only per-session app volume percentages and gradient VU meters. No volume controls or microphone capture.
- Eight layouts, five in the default 20-second rotation, with a short crossfade. Freeze one whenever needed.
- Music pages use artwork-derived color blobs and a cached, soft cover shadow with a 16-pixel logical offset, tinted from the mean color of the most common quantized cover-color group. Other pages average four wallpaper regions. Optional ambient drift moves the cached background at a nominal 1 pixel/second in low-rate snapshots, without re-sampling or re-blurring. Restart after changing wallpaper; derived-color caches contain no raw artwork or song log.
- Network download/upload rates and totals since engine start, with a dedicated screen. Adapter counters can overlap on VPN/bridges; they are not billing totals.
- CPU usage; NVIDIA GPU usage/temperature through the installed NVML driver. CPU temperature is optional through an already-running LibreHardwareMonitor/OpenHardwareMonitor WMI provider. Missing sensors show `—`. No sensor suite or driver is installed.
- Native HID discovery for Thermalright `0416:5302`, with optional WinUSB discovery for exposed vendor interfaces.
- A device-info handshake that detects the screen profile. No background service or startup registration.
- RGB565 and JPEG profiles, local image display, and saved profile/rotation choices.
- Optional one-frame-per-second playback: the still image is encoded once and reused.

I confirmed the first physical render on my panel on 2026-10-15: native Windows HID, PM `0x80`, SUB `0x01`, 1280 x 480 JPEG. During an approved 20-second trial at 1 fps, the labelled chart and its red, green, blue, and white bars displayed correctly. The new dashboard/fades, other panels, arbitrary-image playback and reconnection after changing cables have not been validated on physical hardware.

The earlier WinUI prototype demonstration displayed live Spotify metadata/artwork, audio sessions, CPU usage and RTX 4060 usage/temperature. CPU temperature had no provider and remained unavailable. It preceded the OpenSilver migration and read-only VU revision: it is not validation of the new UI. No tests have been executed for this migration.

## CLI

```powershell
vp help
vp dashboard
vp dashboard --watch
vp dashboard --export frame.jpg
vp scenes
vp media toggle
vp volumes
vp discover
vp profiles
vp probe 1
vp pattern 1 auto
vp image 1 "C:\Pictures\panel.png"
vp play 1 "C:\Pictures\panel.png" --seconds 20
vp settings --profile auto --rotate 0
```

`vp dashboard --device 1` explicitly starts the five-screen USB rotation. Add `--scene Artwork` to hold one layout, or `--profile wide-1280x480` to override detection. Ctrl+C stops. A disconnect stops the dashboard with an error; it does not silently switch devices. `--export` saves a JPEG without opening USB and never overwrites an existing file.

The eight scene IDs are `Music`, `Artwork`, `Mixer`, `System`, `Clock`, `TrackDetails`, `VolumeFocus`, and `Network`. The optional five-screen rotation is Music → Artwork → Mixer → System → Network. The desktop preview crossfades over 650 ms; USB transitions use five intermediate blended frames plus the destination. USB timing depends on the panel/transport, not a promised frame rate. Dashboard snapshots refresh every two seconds. Encoded frames are reused until visible values change; enabling ambient drift adds low-rate frame updates on otherwise static scenes. The clock updates by the minute.

`discover` only enumerates devices. `probe` requests device information. `pattern`, `image`, `hold`, and `play` send to the physical screen. `play` stops with Ctrl+C and waits for the same panel path if it disconnects. No app is registered to start automatically.

The desktop app is deliberately just a large screen preview and navigator: previous/next, scene, geometry, optional rotation and ambient drift. Page Up/Page Down also switch screens. There are no media buttons, volume controls, duplicated status cards or USB Start button in the viewer. All physical panel commands remain explicit CLI/engine operations. Auto theme uses local PC time: light from 07:00 to 19:00, dark otherwise; manual overrides are saved. Settings and cached backgrounds stay under `%LOCALAPPDATA%\VibePanel`; no song history or audio is recorded. Scene choices are session-only; CLI profile/rotation/image settings remain persistent.

The presentation references only [the portable contract](src/VibePanel.Contracts/PanelContract.cs); USB, JPEG rendering, Windows media and sensor access remain in the native engine. See [the JSON protocol](docs/engine-protocol.md). [OpenSilver's Photino launcher](https://doc.opensilver.net/documentation/general/photino-launcher.html) avoids MAUI/Catalyst. Linux/macOS native providers remain pending; portable UI compilation is not full hardware support.

## Build

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
dotnet run --project .\src\VibePanel.Cli -- discover
./scripts/Publish.ps1 -Architecture x64
```

Portable ZIP: `artifacts/packages/VibePanel-win-x64-preview.zip`. Extract it and run `VibePanel.exe`, or `engine/vp.exe help`. The .NET runtime is included; Windows WebView2 Evergreen is required and not auto-installed. Keep the whole folder. Use `-Architecture ARM64` for Windows ARM64. There is no installer, release tag or published release yet. CI builds/packages Windows x64/ARM64 and compiles the Linux UI only; it never executes tests. See [portable notes](PORTABLE.md).

Codex hook integration is a future opt-in adapter, not installed by this preview. No Codex configuration, hook trust, or Python environment is modified.

## Protocol and scope

My connected panel advertises USB ID `0416:5302`. Its HID interface exposes 513-byte output reports and 37-byte input reports, including the Windows report ID. VibePanel uses that native interface without changing drivers or registry entries.

I wrote the C# implementation independently, using protocol documentation and comparison with the open-source Linux implementation. I did not copy GPL implementation code. The evidence and limits are in [the protocol notes](docs/protocol-notes.md).

## Contributing

I welcome issues and pull requests. `main` is protected: I review the final change before it lands. Please read [AGENTS.md](AGENTS.md) before changing code, and keep physical-device behaviour clearly marked until it has been observed.

## License

VibePanel source code is [MIT licensed](LICENSE). The VibePanel and VibeWare names and visual identity are not part of that grant; see [TRADEMARKS.md](TRADEMARKS.md).
