# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Build Windows icon sizes from two first-party SVG sources using the scanned Skia runtime."""
from pathlib import Path
import struct
import skia

root = Path(__file__).resolve().parents[1]
assets = root / 'src/VibePanel.App/Assets'
images = []
for size in (16, 24, 32, 48, 64, 128, 256):
    source = assets / ('vibepanel-mark.svg' if size <= 32 else 'vibepanel-app.svg')
    dom = skia.SVGDOM.MakeFromStream(skia.MemoryStream(source.read_bytes()))
    surface = skia.Surface(size, size)
    surface.getCanvas().clear(skia.ColorTRANSPARENT)
    surface.getCanvas().scale(size / (16 if size <= 32 else 256), size / (16 if size <= 32 else 256))
    dom.render(surface.getCanvas())
    png = bytes(surface.makeImageSnapshot().encodeToData())
    images.append((size, png))
    if size == 256:
        (assets / 'vibepanel-icon.png').write_bytes(png)
header = struct.pack('<HHH', 0, 1, len(images))
offset = 6 + 16 * len(images)
entries, data = [], []
for size, png in images:
    entries.append(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(png), offset))
    data.append(png)
    offset += len(png)
(assets / 'vibepanel.ico').write_bytes(header + b''.join(entries) + b''.join(data))
print('Built 16, 24, 32, 48, 64, 128 and 256 px VP icons.')
# VBWR E
