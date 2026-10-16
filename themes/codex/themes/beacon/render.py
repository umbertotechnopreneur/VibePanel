# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("beacon")
        return
    c.backdrop("beacon")
    c.header("OPENAI CODEX / BEACON", extended=True)
    c.status_symbol(194, 242, 104)
    c.label(s.chat_title, 365, 139, 832, s.accent)
    c.text(s.status, 365, 229, 58, s.signal, 830, bold=True)
    c.text(s.activity_detail(), 365, 286, 24, s.muted, 830)
    c.line(365, 330, 1190, 330, s.fg, opacity=.12)
    c.text(s.model or s.phase, 365, 367, 17, s.muted, 830)
    c.footer("DEMO DATA · PREVIEW" if s.preview else "VIBE PANEL · OPENAI CODEX")
# VBWR E
