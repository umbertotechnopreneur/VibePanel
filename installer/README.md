# Windows MSI

`Package.wxs` installs the published x64 or ARM64 payload for the current user.
It creates one Start entry and one saved-session startup entry. The MSI owns
Windows uninstall registration. User settings stay outside its install folder.

Publish with `scripts/Publish.ps1`, then pass its staging folder to:

```powershell
./scripts/Build-Msi.ps1 -PayloadPath artifacts/staging/<published-folder> -Architecture x64 -Version 0.1.1
```

The build restores WiX 4.0.6 locally when needed. It runs no tests or MSI
validation. After delivery, run `scripts/Clean-BuildOutputs.ps1 -KeepMsiPath`
with the final file under `artifacts/packages/`. This removes generated output,
including the local build tool, while preserving that MSI.
