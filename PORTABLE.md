# VibePanel portable preview

Extract the whole ZIP. Run `VibePanel.exe` for the OpenSilver + Photino interface, or open a terminal and run `engine/vp.exe help` for the CLI. Keep the `engine` and `wwwroot` folders beside the desktop app.

The .NET runtime is included. Windows WebView2 Evergreen Runtime is required; VibePanel does not install it, drivers, services, startup entries, or monitoring tools. The UI opens a local preview only. USB output starts only when I press **Start display** or explicitly invoke a CLI panel command.

App audio levels are read-only. No microphone is captured and no audio is recorded. Settings and derived-color background caches stay in `%LOCALAPPDATA%\VibePanel`; portable means no installer, not zero local state. Network totals reset with the engine and aggregate active non-loopback/non-tunnel adapter counters; VPN/bridges can overlap. These are not billing totals.

This preview's native engine supports Windows only. The presentation targets portable .NET, but Linux/macOS USB, media, audio and sensor providers remain unimplemented. No claim of cross-platform hardware compatibility is made.

The approved physical result covers the earlier labelled JPEG chart on one 1280×480 Thermalright panel. New UI, media controls, VU meters, network counters and scene fades have build evidence only until separately approved for functional verification.
