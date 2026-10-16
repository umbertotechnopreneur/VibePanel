# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("editorial")
        return
    c.backdrop("editorial")
    c.header("MEDIA / EDITORIAL")
    c.rect(893, 95, 290, 290, s.palette[1], opacity=.45)
    c.cover(877, 79, 290, radius=1)
    c.media_text(56, 161, 745, 50)
    c.playback_indicators(80, 274, 36)
    c.label(s.source, 343, 311, 448)
    c.stereo_vu(56, 341, 735, 87)
    c.app_volume(877, 415, 290)
    c.progress(56, 440, 735, 4, labels=False)
    c.text(s.timing(), 1167, 464, min(14, 18 / s.font_scale), s.muted, 340, align="right")
    c.footer(width=735)
# VBWR E
