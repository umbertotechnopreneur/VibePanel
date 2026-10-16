# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("orbit")
        return
    c.backdrop("orbit")
    c.header("MEDIA / ORBIT")
    c.cover(136, 74, 332, circular=True)
    c.ring(302, 240, 181, s.progress, stroke=3, marker=True)
    c.media_text(548, 188, 664, 42)
    c.label(s.source, 548, 337, 664)
    c.text(s.timing(), 302, 443, 16, s.muted, 400, align="center")
    c.vu(548, 375, 570, 25)
    c.footer()
# VBWR E
