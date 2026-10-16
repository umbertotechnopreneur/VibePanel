# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("flow")
        return
    c.backdrop("flow")
    c.header("OPENAI CODEX / FLOW", extended=True)
    c.text(s.chat_title, 48, 121, 28, width=1180, bold=True)
    c.text(s.status, 48, 175, 39, s.signal, 1180)
    events = s.events()
    if events:
        count = len(events)
        tile = 1160 / count
        for index, event in enumerate(events):
            x = 58 + index * tile
            c.line(x + 16, 248, x + tile - 30, 248, s.signal, 2, .3)
            c.circle(x + 16, 248, 8, s.signal, 1 if index == count - 1 else .4)
            c.label(str(index + 1).zfill(2), x, 218, tile - 30)
            c.text(event.get("status") or event.get("event"), x, 299, 23, width=tile - 26, bold=True)
            c.text(event.get("tool") or event.get("event"), x, 335, 14, s.muted, tile - 26)
    else:
        c.text("No activity received yet", 48, 282, 27, s.muted, 1180)
    c.line(48, 390, 1232, 390, s.fg, opacity=.1)
    c.text(s.activity_detail(), 48, 425, 18, s.muted, 1180)
    c.footer()
# VBWR E
