# Build Vibe Panel

I use the .NET 10 SDK and Python with pip on the packaging PC.

From the repository folder:

```powershell
dotnet restore .\VibePanel.slnx
dotnet build .\VibePanel.slnx --configuration Release --no-restore
./scripts/Publish.ps1 -Architecture x64
```

The ZIP is written to `artifacts/packages/VibePanel-win-x64-preview.zip`. Use `-Architecture ARM64` for that architecture; my local package check covered x64.

Packaging downloads an isolated Python runtime and the pinned theme dependencies. It does not change the system Python installation. ZIP users do not need Python or .NET installed separately.

Tests and hardware trials need the owner's approval before each run. Read [AGENTS.md](../AGENTS.md).
