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
- Make frequent focused checkpoint commits on the existing branch. Do not create a branch or workspace to do this.
- Keep the protocol core independent from the UI and CLI. The UI and CLI must not duplicate USB protocol code.
- Do not add telemetry, network calls, startup registration, sensor polling, or a hardware-monitoring dependency without an explicit request.
- The owner requested local Windows media sessions, read-only per-session volume percentages and VU meters, CPU usage, and existing CPU/GPU sensor providers. Do not add volume setters, sliders, microphone capture, or recording. Keep sampling modest; do not install a sensor driver or monitoring suite. Missing temperatures must remain unavailable, not guessed from ACPI zones.
- The primary library contains ten Multimedia and ten OpenAI Codex themes. Discover manifests from pack folders; keep stable internal category IDs `multimedia` and `codex`. Installing a theme never installs or trusts a provider or Codex hook.
- Use generic OS media APIs, never a direct Spotify integration. Missing quality, shuffle, volume, chat title or activity remains unavailable. Keep hardware monitoring secondary.
- Cache wallpaper palettes, blurred backgrounds and artwork. Preview refresh is one second, optional theme cycling is twenty seconds. Do not turn fades or ambient drift into continuous high-frame-rate rendering; do not claim low CPU usage without measurements.
- Preserve the legacy eight C# scenes for compatibility, but do not reintroduce their controls or hardware polling into the new theme viewer.
- A USB probe or frame send touches a physical device. Make those commands explicit, show the selected device/profile, and never execute them during automated tests.
- The Thermalright protocol has been independently documented by community work. Study upstream implementation when useful, but do not copy GPL source code or assets into this MIT project. Document which code and protocol references were studied; do not claim source was unseen after inspecting it.
- Do not claim a profile or renderer works on physical hardware until it has actually been observed on that hardware.

## Writing and branding

- Keep original public copy in plain English, with the concrete problem and solution before architecture.
- Write as the solo maintainer using first-person singular when author voice is needed.
- Use **Vibe Panel** as the product codename and **Umberto Giacobbi** as its author. Keep `VibePanel` for repository/code identifiers. Do not substitute VibeWare for either.
- Keep the approved VibeWare floppy unchanged, link it to `https://umbertogiacobbi.biz/vibeware/manifesto`, and put the initiative section at the bottom of the README. The tagline is `Human intent, AI execution, and plenty of tokens`.
- Keep the hobby/as-is/no-warranty disclosure short. Do not promise support, compatibility, verified security or performance. Record decisions in `lesson.md` and limits/evidence in `cave.md`.
- Preserve the VibeWare markers `VBWR B` and `VBWR E` in first-party source and project files.
- The VibePanel name and visual identity are not granted by the MIT license; see `TRADEMARKS.md`.

## Delivery and validation

- Keep `main` protected. Stay on the current branch unless the owner approves a new one; merge through reviewed pull requests after required checks pass.
- CI priority is Windows, Linux, then macOS. The owner authorizes an administrator override only for a failed, cancelled, unavailable or stalled macOS-only check when Windows, Linux, other required checks and review requirements pass. Preserve protections and report that limitation. This does not authorize starting tests, builds, Git delivery, installation or publication by itself.
- Do not commit credentials, local device paths, captures, logs, `artifacts/`, `bin/`, or `obj/`.
- Use `artifacts/` for generated output. Preserve unrelated working-tree changes.
- For C# changes, run restore, format verification, and a Release build before handoff. Do not perform physical-device writes merely to validate a build.
