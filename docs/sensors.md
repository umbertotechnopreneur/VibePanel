# System sensors

Vibe Panel uses the official compiled LibreHardwareMonitorLib 0.9.6 library for CPU,
GPU and motherboard readings. It includes the library and required runtime dependencies,
without the LibreHardwareMonitor application or any driver installer.

CPU and motherboard temperatures and fan RPM need PawnIO. Its official signed edition
is available from [pawnio.eu](https://pawnio.eu/). Vibe Panel does not install it.
The System sidebar shows existing registration and the driver path when available.
**Open driver folder** reveals that location; it does not execute or allow the driver.
Registration is not proof that every sensor can be read.

GPU readings and physical RAM can remain available without PawnIO. RAM is read with
Windows' [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex);
the library's memory/SPD group stays disabled to avoid loading an additional driver.
No values are inferred from load or ACPI thermal zones. Zero RPM is a measured stop;
an unavailable RPM is a missing reading. Generic motherboard channels retain their names.

Some hardware also needs administrator rights. Elevation does not install a missing
driver or bypass a Windows security block. Windows ARM64 CPU/motherboard access remains
disabled pending implementation evidence. Linux/macOS sensor providers are not implemented.

The old Open Hardware Monitor/WinRing0 integration has been removed from the source and
new payload. Microsoft documents the
[WinRing0 vulnerable-driver alert](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0).
Do not treat that alert as proof that an exception is safe. Older installed versions
still use their own files until separately updated; source changes do not modify them.
