# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_media("cinematic")
        return
    c.backdrop("cinematic", artwork=True)
    c.header("MEDIA / CINEMATIC")
    c.cover(528, 58, 224, radius=11)
    c.text(s.title, 640, 332, min(48, 62 / s.font_scale), width=1160, align="center", bold=True)
    c.text(s.artist, 640, 373, min(24, 30 / s.font_scale), s.muted, 1100, align="center")
    c.label(s.playback_status(), 40, 387, 270, s.accent)
    c.progress(40, 405, 1200, 5, labels=False)
    c.text(s.timing(), 40, 437, 14, s.muted, 600)
    c.vu(981, 425, 174, 17)
    c.footer(s.source)
# VBWR E
