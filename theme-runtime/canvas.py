# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Shared Skia primitives. Theme scripts own composition; expensive layers are cached."""
from __future__ import annotations

import base64
import hashlib
import math
from collections import OrderedDict
from datetime import datetime

import numpy as np
import skia

_CACHE = OrderedDict()
_COVERS = OrderedDict()
_FONTS = OrderedDict()
FALLBACK_PALETTE = ("#6677CC", "#A575BC", "#4AABB1", "#C8A48C")
SAMPLING = skia.SamplingOptions(skia.FilterMode.kLinear)
# Each Codex composition has its own color identity. Wallpaper samples tint these
# anchors gently rather than giving all ten themes the same four colors.
CODEX_IDENTITIES = {
    "beacon": ("#111629", "#F0F2FC", ("#7395FF", "#B694EC", "#4DBDCA", "#D8ADD9")),
    "timeline": ("#271C29", "#FFF9F2", ("#DB9D8D", "#BB9ADB", "#C9AA91", "#F2C5B4")),
    "terminal": ("#071713", "#EEF8EE", ("#56BC94", "#77BBA7", "#2D8174", "#A8C797")),
    "focus": ("#291D31", "#FFF1F5", ("#C394D8", "#EB96BA", "#A494D2", "#E6B4AA")),
    "pulse": ("#081B2C", "#EFFAFF", ("#46BEE7", "#466BC8", "#A27DE3", "#64D6C5")),
    "approval": ("#201329", "#FFF4E8", ("#9778BC", "#5F467D", "#CC9C83", "#754E85")),
    "flow": ("#261F36", "#FFFBF5", ("#A28BE6", "#F29D96", "#C1A7ED", "#F3BAAB")),
    "quiet": ("#102521", "#F1F7F2", ("#8BBDA8", "#75A6A4", "#D1CCAA", "#94A0C1")),
    "dispatch": ("#10242F", "#F0F5F4", ("#63A4B8", "#CF9270", "#597BA5", "#99C1B2")),
    "chronicle": ("#2B241E", "#FCF5E9", ("#C3A070", "#A9B797", "#C69079", "#869D9B")),
}
MEDIA_TREATMENTS = {
    "aurora": {"photo": True, "blur": 21, "dark": .24, "light": .48, "blob": .18},
    "editorial": {"photo": False, "blob": .50},
    "cinematic": {"photo": True, "blur": 5, "dark": .14, "light": .42, "blob": .05},
    "signal": {"photo": False, "blob": .27},
    "orbit": {"photo": True, "blur": 48, "dark": .28, "light": .44, "blob": .32},
    "prism": {"photo": False, "blob": .65},
    "ribbon": {"photo": False, "blob": .42},
    "nocturne": {"photo": True, "blur": 34, "dark": .34, "light": .50, "blob": .14},
    "halo": {"photo": False, "blob": .50},
    "duotone": {"photo": True, "blur": 40, "dark": .28, "light": .48, "blob": .25},
}


def cached(key, factory):
    if key in _CACHE:
        _CACHE.move_to_end(key)
        return _CACHE[key]
    value = factory()
    _CACHE[key] = value
    while len(_CACHE) > 12:
        _CACHE.popitem(last=False)
    return value


def rgb(value):
    value = str(value).lstrip("#")
    if len(value) != 6:
        return (102, 119, 204)
    try:
        return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))
    except ValueError:
        return (102, 119, 204)


def color(value, opacity=1):
    red, green, blue = rgb(value)
    return skia.ColorSetARGB(round(max(0, min(1, opacity)) * 255), red, green, blue)


def blend(a, b, amount):
    return "#" + "".join(f"{round(x + (y - x) * amount):02X}" for x, y in zip(rgb(a), rgb(b)))


def number(value, default=0):
    try:
        value = float(value)
        return value if math.isfinite(value) else default
    except (ValueError, TypeError):
        return default


def clip(value, low=0, high=1):
    return max(low, min(high, number(value)))


def clean(value, fallback=""):
    return " ".join(str(value or fallback).split())[:300]


def duration(seconds):
    value = max(0, int(number(seconds)))
    if value >= 3600:
        return f"{value // 3600}:{value // 60 % 60:02}:{value % 60:02}"
    return f"{value // 60}:{value % 60:02}"


def decoded_cover(media):
    key = clean(media.get("coverKey"))
    encoded = media.get("coverBase64")
    if "coverBase64" in media and encoded is None:
        return None, "", FALLBACK_PALETTE, FALLBACK_PALETTE[0]
    if encoded:
        if not isinstance(encoded, str) or len(encoded) > 16_000_000:
            raise ValueError("Cover payload is too large.")
        key = key or hashlib.sha256(encoded.encode("ascii")).hexdigest()
        if key not in _COVERS:
            raw = base64.b64decode(encoded, validate=True)
            image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
            if image is None or image.width() * image.height() > 16_777_216:
                raise ValueError("Cover is not a supported image or is too large.")
            ratio = min(1, 1024 / max(image.width(), image.height()))
            image = image.resize(max(1, round(image.width() * ratio)), max(1, round(image.height() * ratio)), SAMPLING)
            pixels = image.resize(16, 16, SAMPLING).toarray(colorType=skia.ColorType.kRGBA_8888_ColorType)
            palette = []
            for row, column in ((0, 0), (0, 8), (8, 0), (8, 8)):
                mean = pixels[row:row + 8, column:column + 8, :3].mean(axis=(0, 1))
                palette.append("#" + "".join(f"{round(float(channel)):02X}" for channel in mean))
            samples = pixels[:, :, :3].reshape(-1, 3).astype(np.uint16)
            opaque = pixels[:, :, 3].reshape(-1) >= 128
            samples = samples[opaque]
            dominant = palette[0]
            if len(samples):
                # Quantize to 8 bins/channel, then average the most populous cluster.
                bins = samples // 32
                keys = bins[:, 0] * 64 + bins[:, 1] * 8 + bins[:, 2]
                largest = int(np.bincount(keys, minlength=512).argmax())
                mean = samples[keys == largest].mean(axis=0)
                dominant = "#" + "".join(f"{round(float(channel)):02X}" for channel in mean)
            _COVERS[key] = (image, tuple(palette), dominant)
            while len(_COVERS) > 4:
                _COVERS.popitem(last=False)
    if key in _COVERS:
        _COVERS.move_to_end(key)
        image, palette, dominant = _COVERS[key]
        return image, key, palette, dominant
    return None, "", FALLBACK_PALETTE, FALLBACK_PALETTE[0]


class ThemeContext:
    def __init__(self, state, request, manifest):
        self.media = state.get("media") or {}
        self.audio = state.get("audio") or {}
        self.codex = state.get("codex") or {}
        self.desktop = state.get("desktop") or {}
        self.clock = state.get("clock") or {}
        self.light = bool(request.get("light"))
        self.font_family = clean(request.get("fontFamily"), "Cascadia Mono")
        self.font_scale = clip(request.get("fontScale", 1), .65, 1.6)
        self.ambient_drift = bool(request.get("ambientDrift"))
        self.category = manifest.get("category", "multimedia")
        self.style = str(manifest.get("id", "")).rsplit(".", 1)[-1]
        self.theme_directory = str(request.get("themePath", ""))
        self.preview = bool(state.get("preview") or self.codex.get("preview"))
        self.cover, self.cover_key, cover_palette, self.dominant = decoded_cover(self.media)
        desktop_palette = self.desktop.get("palette")
        desktop_palette = tuple(desktop_palette[:4]) if isinstance(desktop_palette, list) and desktop_palette else FALLBACK_PALETTE
        source_palette = cover_palette if self.category == "multimedia" and self.cover else desktop_palette
        self.palette = tuple(source_palette[i % len(source_palette)] for i in range(4))
        self.fg = "#182133" if self.light else "#F5F5FC"
        self.muted = "#586378" if self.light else "#ACB7CD"
        self.base = "#F2F1ED" if self.light else "#101522"
        if self.category == "codex":
            dark_base, light_base, anchors = CODEX_IDENTITIES.get(self.style, CODEX_IDENTITIES["beacon"])
            self.palette = tuple(blend(anchor, self.palette[index], .22) for index, anchor in enumerate(anchors))
            self.base = light_base if self.light else dark_base
            self.fg = blend("#172035" if self.light else "#F9F8FD", anchors[0], .10)
            self.muted = blend("#626471" if self.light else "#B9BBCD", anchors[2], .18)
        elif self.style in ("editorial", "orbit", "duotone"):
            self.base = "#FFF7ED" if self.light else blend("#101924", self.dominant, .18)
        self.accent = blend(self.palette[0], "#193B68" if self.light else "#CBEAFF", .42)
        self.title = clean(self.media.get("title"), "Nothing playing")
        self.artist = clean(self.media.get("artist"), "Waiting for a media session")
        self.album = clean(self.media.get("album"))
        self.source = clean(self.media.get("source"), "MEDIA")
        self.playing = bool(self.media.get("playing"))
        self.has_media = bool(self.media.get("source") or self.cover or self.playing or number(self.media.get("durationSeconds")) > 0)
        self.shuffle = self.media.get("shuffle") if isinstance(self.media.get("shuffle"), bool) else None
        self.position = max(0, number(self.media.get("positionSeconds")))
        self.length = max(0, number(self.media.get("durationSeconds")))
        self.progress = clip(self.position / self.length) if self.length else None
        self.volume = self.audio.get("volumePercent")
        self.peak = self.audio.get("peak")
        self.chat_title = clean(self.codex.get("chatTitle") or self.codex.get("chatName"), "OpenAI Codex")
        self.status = clean(self.codex.get("status"), "Waiting for Codex")
        self.last_status = self.status
        self.event = clean(self.codex.get("event"))
        self.phase = clean(self.codex.get("phase"))
        self.is_live = bool(self.codex.get("isLive"))
        self.stale = bool(self.codex.get("stale"))
        if not self.event and not self.preview:
            self.status = "Waiting for Codex"
            self.is_live = False
        elif self.stale:
            self.status = "Last activity"
            self.is_live = False
        elif self.event == "SessionEnd":
            self.status = "Session closed"
            self.is_live = False
        elif self.event == "Interrupt":
            self.status = "Interrupted"
            self.is_live = False
        self.tool = clean(self.codex.get("tool") or self.codex.get("toolKind"))
        self.model = clean(self.codex.get("model"))
        self.approval = self.event == "PermissionRequest" and self.is_live and not self.stale
        self.ready = self.event == "Stop" and not self.stale
        if self.approval:
            self.signal = "#9E5218" if self.light else "#FFC16E"
        elif self.ready:
            self.signal = "#26754D" if self.light else "#8DE2AE"
        elif self.event == "Interrupt" and not self.stale:
            self.signal = "#AA3950" if self.light else "#F7A4AC"
        elif not self.event or self.stale or self.event == "SessionEnd":
            self.signal = self.muted
        else:
            self.signal = self.accent
        try:
            self.now = datetime.fromisoformat(str(self.clock.get("iso", "")).replace("Z", "+00:00"))
        except ValueError:
            self.now = datetime.now().astimezone()

    def timing(self):
        return duration(self.position) + " / " + (duration(self.length) if self.length else "—")

    def activity_detail(self):
        if self.approval:
            return "Respond on your computer"
        if self.stale:
            return self.last_status + " · last received event"
        if not self.event:
            return "Connect the Codex hooks to see activity"
        if self.event == "SessionEnd":
            return "No active session"
        if self.event == "Interrupt":
            return "Work was interrupted"
        return self.tool or self.event

    def playback_status(self):
        return "PLAYING" if self.playing else "PAUSED" if self.has_media else "NO SESSION"

    def events(self):
        items = self.codex.get("events") or []
        items = [item for item in items if isinstance(item, dict)][-4:]
        if not items and self.event:
            items = [self.codex]
        return items


class Canvas:
    def __init__(self, native, width, height, context):
        self.native, self.ctx = native, context
        self.compact = width / height < 2.0
        self.width = 480 if self.compact else 1280
        self.height = 480
        native.clear(color(context.base))
        self.scale = min(width / self.width, height / self.height)
        native.save()
        native.translate((width - self.width * self.scale) / 2, (height - self.height * self.scale) / 2)
        native.scale(self.scale, self.scale)
        native.clipRect(skia.Rect.MakeWH(self.width, self.height))

    def finish(self):
        self.native.restore()

    def paint(self, tint, opacity=1, stroke=0):
        paint = skia.Paint(Color=color(tint, opacity), AntiAlias=True)
        if stroke:
            paint.setStyle(skia.Paint.kStroke_Style)
            paint.setStrokeWidth(stroke)
        return paint

    def rect(self, x, y, width, height, tint, radius=0, opacity=1, stroke=0):
        rect = skia.Rect.MakeXYWH(x, y, width, height)
        paint = self.paint(tint, opacity, stroke)
        if radius:
            self.native.drawRoundRect(rect, radius, radius, paint)
        else:
            self.native.drawRect(rect, paint)

    def line(self, x1, y1, x2, y2, tint, width=1, opacity=1):
        self.native.drawLine(x1, y1, x2, y2, self.paint(tint, opacity, width))

    def circle(self, x, y, radius, tint, opacity=1, stroke=0):
        self.native.drawCircle(x, y, radius, self.paint(tint, opacity, stroke))

    def polygon(self, points, tint, opacity=1):
        path = skia.Path()
        path.moveTo(*points[0])
        for point in points[1:]:
            path.lineTo(*point)
        path.close()
        self.native.drawPath(path, self.paint(tint, opacity))

    def gradient(self, x, y, width, height, colors, vertical=False, radius=0):
        end = (x, y + height) if vertical else (x + width, y)
        shader = skia.GradientShader.MakeLinear([(x, y), end], [color(c) for c in colors])
        paint = skia.Paint(Shader=shader, AntiAlias=True)
        self.native.drawRoundRect(skia.Rect.MakeXYWH(x, y, width, height), radius, radius, paint)

    def text(self, value, x, y, size=24, tint=None, width=None, align="left", bold=False):
        value = clean(value)
        if not value:
            return
        size *= self.ctx.font_scale
        key = (self.ctx.font_family, round(size, 2), bold)
        if key not in _FONTS:
            style = skia.FontStyle.Bold() if bold else skia.FontStyle.Normal()
            face = skia.Typeface.MakeFromName(self.ctx.font_family, style)
            face = face or skia.Typeface.MakeFromName("monospace", style)
            _FONTS[key] = skia.Font(face, size)
            while len(_FONTS) > 96:
                _FONTS.popitem(last=False)
        font = _FONTS[key]
        if width is not None and font.measureText(value) > width:
            left, right = 0, len(value)
            while left < right:
                mid = (left + right + 1) // 2
                if font.measureText(value[:mid] + "…") <= width:
                    left = mid
                else:
                    right = mid - 1
            value = value[:left] + "…"
        measured = font.measureText(value)
        if align == "right":
            x -= measured
        elif align == "center":
            x -= measured / 2
        if not self.ctx.light and self.ctx.category == "multimedia" and MEDIA_TREATMENTS.get(self.ctx.style, {}).get("photo"):
            self.native.drawString(value, x + 1, y + 2, font, self.paint("#000000", .55))
        self.native.drawString(value, x, y, font, self.paint(tint or self.ctx.fg))

    def label(self, value, x, y, width=None, tint=None):
        self.text(str(value).upper(), x, y, 13, tint or self.ctx.muted, width)

    def header(self, name=None, extended=False):
        if self.compact:
            self.label(name or self.ctx.category, 24, 28, 150)
            self.text(self.ctx.now.strftime("%H:%M"), 456, 28, 22, align="right")
            self.text(self.ctx.now.strftime("%d %b %Y"), 456, 54, 11, self.ctx.muted, align="right")
        else:
            self.label(name or self.ctx.category, 40, 35, 370)
            self.text(self.ctx.now.strftime("%H:%M"), 1240, 42, 28, align="right")
            date = self.ctx.now.strftime("%A %d %B %Y" if extended else "%d %b %Y")
            if str(self.ctx.clock.get("locale", "")).lower().startswith("it"):
                days = ("Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato", "Domenica")
                months = ("gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre")
                date = f"{self.ctx.now.day} {months[self.ctx.now.month - 1]} {self.ctx.now.year}"
                if extended:
                    date = days[self.ctx.now.weekday()] + " " + date
            date_right = 1240 - 170 * self.ctx.font_scale
            self.text(date.upper(), date_right, 39, 13, self.ctx.muted, date_right - 450, align="right")

    def backdrop(self, style="aurora", artwork=None):
        ctx = self.ctx
        treatment = MEDIA_TREATMENTS.get(style, {}) if ctx.category == "multimedia" else {}
        use_art = treatment.get("photo", False) if artwork is None else bool(artwork)
        use_art = bool(use_art and ctx.cover)
        key = ("background-v2", self.width, style, ctx.light, ctx.base, ctx.palette,
               ctx.cover_key if use_art else "", ctx.dominant if ctx.category == "multimedia" else "",
               ctx.signal if ctx.category == "codex" and (ctx.approval or ctx.ready) else "")
        def create():
            # Only cached ambience is half-resolution. Cover and glyphs stay sharp.
            full_width, full_height = self.width + 48, self.height + 48
            surface = skia.Surface(full_width // 2, full_height // 2)
            native = surface.getCanvas()
            native.clear(color(ctx.base))
            native.scale(.5, .5)
            if use_art:
                image = ctx.cover
                ratio = max(full_width / image.width(), full_height / image.height())
                crop_width, crop_height = full_width / ratio, full_height / ratio
                source = skia.Rect.MakeXYWH((image.width() - crop_width) / 2,
                    (image.height() - crop_height) / 2, crop_width, crop_height)
                paint = self.paint("#FFFFFF")
                paint.setImageFilter(skia.ImageFilters.Blur(treatment.get("blur", 25), treatment.get("blur", 25)))
                # Aspect-fill at full opacity; the earlier double-opacity layer hid it.
                native.drawImageRect(image, source, skia.Rect.MakeWH(full_width, full_height), SAMPLING, paint)
                native.drawColor(color(ctx.base, treatment.get("light" if ctx.light else "dark", .30)))
                if not ctx.light:
                    scrim = skia.GradientShader.MakeLinear([(0, 0), (0, full_height)],
                        [color("#08101C", .05), color("#08101C", .62 if style == "cinematic" else .23)])
                    native.drawPaint(skia.Paint(Shader=scrim))
            arrangements = {
                "aurora": ((.05, .12), (.75, -.1), (1.05, .85), (.35, 1.15)),
                "editorial": ((-.1, .92), (.75, 1.25), (1.05, .9), (.1, 1.4)),
                "orbit": ((-.05, .15), (.1, 1.05), (.84, .3), (.56, 1.4)),
                "prism": ((.1, .1), (.76, -.12), (1.05, .96), (.54, 1.3)),
                "signal": ((-.2, .3), (1.15, .6), (.5, 1.6), (.6, -.9)),
                "halo": ((.5, .5), (.5, -.3), (1.1, .7), (-.1, .9)),
                "beacon": ((-.12, .1), (.78, -.25), (.02, 1.1), (1.15, 1.1)),
                "timeline": ((-.1, .6), (.8, 1.2), (1.1, -.1), (.5, 1.4)),
                "terminal": ((-.3, -.5), (1.3, 1.4), (.0, 1.7), (1.3, -.5)),
                "focus": ((.5, 1.3), (1.1, .0), (-.15, .2), (.5, -.8)),
                "pulse": ((.16, .53), (.2, .5), (.35, .6), (.96, 1.5)),
                "approval": ((.1, .0), (.56, -.5), (1.04, .75), (.02, 1.1)),
                "flow": ((-.08, 1.1), (1.1, .9), (.0, -.2), (.9, 1.4)),
                "quiet": ((-.3, .8), (1.2, 1.2), (.5, -1.0), (1.3, -.5)),
                "dispatch": ((-.1, .15), (.85, 1.1), (1.0, -.4), (.1, 1.5)),
                "chronicle": ((-.15, .4), (.7, 1.4), (1.1, -.1), (.6, -1.0)),
            }
            positions = arrangements.get(style, ((.08, .23), (.74, .11), (.94, .88), (.39, .94)))
            strength = treatment.get("blob", .45 if ctx.light else .60)
            if style in ("quiet", "terminal"):
                strength = .25
            for index, (left, top) in enumerate(positions):
                shade = ctx.palette[index]
                radius = self.width * (.30 if index % 2 else .43)
                if style == "orbit" and index < 2:
                    shade = blend(ctx.dominant, "#FFF2D8", .62 if index == 0 else .30)
                    radius = self.width * .43
                shader = skia.GradientShader.MakeRadial((left * self.width, top * self.height), radius,
                    [color(shade, .78 if style == "orbit" and index < 2 else strength), color(shade, 0)])
                native.drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
            if use_art and style in ("aurora", "orbit", "nocturne") and not ctx.light:
                scrim = skia.GradientShader.MakeLinear([(self.width * .28, 0), (full_width, 0)],
                    [color("#07111D", 0), color("#07111D", .40)])
                native.drawPaint(skia.Paint(Shader=scrim))
            if style in ("prism", "duotone", "dispatch"):
                path = skia.Path()
                path.moveTo(self.width * .24, 0)
                path.lineTo(self.width * .63, self.height)
                path.lineTo(full_width, self.height * .20)
                path.lineTo(full_width, 0)
                path.close()
                native.drawPath(path, self.paint(ctx.palette[1], .17 if ctx.light else .24))
                path = skia.Path()
                path.moveTo(0, full_height)
                path.lineTo(self.width * .70, self.height * .18)
                path.lineTo(self.width * .44, full_height)
                path.close()
                native.drawPath(path, self.paint(ctx.fg, .065))
            if style in ("ribbon", "halo"):
                path = skia.Path()
                path.moveTo(-70, 420)
                path.cubicTo(350, -100, 850, 650, self.width + 100, 170)
                paint = self.paint(ctx.palette[1], .48, 94)
                paint.setImageFilter(skia.ImageFilters.Blur(28, 28))
                native.drawPath(path, paint)
            if style == "terminal":
                for column in range(0, full_width, 80):
                    native.drawLine(column, 0, column, full_height, self.paint(ctx.palette[0], .07, 1))
                for row in range(0, full_height, 60):
                    native.drawLine(0, row, full_width, row, self.paint(ctx.palette[0], .06, 1))
            if ctx.category == "codex" and (ctx.approval or ctx.ready):
                glow_color = "#FFA83D" if ctx.approval else "#57C88D"
                for left, top, radius in ((1.03, .24, .44), (-.10, .93, .35)):
                    shader = skia.GradientShader.MakeRadial((left * self.width, top * self.height), self.width * radius,
                        [color(glow_color, .58 if ctx.light else .72), color(glow_color, 0)])
                    native.drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
                if style == "approval":
                    for left, top, radius in ((.92, .25, 34), (.05, .80, 25), (.72, .08, 20), (1.02, .72, 42)):
                        paint = self.paint(glow_color, .55)
                        paint.setImageFilter(skia.ImageFilters.Blur(18, 18))
                        native.drawCircle(left * self.width, top * self.height, radius, paint)
            return surface.makeImageSnapshot()
        image = cached(key, create)
        # Translate one cached layer by <=1 pixel per second; no repeated blur.
        tick = int(self.ctx.now.timestamp()) % 48
        drift = (tick if tick <= 24 else 48 - tick) if ctx.ambient_drift else 12
        self.native.drawImageRect(image, skia.Rect.MakeXYWH(-drift, -12, self.width + 48, self.height + 48), SAMPLING)

    def panel(self, x, y, width, height, radius=24, opacity=None):
        opacity = opacity if opacity is not None else (.58 if self.ctx.light else .38)
        self.rect(x, y + 8, width, height, "#000000", radius, .06 if self.ctx.light else .12)
        self.rect(x, y, width, height, "#FFFFFF" if self.ctx.light else self.ctx.base, radius, opacity)
        self.rect(x, y, width, height, self.ctx.fg, radius, .19 if self.ctx.light else .15, 1)

    def glow(self, x, y, radius, tint=None, opacity=.45):
        tint = tint or self.ctx.signal
        size = max(2, int(radius * 2))
        key = ("glow", size, tint, opacity)
        def create():
            surface = skia.Surface(size // 2, size // 2)
            surface.getCanvas().clear(skia.ColorTRANSPARENT)
            shader = skia.GradientShader.MakeRadial((size / 4, size / 4), size / 4,
                [color(tint, opacity), color(tint, 0)])
            surface.getCanvas().drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
            return surface.makeImageSnapshot()
        self.native.drawImageRect(cached(key, create), skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), SAMPLING)

    def cover(self, x, y, size, radius=16, circular=False, shadow=True):
        if shadow:
            key = ("shadow", size, radius, circular, self.ctx.dominant)
            def create_shadow():
                surface = skia.Surface(int(size + 96), int(size + 96))
                surface.getCanvas().clear(skia.ColorTRANSPARENT)
                paint = self.paint(self.ctx.dominant, .65)
                paint.setImageFilter(skia.ImageFilters.Blur(16, 16))
                r = size / 2 if circular else radius
                surface.getCanvas().drawRoundRect(skia.Rect.MakeXYWH(48, 64, size, size), r, r, paint)
                return surface.makeImageSnapshot()
            self.native.drawImage(cached(key, create_shadow), x - 48, y - 48)
        rect = skia.Rect.MakeXYWH(x, y, size, size)
        r = size / 2 if circular else radius
        self.native.save()
        self.native.clipRRect(skia.RRect.MakeRectXY(rect, r, r), doAntiAlias=True)
        if self.ctx.cover:
            image = self.ctx.cover
            side = min(image.width(), image.height())
            source = skia.Rect.MakeXYWH((image.width() - side) / 2, (image.height() - side) / 2, side, side)
            self.native.drawImageRect(image, source, rect, SAMPLING)
        else:
            self.gradient(x, y, size, size, [self.ctx.palette[0], self.ctx.palette[2]], True)
            self.circle(x + size / 2, y + size / 2, size * .25, "#FFFFFF", .5, 2)
            self.circle(x + size / 2, y + size / 2, size * .05, "#FFFFFF", .7)
        self.native.restore()
        self.rect(x, y, size, size, self.ctx.fg, r, .16, 1)

    def progress(self, x, y, width, height=5, labels=True):
        self.rect(x, y, width, height, self.ctx.fg, height / 2, .14)
        if self.ctx.progress is not None:
            fill = width * self.ctx.progress
            if fill > .5:
                self.gradient(x, y, fill, height, [self.ctx.accent, blend(self.ctx.palette[1], self.ctx.fg, .35)], radius=height / 2)
        if labels:
            self.text(duration(self.ctx.position), x, y + 29, 14, self.ctx.muted)
            self.text(duration(self.ctx.length) if self.ctx.length else "—", x + width, y + 29, 14, self.ctx.muted, align="right")

    def vu(self, x, y, width=190, height=18, label=True):
        peak = self.ctx.peak
        if peak is None:
            self.text("LEVEL —", x, y + height, min(12, height), self.ctx.muted, width)
            if label:
                value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
                self.text(value, x + width + 16, y + height, 17, self.ctx.fg)
            return
        normalized = clip((20 * math.log10(max(.001, number(peak))) + 60) / 60) if peak is not None else 0
        count = max(8, min(48, int(width / 9)))
        for index in range(count):
            shade = blend(self.ctx.palette[2], "#E6B06F", index / max(1, count - 1))
            self.rect(x + index * width / count, y, width / count - 3, height, shade, 2,
                      1 if index / count < normalized else .18)
        if label:
            value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
            self.text(value, x + width + 16, y + height, 17, self.ctx.fg)

    def ring(self, x, y, radius, value=None, tint=None, stroke=4, marker=False):
        self.circle(x, y, radius, self.ctx.fg, .13, stroke)
        if value is not None and value > 0:
            paint = self.paint(tint or self.ctx.accent, 1, stroke)
            paint.setStrokeCap(skia.Paint.kRound_Cap)
            self.native.drawArc(skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), -90, 360 * clip(value), False, paint)
            if marker:
                angle = math.radians(-90 + 360 * clip(value))
                self.circle(x + math.cos(angle) * radius, y + math.sin(angle) * radius, 5.5, self.ctx.fg)

    def footer(self, text=None):
        text = text or ("DEMO DATA · PREVIEW" if self.ctx.preview else "VIBE PANEL")
        if self.ctx.preview and not text.startswith("DEMO DATA"):
            text = "DEMO DATA · PREVIEW · " + text
        if self.ctx.category == "multimedia" and self.ctx.shuffle is not None:
            text += " · SHUFFLE " + ("ON" if self.ctx.shuffle else "OFF")
        self.label(text, 24 if self.compact else 40, 461, self.width - 80)

    def media_text(self, x, y, width, title_size=44, album=True):
        # Keep a fixed-height title slot even with the font-size override.
        safe_title_size = min(title_size, 58 / self.ctx.font_scale)
        safe_artist_size = min(24, 32 / self.ctx.font_scale)
        self.label(self.ctx.playback_status(), x, y - 44, width, self.ctx.accent)
        self.text(self.ctx.title, x, y + 12, safe_title_size, width=width, bold=True)
        self.text(self.ctx.artist, x, y + 57, safe_artist_size, self.ctx.muted, width)
        if album and self.ctx.album:
            self.text(self.ctx.album, x, y + 92, 17, self.ctx.muted, width)

    def status_symbol(self, x, y, radius=60):
        self.circle(x, y, radius, self.ctx.signal, .13)
        self.circle(x, y, radius, self.ctx.signal, .7, 2)
        if self.ctx.approval:
            self.line(x, y - radius * .42, x, y + radius * .08, self.ctx.signal, 7)
            self.circle(x, y + radius * .38, 4, self.ctx.signal)
        elif self.ctx.ready:
            self.line(x - radius * .38, y, x - radius * .08, y + radius * .25, self.ctx.signal, 6)
            self.line(x - radius * .08, y + radius * .25, x + radius * .42, y - radius * .30, self.ctx.signal, 6)
        elif self.ctx.stale or not self.ctx.event or self.ctx.event in ("SessionEnd", "Interrupt"):
            self.line(x - radius * .26, y, x + radius * .26, y, self.ctx.signal, 4)
        else:
            for offset in (-.28, 0, .28):
                self.circle(x + radius * offset, y, 4, self.ctx.signal, .8)

    def activity_rows(self, x, y, width, row_height=64, limit=4):
        events = self.ctx.events()[-limit:]
        if not events:
            self.text("No activity received yet", x, y + 24, 19, self.ctx.muted, width)
            return
        for index, item in enumerate(events):
            top = y + index * row_height
            self.circle(x + 6, top + 8, 4, self.ctx.signal, 1 if index == len(events) - 1 else .4)
            if index < len(events) - 1:
                self.line(x + 6, top + 15, x + 6, top + row_height - 6, self.ctx.signal, 1, .3)
            self.text(item.get("status") or item.get("event"), x + 26, top + 15, 20, width=width - 35)
            detail = item.get("tool") or item.get("toolKind") or item.get("event")
            self.text(detail, x + 26, top + 38, 12, self.ctx.muted, width - 35)

    def compact_media(self, variant):
        self.backdrop(variant)
        self.header("MEDIA / " + variant)
        if variant in ("orbit", "halo"):
            self.cover(154, 69, 172, circular=True)
            self.ring(240, 155, 94, self.ctx.progress, stroke=3)
        elif variant in ("editorial", "duotone"):
            self.cover(280, 82, 156, radius=2)
            self.label(self.ctx.source, 24, 102, 230)
            self.text("NOW", 24, 151, 42, self.ctx.accent, 220, bold=True)
            self.text(self.ctx.playback_status(), 24, 190, 25, self.ctx.fg, 230)
        else:
            self.cover(154, 67, 172, radius=20 if variant != "prism" else 2)
        self.text(self.ctx.title, 24, 288, 27, width=432, bold=True)
        self.text(self.ctx.artist, 24, 322, 19, self.ctx.muted, 432)
        self.text(self.ctx.album, 24, 350, 13, self.ctx.muted, 432)
        self.progress(24, 379, 432, 5)
        self.vu(24, 428, 180, 13)
        self.footer()

    def compact_codex(self, variant):
        self.backdrop(variant)
        self.header("CODEX / " + variant)
        if variant in ("beacon", "pulse", "approval", "quiet"):
            self.status_symbol(240, 150, 64)
            self.text(self.ctx.status, 240, 261, 29, self.ctx.signal, 432, align="center", bold=True)
            self.text(self.ctx.chat_title, 240, 303, 20, width=432, align="center")
            self.text(self.ctx.activity_detail(), 240, 343, 14, self.ctx.muted, 432, align="center")
        else:
            self.text(self.ctx.chat_title, 24, 110, 25, width=432, bold=True)
            self.text(self.ctx.status, 24, 154, 25, self.ctx.signal, 432)
            self.activity_rows(24, 211, 420, 66, 3)
        self.line(24, 422, 456, 422, self.ctx.fg, opacity=.12)
        self.footer(self.ctx.activity_detail())
# VBWR E
