# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("approval")
        return
    c.backdrop("approval")
    c.header("OPENAI CODEX / APPROVAL", extended=True)
    c.panel(80, 96, 1120, 300, radius=27)
    c.rect(80, 96, 1120, 300, s.signal, 27, .4 if s.approval else .1, 1)
    c.status_symbol(230, 245, 83)
    c.label(s.chat_title, 372, 153, 780)
    c.text(s.status, 372, 216, 48, s.signal, 780, bold=True)
    c.text(s.activity_detail(), 372, 282, 33, width=780)
    c.label(s.tool or s.event or "WAITING FOR EVENTS", 372, 349, 780)
    c.footer()
# VBWR E
