# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Shared Skia primitives. Theme scripts own composition; expensive layers are cached."""
from __future__ import annotations

import base64
import hashlib
import math
import time
from collections import OrderedDict
from datetime import datetime
from pathlib import Path

import numpy as np
import skia

from color_adjustments import ColorAdjustments
from palette import ThemePalette

_CACHE = OrderedDict()
_COVERS = OrderedDict()
_FONTS = OrderedDict()
_METER_HISTORY = OrderedDict()
_ACTIVE_THEME_TIMING = None
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
SYSTEM_IDENTITIES = {
    "signal": ("#0D161C", "#F1F6F2", ("#7BE5C0", "#76D5E9", "#B6CF94", "#92A5B7")),
    "ledger": ("#25221D", "#F6F2E8", ("#DA986D", "#98B699", "#B4A290", "#D2B98B")),
    "orbit": ("#091D26", "#EEF7F4", ("#73DCE0", "#A7E0B5", "#D8B88B", "#93B8C2")),
    "terminal": ("#07150D", "#F0F6E9", ("#91DD8B", "#B4DCA0", "#69BDA0", "#94B584")),
    "thermal": ("#25150F", "#FFF0E6", ("#FF9F67", "#F7C16E", "#CB9680", "#C6C399")),
    "blueprint": ("#092849", "#EDF5FB", ("#8EDAF2", "#A8C9F9", "#90D8C4", "#A8BBD1")),
    "horizon": ("#211D2D", "#F8F1EA", ("#E6AEA1", "#9BC6BA", "#C7B5D8", "#D5BE9F")),
    "blocks": ("#111B38", "#F5F1E7", ("#7191FF", "#F4CF4D", "#A0CBB8", "#C3BDDD")),
    "quiet": ("#18231F", "#F3F5EF", ("#A6C3AE", "#9CBAB9", "#C6C8AF", "#ADB6B0")),
    "pulse": ("#090D15", "#F5F3F8", ("#63D8EA", "#EC97D4", "#B6C59B", "#A8AFC6")),
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


def progress_color(tint, fraction, light=False):
    """Keep the theme's low end, then move through amber to red at full scale."""
    amount = clip(fraction)
    warm = blend(tint, "#AE6A16" if light else "#F4B557", .8)
    red = "#C73542" if light else "#FF5963"
    return blend(tint, warm, amount / .6) if amount <= .6 else blend(warm, red, (amount - .6) / .4)


def clean(value, fallback=""):
    return " ".join(str(value or fallback).split())[:300]


def duration(seconds):
    value = max(0, int(number(seconds)))
    if value >= 3600:
        return f"{value // 3600}:{value // 60 % 60:02}:{value % 60:02}"
    return f"{value // 60}:{value % 60:02}"


def optional_number(value):
    if value is None or isinstance(value, bool):
        return None
    try:
        result = float(value)
        return result if math.isfinite(result) else None
    except (TypeError, ValueError):
        return None


def dbfs(value):
    result = optional_number(value)
    return max(-120, min(0, result)) if result is not None else None


def db_label(value):
    return "—" if value is None else f"{value:.1f}"


class AudioAnalysis:
    """Measured system-output data, with bounded display ballistics only.

    This renderer does not capture audio or compute an FFT. Values and holds come
    from the native provider. The smoother follows actual values; it never creates
    random bands, waves, peaks, or a spectrum from a session's volume percentage.
    """
    def __init__(self, raw):
        raw = raw if isinstance(raw, dict) else {}
        self.available = raw.get("available") is True and raw.get("source") == "system-output"
        self.status = clean(raw.get("status"), "Audio analysis unavailable")
        self.sample_rate = max(0, int(number(raw.get("sampleRate"))))
        self.fft_size = max(0, int(number(raw.get("fftSize"))))
        self.channels = max(0, int(number(raw.get("channels"))))
        self.updated_at = clean(raw.get("updatedAtUtc"))
        self.source = "system-output"
        self.bands, self.frequencies = [], []
        values = raw.get("bandDbfs")
        frequencies = raw.get("frequenciesHz")
        if self.available and isinstance(values, list) and isinstance(frequencies, list) and len(values) == len(frequencies) == 32:
            measured = [dbfs(value) for value in values]
            centers = [optional_number(value) for value in frequencies]
            if all(value is not None for value in measured) and all(value is not None and value > 0 for value in centers):
                if all(centers[index] < centers[index + 1] for index in range(31)):
                    self.bands, self.frequencies = measured, centers
        self.rms = [dbfs(raw.get("leftRmsDbfs")), dbfs(raw.get("rightRmsDbfs"))]
        self.peaks = [dbfs(raw.get("leftPeakDbfs")), dbfs(raw.get("rightPeakDbfs"))]
        self.holds = [dbfs(raw.get("leftPeakHoldDbfs")), dbfs(raw.get("rightPeakHoldDbfs"))]
        if self.channels < 2:
            self.rms[1], self.peaks[1], self.holds[1] = None, None, None
        if not self.available:
            self.rms, self.peaks, self.holds = [None, None], [None, None], [None, None]
        self.display_bands = list(self.bands)
        self.display_rms = list(self.rms)
        self.display_peaks = list(self.peaks)
        key = (self.source, self.sample_rate, self.fft_size, self.channels)
        if not self.available:
            _METER_HISTORY.clear()
            return
        now = time.monotonic()
        previous = _METER_HISTORY.get(key)
        elapsed = now - previous["at"] if previous else 0
        if previous and 0 < elapsed < 2:
            def follow(values, old, attack, release):
                if len(values) != len(old):
                    return list(values)
                result = []
                for target, current in zip(values, old):
                    if target is None or current is None:
                        result.append(target)
                    else:
                        tau = attack if target > current else release
                        result.append(current + (target - current) * (1 - math.exp(-elapsed / tau)))
                return result
            self.display_bands = follow(self.bands, previous["bands"], .025, .32)
            self.display_rms = follow(self.rms, previous["rms"], .055, .36)
            self.display_peaks = follow(self.peaks, previous["peaks"], .008, .45)
        _METER_HISTORY[key] = {"at": now, "bands": self.display_bands,
                               "rms": self.display_rms, "peaks": self.display_peaks}
        _METER_HISTORY.move_to_end(key)
        while len(_METER_HISTORY) > 4:
            _METER_HISTORY.popitem(last=False)

    def caption(self):
        parts = ["SYSTEM OUTPUT"]
        if self.sample_rate:
            parts.append(f"{self.sample_rate / 1000:g} kHz")
        if self.fft_size:
            parts.append(f"FFT {self.fft_size}")
        return " · ".join(parts)


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
        global _ACTIVE_THEME_TIMING

        self.media = state.get("media") or {}
        self.audio = state.get("audio") or {}
        self.codex = state.get("codex") or {}
        self.sensors = state.get("sensors") if isinstance(state.get("sensors"), dict) else {}
        self.artist_profile = state.get("artistProfile") if isinstance(state.get("artistProfile"), dict) else {}
        self.desktop = state.get("desktop") or {}
        self.clock = state.get("clock") or {}
        self.light = bool(request.get("light"))
        self.font_family = clean(request.get("fontFamily"), "Cascadia Mono")
        self.font_scale = clip(request.get("fontScale", 1), .65, 1.6)
        self.ambient_drift = bool(request.get("ambientDrift"))
        self.show_spectrum = request.get("showSpectrum", True)
        self.color_filters_enabled = request.get("colorFiltersEnabled", True)
        # A fresh object for each frame: theme overrides cannot mutate UI input
        # or leak into another theme/frame through the static backdrop caches.
        self.colors = ColorAdjustments(
            saturation_percent=request.get("saturationPercent", 100),
            hue_degrees=request.get("hueDegrees", 0),
            brightness_percent=request.get("brightnessPercent", 0),
            contrast_percent=request.get("contrastPercent", 100),
        ).validate()
        self.category = manifest.get("category", "multimedia")
        self.style = str(manifest.get("id", "")).rsplit(".", 1)[-1]
        self.theme_directory = str(request.get("themePath", ""))
        theme_key = (self.category, str(manifest.get("id", "")), self.theme_directory)
        frame_time = time.monotonic()
        previous = _ACTIVE_THEME_TIMING
        same_theme = previous and previous[0] == theme_key and frame_time >= previous[2]
        started = previous[1] if same_theme else frame_time
        # Retain only the most recent theme, never one timer per installed theme.
        _ACTIVE_THEME_TIMING = (theme_key, started, frame_time)
        self.elapsed_seconds = max(0, frame_time - started)
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
        elif self.category == "system":
            dark_base, light_base, anchors = SYSTEM_IDENTITIES.get(self.style, SYSTEM_IDENTITIES["signal"])
            self.palette = tuple(blend(anchor, "#263D41", .42) if self.light else anchor for anchor in anchors)
            self.base = light_base if self.light else dark_base
            self.fg = "#25332E" if self.light else "#EDF4F1"
            self.muted = "#5E6E66" if self.light else "#9AAEA7"
        elif self.style in ("editorial", "orbit", "duotone"):
            self.base = "#FFF7ED" if self.light else blend("#101924", self.dominant, .18)
        self.accent = blend(self.palette[0], "#193B68" if self.light else "#CBEAFF", .42)
        self.palette = ThemePalette(self.palette, {
            "textPrimary": self.fg, "textSecondary": self.muted, "accent": self.accent,
            "warning": "#9E5218" if self.light else "#FFC16E",
            "info": self.accent,
        }, manifest, request.get("palette"), self.light)
        self.fg = self.palette.textPrimary
        self.muted = self.palette.textSecondary
        self.accent = self.palette.accent
        self.title = clean(self.media.get("title"), "Nothing playing")
        self.artist = clean(self.media.get("artist"), "Waiting for a media session")
        self.album = clean(self.media.get("album"))
        self.source = clean(self.media.get("source"), "MEDIA")
        self.playing = bool(self.media.get("playing"))
        self.playback_state = clean(self.media.get("playbackStatus")).lower()
        self.has_media = bool(self.media.get("source") or self.cover or self.playing or number(self.media.get("durationSeconds")) > 0)
        self.shuffle = self.media.get("shuffle") if isinstance(self.media.get("shuffle"), bool) else None
        self.position = max(0, number(self.media.get("positionSeconds")))
        self.length = max(0, number(self.media.get("durationSeconds")))
        self.progress = clip(self.position / self.length) if self.length else None
        self.volume = self.audio.get("volumePercent")
        self.peak = self.audio.get("peak")
        self.analysis = AudioAnalysis(self.audio.get("analysis"))
        self.chat_title = clean(self.codex.get("chatTitle"), "OpenAI Codex")
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
            self.signal = self.palette.warning
        elif self.ready:
            self.signal = "#26754D" if self.light else "#8DE2AE"
        elif self.event == "Interrupt" and not self.stale:
            self.signal = "#AA3950" if self.light else "#F7A4AC"
        elif not self.event or self.stale or self.event == "SessionEnd":
            self.signal = self.muted
        else:
            self.signal = self.palette.info
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
        if self.playing:
            return "PLAYING"
        if not self.has_media:
            return "NO SESSION"
        return {"paused": "PAUSED", "stopped": "STOPPED", "closed": "CLOSED"}.get(self.playback_state, "NOT PLAYING")

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

    def text_size(self, role="caption"):
        """Readable secondary type in logical pixels; titles keep their own scale.

        Floors intentionally protect instrument labels when fontScale is reduced.
        Caps reserve a predictable slot at the largest supported user setting.
        """
        base, minimum, maximum = {
            "instrument": (16, 16, 18),
            "caption": (18, 16, 22),
            "body": (20, 18, 26),
        }[role]
        return clip(base * self.ctx.font_scale, minimum, maximum) / self.ctx.font_scale

    def caption(self, value, x, y, tint=None, width=None, align="left", bold=False):
        self.text(value, x, y, self.text_size("caption"), tint or self.ctx.muted,
                  width, align=align, bold=bold)

    def label(self, value, x, y, width=None, tint=None):
        self.caption(str(value).upper(), x, y, tint, width)

    def header(self, name=None, extended=False):
        if self.compact:
            self.label(name or self.ctx.category, 24, 28, 245)
            self.text(self.ctx.now.strftime("%H:%M"), 456, 30,
                      clip(22 * self.ctx.font_scale, 22, 28) / self.ctx.font_scale, align="right")
            self.caption(self.ctx.now.strftime("%d %b %Y"), 456, 59, width=280, align="right")
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
            self.caption(date.upper(), date_right, 39, width=date_right - 450, align="right")

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
                glow_color = ctx.palette.warning if ctx.approval else "#57C88D"
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
                active = progress_color(self.ctx.accent, self.ctx.progress, self.ctx.light)
                self.gradient(x, y, fill, height, [blend(active, self.ctx.fg, .12), active], radius=height / 2)
        if labels:
            self.caption(duration(self.ctx.position), x, y + 31)
            self.caption(duration(self.ctx.length) if self.ctx.length else "—", x + width, y + 31, align="right")

    def vu(self, x, y, width=190, height=18, label=True):
        peak = self.ctx.peak
        if peak is None:
            self.caption("LEVEL —", x, y + height, width=width)
            if label:
                value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
                self.text(value, x + width + 16, y + height, self.text_size("caption"), self.ctx.fg)
            return
        normalized = clip((20 * math.log10(max(.001, number(peak))) + 60) / 60) if peak is not None else 0
        count = max(8, min(48, int(width / 9)))
        for index in range(count):
            shade = progress_color(self.ctx.palette[2], index / max(1, count - 1), self.ctx.light)
            self.rect(x + index * width / count, y, width / count - 3, height, shade, 2,
                      1 if index / count < normalized else .18)
        if label:
            value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
            self.text(value, x + width + 16, y + height, self.text_size("caption"), self.ctx.fg)

    def app_volume(self, x, y, width=180, stacked=False):
        """The media application's gain setting, never the system-output meter."""
        value = optional_number(self.ctx.volume)
        value = f"{clip(value, 0, 100):.0f}%" if value is not None else "—"
        label_width = width if stacked else width - 82
        self.caption("APP VOL" if label_width < 140 else "APP VOLUME", x, y, width=label_width)
        self.text(value, x if stacked else x + width, y + 36 if stacked else y,
                  min(25, 31 / self.ctx.font_scale) if stacked else self.text_size("body"),
                  width=width if stacked else 72, align="left" if stacked else "right", bold=stacked)

    def playback_indicators(self, x, y, size=36):
        """Large read-only glyphs, drawn locally without icon fonts or controls."""
        caption = self.text_size("instrument")
        tint = self.ctx.accent if self.ctx.playing else self.ctx.fg
        if not self.ctx.has_media or (not self.ctx.playing and self.ctx.playback_state != "paused"):
            self.circle(x + size / 2, y + size / 2, size * .42, self.ctx.muted, .6, 2)
            self.line(x + size * .31, y + size / 2, x + size * .69, y + size / 2, self.ctx.muted, 2)
        elif self.ctx.playing:
            self.polygon([(x + size * .26, y + size * .10), (x + size * .26, y + size * .90),
                          (x + size * .90, y + size * .50)], tint)
        else:
            self.rect(x + size * .20, y + size * .12, size * .20, size * .76, tint, 2)
            self.rect(x + size * .60, y + size * .12, size * .20, size * .76, tint, 2)
        status = {"NO SESSION": "NO MEDIA", "NOT PLAYING": "INACTIVE"}.get(
            self.ctx.playback_status(), self.ctx.playback_status())
        self.text(status, x + size / 2, y + size + 23, caption,
                  self.ctx.muted, 96, align="center")
        left = x + size + 62
        active = self.ctx.shuffle is True
        known = self.ctx.shuffle is not None
        tint = self.ctx.accent if active else self.ctx.muted
        opacity = 1 if active else .6 if known else .35
        for inverted in (False, True):
            top, bottom = (.22, .78) if not inverted else (.78, .22)
            path = skia.Path()
            path.moveTo(left + size * .06, y + size * top)
            path.cubicTo(left + size * .45, y + size * top,
                         left + size * .48, y + size * bottom,
                         left + size * .86, y + size * bottom)
            self.native.drawPath(path, self.paint(tint, opacity, max(2, size * .065)))
            self.polygon([(left + size, y + size * bottom),
                          (left + size * .77, y + size * (bottom - .16)),
                          (left + size * .77, y + size * (bottom + .16))], tint, opacity)
        state = "ON" if active else "OFF" if known else "—"
        self.text("SHFL " + state, left + size / 2, y + size + 23, caption,
                  tint, 96, align="center")

    def spectrum(self, x, y, width, height=130):
        """Measured 32-band log-frequency spectrum. No input means no bars."""
        if not self.ctx.show_spectrum:
            return
        analysis = self.ctx.analysis
        small = self.text_size("instrument")
        # Keep the source name and units readable; omit optional FFT metadata in
        # narrow slots instead of shrinking the source into an eight-pixel label.
        heading = analysis.caption() if width >= 600 else "SYSTEM OUTPUT"
        self.text(heading, x, y + 17, small, self.ctx.muted, width - 65)
        self.text("dBFS", x + width, y + 17, small, self.ctx.muted, 58, align="right")
        left, top = x + 41, y + 38
        plot_width, plot_height = width - 41, height - 65
        bottom = top + plot_height
        for level in ((-60, -30, 0) if plot_height >= 54 else (-60, 0)):
            line_y = bottom - (level + 60) / 60 * plot_height
            self.line(left, line_y, x + width, line_y, self.ctx.fg, opacity=.13)
            self.text(str(level), left - 8, line_y + 5, small,
                      self.ctx.muted, 33, align="right")
        if not analysis.bands:
            baseline = top + plot_height / 2 + 6
            self.text("Spectrum unavailable", left + plot_width / 2, baseline,
                      small, self.ctx.muted, plot_width - 16, align="center")
            if plot_height >= 70:
                self.text(analysis.status, left + plot_width / 2, baseline + 25,
                          small, self.ctx.muted, plot_width - 16, align="center")
            return
        logs = [math.log10(value) for value in analysis.frequencies]
        low, high = logs[0] - (logs[1] - logs[0]) / 2, logs[-1] + (logs[-1] - logs[-2]) / 2
        span = high - low
        shader = skia.GradientShader.MakeLinear([(left, top), (left, bottom)],
            [color(progress_color(self.ctx.palette[2], value, self.ctx.light)) for value in (1, .5, 0)])
        paint = skia.Paint(Shader=shader, AntiAlias=True)
        for index, value in enumerate(analysis.display_bands):
            a = low if index == 0 else (logs[index - 1] + logs[index]) / 2
            b = high if index == 31 else (logs[index] + logs[index + 1]) / 2
            bar_x = left + (a - low) / span * plot_width
            bar_width = max(1, (b - a) / span * plot_width - 2)
            bar_height = clip((value + 60) / 60) * plot_height
            if bar_height > .1:
                self.native.drawRoundRect(skia.Rect.MakeXYWH(bar_x + 1, bottom - bar_height,
                                                            bar_width, bar_height), 1.5, 1.5, paint)
        # Only frequency labels within the provider's measured range are shown.
        first_x = left + (logs[0] - low) / span * plot_width
        final_x = left + (logs[-1] - low) / span * plot_width
        labels = [(analysis.frequencies[0], first_x, "left")]
        last_x = first_x
        for frequency in (100, 1000, 10000):
            if analysis.frequencies[0] < frequency < analysis.frequencies[-1]:
                label_x = left + (math.log10(frequency) - low) / span * plot_width
                if label_x - last_x >= 62 and final_x - label_x >= 92:
                    labels.append((frequency, label_x, "center"))
                    last_x = label_x
        labels.append((analysis.frequencies[-1], final_x, "right"))
        for frequency, label_x, align in labels:
            value = f"{frequency / 1000:.3g}k" if frequency >= 1000 else f"{frequency:.3g}"
            if align == "right":
                value += " Hz"
            self.text(value, label_x, y + height - 4, small, self.ctx.muted, 88 if align == "right" else 58, align=align)

    def stereo_vu(self, x, y, width, height=100):
        """RMS fill, instantaneous peak line and provider peak-hold marker."""
        analysis = self.ctx.analysis
        small = self.text_size("instrument")
        numerical_width = 204
        column_width = numerical_width / 3
        meter_x = x + 25
        meter_width = width - numerical_width - 41
        title = "SYSTEM OUTPUT" if width >= 420 else "SYSTEM MIX"
        if analysis.channels > 2:
            title = "SYSTEM · FRONT L/R" if width >= 600 else "FRONT L/R"
        self.text(title, x, y + 17, small, self.ctx.muted, width - numerical_width - 12)
        for column, name in enumerate(("RMS", "PK", "HOLD")):
            self.text(name, x + width - numerical_width + (column + 1) * column_width,
                      y + 17, small, self.ctx.muted, column_width - 6, align="right")
        row_height = (height - 52) / 2
        bar_height = min(18, row_height * .63)
        for index, channel in enumerate(("L", "R")):
            bar_y = y + 31 + index * row_height
            baseline = bar_y + bar_height - 1
            self.text(channel, x, baseline, small, self.ctx.muted, 18)
            self.rect(meter_x, bar_y, meter_width, bar_height, self.ctx.fg, 3, .09)
            rms = analysis.display_rms[index]
            if rms is not None and rms > -60:
                fill = meter_width * clip((rms + 60) / 60)
                self.native.save()
                self.native.clipRect(skia.Rect.MakeXYWH(meter_x, bar_y, fill, bar_height))
                self.gradient(meter_x, bar_y, meter_width, bar_height,
                              [progress_color(self.ctx.palette[2], value, self.ctx.light) for value in (0, .5, 1)], radius=3)
                self.native.restore()
            peak, hold = analysis.display_peaks[index], analysis.holds[index]
            if peak is not None and peak >= -60:
                peak_x = meter_x + clip((peak + 60) / 60) * meter_width
                self.line(peak_x, bar_y - 1, peak_x, bar_y + bar_height + 1,
                          progress_color(self.ctx.palette[2], (peak + 60) / 60, self.ctx.light), 2)
            if hold is not None and hold >= -60:
                hold_x = meter_x + clip((hold + 60) / 60) * meter_width
                self.line(hold_x, bar_y - 3, hold_x, bar_y + bar_height + 3, self.ctx.fg, 2)
            for column, value in enumerate((analysis.rms[index], analysis.peaks[index], analysis.holds[index])):
                self.text(db_label(value), x + width - numerical_width + (column + 1) * column_width,
                          baseline, small, self.ctx.fg, column_width - 6, align="right")
        ticks = (-60, -30, -12, 0) if meter_width >= 330 else (-60, -30, 0) if meter_width >= 170 else (-60, 0)
        for level in ticks:
            tick_x = meter_x + (level + 60) / 60 * meter_width
            self.line(tick_x, y + height - 23, tick_x, y + height - 20, self.ctx.muted, opacity=.5)
            align = "left" if level == -60 else "right" if level == 0 else "center"
            self.text(str(level), tick_x, y + height - 3, small,
                      self.ctx.muted, 34, align=align)
        if not analysis.available:
            summary = "UNAVAILABLE · dBFS"
        else:
            summary = "dBFS" + (" · " + analysis.status if analysis.status and analysis.status != "Live" else "")
        self.text(summary, x + width, y + height - 3, small, self.ctx.muted,
                  numerical_width, align="right")

    def ring(self, x, y, radius, value=None, tint=None, stroke=4, marker=False):
        self.circle(x, y, radius, self.ctx.fg, .13, stroke)
        if value is not None and value > 0:
            paint = self.paint(progress_color(tint or self.ctx.accent, value, self.ctx.light), 1, stroke)
            paint.setStrokeCap(skia.Paint.kRound_Cap)
            self.native.drawArc(skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), -90, 360 * clip(value), False, paint)
            if marker:
                angle = math.radians(-90 + 360 * clip(value))
                self.circle(x + math.cos(angle) * radius, y + math.sin(angle) * radius, 5.5, self.ctx.fg)

    def footer(self, text=None, width=None):
        text = text or ("DEMO DATA · PREVIEW" if self.ctx.preview else "VIBE PANEL")
        if self.ctx.preview and not text.startswith("DEMO DATA"):
            text = "DEMO DATA · PREVIEW · " + text
        self.label(text, 24 if self.compact else 40, 470, width or self.width - 80)

    def media_text(self, x, y, width, title_size=44, album=True):
        # Keep a fixed-height title slot even with the font-size override.
        safe_title_size = min(title_size, 58 / self.ctx.font_scale)
        safe_artist_size = min(24, 32 / self.ctx.font_scale)
        self.label(self.ctx.playback_status(), x, y - 44, width, self.ctx.accent)
        self.text(self.ctx.title, x, y + 12, safe_title_size, width=width, bold=True)
        self.text(self.ctx.artist, x, y + 57, safe_artist_size, self.ctx.muted, width)
        if album and self.ctx.album:
            self.caption(self.ctx.album, x, y + 92, width=width)

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
            for index, offset in enumerate((-.28, 0, .28)):
                # Pulse only for received live work; these dots are not progress.
                opacity = .8
                if self.ctx.is_live:
                    opacity = .35 + .55 * (1 + math.sin(self.ctx.elapsed_seconds * 4 - index * .9)) / 2
                self.circle(x + radius * offset, y, 4, self.ctx.signal, opacity)

    def activity_rows(self, x, y, width, row_height=64, limit=4):
        events = self.ctx.events()[-limit:]
        if not events:
            self.text("No activity received yet", x, y + 24, self.text_size("body"), self.ctx.muted, width)
            return
        for index, item in enumerate(events):
            top = y + index * row_height
            self.circle(x + 6, top + 8, 4, self.ctx.signal, 1 if index == len(events) - 1 else .4)
            if index < len(events) - 1:
                self.line(x + 6, top + 15, x + 6, top + row_height - 6, self.ctx.signal, 1, .3)
            self.text(item.get("status") or item.get("event"), x + 26, top + 19, self.text_size("body"), width=width - 35)
            detail = item.get("tool") or item.get("toolKind") or item.get("event")
            self.caption(detail, x + 26, top + 47, width=width - 35)

    def compact_media(self, variant):
        self.backdrop(variant)
        self.header("MEDIA / " + variant)
        circular = variant in ("orbit", "halo")
        self.cover(24, 75, 124, radius=2 if variant in ("editorial", "prism", "duotone") else 14,
                   circular=circular)
        if circular:
            self.ring(86, 137, 69, self.ctx.progress, stroke=2)
        self.text(self.ctx.title, 164, 107, clip(25 * self.ctx.font_scale, 24, 32) / self.ctx.font_scale,
                  width=292, bold=True)
        self.text(self.ctx.artist, 164, 141, self.text_size("body"), self.ctx.muted, 292)
        self.caption(self.ctx.album, 164, 168, width=292)
        self.text(self.ctx.timing(), 24, 223, self.text_size("instrument"), self.ctx.muted, 132)
        self.playback_indicators(178, 184, 29)
        self.app_volume(348, 195, 108, stacked=True)
        self.spectrum(24, 245, 432, 90)
        self.stereo_vu(24, 339, 432, 98)
        self.progress(24, 444, 432, 3, labels=False)
        self.footer()

    def compact_codex(self, variant):
        self.backdrop(variant)
        self.header("CODEX / " + variant)
        if variant in ("beacon", "pulse", "approval", "quiet"):
            self.status_symbol(240, 150, 64)
            self.text(self.ctx.status, 240, 261, clip(29 * self.ctx.font_scale, 28, 42) / self.ctx.font_scale,
                      self.ctx.signal, 432, align="center", bold=True)
            self.text(self.ctx.chat_title, 240, 307, self.text_size("body"), width=432, align="center")
            self.caption(self.ctx.activity_detail(), 240, 349, width=432, align="center")
        else:
            title_size = clip(25 * self.ctx.font_scale, 24, 34) / self.ctx.font_scale
            self.text(self.ctx.chat_title, 24, 110, title_size, width=432, bold=True)
            self.text(self.ctx.status, 24, 154, title_size, self.ctx.signal, 432)
            self.activity_rows(24, 211, 420, 66, 3)
        self.line(24, 422, 456, 422, self.ctx.fg, opacity=.12)
        self.footer(self.ctx.activity_detail())
# VBWR E
