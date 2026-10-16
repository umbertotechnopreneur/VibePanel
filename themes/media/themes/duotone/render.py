# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("duotone")
        return
    c.backdrop("duotone")
    c.rect(0, 65, 460, 365, s.palette[0], opacity=.25)
    c.rect(460, 65, 820, 365, s.base, opacity=.4)
    c.header("MEDIA / DUOTONE")
    c.cover(87, 105, 280, radius=0)
    c.label(s.source, 505, 123, 500, s.accent)
    c.playback_indicators(1060, 76, 38)
    c.text(s.title, 505, 201, min(53, 64 / s.font_scale), width=710, bold=True)
    c.text(s.artist, 505, 255, min(26, 34 / s.font_scale), s.muted, 710)
    c.caption(s.album, 505, 294, width=710)
    c.spectrum(505, 327, 710, 99)
    c.app_volume(87, 420, 280)
    c.progress(505, 438, 710, 4, labels=False)
    c.caption(s.timing(), 1215, 470, width=340, align="right")
    c.footer(width=800)
# VBWR E
