# Windows MSI

`Package.wxs` installs the published x64 or ARM64 payload for the current user.
It creates one Start entry and one saved-session startup entry. The MSI owns
Windows uninstall registration. User settings stay outside its install folder.
Opening the MSI shows a license/setup page, progress, and a completion or error
page. It does not launch the studio after installation.

Before install, repair or removal changes files, a native helper closes only
verified Vibe Panel processes. New studios support a normal installer exit;
old or stuck copies are stopped after a bounded wait. The helper also releases
an owned Open Hardware Monitor driver: stop, wait, delete service, wait for the
registration to disappear, then remove and check the generated `.sys` files.
It preserves drivers owned by other software and fails clearly on shared,
busy or pending-deletion drivers. Reading a locked file is not this check.

Cleanup runs as the current user. It requests administrator permission only
when needed, including an old elevated studio or driver. A silent, unelevated
install fails instead of opening an unexpected permission dialog. Details go
to `%LOCALAPPDATA%\VibePanel\logs\setup.log`. The helper is embedded in the MSI;
it adds no installed service or runtime dependency.

The new MSI releases resources before removing an older MSI, including 0.1.1.
Keep the existing payload component identities stable for this upgrade order.
Setup replaces its complete owned payload, including preview assemblies with
unchanged file versions. Edited bundled files can be replaced; user themes and
settings belong outside this install tree.
MSI handles rollback for its files, shortcuts and registration. It does not
restart the studio or reload a removed runtime driver on rollback; OHM can
create its driver again on a later owner-requested sensor read.

Publish with `scripts/Publish.ps1`, then pass its staging folder to:

```powershell
./scripts/Build-Msi.ps1 -PayloadPath artifacts/staging/<published-folder> -Architecture x64 -Version 0.1.2
```

The build uses existing Visual Studio C++ tools and restores WiX 4.0.6 and its
matching UI extension locally when needed. It runs no tests, cleanup helper or
MSI validation. After delivery, run `scripts/Clean-BuildOutputs.ps1 -KeepMsiPath`
with the final file under `artifacts/packages/`. This removes generated output,
including the local build tool, while preserving that MSI.

Installer execution still needs owner-approved checks: normal/elevated app
shutdown, cancelled elevation, a shared/foreign driver, locked/pending driver
removal, upgrade from 0.1.1, repair, uninstall, rollback, and visible progress.
Compilation alone does not establish these results.

References: [WiX UI](https://docs.firegiant.com/wix/tools/wixext/wixui/),
[upgrade order](https://docs.firegiant.com/wix/schema/wxs/majorupgrade/),
[service deletion](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-deleteservice).
