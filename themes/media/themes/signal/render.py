# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("signal")
        return
    c.backdrop("signal")
    c.header("MEDIA / SIGNAL")
    c.panel(40, 82, 1200, 326, radius=18)
    c.cover(68, 110, 180, radius=8)
    c.media_text(281, 145, 680, 38, album=False)
    c.label(s.album or s.source, 281, 248, 650)
    c.label("OUTPUT LEVEL · READ ONLY", 68, 329, 450)
    c.vu(68, 351, 1020, 27)
    c.rect(1016, 110, 191, 180, s.base, radius=14, opacity=.7)
    c.label("POSITION", 1038, 144, 154)
    c.text(s.timing(), 1038, 180, 18, width=152)
    c.label("SOURCE", 1038, 229, 154)
    c.text(s.source, 1038, 258, 16, width=152)
    c.progress(40, 432, 1200, labels=False)
    c.footer()
# VBWR E
