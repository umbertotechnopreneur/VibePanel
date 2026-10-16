# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("chronicle")
        return
    c.backdrop("chronicle")
    c.header("OPENAI CODEX / CHRONICLE", extended=True)
    c.text(s.now.strftime("%H:%M"), 55, 203, 93, s.accent, 480, bold=True)
    c.text(s.chat_title, 60, 270, 28, width=485)
    c.text(s.status, 60, 320, 29, s.signal, 485)
    c.text(s.activity_detail(), 60, 361, 15, s.muted, 485)
    c.line(591, 93, 591, 396, s.fg, opacity=.14)
    c.label("ACTIVITY JOURNAL", 635, 111, 580)
    c.activity_rows(632, 147, 580, 61, 4)
    c.footer()
# VBWR E
