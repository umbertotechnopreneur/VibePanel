# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("prism")
        return
    c.backdrop("prism")
    c.header("MEDIA / PRISM")
    c.polygon([(394, 84), (649, 84), (484, 371), (394, 371)], s.palette[1], .08)
    c.cover(56, 86, 286, radius=2)
    c.media_text(400, 177, 812, 50)
    c.playback_indicators(80, 383, 36)
    c.app_volume(973, 295, 239)
    c.spectrum(400, 310, 812, 92)
    c.progress(400, 422, 812, 5, labels=False)
    c.caption(s.timing(), 1212, 470, width=340, align="right")
    c.footer(s.source, width=800)
# VBWR E
