# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("orbit")
        return
    c.backdrop("orbit")
    c.header("MEDIA / ORBIT")
    c.cover(136, 74, 332, circular=True)
    c.ring(302, 240, 181, s.progress, stroke=3, marker=True)
    c.label(s.source, 548, 85, 450, s.accent)
    c.text(s.title, 548, 155, min(38, 50 / s.font_scale), width=460, bold=True)
    c.text(s.artist, 548, 193, min(23, 29 / s.font_scale), s.muted, 664)
    c.text(s.album, 548, 222, min(15, 20 / s.font_scale), s.muted, 664)
    c.playback_indicators(1060, 76, 38)
    c.app_volume(548, 246, 235)
    c.spectrum(548, 261, 664, 90)
    c.stereo_vu(548, 361, 664, 78)
    c.text(s.timing(), 302, 437, min(16, 20 / s.font_scale), s.muted, 400, align="center")
    c.footer()
# VBWR E
