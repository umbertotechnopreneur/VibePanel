# Protocol notes

## My starting point

My panel is present on Windows as `USB\VID_0416&PID_5302\USBDISPLAY`.

- `MI_00` is a vendor-defined HID interface.
- `MI_01` is a vendor-specific WinUSB interface.
- The HID descriptor reports 513-byte output and 37-byte input reports, including Windows report ID zero.

The current panel is controlled through `MI_00`. Its presence under WinUSB at `MI_01` did not prove that it exposes data endpoints. No driver or registry setup was needed for HID access.

The earlier attempt inferred interface GUIDs from unrelated registry details. That was incorrect: a GUID-looking child key is not a registered device interface, and the `F72FE0D4` GUID in `winusb.inf` belongs to ADB. Optional WinUSB discovery now searches actually registered interface classes.

## Probe

The documented device-information request is 512 zero-padded bytes:

- bytes `0..3`: `DA DB DC DD`
- byte `12`: `01`

On Windows HID I write a 513-byte report: report ID zero followed by the 512-byte query. The observed input report contains a zero report ID and 36 protocol bytes. The HID reply places SUB at byte `4` and PM at byte `5`; byte `12` is `01`. The marker `10` at byte `16` means an optional serial is present, not general success.

The observed device returned PM `80`, SUB `01`. The upstream mapping selects 1280 x 480 JPEG. Reported firmware revision `0407` alone cannot select a model: several panels share that revision.

The implementation now waits 50 ms before init and 200 ms after init, matching upstream pacing. It does not apply an unverified firmware-reset or skip-init workaround to this ordinary 36-byte reply.

## Experimental frame packet

The picture header contains magic at `0..3`, command `02` at `4..5`, encoding at `6..7` (`00` JPEG, `01` RGB565), little-endian width/height at `8..11`, command `02` at `12..15`, and encoded payload length at `16..19`. These are not PM/SUB bytes. JPEG declares native dimensions; the legacy RGB565 header declares 240 x 320.

I append the encoded payload and zero padding to a 512-byte boundary. Windows HID sends each 512-byte chunk in its own 513-byte report, prefixed by report ID zero. Each write is awaited before reusing the buffer. A 1 ms settle follows the completed frame. JPEG encoding starts at quality 95 and reduces quality as needed to stay within 450000 bytes; oversize payloads are rejected.

The project includes square, portrait, landscape, and wide profiles. Unknown PM bytes require an explicit profile; they never silently default to 360 x 360. PM 128/SUB 2 has a documented 90-degree wire rotation, distinct from the observed SUB 1. Other listed profiles remain unverified on physical hardware.

## Validation boundary

Before the owner requested approval before every test run, 24 offline checks passed and the physical device-info exchange succeeded. A single JPEG transfer and a 20-second 1 fps transfer completed without host errors. The owner reported seeing only the Thermalright logo. Those successful writes are not evidence of a rendered custom image. Later pacing, size-cap, application, and UI edits have not been tried on the panel.

On 2026-10-15, after explicit approval for one further 20-second trial, I sent the labelled JPEG chart at 1 fps using the updated build. The CLI completed normally and reported 19 frame submissions within the bounded run. I confirmed that TOP and the coloured bars appeared and reported that the result worked perfectly. This establishes a physical custom render on this PM `0x80` / SUB `0x01` panel with the current HID transport and JPEG encoder. It does not identify which individual change resolved the earlier logo-only observation.

That trial ended normally before I moved the panel to another cable. Reconnection on a new USB path, indefinite playback, arbitrary-image input, and other panel profiles still require their own approved verification. No driver or registry settings were changed for this result.

The subsequent owner-requested desktop demonstration opened the WinUI app and displayed live Spotify artwork/metadata, audio sessions, CPU usage and NVIDIA GPU readings without writing to USB. This is startup/read-only preview evidence, not a test of playback controls, volume writes, or physical dashboard/fade rendering.

Every additional hardware trial requires explicit owner approval. No further test is implied by a build or implementation request.

## Sources studied

I studied the [`USBLCDNEW` protocol reference](https://github.com/Lexonight1/thermalright-trcc-linux/blob/main/doc/PROTOCOL_USBLCDNEW.md), the [Trofeo protocol measurements](https://github.com/Sandjab/Trofeo/blob/main/docs/PROTOCOL.md), and the actual upstream [`hid_lcd.py`](https://github.com/Lexonight1/thermalright-trcc-linux/blob/main/src/trcc/adapters/device/hid_lcd.py), [`transport.py`](https://github.com/Lexonight1/thermalright-trcc-linux/blob/main/src/trcc/adapters/device/transport.py), `_base.py`, `protocol.py`, and renderer paths. I compared the Windows write operation with the upstream HIDAPI Windows backend too. These references informed protocol facts and timing; no upstream source or assets were copied into VibePanel.

[Issue 267](https://github.com/Lexonight1/thermalright-trcc-linux/issues/267) documents the same broad symptom, successful writes without an image, across multiple firmware variants. It is a useful diagnostic reference, not proof of a cause on my own panel.

Thermalright has not endorsed this project.
