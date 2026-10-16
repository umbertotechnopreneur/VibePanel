# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("halo")
        return
    c.backdrop("halo")
    c.header("MEDIA / HALO")
    c.cover(490, 85, 252, circular=True)
    c.ring(616, 211, 143, s.progress, stroke=3)
    c.label(s.source, 44, 89, 386, s.accent)
    c.text(s.title, 44, 144, min(35, 47 / s.font_scale), width=386, bold=True)
    c.text(s.artist, 44, 184, min(21, 29 / s.font_scale), s.muted, 386)
    c.text(s.album, 44, 216, min(15, 20 / s.font_scale), s.muted, 386)
    c.playback_indicators(70, 254, 42)
    c.app_volume(44, 360, 235, stacked=True)
    c.text(s.timing(), 616, 405, min(23, 30 / s.font_scale), s.muted, 350, align="center")
    c.spectrum(804, 97, 408, 176)
    c.stereo_vu(804, 288, 408, 139)
    c.footer()
# VBWR E
