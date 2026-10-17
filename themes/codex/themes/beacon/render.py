# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("beacon")
        return
    c.backdrop("beacon")
    c.header("OPENAI CODEX / BEACON", extended=True)
    c.status_symbol(194, 242, 104)
    c.label(s.chat_title, 365, 139, 832, s.accent)
    c.text(s.status, 365, 229, min(58, 74 / s.font_scale), s.signal, 830, bold=True)
    c.text(s.activity_detail(), 365, 291, min(24, 32 / s.font_scale), s.muted, 830)
    c.line(365, 338, 1190, 338, s.fg, opacity=.12)
    c.caption(s.model or s.phase, 365, 376, s.muted, 830)
    c.footer("DEMO DATA · PREVIEW" if s.preview else "VIBE PANEL · OPENAI CODEX")
# VBWR E
