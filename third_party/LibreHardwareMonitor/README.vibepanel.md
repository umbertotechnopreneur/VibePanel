# LibreHardwareMonitor source used by Vibe Panel

This folder contains the library source from
[fork commit e5fa436](https://github.com/umbertotechnopreneur/LibreHardwareMonitor/tree/e5fa436eae6cb9ad5e8693d9909cd81ddf473629),
based on upstream commit `e68a7ef7326fa875210e89e1402d93a45ffe4d3e`.
The NCT6792D decoder and ASRock B550M-ITX/ac source mapping are submitted in
[upstream PR #2590](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2590).

The library stays under MPL-2.0. Original source notices and the license are
preserved. Vibe Panel includes this source under `sources/LibreHardwareMonitor`
in its distribution. It does not include the upstream GUI or a driver installer.

The project file is adapted for Vibe Panel: .NET 10 only, repository-local build
output, no automatic NuGet packaging, and a fork-specific informational version.
Redundant .NET 10 framework package references are omitted. The C# sensor source
and embedded modules are unchanged from the pinned fork commit.

Every new external version must be staged and pass an explicit targeted Defender
scan before use. Existing clean hash evidence is reused for unchanged imports;
compilation does not start a scan. New source edits and generated output are not
external imports.

The source correction has compiled. Live hardware behavior and physical names
for the raw chip temperature sources still need confirmation.
