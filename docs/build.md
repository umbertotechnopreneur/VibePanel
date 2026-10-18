# Build Vibe Panel

I use the .NET 10 SDK and Python with pip on the packaging PC.

Windows builds also need PowerShell 7, Visual Studio's C++ v143 tools for the target architecture, and the Windows SDK. No tool installer runs as part of the build.

`scripts/Build-PhotinoNative.ps1` builds the Windows native bridge with owned UTF strings. It checks the pinned Apache-2.0 source archive and native SDK package hashes, then caches its output under `artifacts/native/photino-memory/`. The first build downloads those sources and SDK packages. Later builds reuse the checked DLL when its patch and hash match.

From the repository folder:

For a local reinstall, use this command. It uninstalls registered Vibe Panel
packages, clears `artifacts`, `bin`, `obj` and generated web files, increments
`installer/Version.txt`, builds a fresh MSI and opens its normal installer UI.
After installation it compares every installed payload file with the new build
and removes build output, keeping only the final MSI. Settings and personal
themes stay in their user folders. Cancellation or any failed step stops it.

```powershell
pwsh -NoProfile -File ./scripts/Reinstall.ps1 -Architecture x64
```

The packaging scripts stage external packages and scan their archives and
extracted files with Microsoft Defender before use. Scan records are written
under `artifacts/security` during the build; final cleanup removes those logs.
A detection or unavailable scanner stops packaging. This is not a full security
review. No tests or USB commands run during reinstall.

For build-only work (no installation):

```powershell
./scripts/Publish.ps1 -Architecture x64
```

The ZIP is written to `artifacts/packages/VibePanel-win-x64-preview.zip`. Each
build first uses a unique ignored staging folder, so packaging does not overwrite
or stop a portable copy that is currently running. Use `-Architecture ARM64` for
that architecture; my local package check covered x64.

Packaging downloads an isolated Python runtime and the pinned theme dependencies. It does not change the system Python installation. ZIP users do not need Python or .NET installed separately.

The CLI output contains its own `themes`, `theme-runtime` and `plugins` folders, in local builds too. The engine never searches parent folders for bundled code. In a ZIP these folders live under `engine/`.

Tests and hardware trials need the owner's approval before each run. Read [AGENTS.md](../AGENTS.md).
