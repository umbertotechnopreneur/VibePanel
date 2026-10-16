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
    c.label(s.source, 505, 123, 710, s.accent)
    c.text(s.title, 505, 201, 53, width=710, bold=True)
    c.text(s.artist, 505, 255, 26, s.muted, 710)
    c.text(s.album, 505, 294, 17, s.muted, 710)
    c.progress(505, 343, 710)
    c.vu(505, 398, 610, 17)
    c.footer()
# VBWR E
