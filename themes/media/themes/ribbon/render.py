# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("ribbon")
        return
    c.backdrop("ribbon")
    c.header("MEDIA / RIBBON")
    c.panel(80, 114, 1120, 243, radius=35)
    c.cover(105, 138, 194, radius=24)
    c.media_text(340, 181, 810, 43, album=False)
    c.label(s.album or s.source, 340, 300, 780)
    c.progress(105, 396, 690, 5)
    c.vu(900, 395, 216, 20)
    c.footer()
# VBWR E
