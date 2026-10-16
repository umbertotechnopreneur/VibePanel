# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("quiet")
        return
    c.backdrop("quiet")
    c.header("OPENAI CODEX / QUIET", extended=True)
    c.status_symbol(147, 246, 46)
    c.text(s.status, 245, 228, 56, s.signal, 939, bold=True)
    c.text(s.chat_title, 245, 284, 27, width=939)
    c.text(s.activity_detail(), 245, 329, 18, s.muted, 939)
    c.line(245, 371, 344, 371, s.signal, 2, .5)
    c.footer()
# VBWR E
