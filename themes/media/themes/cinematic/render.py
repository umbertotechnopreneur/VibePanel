# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("cinematic")
        return
    c.backdrop("cinematic", artwork=True)
    c.header("MEDIA / CINEMATIC")
    c.panel(40, 110, 1200, 245, radius=28)
    c.cover(65, 135, 195, radius=12)
    c.media_text(299, 181, 896, 51)
    c.label(s.source, 299, 322, 750)
    c.progress(65, 390, 820, 6)
    c.vu(961, 393, 187, 24)
    c.footer()
# VBWR E
