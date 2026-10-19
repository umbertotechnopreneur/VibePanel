# Sensor names and decoding

Useful names need both a correct chip decoder and a supported motherboard mapping.
A similar temperature in two programs does not establish the same physical source.

The inspected LibreHardwareMonitor 0.9.6 NCT6792D path uses a selector without masking
its flag bits and reads zero-address placeholders as registers. This can lose real
temperatures and overwrite a channel with a configuration value. The generic ASRock
mapping also omits some chip sources. The exact B550M-ITX/ac model is absent from the
inspected mapping. These findings do not establish physical aliases such as Southbridge.

This release preserves the library's raw names and values. It does not repair that
dependency decoder. Generic board temperatures should not be treated as confirmed
component temperatures. Changing labels or applying guessed offsets would not recover
the lost readings. A dependency repair needs separate source review and validation.

I submitted the decoder repair and exact-board source mapping in
[LibreHardwareMonitor PR #2590](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2590).
The fork compiles for .NET 10 x64. The draft has not been checked on live hardware,
and Vibe Panel 0.1.15 still uses the official package without this patch.

Sources studied, without copying their implementation into the MIT application:

- [0.9.6 NCT decoder](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/Lpc/Nct677X.cs#L790).
- [0.9.6 ASRock mapping](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Motherboard/SuperIOHardware.cs#L4063).
- [Pinned upstream decoder](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/e68a7ef7326fa875210e89e1402d93a45ffe4d3e/LibreHardwareMonitorLib/Hardware/Motherboard/Lpc/Nct677X.cs#L943), which retains this path.

Static inspection of an owner-supplied HWMonitor executable found embedded CPUID
drivers and sensor labels. It did not establish the exact board/channel mapping.
No proprietary code, driver or database was imported. CPUID documents HWMonitor as
an application of its [System Monitoring SDK](https://www.cpuid-pro.com/products-system-monitoring-kit.php).
