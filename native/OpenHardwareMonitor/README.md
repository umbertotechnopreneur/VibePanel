# Open Hardware Monitor compatibility build

This is the original Open Hardware Monitor 0.9.6 library, rebuilt for .NET 10.
It is not Libre Hardware Monitor or the OpenHardwareMonitorLib 1.x fork.

Upstream commit: `09c16898a4a84c36644e7c07baebf1efd299691e`.
Source archive SHA-256: `8E2B61432779871C03C3225F72F8BA936E27B33916C3F8DEA6C062B54D73ACD2`.

`scripts/Prepare-OpenHardwareMonitor.ps1` downloads that pinned archive at build
time and makes four small compatibility changes:

- `Ring0.cs`: use `MutexAcl.OpenExisting` with the same access rights.
- `KernelDriver.cs`: use `FileInfo` access-control extensions with the same ACL.
- `Computer.cs`: remove the unsupported declarative code-access-security attributes.
- `PInvokeDelegateFactory.cs`: use `AssemblyBuilder.DefineDynamicAssembly`.

The sensor algorithms, WinRing0 binaries and driver lifecycle are unchanged.
The Windows operating system still checks access to the driver. This build does
not add a sensor service, another provider, or support for newer hardware.
The library's original `AsAny` Win32 marshalling remains; its obsolete warning
is suppressed only in this dependency project.

The upstream source files keep their MPL-2.0 notices. The portable package
includes the exact modified library sources and a standalone build project in
`licenses/OpenHardwareMonitor-source.zip`. Extract that archive, then run
`dotnet build OpenHardwareMonitor.csproj -c Release` with the .NET 10 SDK.
This builds the DLL without running a sensor or installing a driver.
See `source/Licenses` for the original license texts and driver notices.
