# Vibe Panel

I made Vibe Panel to put music and PC information on a small USB screen. I'm [Umberto Giacobbi](https://umbertogiacobbi.biz).

![Ten Vibe Panel music and system themes shown with demo data](docs/theme-gallery.png)

- **Music:** See the song and artwork from Spotify and other players that share a Windows media session.
- **Your PC:** See available CPU, GPU, RAM, temperatures, power and fan readings. What you see depends on your hardware and sensor access.
- **Codex:** Try the early Codex themes. This part still needs work.

I built this for a Thermalright-compatible USB screen (`0416:5302`). Other screens and profiles have not all been checked. [Hardware details](docs/protocol-notes.md).

## Get it

[Download Vibe Panel 0.1](https://github.com/umbertotechnopreneur/VibePanel/releases/tag/0.1). On Windows, pick the MSI for your PC: **x64** or **ARM64**. Open it and follow the steps. A portable ZIP is available too.

Windows is the supported platform today. Linux and macOS do not yet have USB or audio support. CPU and motherboard sensor access on Windows ARM64 is still unavailable. [How to use Vibe Panel](PORTABLE.md) · [Sensor setup](docs/sensors.md) · [Current limits](cave.md).

**Themes run Python code on your PC. For now, only download themes from [this official repository](https://github.com/umbertotechnopreneur/VibePanel).**

I made this hobby project in a few days on holiday because I was unhappy with my display software. I learned from other people's public work and used AI to help write it. [Credits](docs/protocol-notes.md#sources-studied). Memory, speed and security checks are still on my list; I have not finished them.

The software is open source under the [MIT license](LICENSE); the hardware is sold separately. Some included components have their own [licenses](THIRD_PARTY_NOTICES.md). This is a hobby project, with no warranty.

## More tools

[VoiceDucker](https://github.com/umbertotechnopreneur/VoiceDucker) · [Ulanzi Dock](https://github.com/umbertotechnopreneur/ulanzi-dock-vibeware)

---

<a href="https://umbertogiacobbi.biz/vibeware/manifesto"><img src="src/VibePanel.App/Assets/vibeware-logo.png" alt="VibeWare" width="64"></a>

Part of my VibeWare initiative. [My manifesto](https://umbertogiacobbi.biz/vibeware/manifesto).

[Terms](https://umbertogiacobbi.biz/terms/) · [Privacy](https://umbertogiacobbi.biz/privacy/)

## External binary security

Before importing a new external library or binary, I scan its archive and extracted files with Microsoft Defender, record the source, hashes, command and result, and proceed only after a clean scan. Unchanged files reuse their recorded evidence; builds do not start scans. See the [build guide](docs/build.md).
