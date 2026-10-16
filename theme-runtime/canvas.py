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
        self.theme_directory = str(request.get("themePath", ""))
        self.preview = bool(state.get("preview") or self.codex.get("preview"))
        self.cover, self.cover_key, cover_palette, self.dominant = decoded_cover(self.media)
        desktop_palette = self.desktop.get("palette")
        desktop_palette = tuple(desktop_palette[:4]) if isinstance(desktop_palette, list) and desktop_palette else FALLBACK_PALETTE
        self.palette = cover_palette if self.category == "multimedia" and self.cover else desktop_palette
        self.palette = tuple(self.palette[i % len(self.palette)] for i in range(4))
        self.fg = "#182133" if self.light else "#F5F5FC"
        self.muted = "#586378" if self.light else "#ACB7CD"
        self.base = "#F2F1ED" if self.light else "#101522"
        self.accent = blend(self.palette[0], "#193B68" if self.light else "#CBEAFF", .42)
        self.title = clean(self.media.get("title"), "Nothing playing")
        self.artist = clean(self.media.get("artist"), "Waiting for a media session")
        self.album = clean(self.media.get("album"))
        self.source = clean(self.media.get("source"), "MEDIA")
        self.playing = bool(self.media.get("playing"))
        self.shuffle = self.media.get("shuffle") if isinstance(self.media.get("shuffle"), bool) else None
        self.position = max(0, number(self.media.get("positionSeconds")))
        self.length = max(0, number(self.media.get("durationSeconds")))
        self.progress = clip(self.position / self.length) if self.length else None
        self.volume = self.audio.get("volumePercent")
        self.peak = self.audio.get("peak")
        self.chat_title = clean(self.codex.get("chatTitle") or self.codex.get("chatName"), "OpenAI Codex")
        self.status = clean(self.codex.get("status"), "Waiting for Codex")
        self.event = clean(self.codex.get("event"))
        self.phase = clean(self.codex.get("phase"))
        self.is_live = bool(self.codex.get("isLive"))
        self.stale = bool(self.codex.get("stale"))
        self.tool = clean(self.codex.get("tool") or self.codex.get("toolKind"))
        self.model = clean(self.codex.get("model"))
        self.approval = self.event == "PermissionRequest" and self.is_live and not self.stale
        self.ready = self.event == "Stop" and not self.stale
        self.signal = ("#B7691F" if self.light else "#FFBD75") if self.approval else self.accent
        try:
            self.now = datetime.fromisoformat(str(self.clock.get("iso", "")).replace("Z", "+00:00"))
        except ValueError:
            self.now = datetime.now().astimezone()

    def timing(self):
        return duration(self.position) + " / " + (duration(self.length) if self.length else "—")

    def activity_detail(self):
        if self.preview:
            return "DEMO DATA · local preview"
        if self.approval:
            return "Respond on your computer"
        if self.stale:
            return "Last received activity · connection may be idle"
        if not self.event:
            return "Connect the Codex hooks to see activity"
        return self.tool or self.event

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
            self.text(date.upper(), 1120, 39, 13, self.ctx.muted, 670, align="right")

    def backdrop(self, style="aurora", artwork=False):
        ctx = self.ctx
        key = ("background", self.width, style, ctx.light, ctx.palette, ctx.cover_key if artwork else "")
        def create():
            surface = skia.Surface(self.width + 48, self.height + 48)
            native = surface.getCanvas()
            native.clear(color(ctx.base))
            if artwork and ctx.cover:
                paint = self.paint(ctx.base, .20 if ctx.light else .32)
                paint.setImageFilter(skia.ImageFilters.Blur(30, 30))
                native.drawImageRect(ctx.cover, skia.Rect.MakeWH(self.width + 48, self.height + 48), SAMPLING, paint)
                native.drawColor(color(ctx.base, .58 if ctx.light else .46))
            positions = ((.08, .23), (.74, .11), (.94, .88), (.39, .94))
            for index, (left, top) in enumerate(positions):
                shade = ctx.palette[index]
                radius = self.width * (.38 if index % 2 else .48)
                shader = skia.GradientShader.MakeRadial((left * self.width, top * self.height), radius,
                    [color(shade, .30 if ctx.light else .40), color(shade, 0)])
                native.drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
            if style in ("prism", "duotone", "dispatch"):
                path = skia.Path()
                path.moveTo(self.width * .3, 0)
                path.lineTo(self.width * .73, self.height)
                path.lineTo(self.width, self.height * .24)
                path.lineTo(self.width, 0)
                path.close()
                native.drawPath(path, self.paint(ctx.fg, .045))
            if style in ("ribbon", "flow", "halo"):
                path = skia.Path()
                path.moveTo(-70, 420)
                path.cubicTo(350, -100, 850, 650, self.width + 100, 170)
                paint = self.paint(ctx.palette[1], .25, 86)
                paint.setImageFilter(skia.ImageFilters.Blur(28, 28))
                native.drawPath(path, paint)
            return surface.makeImageSnapshot()
        image = cached(key, create)
        # Translate one cached layer by <=1 pixel per second; no repeated blur.
        tick = int(self.ctx.now.timestamp()) % 48
        drift = (tick if tick <= 24 else 48 - tick) if ctx.ambient_drift else 12
        self.native.drawImage(image, -drift, -12)

    def panel(self, x, y, width, height, radius=24):
        self.rect(x, y, width, height, "#FFFFFF" if self.ctx.light else "#0B1020", radius, .55)
        self.rect(x, y, width, height, self.ctx.fg, radius, .12, 1)

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
        normalized = clip((20 * math.log10(max(.001, number(peak))) + 60) / 60) if peak is not None else 0
        count = max(8, min(48, int(width / 9)))
        for index in range(count):
            shade = blend(self.ctx.palette[2], "#E6B06F", index / max(1, count - 1))
            self.rect(x + index * width / count, y, width / count - 3, height, shade, 2,
                      1 if index / count < normalized else .18)
        if label:
            value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
            self.text(value, x + width + 16, y + height, 17, self.ctx.fg)

    def ring(self, x, y, radius, value=None, tint=None, stroke=4):
        self.circle(x, y, radius, self.ctx.fg, .13, stroke)
        if value is not None and value > 0:
            paint = self.paint(tint or self.ctx.accent, 1, stroke)
            paint.setStrokeCap(skia.Paint.kRound_Cap)
            self.native.drawArc(skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), -90, 360 * clip(value), False, paint)

    def footer(self, text=None):
        text = text or ("DEMO DATA · PREVIEW" if self.ctx.preview else "VIBE PANEL")
        if self.ctx.preview and not text.startswith("DEMO DATA"):
            text = "DEMO DATA · PREVIEW · " + text
        if self.ctx.category == "multimedia" and self.ctx.shuffle is not None:
            text += " · SHUFFLE " + ("ON" if self.ctx.shuffle else "OFF")
        self.label(text, 24 if self.compact else 40, 461, self.width - 80)

    def media_text(self, x, y, width, title_size=44, album=True):
        self.label("PLAYING" if self.ctx.playing else "PAUSED" if self.ctx.media.get("title") else "MEDIA", x, y - 35, width, self.ctx.accent)
        self.text(self.ctx.title, x, y + 12, title_size, width=width, bold=True)
        self.text(self.ctx.artist, x, y + 57, 24, self.ctx.muted, width)
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
        self.backdrop(variant, artwork=variant in ("aurora", "cinematic", "nocturne"))
        self.header("MEDIA / " + variant)
        if variant in ("orbit", "halo"):
            self.cover(154, 69, 172, circular=True)
            self.ring(240, 155, 94, self.ctx.progress, stroke=3)
        elif variant in ("editorial", "duotone"):
            self.cover(280, 82, 156, radius=2)
            self.label(self.ctx.source, 24, 102, 230)
            self.text("NOW", 24, 151, 42, self.ctx.accent, 220, bold=True)
            self.text("PLAYING" if self.ctx.playing else "PAUSED", 24, 190, 25, self.ctx.fg, 230)
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
