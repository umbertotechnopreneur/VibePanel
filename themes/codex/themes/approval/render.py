# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("approval")
        return
    c.backdrop("approval")
    c.header("OPENAI CODEX / APPROVAL", extended=True)
    c.panel(134, 92, 1008, 302, radius=25, opacity=.42 if not s.light else .64)
    c.rect(134, 92, 1008, 302, s.signal, 25, .46 if s.approval else .18, 1)
    c.glow(277, 244, 137, s.signal, .32 if s.approval or s.ready else .12)
    c.status_symbol(277, 244, 77)
    c.label(s.chat_title, 420, 143, 678)
    c.text(s.status, 420, 211, min(43, 58 / s.font_scale), s.signal, 678, bold=True)
    c.text(s.activity_detail(), 420, 278, min(35, 45 / s.font_scale), width=678)
    c.label(s.tool or s.event or "WAITING FOR EVENTS", 420, 353, 678)
    c.footer()
# VBWR E
