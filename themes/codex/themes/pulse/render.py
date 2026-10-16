# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("pulse")
        return
    c.backdrop("pulse")
    c.header("OPENAI CODEX / PULSE", extended=True)
    # Concentric rings are an activity emblem, never fabricated progress.
    c.glow(260, 245, 188, s.signal, .34 if s.is_live or s.ready else .10)
    c.circle(260, 245, 151, s.signal, .08)
    c.circle(260, 245, 125, s.signal, .12, 1)
    c.circle(260, 245, 100, s.signal, .22, 1)
    c.status_symbol(260, 245, 69)
    c.panel(491, 115, 714, 265, radius=26)
    c.label(s.chat_title, 527, 155, 640)
    c.text(s.status, 527, 223, min(44, 58 / s.font_scale), s.signal, 640, bold=True)
    c.text(s.activity_detail(), 527, 279, c.text_size("body"), s.muted, 640)
    c.line(527, 313, 1168, 313, s.fg, opacity=.12)
    c.label(s.model or s.event or "WAITING FOR EVENTS", 527, 352, 640)
    c.footer()
# VBWR E
