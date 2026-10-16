# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("focus")
        return
    c.backdrop("focus")
    c.header("OPENAI CODEX / FOCUS", extended=True)
    c.text(s.chat_title.upper(), 640, 142, 15, s.accent, 1080, align="center")
    c.text(s.status, 640, 251, 70, s.signal, 1160, align="center", bold=True)
    c.text(s.activity_detail(), 640, 314, 24, s.muted, 1120, align="center")
    c.line(572, 368, 708, 368, s.signal, 3, .6)
    c.text(s.tool or s.event or "No activity received yet", 640, 408, 17, s.muted, 1120, align="center")
    c.footer()
# VBWR E
