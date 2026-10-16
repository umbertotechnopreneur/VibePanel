# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("halo")
        return
    c.backdrop("halo")
    c.header("MEDIA / HALO")
    c.cover(490, 115, 252, circular=True)
    c.ring(616, 241, 143, s.progress, stroke=3)
    c.label("NOW PLAYING" if s.playing else "MEDIA", 44, 155, 390, s.accent)
    c.text(s.title, 44, 208, 36, width=386, bold=True)
    c.text(s.artist, 44, 251, 21, s.muted, 386)
    c.text(s.album, 44, 285, 15, s.muted, 386)
    c.label("PLAYBACK POSITION", 816, 153, 385)
    c.text(s.timing(), 816, 204, 28, width=385)
    c.label("OUTPUT LEVEL", 816, 270, 385)
    c.vu(816, 290, 292, 22)
    c.label(s.source, 816, 365, 385)
    c.footer()
# VBWR E
