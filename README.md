# VibePanel

I wanted to use my small Thermalright USB display without leaving TRCC and a full hardware-monitoring stack running all day. So I started VibePanel: a small Windows app and CLI that talk to the panel directly.

VibePanel is part of [VibeWare](https://umbertogiacobbi.biz/vibeware/manifesto), my personal open-source initiative. I keep it understandable, local, and modest.

## What it does

- A dark/lilac WinUI 3 desktop app and a branded `vp` CLI.
- Spotify track, artist and artwork through local Windows media sessions; playback controls without an API key or login. If Spotify is absent, the current Windows player is used.
- Read-only per-session app volume percentages and gradient VU meters. No volume controls or microphone capture.
- Eight layouts, five in the default 20-second rotation, with a short crossfade. Freeze one whenever needed.
- Four wallpaper-derived colors, sampled once per renderer session, with a reusable background JPEG on disk. Restart the app after changing wallpaper.
- CPU usage; NVIDIA GPU usage/temperature through the installed NVML driver. CPU temperature is optional through an already-running LibreHardwareMonitor/OpenHardwareMonitor WMI provider. Missing sensors show `—`. No sensor suite or driver is installed.
- Native HID discovery for Thermalright `0416:5302`, with optional WinUSB discovery for exposed vendor interfaces.
- A device-info handshake that detects the screen profile. No background service or startup registration.
- RGB565 and JPEG profiles, local image display, and saved profile/rotation choices.
- Optional one-frame-per-second playback: the still image is encoded once and reused.

I confirmed the first physical render on my panel on 2026-10-15: native Windows HID, PM `0x80`, SUB `0x01`, 1280 x 480 JPEG. During an approved 20-second trial at 1 fps, the labelled chart and its red, green, blue, and white bars displayed correctly. The new dashboard/fades, other panels, arbitrary-image playback and reconnection after changing cables have not been validated on physical hardware.

The owner-requested desktop demonstration displayed live Spotify metadata/artwork, Windows audio sessions, CPU usage and RTX 4060 usage/temperature. CPU temperature had no provider and correctly remained unavailable. That demonstration preceded the read-only VU-meter revision. Playback controls, the revised meters and USB scene transitions still need approved functional verification; no automated tests were executed for this dashboard work.

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

The scene IDs are `Music`, `Artwork`, `Mixer`, `System`, `Clock`, `TrackDetails`, `VolumeFocus`, and `Minimal`. The first five rotate by default. Desktop preview crossfades use the compositor; USB transitions use five intermediate blended frames plus the destination. USB timing depends on the panel/transport, not a promised frame rate. Between transitions, data is sampled every two seconds, and encoded frames are reused until visible values change. The clock updates by the minute.

`discover` only enumerates devices. `probe` requests device information. `pattern`, `image`, `hold`, and `play` send to the physical screen. `play` stops with Ctrl+C and waits for the same panel path if it disconnects. No app is registered to start automatically.

The desktop app opens a local preview, mixer, and system view. It enumerates panels but does not probe or write USB until a panel command is invoked. Choose a scene or turn on five-screen rotation, then Start dashboard. Still-image tools remain in the collapsed panel. Settings and cached backgrounds stay under `%LOCALAPPDATA%\VibePanel`; no song history or audio is recorded.

## Build

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
dotnet run --project .\src\VibePanel.Cli -- discover
./scripts/Publish.ps1 -Architecture x64
```

Portable output: `artifacts/portable/win-x64/cli/vp.exe` and `artifacts/portable/win-x64/desktop/VibePanel.exe`. Keep each whole folder, not just its EXE. The publish script includes the .NET runtime and Windows App SDK; there is no installer or published release yet. Use `-Architecture ARM64` for an ARM64 build. CI builds and publishes both Windows architectures; it deliberately does not execute tests. Build success is not hardware validation.

## Protocol and scope

My connected panel advertises USB ID `0416:5302`. Its HID interface exposes 513-byte output reports and 37-byte input reports, including the Windows report ID. VibePanel uses that native interface without changing drivers or registry entries.

I wrote the C# implementation independently, using protocol documentation and comparison with the open-source Linux implementation. I did not copy GPL implementation code. The evidence and limits are in [the protocol notes](docs/protocol-notes.md).

## Contributing

I welcome issues and pull requests. `main` is protected: I review the final change before it lands. Please read [AGENTS.md](AGENTS.md) before changing code, and keep physical-device behaviour clearly marked until it has been observed.

## License

VibePanel source code is [MIT licensed](LICENSE). The VibePanel and VibeWare names and visual identity are not part of that grant; see [TRADEMARKS.md](TRADEMARKS.md).
