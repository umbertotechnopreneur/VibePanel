# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("nocturne")
        return
    c.backdrop("nocturne", artwork=True)
    c.header("MEDIA / NOCTURNE")
    c.rect(0, 66, 1280, 365, s.base, opacity=.3)
    c.cover(70, 100, 265, radius=12)
    c.label(s.playback_status() + " · " + s.source, 397, 124, 800, s.accent)
    c.text(s.title, 397, 197, min(46, 58 / s.font_scale), width=798, bold=True)
    c.text(s.artist, 397, 246, min(25, 32 / s.font_scale), s.muted, 798)
    c.caption(s.album, 397, 284, width=798)
    c.playback_indicators(90, 383, 36)
    c.app_volume(283, 397, 110, stacked=True)
    c.stereo_vu(397, 321, 798, 104)
    c.progress(397, 433, 798, 3, labels=False)
    c.caption(s.timing(), 1195, 470, width=340, align="right")
    c.footer(width=800)
# VBWR E
