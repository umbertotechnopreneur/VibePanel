# Build Vibe Panel

I use the .NET 10 SDK and Python with pip on the packaging PC.

Windows builds also need PowerShell 7, Visual Studio's C++ v143 tools for the target architecture, and the Windows SDK. No tool installer runs as part of the build.

`scripts/Build-PhotinoNative.ps1` builds the Windows native bridge with owned UTF strings. It checks the pinned Apache-2.0 source archive and native SDK package hashes, then caches its output under `artifacts/native/photino-memory/`. The first build downloads those sources and SDK packages. Later builds reuse the checked DLL when its patch and hash match.

From the repository folder:

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
./scripts/Publish.ps1 -Architecture x64
```

The ZIP is written to `artifacts/packages/VibePanel-win-x64-preview.zip`. Each
build first uses a unique ignored staging folder, so packaging does not overwrite
or stop a portable copy that is currently running. Use `-Architecture ARM64` for
that architecture; my local package check covered x64.

Packaging downloads an isolated Python runtime and the pinned theme dependencies. It does not change the system Python installation. ZIP users do not need Python or .NET installed separately.

Tests and hardware trials need the owner's approval before each run. Read [AGENTS.md](../AGENTS.md).
