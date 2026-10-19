# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Ten System layouts with a stable CPU/GPU/RAM identity and measured detail."""
from __future__ import annotations

import time
from dataclasses import dataclass

from canvas import blend, clean, optional_number
from hardware import _caption, _text, _heading, _footer, _components, _record, _trace


@dataclass
class Card:
    key: str
    label: str
    name: str
    value: str
    value_label: str
    frequency: str
    details: list[str]
    load: float | None


def _number(value, suffix="", digits=0):
    value = optional_number(value)
    return "Unavailable" if value is None else f"{value:.{digits}f}{suffix}"


def _clock(value):
    value = optional_number(value)
    return "Clock unavailable" if value is None else f"{value / 1000:.2f} GHz"


def _reading(raw, kind, *names):
    readings = raw.get("readings") if isinstance(raw.get("readings"), list) else []
    for name in names:
        for reading in readings:
            if isinstance(reading, dict) and reading.get("type") == kind and clean(reading.get("name")).casefold() == name.casefold():
                value = optional_number(reading.get("value"))
                if value is not None:
                    return value
    return None


def _fans(raw):
    result = []
    for fan in raw.get("fans", []) if isinstance(raw.get("fans"), list) else []:
        if isinstance(fan, dict) and (rpm := optional_number(fan.get("rpm"))) is not None:
            result.append((clean(fan.get("name"), "Fan"), rpm))
    return result


def _processor_card(raw, key, label, style):
    raw = raw if isinstance(raw, dict) else {}
    temperature = optional_number(raw.get("temperatureC"))
    load = optional_number(raw.get("usagePercent"))
    frequency = _clock(raw.get("clockMHz"))
    thermal = style in ("thermal", "orbit", "quiet")
    value = _number(temperature, "°C", 1) if thermal else _number(load, "%", 1)
    value_label = "Temperature" if thermal else "Load"
    if style == "blueprint":
        value, value_label = _clock(raw.get("clockMHz")), "Clock"
    # Temperature and clock are anchored, even in layouts whose main value is load.
    frequency = " · ".join(part for part in (frequency if optional_number(raw.get("clockMHz")) is not None else "", _number(temperature, "°C", 1) if temperature is not None else "") if part)
    if thermal:
        frequency = " · ".join(part for part in (_clock(raw.get("clockMHz")) if optional_number(raw.get("clockMHz")) is not None else "", _number(load, "%", 1) + " load" if load is not None else "") if part)
    fans = _fans(raw)
    fan_text = "Fans " + "/".join(f"{rpm:.0f}" for _, rpm in fans[:2]) + " RPM" if fans else "Fan RPM unavailable"
    if key == "cpu":
        power = _reading(raw, "Power", "Package", "CPU Package")
        effective = _reading(raw, "Clock", "Cores (Average Effective)")
        detail = _number(power, " W") + " package"
        if effective is not None:
            detail += f" · {effective / 1000:.2f} GHz effective"
        temperatures = []
        for name, short in (("SoC", "SoC"), ("CCD1 (Tdie)", "CCD1"), ("CCD2 (Tdie)", "CCD2")):
            if (value_t := _reading(raw, "Temperature", name)) is not None:
                temperatures.append(short + " " + _number(value_t, "°C", 1))
        details = [detail, " · ".join(temperatures) or "Die temperatures unavailable",
                   fan_text if fans else _number(load, "%", 1) + " CPU load"]
    else:
        used = _reading(raw, "SmallData", "GPU Memory Used")
        total = _reading(raw, "SmallData", "GPU Memory Total")
        memory = (f"{used / 1024:.1f} / {total / 1024:.1f} GiB VRAM"
                  if used is not None and total is not None else "VRAM unavailable")
        hotspot = _reading(raw, "Temperature", "GPU Hot Spot", "GPU Hotspot")
        memory_clock = _reading(raw, "Clock", "GPU Memory")
        extras = []
        if hotspot is not None:
            extras.append("Hotspot " + _number(hotspot, "°C", 1))
        if memory_clock is not None:
            extras.append("Mem " + _number(memory_clock, " MHz"))
        powers = [(clean(reading.get("name")), optional_number(reading.get("value")))
                  for reading in raw.get("readings", []) if isinstance(reading, dict) and reading.get("type") == "Power"
                  and optional_number(reading.get("value")) is not None]
        power_text = ""
        if powers:
            first = int(time.monotonic() / 5) % len(powers)
            shown = (powers + powers)[first:first + min(2, len(powers))]
            power_text = " · ".join(f"{name.removeprefix('GPU ')}: {watts:.1f} W" for name, watts in shown)
        details = [power_text, memory if used is not None and total is not None else "", fan_text] if powers else [memory, " · ".join(extras) or "GPU detail unavailable", fan_text]
    details = [detail for detail in details if "unavailable" not in detail.casefold()]
    if value == "Unavailable" or value == "Clock unavailable":
        value, value_label = "", ""
    return Card(key, label, clean(raw.get("name"), label), value, value_label,
                frequency, details, load)


def _memory_card(raw):
    raw = raw if isinstance(raw, dict) else {}
    used, total = optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB"))
    modules = [module for module in raw.get("modules", []) if isinstance(module, dict)] if isinstance(raw.get("modules"), list) else []
    kind = clean(raw.get("type"), "RAM type unavailable")
    name = kind + (f" · {len(modules)} DIMMs" if modules else "")
    value = f"{used:.1f} / {total:.1f}" if used is not None and total is not None else "Unavailable"
    speed = optional_number(raw.get("configuredSpeedMTs"))
    frequency = f"{speed:.0f} MT/s configured" if speed is not None else "RAM speed unavailable"
    clock = optional_number(raw.get("clockMHz"))
    capacities = [optional_number(module.get("capacityGiB")) for module in modules]
    installed = f"{sum(capacities):.0f} GiB installed" if capacities and all(value is not None for value in capacities) else "Installed capacity unavailable"
    if capacities and len(set(capacities)) == 1 and capacities[0] is not None:
        installed += f" · {len(capacities)} × {capacities[0]:.0f} GiB"
    module_name = clean(modules[int(time.monotonic() / 7) % len(modules)].get("name")) if modules else "DIMM information unavailable"
    details = [detail for detail in [installed, _number(clock, " MHz") + " memory clock", module_name] if "unavailable" not in detail.casefold()]
    frequency = frequency if speed is not None else ""
    name = kind if raw.get("type") else ""
    if modules:
        name += (" · " if name else "") + f"{len(modules)} DIMMs"
    if value == "Unavailable":
        value = ""
    return Card("memory", "RAM", name, value, "GiB used / total", frequency,
                details, optional_number(raw.get("usagePercent")))


def _cards(s, style):
    raw = s.sensors if isinstance(s.sensors, dict) else {}
    gpus = raw.get("gpus") if isinstance(raw.get("gpus"), list) else []
    index = int(time.monotonic() / 7) % len(gpus) if gpus else 0
    gpu = gpus[index] if gpus and isinstance(gpus[index], dict) else {}
    label = f"GPU {index + 1}/{len(gpus)}" if len(gpus) > 1 else "GPU"
    return [_processor_card(raw.get("cpu"), "cpu", "CPU", style),
            _processor_card(gpu, "gpu", label, style), _memory_card(raw.get("memory"))]


def _treatment(c, s, card, box, style, index):
    x, y, width, height = box
    tint, ink, muted = s.palette[index % 3], s.fg, s.muted
    if style == "blocks":
        backgrounds = (("#244BC4", "#FFFFFF", "#D0DCFF"), ("#EAE6DA", "#1D2E38", "#526264"), ("#F0CA43", "#23302D", "#525B40"))
        background, ink, muted = backgrounds[index % 3]
        ink = s.palette.overrides.get("textPrimary", ink)
        muted = s.palette.overrides.get("textSecondary", muted)
        tint = ink
        c.rect(x, y, width, height, background)
    elif style == "blueprint":
        c.rect(x, y, width, height, tint, opacity=.07)
        for yy in (y, y + height):
            c.line(x, yy, x + width, yy, tint, opacity=.5)
        for fraction in (0, .25, .5, .75, 1):
            xx = x + width * fraction
            c.line(xx, y + height - 8, xx, y + height, tint, opacity=.65)
    elif style in ("thermal", "orbit"):
        if card.key != "memory":
            c.hardware_photo("cpu" if card.key == "cpu" else "gpu", x, y, width, height, s.base, .84 if s.light else .50)
        else:
            c.rect(x, y, width, height, tint, opacity=.07)
        c.line(x, y, x + width, y, tint, 3)
    elif style in ("ledger", "terminal", "quiet"):
        c.gradient(x, y, width, height, [blend(s.base, tint, .10 if s.light else .13), blend(s.base, tint, .025)], vertical=True, radius=12)
        c.line(x, y + height, x + width, y + height, tint, opacity=.24)
    else:
        c.gradient(x, y, width, height, [blend(s.base, tint, .16), blend(s.base, tint, .035)], vertical=True, radius=14)
        c.line(x, y + 16, x, y + height - 16, tint, 3)
    return tint, ink, muted


def _panel(c, s, card, box, style, index, components):
    x, y, width, height = box
    tint, ink, muted = _treatment(c, s, card, box, style, index)
    pad = 20
    left, span = x + pad, width - pad * 2
    if height < 170:
        _text(c, card.label + " / " + card.name, left, y + 22, 16, span, ink, True)
        _text(c, card.frequency.replace(" configured", " cfg"), left, y + 52, 17, span * .61, tint)
        _text(c, card.value, left + span, y + 52, 22 if width < 550 else 30, span * .38, ink, True, "right")
        _text(c, card.value_label, left + span, y + 73, 12, span * .42, muted, align="right")
        detail = card.details[0] if card.details else ""
        _text(c, detail, left, y + 78, 14, span * .58, muted)
        if height >= 105:
            more = card.details[1] if len(card.details) > 1 else ""
            _text(c, more, left, y + 102, 14, span, muted)
        return
    _caption(c, card.label, left, y + 24, span, tint)
    _text(c, card.name, left, y + 53, 17, span, ink, True)
    _text(c, card.value, left, y + 122, 38 if card.key == "memory" else 54, span, ink, True)
    _text(c, card.value_label, left, y + 151, 15, span, muted)
    _text(c, card.frequency, left, y + 188, 22, span, tint, True)
    details = [detail for detail in card.details if detail]
    spacing = min(30, 68 / max(1, len(details) - 1))
    for row, detail in enumerate(details):
        _text(c, detail, left, y + 217 + row * spacing, 16, span, muted)
    if style in ("signal", "horizon", "pulse"):
        component = next((item for item in components if item.key == card.key or card.key == "gpu" and item.key.startswith("gpu-")), None)
        if component:
            _trace(c, component, component.metric("usagePercent"), (left, y + height - 18, span, 14), tint)
    elif card.load is not None and style in ("blocks", "blueprint"):
        c.rect(left, y + height - 8, span, 3, ink, opacity=.13)
        c.rect(left, y + height - 8, span * max(0, min(1, card.load / 100)), 3, tint)


def _board_strip(c, s):
    raw = s.sensors.get("motherboard")
    raw = raw if isinstance(raw, dict) else {}
    fans = _fans(raw)
    active = [(name, rpm) for name, rpm in fans if rpm > 0]
    chosen = active or fans
    fan = chosen[int(time.monotonic() / 5) % len(chosen)] if chosen else None
    fan_text = f"{fan[0]} · {fan[1]:.0f} RPM" if fan else ""
    readings = raw.get("temperatures") if isinstance(raw.get("temperatures"), list) else []
    readings = [item for item in readings if isinstance(item, dict) and optional_number(item.get("temperatureC")) is not None]
    channel = readings[int(time.monotonic() / 5) % len(readings)] if readings else None
    temperature = clean(channel.get("name")) + " " + _number(channel.get("temperatureC"), "°C", 1) if channel else ""
    x, width = (24, 432) if c.compact else (40, 1200)
    c.line(x, 397, x + width, 397, s.fg, opacity=.16)
    if c.compact:
        _text(c, fan_text + " · " + temperature, x, 420, 14, width, s.palette[2])
    else:
        _text(c, " · ".join(part for part in (clean(raw.get("name")), temperature) if part), x, 420, 16, width * .66, s.muted)
        _text(c, fan_text + (f" / {len(fans)} channels" if fans else ""), x + width, 420, 16, width * .33, s.palette[2], align="right")


def _atmosphere(c, s, style):
    # Cached Skia primitives keep the background independent of sensor text.
    strength = .10 if s.light else .22
    c.gradient(0, 0, c.width, 480,
               [blend(s.base, s.palette[0], strength), s.base,
                blend(s.base, s.palette[1], strength * .65)], vertical=True)
    if style in ("orbit", "pulse", "signal"):
        for radius in (110, 165, 220):
            c.circle(c.width - 50, 70, radius, s.palette[1], opacity=.055, stroke=1)
        c.circle(c.width - 50, 70, 75, s.palette[1], opacity=.035)
    elif style == "blueprint":
        for x in range(0, int(c.width), 24):
            c.line(x, 0, x, 480, s.palette[0], opacity=.045)
        for y in range(0, 480, 24):
            c.line(0, y, c.width, y, s.palette[0], opacity=.045)
    elif style == "horizon":
        for offset in range(6):
            c.line(0, 320 + offset * 23, c.width, 150 + offset * 23,
                   s.palette[1], opacity=.07, width=2)


def render(c, s, style):
    components = _components(s.sensors)
    _record(components, clean(s.sensors.get("updatedAtUtc")))
    _atmosphere(c, s, style)
    _heading(c, s, style)
    cards = _cards(s, style)
    margin, gap = (24, 8) if c.compact else (40, 22)
    width, top, height = c.width - margin * 2, 76, 306
    if style == "horizon":
        c.gradient(0, 250, c.width, 230, [s.base, blend(s.base, s.palette[1], .20)], vertical=True)
    if c.compact or style == "terminal":
        row_height = (height - gap * 2) / 3
        boxes = [(margin, top + index * (row_height + gap), width, row_height) for index in range(3)]
    elif style == "ledger":
        left = width * .51
        boxes = [(margin, top, left - gap, height),
                 (margin + left, top, width - left, (height - gap) / 2),
                 (margin + left, top + (height + gap) / 2, width - left, (height - gap) / 2)]
    elif style == "pulse":
        left = width * .64
        boxes = [(margin, top, left - gap, (height - gap) / 2),
                 (margin, top + (height + gap) / 2, left - gap, (height - gap) / 2),
                 (margin + left, top, width - left, height)]
    else:
        span = (width - gap * 2) / 3
        boxes = [(margin + index * (span + gap), top, span, height) for index in range(3)]
    for index, (card, box) in enumerate(zip(cards, boxes)):
        _panel(c, s, card, box, style, index, components)
    _board_strip(c, s)
    notice = clean(s.sensors.get("notice"))
    # Detailed access diagnostics belong in Sensor Debug, outside the panel composition.
    _footer(c, s, components)
# VBWR E
