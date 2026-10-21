# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("cinematic")
        return
    c.backdrop("cinematic", artwork=True)
    c.header("MEDIA / CINEMATIC")
    c.cover(528, 58, 224, radius=11)
    c.playback_indicators(68, 83, 40)
    c.spectrum(40, 166, 400, 108)
    c.app_volume(840, 96, 385)
    c.stereo_vu(840, 137, 400, 135)
    c.text(s.title, 640, 332, min(43, 52 / s.font_scale), width=1160, align="center", bold=True)
    c.text(s.artist, 640, 373, min(24, 30 / s.font_scale), s.muted, 1100, align="center")
    c.progress(40, 405, 1200, 5, labels=False)
    c.caption(s.timing(), 640, 436, width=600, align="center")
    c.footer(s.source)
# VBWR E
