# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("aurora")
        return
    c.backdrop("aurora", artwork=True)
    c.header("MEDIA / AURORA GLASS")
    c.cover(48, 72, 300, radius=16)
    c.media_text(398, 192, 818, 49)
    c.progress(48, 401, 1180, 8, labels=False)
    c.text(s.timing(), 48, 437, 15, s.muted, 600)
    c.vu(930, 427, 225, 18)
    c.footer(s.source)
# VBWR E
