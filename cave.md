# Caveats and current evidence

I am Umberto Giacobbi. I started Vibe Panel as a holiday hobby project because I was unhappy with my display's bundled software. I share it as is, without warranty or a support commitment.

## Known limits

- Windows has native media/audio/USB providers. Linux/macOS UI compilation does not establish hardware support.
- The current theme library has twenty distinct wide compositions. Narrow/square profiles use a shared compact fallback.
- Generic OS metadata may omit quality, shuffle, volume or artwork. I do not guess missing values.
- VU meters show observed amplitude, not a spectrum. There is no microphone recording or volume control.
- The viewer is local preview only. Hardware commands probe/write the explicitly selected device.
- A one-second update policy is not a measured CPU, battery, latency or USB throughput claim. JPEG frames are complete frames; no partial-region transfer is claimed.
- Codex hooks are optional and not installed or trusted automatically. Activity is the latest observed hook event, not necessarily the foreground chat. Missing titles remain unnamed, and stale state is marked.
- Theme folders contain executable Python, not sandboxed documents. Use trusted packs. There is no marketplace installer yet.
- No sensor suite/driver is installed. Hardware-monitoring work is secondary to the new Media/Codex screens.
- Portable packages still store local preferences and derived caches. WebView2 remains an external prerequisite.

## Evidence ledger

- **Historical physical result:** the earlier labelled JPEG chart displayed correctly on one Thermalright 1280 × 480 native-HID panel. This does not validate all profiles or the new theme library.
- **Current .NET implementation:** Release x64 solution build passed with zero warnings/errors on 2026-10-16. Compiling the checks project does not execute its tests.
- **Theme rendering:** one owner-authorized local run on 2026-10-16 produced 40/40 JPEGs (twenty themes, light and dark), each 1280 × 480 and below 450,000 bytes. I inspected the four contact sheets. Inputs were synthetic and marked DEMO; no live media or hardware was used. This covers the default font/size and wide geometry, not every setting or edge case.
- **Hardware:** no physical trial was run for the twenty new themes.
- **Codex integration:** plugin source prepared; installation/trust and live hook delivery have not been validated.
- **Distribution:** Windows x64 portable ZIP built with bundled Python and pinned dependencies. ARM64 packaging and clean-machine execution remain unverified locally. No public release tag or installer is claimed.

I record failures and unverified paths here instead of treating a successful build as a finished product.
