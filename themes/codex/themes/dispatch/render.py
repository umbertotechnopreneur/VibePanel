# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("dispatch")
        return
    c.backdrop("dispatch")
    c.header("OPENAI CODEX / DISPATCH", extended=True)
    c.panel(40, 86, 748, 329, radius=22)
    c.label("CURRENT CHAT", 73, 125, 680)
    c.text(s.chat_title, 73, 182, 36, width=680, bold=True)
    c.text(s.status, 73, 256, 43, s.signal, 680, bold=True)
    c.text(s.activity_detail(), 73, 303, 19, s.muted, 680)
    c.label(s.model or s.phase or s.event, 73, 375, 680)
    c.panel(810, 86, 430, 153, radius=22)
    c.label("LATEST EVENT", 839, 124, 365)
    c.text(s.event or "—", 839, 170, 23, width=365)
    c.text(s.tool, 839, 207, 16, s.muted, 365)
    c.panel(810, 261, 430, 154, radius=22)
    c.label("RECEIVED ACTIVITY", 839, 300, 365)
    c.activity_rows(835, 323, 365, 61, 1)
    c.footer()
# VBWR E
