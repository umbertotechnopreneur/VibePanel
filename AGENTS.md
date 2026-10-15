# VibePanel repository instructions

## Owner approval comes first

- Tests may be created or updated locally, but every execution requires explicit owner approval first. A request to implement, fix, or continue work is not permission to run tests, smoke tests, or physical-device trials.
- Ask the owner before creating any branch, workspace, checkout, or worktree. Do not infer this permission from a generic implementation or continuation request.
- Keep current work on the existing branch unless the owner explicitly approves a change of working location.

## Purpose

VibePanel is an MIT-licensed VibeWare utility for controlling compatible USB display panels on Windows without requiring a full hardware-monitoring suite. Keep it small, transparent, and safe.

## Product boundaries

- Use .NET 10 and C#. The owner chose OpenSilver + Photino instead of WinUI 3. Keep a separate CLI/engine and a versioned JSON-lines stdin/stdout contract; no listening network port is needed.
- Ship portable ZIP folders. Windows x64/ARM64 hardware support is the current implementation; a portable UI build must never be described as working Linux/macOS USB/audio support without those backends and evidence.
- Make frequent focused checkpoint commits on the existing branch. Do not create a branch or workspace to do this.
- Keep the protocol core independent from the UI and CLI. The UI and CLI must not duplicate USB protocol code.
- Do not add telemetry, network calls, startup registration, sensor polling, or a hardware-monitoring dependency without an explicit request.
- The owner requested local Windows media sessions, read-only per-session volume percentages and VU meters, CPU usage, and existing CPU/GPU sensor providers. Do not add volume setters, sliders, microphone capture, or recording. Keep sampling modest; do not install a sensor driver or monitoring suite. Missing temperatures must remain unavailable, not guessed from ACPI zones.
- Eight dashboard layouts are available; five rotate every 20 seconds with short fades. Cache wallpaper palettes/background JPEGs and artwork; do not turn transitions into continuous high-frame-rate rendering.
- A USB probe or frame send touches a physical device. Make those commands explicit, show the selected device/profile, and never execute them during automated tests.
- The Thermalright protocol has been independently documented by community work. Study upstream implementation when useful, but do not copy GPL source code or assets into this MIT project. Document which code and protocol references were studied; do not claim source was unseen after inspecting it.
- Do not claim a profile or renderer works on physical hardware until it has actually been observed on that hardware.

## Writing and branding

- Keep original public copy in plain English, with the concrete problem and solution before architecture.
- Write as the solo maintainer using first-person singular when author voice is needed.
- Preserve the VibeWare markers `VBWR B` and `VBWR E` in first-party source and project files.
- The VibePanel name and visual identity are not granted by the MIT license; see `TRADEMARKS.md`.

## Delivery and validation

- Keep `main` protected. Work on focused branches and merge through reviewed pull requests after required checks pass.
- Do not commit credentials, local device paths, captures, logs, `artifacts/`, `bin/`, or `obj/`.
- Use `artifacts/` for generated output. Preserve unrelated working-tree changes.
- For C# changes, run restore, format verification, and a Release build before handoff. Do not perform physical-device writes merely to validate a build.
