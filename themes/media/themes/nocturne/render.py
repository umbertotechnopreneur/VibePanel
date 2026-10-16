# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("nocturne")
        return
    c.backdrop("nocturne", artwork=True)
    c.header("MEDIA / NOCTURNE")
    c.rect(0, 66, 1280, 365, s.base, opacity=.3)
    c.cover(70, 100, 265, radius=12)
    c.label(s.playback_status() + " · " + s.source, 397, 124, 800, s.accent)
    c.text(s.title, 397, 197, 46, width=798, bold=True)
    c.text(s.artist, 397, 246, 25, s.muted, 798)
    c.text(s.album, 397, 284, 16, s.muted, 798)
    c.progress(397, 344, 798, 3)
    c.vu(70, 406, 180, 12)
    c.footer()
# VBWR E
