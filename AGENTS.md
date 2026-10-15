# VibePanel repository instructions

## Purpose

VibePanel is an MIT-licensed VibeWare utility for controlling compatible USB display panels on Windows without requiring a full hardware-monitoring suite. Keep it small, transparent, and safe.

## Product boundaries

- Target Windows 11 x64 and ARM64 with .NET 10, C#, WinUI 3, and a separate CLI.
- Keep the protocol core independent from the UI and CLI. The UI and CLI must not duplicate USB protocol code.
- Do not add telemetry, network calls, startup registration, sensor polling, or a hardware-monitoring dependency without an explicit request.
- A USB probe or frame send touches a physical device. Make those commands explicit, show the selected device/profile, and never execute them during automated tests.
- The Thermalright protocol has been independently documented by community work. Do not copy GPL source code or assets into this MIT project. Document protocol provenance as factual research and keep this implementation clean-room.
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
