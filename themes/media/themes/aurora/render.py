# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("aurora")
        return
    c.backdrop("aurora", artwork=True)
    c.header("MEDIA / AURORA GLASS")
    c.cover(48, 80, 280, radius=18)
    c.media_text(376, 173, 820, 48)
    c.progress(48, 401, 1180, 7)
    c.vu(930, 428, 225, 16)
    c.footer(s.source)
# VBWR E
