# VibePanel

I wanted to use my small Thermalright USB display without leaving TRCC and a full hardware-monitoring stack running all day. So I started VibePanel: a small Windows app and CLI that talk to the panel directly.

VibePanel is part of [VibeWare](https://umbertogiacobbi.biz/vibeware/manifesto), my personal open-source initiative. I keep it understandable, local, and modest.

## What I have started

- WinUI 3 desktop app with the compact VibeWare look.
- `vp` CLI with clear, explicit commands.
- Native WinUSB discovery for Thermalright `0416:5302`.
- A documented probe handshake, with no sensor suite or background service.
- Several RGB565 profile choices, plus custom width and height.

The first frame sender is experimental. It is intentionally a diagnostic pattern, not a promise that every panel size is already correct. I have not yet validated a physical render from this repository.

On my current panel, Windows has loaded WinUSB but has not exposed a user-mode device-interface path. VibePanel reports that state plainly and does not alter the driver or registry. Direct control needs a separately reviewed setup step before I try a physical frame.

## CLI

```powershell
vp discover
vp profiles
vp probe 1
vp pattern 1 square-360 --i-understand
vp pattern 1 custom --width 320 --height 320 --i-understand
```

`discover` only enumerates devices. `probe` sends the small documented device-information request. `pattern` changes the physical screen and requires `--i-understand`.

## Build

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
dotnet run --project .\src\VibePanel.Cli -- discover
```

The desktop app targets Windows 11 on x64 and ARM64. This first repository has no installer or release package.

## Protocol and scope

My connected panel advertises USB ID `0416:5302`. Windows exposes a HID sibling and a vendor-specific WinUSB interface. VibePanel uses the WinUSB interface, bulk OUT endpoint `0x02`, and bulk IN endpoint `0x81`; it does not need Open Hardware Monitor, Libre Hardware Monitor, or TRCC.

I wrote this as a clean-room MIT implementation from published protocol facts. I did not copy GPL implementation code. The exact evidence and limits are in [the protocol notes](docs/protocol-notes.md).

## Contributing

I welcome issues and pull requests. `main` is protected: I review the final change before it lands. Please read [AGENTS.md](AGENTS.md) before changing code, and keep physical-device behaviour clearly marked until it has been observed.

## License

VibePanel source code is [MIT licensed](LICENSE). The VibePanel and VibeWare names and visual identity are not part of that grant; see [TRADEMARKS.md](TRADEMARKS.md).
