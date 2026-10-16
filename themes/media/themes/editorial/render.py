# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("editorial")
        return
    c.backdrop("editorial")
    c.header("MEDIA / EDITORIAL")
    c.rect(893, 95, 290, 290, s.palette[1], opacity=.45)
    c.cover(877, 79, 290, radius=1)
    c.media_text(56, 161, 745, 50)
    c.line(56, 302, 106, 302, s.accent, 4)
    c.label(s.source, 56, 341, 720)
    c.progress(56, 398, 735)
    c.vu(877, 409, 200, 18)
    c.footer()
# VBWR E
