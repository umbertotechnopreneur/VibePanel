# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("orbit")
        return
    c.backdrop("orbit")
    c.header("MEDIA / ORBIT")
    c.cover(86, 87, 300, circular=True)
    c.ring(236, 237, 163, s.progress, stroke=4)
    c.media_text(470, 176, 734, 43)
    c.label(s.source, 470, 329, 720)
    c.text(s.timing(), 236, 438, 16, s.muted, 385, align="center")
    c.vu(470, 382, 620, 26)
    c.footer()
# VBWR E
