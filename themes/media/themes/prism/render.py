# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("prism")
        return
    c.backdrop("prism")
    c.header("MEDIA / PRISM")
    c.polygon([(394, 84), (649, 84), (484, 371), (394, 371)], s.palette[1], .08)
    c.cover(56, 86, 286, radius=2)
    c.media_text(400, 177, 812, 50)
    c.label(s.source, 400, 324, 720)
    c.progress(56, 410, 810, 5)
    c.vu(957, 405, 205, 22)
    c.footer()
# VBWR E
