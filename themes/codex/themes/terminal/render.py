# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
def render(c, s):
    if c.compact:
        c.compact_codex("terminal")
        return
    c.backdrop("terminal")
    c.header("OPENAI CODEX / TERMINAL", extended=True)
    c.panel(42, 82, 1196, 337, radius=12)
    c.circle(67, 107, 4, s.palette[1])
    c.circle(84, 107, 4, s.palette[3])
    c.circle(101, 107, 4, s.palette[2])
    c.label(s.chat_title, 131, 112, 1035)
    c.line(43, 130, 1237, 130, s.fg, opacity=.1)
    c.text("> " + s.status, 68, 181, 31, s.signal, 1144, bold=True)
    c.label("LATEST EVENT", 68, 236, 200)
    c.text(s.event or "—", 294, 237, 19, width=880)
    c.label("TOOL", 68, 279, 200)
    c.text(s.tool or "—", 294, 280, 19, width=880)
    c.label("MODEL", 68, 322, 200)
    c.text(s.model or "—", 294, 323, 19, width=880)
    c.text(s.activity_detail(), 68, 385, 17, s.muted, 1140)
    c.footer()
# VBWR E
