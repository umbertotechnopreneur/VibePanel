# Protocol notes

## My starting point

My panel is present on Windows as `USB\VID_0416&PID_5302\USBDISPLAY`.

- `MI_00` is a vendor-defined HID interface.
- `MI_01` is a vendor-specific WinUSB interface.
- The WinUSB path exposes bulk OUT `0x02` and bulk IN `0x81`.

VibePanel uses only `MI_01`. It does not install a replacement driver.

On my current PC, Windows has bound the generic WinUSB driver but the panel has not registered an application device-interface GUID. A driver binding by itself is not enough for a desktop app to open a handle. VibePanel can diagnose that state; it does not silently write a registry value, replace a driver, or ask for elevation. A future setup path must be explicit, reversible, and physically tested.

## Probe

The documented device-information request is 512 zero-padded bytes:

- bytes `0..3`: `DA DB DC DD`
- byte `12`: `01`

I read 512 bytes back from endpoint `0x81`. A compatible reply echoes the magic bytes, has `01` at byte `12`, and `10` at byte `16`. Bytes `4` and `5` are retained as product and sub-mode values.

## Experimental frame packet

The initial frame packet uses the same preamble, product/sub-mode bytes, command `02` at byte `12`, a little-endian payload size at bytes `16..19`, and an RGB565 diagnostic pattern after its 20-byte header. USB transfers are sent through endpoint `0x02`.

This packet structure is implementation work in progress. The project includes square 240, 320, 360, and 480 profiles plus portrait and landscape options so I do not quietly assume every panel is 360 by 360. A physical observation is still needed for each panel/profile combination.

## Clean-room boundary

I used publicly available protocol facts, including the [`USBLCDNEW` protocol reference](https://github.com/Lexonight1/thermalright-trcc-linux/blob/main/doc/PROTOCOL_USBLCDNEW.md), to guide independent code. That reference project is GPL-3.0. I did not copy its source, tests, assets, or internal structure into VibePanel.

Thermalright has not endorsed this project.
