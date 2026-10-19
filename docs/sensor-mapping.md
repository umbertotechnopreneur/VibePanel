# Sensor names and decoding

Useful names need both a correct chip decoder and a supported motherboard mapping.
A similar temperature in two programs does not establish the same physical source.

The original LibreHardwareMonitor 0.9.6 NCT6792D path uses a selector without masking
its flag bits and reads zero-address placeholders as registers. This can lose real
temperatures and overwrite a channel with a configuration value. Its generic ASRock
mapping also omits chip sources and does not recognize the exact B550M-ITX/ac model.

Vibe Panel now references
[fork commit e5fa436eae6cb9ad5e8693d9909cd81ddf473629](https://github.com/umbertotechnopreneur/LibreHardwareMonitor/tree/e5fa436eae6cb9ad5e8693d9909cd81ddf473629).
It routes NCT6792D through the existing decoder that masks selectors, skips empty
register entries and preserves the first valid source reading. The exact-board
mapping also exposes `SMBUSMASTER0`. I submitted these changes in
[LibreHardwareMonitor PR #2590](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2590).

The library source remains MPL-2.0 under `third_party/LibreHardwareMonitor`, with a
.NET 10 project adapted for Vibe Panel's build and packaging. The distribution
includes it under `sources/LibreHardwareMonitor`. Sensor code and embedded modules
match the pinned fork. Vibe Panel 0.1.16 was installed with this library. On the
local B550M-ITX/ac/NCT6792D system, the owner reported checking the displayed values
against CPUID HWMonitor and seeing matching readings. No automated tests were run.

Names such as `PECI 0`, `CPUTIN`, `SYSTIN`, `AUXTIN0` and `SMBUSMASTER0` identify chip
sources. They do not establish physical locations such as Southbridge or Mainboard.
Similar readings in two programs are not enough to assign those names. No guessed
offsets or physical aliases are applied.

Source references for the original decoder and mapping:

- [0.9.6 NCT decoder](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/Lpc/Nct677X.cs#L790).
- [0.9.6 ASRock mapping](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/SuperIOHardware.cs#L4063).
- [Pinned upstream decoder](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/e68a7ef7326fa875210e89e1402d93a45ffe4d3e/LibreHardwareMonitorLib/Hardware/Motherboard/Lpc/Nct677X.cs#L943), which retains this path.

Static inspection of an owner-supplied HWMonitor executable found embedded CPUID
drivers and sensor labels. It did not establish the exact board/channel mapping.
No proprietary code, driver or database was imported. CPUID documents HWMonitor as
an application of its [System Monitoring SDK](https://www.cpuid-pro.com/products-system-monitoring-kit.php).
