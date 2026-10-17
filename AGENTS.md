# Vibe Panel repository instructions

## Owner approval comes first

- Tests may be created or updated locally, but every execution requires explicit owner approval first. A request to implement, fix, or continue work is not permission to run tests, smoke tests, or physical-device trials.
- Ask the owner before creating any branch, workspace, checkout, or worktree. Do not infer this permission from a generic implementation or continuation request.
- Keep current work on the existing branch unless the owner explicitly approves a change of working location.

## Purpose

I am Umberto Giacobbi. Vibe Panel is my MIT-licensed holiday hobby project for compatible USB displays, started because I was unhappy with the bundled TRCC software. VibeWare is my initiative, not the author or publisher. Keep the product small, transparent, and honest about its limits.

## Product boundaries

- Use .NET 10 and C# for the engine, OpenSilver Modern + Photino for the viewer, and Python/Skia for offscreen themes. Keep versioned JSON-lines stdin/stdout contracts; no listening network port is needed. The webview belongs to the configuration UI, not the panel renderer.
- Ship portable ZIP folders. Windows x64/ARM64 hardware support is the current implementation; a portable UI build must never be described as working Linux/macOS USB/audio support without those backends and evidence.
- The owner requested one Windows studio instance, a notification-area icon and explicit Close/Cancel/Minimize choices. Reopening the app restores and centers that instance. `--start-minimized` starts in the tray. The per-user installer registers one Start entry with the product icon and one tray startup entry. The owner explicitly requested USB output at Windows sign-in: `--restore-last-session` starts the saved device and theme in the tray. Normal launches stay in preview.
- Make frequent focused checkpoint commits on the existing branch. Do not create a branch or workspace to do this.
- Keep the protocol core independent from the UI and CLI. The UI and CLI must not duplicate USB protocol code.
- Do not add telemetry, network calls, startup registration, sensor polling, or a hardware-monitoring dependency without an explicit request.
- The owner requested local Windows media sessions, read-only per-session volume percentages and VU meters, CPU usage, and existing CPU/GPU sensor providers. Do not add volume setters, sliders, microphone capture, or recording. Keep sampling modest; do not install a sensor driver or monitoring suite. Missing temperatures must remain unavailable, not guessed from ACPI zones.
- The primary library contains ten Multimedia and ten OpenAI Codex themes. Discover manifests from pack folders; keep stable internal category IDs `multimedia` and `codex`. Installing a theme never installs or trusts a provider or Codex hook.
- Use generic OS media APIs, never a direct Spotify integration. Missing quality, shuffle, volume, chat title or activity remains unavailable. Keep hardware monitoring secondary.
- The owner requested a real playback spectrum and stereo VU. Use Windows output loopback only for Multimedia themes, bounded in-memory analysis, no microphone, audio files or audio network traffic. Label the full output mix separately from app-session volume. Do not synthesize missing bars or promise 50 ms audio latency from a 50 ms refresh setting.
- Cache wallpaper palettes, blurred backgrounds and artwork. Default refresh is one second; the owner also requested 500/250/100/50/25 ms. Keep one in-flight frame with no backlog. Optional theme cycling is twenty seconds. Reuse the same rendered JPEG for preview and USB; do not claim low CPU usage or the requested frame rate without measurements.
- The viewer owns one `vp --stdio` child. Physical theme output requires Show on display (`start`), followed by explicit `present` requests; `state` remains USB-free. Windows startup with `--restore-last-session` is the owner's explicit exception: issue one start for the saved device and theme, waiting for that device if needed. Stop cancels this startup wait. A failed or disconnected active session does not reconnect itself. Support Poor/Low/Medium/High JPEG quality within the existing frame-size cap.
- Runtime color controls are saturation, hue, brightness and contrast. The studio starts with filters off. Keep the switch and slider values saved separately. Off hides the sliders and bypasses the final color pass, including theme overrides and fades. When enabled, filter once before JPEG encoding.
- Preserve the legacy eight C# scenes for compatibility, but do not reintroduce their controls or hardware polling into the new theme viewer.
- A USB probe or frame send touches a physical device. Make those commands explicit, show the selected device/profile, and never execute them during automated tests.
- The Thermalright protocol has been independently documented by community work. Study upstream implementation when useful, but do not copy GPL source code or assets into this MIT project. Document which code and protocol references were studied; do not claim source was unseen after inspecting it.
- Do not claim a profile or renderer works on physical hardware until it has actually been observed on that hardware.

## Writing and branding

- Keep original public copy in plain English, with the concrete problem and solution before architecture.
- Write as the solo maintainer using first-person singular when author voice is needed.
- Keep public notes very short. Use short sentences and common words a child could understand. No slogans or long branding explanations. Put commands and architecture in linked guides.
- Describe the origin honestly: I made it in a few days during my holiday, frustrated with the existing software, learning from others' public work. Credit those sources. Memory, performance and security checks remain on my list because I have not had time; do not claim them complete or blame their absence on AI.
- Make the current theme warning easy to see: themes run Python code on the user's computer; for now, download them only from the official repository. Do not imply that this replaces the unfinished security review.
- Use **Vibe Panel** as the product codename and **Umberto Giacobbi** as its author. Keep `VibePanel` for repository/code identifiers. Do not substitute VibeWare for either.
- Keep the studio mostly flat. Use spacing and restrained dividers, one tinted color-controls panel, and opaque dropdowns. Keep controls clear in light and dark modes.
- Open a larger, centered studio within the monitor work area. Let the preview fit its viewport; only the theme list scrolls. Keep the preview flat, with a solid backing and no elevation, shadow or blur effect on its backing or changing image.
- Put the author and About below the product title. Keep the unchanged VibeWare logo and manifesto at the top right, and Terms/Privacy visible below. Keep the product icon separate from the initiative logo.
- Keep the approved VibeWare floppy unchanged and a brief initiative link at the bottom of the README: `https://umbertogiacobbi.biz/vibeware/manifesto`. Omit the tagline and slogans from public project notes.
- Keep the hobby/as-is/no-warranty disclosure short. Do not promise support, compatibility, verified security or performance. Record decisions in `lesson.md` and limits/evidence in `cave.md`.
- Preserve the VibeWare markers `VBWR B` and `VBWR E` in first-party source and project files.
- The VibePanel name and visual identity are not granted by the MIT license; see `TRADEMARKS.md`.

## Delivery and validation

- Keep `main` protected. Stay on the current branch unless the owner approves a new one; merge through reviewed pull requests after required checks pass.
- For every owner-authorized Git delivery, create or reuse an open pull request for the current branch. Always assign it to `umbertotechnopreneur` and apply the relevant repository labels.
- CI priority is Windows, Linux, then macOS. The owner authorizes an administrator override only for a failed, cancelled, unavailable or stalled macOS-only check when Windows, Linux, other required checks and review requirements pass. Preserve protections and report that limitation. This does not authorize starting tests, builds, Git delivery, installation or publication by itself.
- Do not commit credentials, local device paths, captures, logs, `artifacts/`, `bin/`, or `obj/`.
- Use `artifacts/` for generated output. Preserve unrelated working-tree changes.
- After every requested build/delivery, clean generated `artifacts/`, `bin/`, `obj/`, `temp/`, `tmp/` and generated web resources inside this repository. Keep only the requested final MSI under `artifacts/packages/`. Do not leave staging folders, ZIPs, build tools, logs or caches behind. Keep installed files and user settings outside the repository intact.
- The Windows MSI must show installation progress and handle upgrade/removal cleanup. Use its normal visible UI when installing unless the owner explicitly requests silent mode. Close only verified Vibe Panel processes before changing files. For an Open Hardware Monitor driver belonging to this user's Vibe Panel installation, wait for it to stop, remove its service registration, verify that registration is gone, and remove/check the generated `.sys` file. Request administrator rights only when cleanup needs them. Preserve drivers owned by other software and report a busy driver or pending deletion as a failure, never as completed cleanup.
- For C# changes, run restore, format verification, and a Release build before handoff. Do not perform physical-device writes merely to validate a build.
