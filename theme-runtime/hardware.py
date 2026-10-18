# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""System-theme compositions. Draw only measured, currently available values."""
from __future__ import annotations

import time
from collections import OrderedDict, deque
from dataclasses import dataclass

from canvas import blend, clean, clip, optional_number


# This is display history, not a sampler. Identical cached snapshots add nothing.
# At most 64 measured series with 60 real samples each survive in this worker.
_HISTORY = OrderedDict()
_MAX_SERIES = 64
_MAX_SAMPLES = 60


@dataclass(frozen=True)
class Metric:
    key: str
    label: str
    value: float
    text: str
    ratio: float | None = None


@dataclass
class Component:
    key: str
    label: str
    name: str
    metrics: list[Metric]

    def metric(self, key):
        return next((item for item in self.metrics if item.key == key), None)

    def primary(self, prefer="usagePercent"):
        return self.metric(prefer) or self.metrics[0]


def _metric(raw, key, label, suffix, digits=0, ratio=False):
    value = optional_number(raw.get(key))
    if value is None or (value < 0 and key != "temperatureC"):
        return None
    text = f"{value:.{digits}f}{suffix}"
    return Metric(key, clean(label), value, text, clip(value / 100) if ratio else None)


def _processor(raw, key, label):
    if not isinstance(raw, dict):
        return None
    metrics = [
        _metric(raw, "usagePercent", "Usage", "%", ratio=True),
        _metric(raw, "clockMHz", raw.get("clockLabel") or "Clock", " MHz"),
        _metric(raw, "temperatureC", raw.get("temperatureLabel") or "Temperature", "°C", 1),
    ]
    metrics = [item for item in metrics if item is not None]
    return Component(key, label, clean(raw.get("name")), metrics) if metrics else None


def _components(raw):
    result = []
    cpu = _processor(raw.get("cpu"), "cpu", "CPU")
    if cpu:
        result.append(cpu)
    gpus = raw.get("gpus")
    gpus = gpus if isinstance(gpus, list) else []
    for index, item in enumerate(gpus):
        component = _processor(item, f"gpu-{index}", "GPU" if len(gpus) == 1 else f"GPU {index + 1}")
        if component:
            result.append(component)
    memory = raw.get("memory")
    if isinstance(memory, dict):
        metrics = [
            _metric(memory, "usagePercent", "Usage", "%", ratio=True),
            _metric(memory, "usedGiB", "Used", " GiB", 1),
            _metric(memory, "totalGiB", "Total", " GiB", 1),
        ]
        metrics = [item for item in metrics if item is not None]
        if metrics:
            result.append(Component("memory", "RAM", "", metrics))
    return result


def _record(components, stamp):
    active = set()
    now = time.monotonic()
    for component in components:
        for metric in component.metrics:
            key = (component.key, component.name, metric.key)
            active.add(key)
            if not stamp:
                _HISTORY.pop(key, None)
                continue
            previous = _HISTORY.get(key)
            if previous is None:
                previous = {"stamp": "", "at": now, "samples": deque(maxlen=_MAX_SAMPLES)}
            if previous["stamp"] != stamp:
                # A long gap starts a new trace rather than joining stale readings.
                if now - previous["at"] > 10:
                    previous["samples"].clear()
                previous["samples"].append(metric.value)
                previous["stamp"], previous["at"] = stamp, now
            elif now - previous["at"] > 10:
                previous["samples"].clear()
            _HISTORY[key] = previous
            _HISTORY.move_to_end(key)
    for key in tuple(_HISTORY):
        if key not in active:
            _HISTORY.pop(key, None)
    while len(_HISTORY) > _MAX_SERIES:
        _HISTORY.popitem(last=False)


def _samples(component, metric):
    series = _HISTORY.get((component.key, component.name, metric.key))
    return list(series["samples"]) if series else []


def _text(c, value, x, y, size, width, tint=None, bold=False, align="left"):
    # Reserve predictable vertical slots at every supported fontScale.
    c.text(value, x, y, min(size, size * 1.14 / c.ctx.font_scale), tint, width, align, bold)


def _caption(c, value, x, y, width, tint=None, align="left"):
    c.text(value, x, y, c.text_size("instrument"), tint or c.ctx.muted, width, align)


def _heading(c, s, style):
    x = 24 if c.compact else 40
    title = {"thermal": "THERMAL ATLAS", "orbit": "AIRFLOW"}.get(style, style.upper())
    _caption(c, "SYSTEM / " + title, x, 34, c.width - x * 2 - 110, s.accent)
    _text(c, s.now.strftime("%H:%M"), c.width - x, 35, 24, 100, align="right")
    c.line(x, 54, c.width - x, 54, s.fg, opacity=.13)


def _footer(c, s, components):
    x = 24 if c.compact else 40
    _caption(c, "VIBE PANEL", x, 468, 180)
    # These are live readings even when the studio is in preview mode.
    sample = clean(s.sensors.get("notice")).startswith("PREVIEW:")
    _caption(c, "SAMPLE DATA" if sample else "LOCAL SENSORS" if components else "SENSORS UNAVAILABLE",
             c.width - x, 468, c.width - x * 2 - 190, align="right")


def _empty(c, s):
    x = 24 if c.compact else 40
    c.line(x, 244, x + (64 if c.compact else 120), 244, s.accent, 2)
    _text(c, "No sensor data available", x, 218, 28 if c.compact else 40, c.width - x * 2)


def _area(c):
    margin = 24 if c.compact else 40
    return margin, 78, c.width - margin * 2, 350


def _rows(area, count, gap=16):
    x, y, width, height = area
    row_height = (height - gap * (count - 1)) / count
    return [(x, y + index * (row_height + gap), width, row_height) for index in range(count)]


def _columns(area, count, gap=28):
    x, y, width, height = area
    column_width = (width - gap * (count - 1)) / count
    return [(x + index * (column_width + gap), y, column_width, height) for index in range(count)]


def _boxes(c, components, area=None, gap=24):
    area = area or _area(c)
    if c.compact:
        return _rows(area, len(components), min(gap, 12))
    if len(components) > 4:
        rows = (len(components) + 2) // 3
        bands = _rows(area, rows, 18)
        return [box for index, band in enumerate(bands)
                for box in _columns(band, min(3, len(components) - index * 3), 24)]
    return _columns(area, len(components), gap)


def _details(c, component, primary, x, y, width, line_height=31, tint=None, muted=None):
    for metric in component.metrics:
        if metric.key == primary.key:
            continue
        _caption(c, metric.label, x, y, width * .55, muted)
        _text(c, metric.text, x + width, y, 20, width * .44, tint, align="right")
        y += line_height
    return y


def _inline(metric):
    if metric.key in ("usedGiB", "totalGiB"):
        return metric.text + " " + metric.label.lower()
    label = metric.label.lower()
    if metric.key == "clockMHz" and ("average" in label or "avg" in label):
        return metric.text + " avg"
    if metric.key == "temperatureC" and "package" in label:
        return metric.text + " package"
    return metric.text


def _bar(c, x, y, width, height, metric, tint, segmented=False, fg=None):
    if metric.ratio is None:
        return
    if segmented:
        count, gap = max(8, min(40, int(width / 10))), 3
        segment_width = (width - (count - 1) * gap) / count
        for index in range(count):
            amount = clip(metric.ratio * count - index)
            left = x + index * (segment_width + gap)
            c.rect(left, y, segment_width, height, fg or c.ctx.fg, opacity=.12)
            if amount > 0:
                c.rect(left, y, segment_width * amount, height, tint)
    else:
        c.rect(x, y, width, height, fg or c.ctx.fg, height / 2, .10)
        if metric.ratio > 0:
            c.rect(x, y, width * metric.ratio, height, tint, height / 2)


def _trace(c, component, metric, box, tint, ticks=False):
    if metric is None:
        return
    samples = _samples(component, metric)
    if len(samples) < 2:
        return
    x, y, width, height = box
    if metric.ratio is not None:
        low, high = 0, 100
    else:
        low, high = min(samples), max(samples)
        if high == low:
            low, high = low - 1, high + 1
        else:
            padding = (high - low) * .12
            low, high = low - padding, high + padding
    if ticks:
        for fraction in (0, .5, 1):
            c.line(x, y + fraction * height, x + width, y + fraction * height,
                   c.ctx.fg, opacity=.08)
    # Join only the real samples. No random curves, repeated fills or interpolation.
    points = [(x + index / (len(samples) - 1) * width,
               y + height * (1 - clip((value - low) / (high - low))))
              for index, value in enumerate(samples)]
    for start, end in zip(points, points[1:]):
        c.line(*start, *end, tint, 2)
    c.circle(*points[-1], 3, tint)


def _dense(c, s, component, box, tint, segmented=False, prefer="usagePercent"):
    x, y, width, height = box
    primary = component.primary(prefer)
    label_width = width * .43
    label = component.label if primary.key == "usagePercent" else component.label + " / " + primary.label
    _caption(c, label, x, y + 20, label_width, tint)
    if height >= 82 and component.name:
        _caption(c, component.name, x, y + 43, label_width)
    _text(c, primary.text, x + width, y + (25 if height < 64 else 30), 32 if height > 90 else 26,
          width * .53, tint, True, "right")
    remaining = [metric for metric in component.metrics if metric.key != primary.key]
    detail_y = y + 62 if height >= 94 else y + height - (12 if height >= 64 else 3)
    if height >= 45:
        text = " · ".join(_inline(metric) for metric in remaining)
        _caption(c, text, x, detail_y, width)
    if height >= 94:
        _bar(c, x, y + height - 12, width, 4, primary, tint, segmented)


def _signal(c, s, components):
    for index, (component, box) in enumerate(zip(components, _boxes(c, components))):
        x, y, width, height = box
        tint = s.palette[index % 3]
        if height < 200:
            _dense(c, s, component, box, tint)
            c.line(x, y + height, x + width, y + height, s.fg, opacity=.12)
            continue
        primary = component.primary("usagePercent")
        _caption(c, component.label, x, y + 22, width, tint)
        _caption(c, component.name, x, y + 55, width)
        _text(c, primary.text, x, y + 143, min(76, width * .24), width, tint, True)
        _caption(c, primary.label, x, y + 177, width)
        _details(c, component, primary, x, y + 225, width)
        _trace(c, component, component.metric("usagePercent"), (x, y + 294, width, 48), tint, True)
        if index < len(components) - 1:
            c.line(x + width + 12, y, x + width + 12, y + height, s.fg, opacity=.13)


def _ledger(c, s, components):
    area = _area(c)
    x, y, width, height = area
    if len(components) == 1:
        boxes = [area]
    elif c.compact and len(components) > 4:
        boxes = _rows(area, len(components), 10)
    elif c.compact:
        first_height = 156 if len(components) <= 3 else 108
        boxes = [(x, y, width, first_height)]
        boxes += _rows((x, y + first_height + 18, width, height - first_height - 18),
                       len(components) - 1, 12)
    else:
        hero_width = width * .54
        boxes = [(x, y, hero_width - 26, height)]
        boxes += _rows((x + hero_width + 18, y, width - hero_width - 18, height),
                       len(components) - 1, 24)
    for index, (component, box) in enumerate(zip(components, boxes)):
        left, top, span, tall = box
        tint = s.palette[index % 3]
        primary = component.primary("usagePercent")
        if index > 0 or tall < 200:
            _dense(c, s, component, box, tint)
            c.line(left, top + tall, left + span, top + tall, s.fg, opacity=.2)
            continue
        _caption(c, "01 / " + component.label, left, top + 20, span, tint)
        _caption(c, component.name, left, top + 56, span)
        _text(c, primary.text, left - 4, top + 187, 112, span, bold=True)
        _caption(c, primary.label.upper(), left, top + 225, span)
        c.line(left, top + 251, left + span, top + 251, s.fg, opacity=.25)
        _details(c, component, primary, left, top + 288, span, 37)


def _terminal(c, s, components):
    x, y, width, height = _area(c)
    boxes = _rows((x, y + 22, width, height - 22), len(components), 8 if len(components) > 4 else 14)
    if not c.compact:
        for label, fraction, span in (("DEVICE", 0, .25), ("LOAD / VALUE", .27, .42), ("DETAILS", .73, .27)):
            _caption(c, label, x + width * fraction, y + 8, width * span, s.accent)
    for index, (component, box) in enumerate(zip(components, boxes)):
        left, top, span, tall = box
        tint = s.palette[index % 3]
        if c.compact or tall < 70:
            _dense(c, s, component, box, tint, True)
        else:
            primary = component.primary("usagePercent")
            _text(c, component.label, left, top + 35, 27, span * .23, tint, True)
            _caption(c, component.name, left, top + 63, span * .23)
            _text(c, primary.text, left + span * .27, top + 35, 31, span * .4, tint, True)
            _bar(c, left + span * .27, top + 55, span * .4, 14, primary, tint, True)
            _details(c, component, primary, left + span * .73, top + 31, span * .27, 30)
        c.line(left, top + tall, left + span, top + tall, tint, opacity=.18)


def _cooling_parts(raw):
    """Normalize measured temperatures, clocks, load, fans and used physical RAM."""
    if not isinstance(raw, dict):
        return [], None
    parts = []
    sources = [("CPU", raw.get("cpu"))]
    gpus = raw.get("gpus") if isinstance(raw.get("gpus"), list) else []
    sources.extend(("GPU" if len(gpus) == 1 else f"GPU {index + 1}", gpu)
                   for index, gpu in enumerate(gpus))
    sources.append(("BOARD", raw.get("motherboard")))
    for label, source in sources:
        if not isinstance(source, dict):
            continue
        metrics = [item for item in (
            _metric(source, "temperatureC", source.get("temperatureLabel") or "Temperature", "°C", 1),
            _metric(source, "usagePercent", "Load", "%", ratio=True),
            _metric(source, "clockMHz", "Clock", " MHz"),
        ) if item is not None]
        fans = []
        raw_fans = source.get("fans") if isinstance(source.get("fans"), list) else []
        for fan in raw_fans[:16]:
            if not isinstance(fan, dict):
                continue
            rpm = optional_number(fan.get("rpm"))
            if rpm is not None and 0 <= rpm <= 20000:
                fans.append(Metric("fanRpm", clean(fan.get("name"), "Fan"), rpm, f"{rpm:.0f} RPM"))
        if metrics or fans:
            parts.append((label, clean(source.get("name")), metrics, fans))
    memory = raw.get("memory")
    used = optional_number(memory.get("usedGiB")) if isinstance(memory, dict) else None
    total = optional_number(memory.get("totalGiB")) if isinstance(memory, dict) else None
    ram = (used, total if total is not None and total >= used else None) if used is not None and used >= 0 else None
    return parts, ram


def _cooling_ram(c, s, ram, y):
    if ram is None:
        return
    used, total = ram
    x = 24 if c.compact else 40
    width = c.width - 2 * x
    c.line(x, y - 20, x + width, y - 20, s.fg, opacity=.15)
    _caption(c, "RAM USED", x, y, 120, s.palette[2])
    value = f"{used:.1f} GiB" + (f" / {total:.1f} GiB" if total is not None else "")
    _text(c, value, x + width, y + 1, 24 if not c.compact else 20, width - 130, s.fg, True, "right")


def _cooling_compact(c, s, parts, ram):
    # Square and portrait layouts share the available height among live rows.
    x, y, width, height = _area(c)
    count = len(parts) + int(ram is not None)
    if count == 0:
        return
    rows = _rows((x, y, width, height), count, 8)
    for index, (label, name, metrics, fans) in enumerate(parts):
        left, top, span, tall = rows[index]
        tint = s.palette[index % 3]
        c.rect(left, top, 4, max(0, tall - 3), tint)
        _caption(c, label, left + 15, top + 20, span * .42, tint)
        primary = next((item for item in metrics if item.key == "temperatureC"),
                       metrics[0] if metrics else fans[0])
        _text(c, primary.text, left + span, top + 29, 27 if tall >= 65 else 21,
              span * .58, tint, True, "right")
        extras = [item.text for item in metrics if item is not primary]
        extras.extend(f"{item.label} {item.text}" for item in fans if item is not primary)
        if extras:
            _caption(c, " · ".join(extras), left + 15, top + tall - 10, span - 15)
        if tall >= 94 and name:
            _caption(c, name, left + 15, top + 46, span * .65)
    if ram is not None:
        left, top, span, _ = rows[-1]
        used, total = ram
        _caption(c, "RAM USED", left + 15, top + 20, span * .42, s.palette[2])
        value = f"{used:.1f} GiB" + (f" / {total:.1f} GiB" if total is not None else "")
        _text(c, value, left + span, top + 29, 24, span * .58, s.palette[2], True, "right")


def _thermal_atlas(c, s, parts, ram):
    if c.compact or len(parts) > 4:
        _cooling_compact(c, s, parts, ram)
        return
    x, y, width, _ = _area(c)
    fans = [(label, fan) for label, _, _, group in parts for fan in group]
    hero_parts = [part for part in parts if part[2]]
    hero_height = 197 if fans else 255
    hero_boxes = _columns((x, y, width, hero_height), len(hero_parts), 18) if hero_parts else []
    for index, ((label, name, metrics, _), box) in enumerate(zip(hero_parts, hero_boxes)):
        left, top, span, tall = box
        tint = s.palette[index % 3]
        c.rect(left, top, span, tall, tint, 4, .10)
        c.line(left, top, left + span, top, tint, 3)
        _caption(c, label, left + 18, top + 30, span - 36, tint)
        primary = next((item for item in metrics if item.key == "temperatureC"), metrics[0] if metrics else None)
        if primary:
            _text(c, primary.text, left + 18, top + 116, min(77, span * .18), span - 36, tint, True)
            _caption(c, primary.label.upper(), left + 18, top + 143, span - 36)
        details = [item.text for item in metrics if item is not primary]
        if details:
            _caption(c, "  /  ".join(details), left + 18, top + tall - 22, span - 36)
        if name and span >= 260:
            _caption(c, name, left + 18, top + 55, span - 36)
    if fans:
        start = y + hero_height + 23 if hero_parts else y + 30
        shown = fans[:6]
        for index, ((label, fan), box) in enumerate(zip(shown, _columns((x, start, width, 76), len(shown), 12))):
            left, top, span, _ = box
            tint = s.palette[index % 3]
            _caption(c, label + " / " + fan.label, left, top + 16, span, tint)
            _text(c, fan.text, left, top + 53, min(29, span * .16), span, tint, True)
    _cooling_ram(c, s, ram, 413)


def _airflow(c, s, parts, ram):
    if c.compact or len(parts) > 4:
        _cooling_compact(c, s, parts, ram)
        return
    x, y, width, _ = _area(c)
    fans = [(label, fan) for label, _, _, group in parts for fan in group]
    thermal_parts = [part for part in parts if any(item.key == "temperatureC" for item in part[2])]
    thermal_boxes = _columns((x, y, width, 137), len(thermal_parts), 23) if thermal_parts else []
    for index, ((label, _, metrics, _), box) in enumerate(zip(thermal_parts, thermal_boxes)):
        left, top, span, _ = box
        tint = s.palette[index % 3]
        primary = next(item for item in metrics if item.key == "temperatureC")
        _caption(c, label + " THERMAL", left, top + 20, span, tint)
        if primary:
            _text(c, primary.text, left, top + 88, min(66, span * .18), span, tint, True)
        c.line(left, top + 117, left + span, top + 117, tint, 2)
    fan_y = 243 if thermal_parts else 100
    fan_boxes = _columns((x, fan_y, width, 105), min(len(fans), 6), 12) if fans else []
    for index, ((label, fan), box) in enumerate(zip(fans[:6], fan_boxes)):
        left, top, span, _ = box
        tint = s.palette[index % 3]
        c.rect(left, top, span, 89, tint, 3, .12)
        _caption(c, label + " / " + fan.label, left + 12, top + 22, span - 24, tint)
        _text(c, fan.text, left + 12, top + 68, min(30, span * .16), span - 24, tint, True)
    metrics_y = 375 if thermal_parts and fans else 290
    for index, (label, _, metrics, _) in enumerate(parts):
        extras = [item.text for item in metrics if item.key != "temperatureC"]
        if extras:
            span = width / len(parts)
            _caption(c, label + "  " + "  /  ".join(extras), x + index * span, metrics_y, span - 8)
    _cooling_ram(c, s, ram, 423)


def _blueprint(c, s, components):
    x, y, width, height = _area(c)
    step = 32
    for left in range(0, c.width + 1, step):
        c.line(left, 55, left, 441, s.palette[0], opacity=.045)
    for top in range(56, 442, step):
        c.line(0, top, c.width, top, s.palette[0], opacity=.045)
    for index, (component, box) in enumerate(zip(components, _boxes(c, components, gap=24))):
        left, top, span, tall = box
        tint = s.palette[index % 3]
        c.rect(left, top, span, tall, tint, stroke=1, opacity=.35)
        if tall < 90:
            _dense(c, s, component, (left + 10, top, span - 20, tall), tint, True)
            continue
        inset = 14 if c.compact else 20
        inner = (left + inset, top + inset, span - inset * 2, tall - inset * 2)
        if tall < 200:
            _dense(c, s, component, inner, tint, True)
            continue
        primary = component.primary("usagePercent")
        _caption(c, f"0{index + 1} / {component.label}", left + inset, top + 30, span - inset * 2, tint)
        c.line(left + inset, top + 47, left + span - inset, top + 47, tint, opacity=.4)
        _caption(c, component.name, left + inset, top + 78, span - inset * 2)
        _text(c, primary.text, left + inset, top + 154, min(70, span * .22), span - inset * 2, tint, True)
        _caption(c, primary.label, left + inset, top + 185, span - inset * 2)
        _bar(c, left + inset, top + 214, span - inset * 2, 16, primary, tint, True)
        _details(c, component, primary, left + inset, top + 282, span - inset * 2, 34)
        for fraction in (0, .25, .5, .75, 1):
            tick_x = left + inset + (span - inset * 2) * fraction
            c.line(tick_x, top + tall - 14, tick_x, top + tall - 9, tint, opacity=.5)


def _horizon(c, s, components):
    c.gradient(0, 258, c.width, 222, [s.base, blend(s.base, s.palette[1], .19)], vertical=True)
    c.gradient(0, 370, c.width, 110,
               [blend(s.base, s.palette[0], .09), blend(s.base, s.palette[2], .25)])
    for index, (component, box) in enumerate(zip(components, _boxes(c, components, gap=40))):
        x, y, width, height = box
        tint = s.palette[index % 3]
        if height < 200:
            _dense(c, s, component, box, tint)
            continue
        primary = component.primary("usagePercent")
        _caption(c, component.label, x, y + 22, width, tint)
        _text(c, primary.text, x, y + 112, min(76, width * .23), width, bold=True)
        _caption(c, primary.label, x, y + 148, width)
        _details(c, component, primary, x, y + 204, width, 34)
        _trace(c, component, component.metric("usagePercent"), (x, y + 286, width, 59), tint)


def _blocks(c, s, components):
    treatments = (("#244BC4", "#FFFFFF", "#D0DCFF"),
                  ("#EAE6DA", "#1D2E38", "#526264"),
                  ("#F0CA43", "#23302D", "#525B40"))
    for index, (component, box) in enumerate(zip(components, _boxes(c, components, gap=12))):
        x, y, width, height = box
        background, foreground, muted = treatments[index % 3]
        foreground = s.palette.overrides.get("textPrimary", foreground)
        muted = s.palette.overrides.get("textSecondary", muted)
        c.rect(x, y, width, height, background)
        pad = 16 if c.compact else 24
        primary = component.primary("usagePercent")
        if height < 200:
            _caption(c, component.label, x + pad, y + 20, width * .4, foreground)
            _text(c, primary.text, x + width - pad, y + (25 if height < 64 else 35),
                  25 if height < 64 else 35, width * .55, foreground, True, "right")
            extras = [item for item in component.metrics if item.key != primary.key]
            if height >= 45:
                _caption(c, " · ".join(_inline(item) for item in extras), x + pad,
                         y + height - (14 if height >= 64 else 3),
                         width - pad * 2, muted)
            continue
        _caption(c, component.label, x + pad, y + 34, width - pad * 2, foreground)
        _caption(c, component.name, x + pad, y + 69, width - pad * 2, muted)
        _text(c, primary.text, x + pad, y + 173, min(83, width * .23), width - pad * 2, foreground, True)
        _caption(c, primary.label, x + pad, y + 209, width - pad * 2, muted)
        c.line(x + pad, y + 239, x + width - pad, y + 239, foreground, opacity=.3)
        _details(c, component, primary, x + pad, y + 280, width - pad * 2, 34, foreground, muted)
        _bar(c, x + pad, y + height - 17, width - pad * 2, 4, primary, foreground, fg=foreground)


def _quiet(c, s, components):
    for index, (component, box) in enumerate(zip(components, _boxes(c, components, gap=50))):
        x, y, width, height = box
        tint = s.palette[index % 3]
        if height < 200:
            _dense(c, s, component, box, s.fg)
            continue
        primary = component.primary("usagePercent")
        _caption(c, component.label, x, y + 38, width, tint)
        _text(c, primary.text, x, y + 153, min(78, width * .23), width, bold=True)
        _caption(c, primary.label, x, y + 188, width)
        c.line(x, y + 225, x + 50, y + 225, tint, 2)
        _details(c, component, primary, x, y + 275, width, 35)
        _caption(c, component.name, x, y + 345, width)


def _pulse(c, s, components):
    memory = next((item for item in components if item.key == "memory"), None)
    processors = [item for item in components if item.key != "memory"]
    x, y, width, height = _area(c)
    if c.compact or not processors or len(processors) > 3:
        for index, (component, box) in enumerate(zip(components, _rows(_area(c), len(components), 16))):
            tint = s.palette[index % 3]
            _dense(c, s, component, box, tint)
            left, top, span, tall = box
            if tall >= 140:
                _trace(c, component, component.metric("usagePercent"),
                       (left + span * .48, top + tall - 55, span * .52, 25), tint)
        return
    lane_width = width * .76 - 30 if memory else width
    lanes = _rows((x, y, lane_width, height), len(processors), 20)
    for index, (component, box) in enumerate(zip(processors, lanes)):
        left, top, span, tall = box
        tint = s.palette[index % 2]
        primary = component.primary("usagePercent")
        c.line(left, top + 7, left, top + tall - 5, tint, 3)
        if tall < 140:
            _dense(c, s, component, (left + 20, top, span - 20, tall), tint)
            continue
        _caption(c, component.label, left + 22, top + 23, span * .55, tint)
        _caption(c, component.name, left + 22, top + 50, span * .55)
        _text(c, primary.text, left + span, top + 39, 46, span * .4, tint, True, "right")
        extras = [item for item in component.metrics if item.key != primary.key]
        _caption(c, " · ".join(_inline(item) for item in extras),
                 left + 22, top + 83, span - 22)
        _trace(c, component, component.metric("usagePercent"),
               (left + 22, top + 103, span - 22, max(18, tall - 111)), tint, True)
    if memory:
        left = x + width * .76
        span = width * .24
        primary = memory.primary("usagePercent")
        c.line(left - 16, y, left - 16, y + height, s.fg, opacity=.18)
        _caption(c, "RAM", left + 4, y + 23, span - 4, s.palette[2])
        _text(c, primary.text, left + 4, y + 127, min(72, span * .24), span - 4, s.palette[2], True)
        _caption(c, primary.label, left + 4, y + 163, span - 4)
        _details(c, memory, primary, left + 4, y + 228, span - 4, 40)
        _bar(c, left + 4, y + height - 8, span - 4, 4, primary, s.palette[2])


_RENDERERS = {
    "signal": _signal,
    "ledger": _ledger,
    "orbit": _airflow,
    "terminal": _terminal,
    "thermal": _thermal_atlas,
    "blueprint": _blueprint,
    "horizon": _horizon,
    "blocks": _blocks,
    "quiet": _quiet,
    "pulse": _pulse,
}


def render(c, s, style):
    components = _components(s.sensors)
    _record(components, clean(s.sensors.get("updatedAtUtc")))
    _heading(c, s, style)
    if style in ("thermal", "orbit"):
        parts, ram = _cooling_parts(s.sensors)
        if parts or ram is not None:
            _RENDERERS[style](c, s, parts, ram)
        else:
            _empty(c, s)
        _footer(c, s, parts if parts else ([ram] if ram is not None else []))
        return
    if components:
        _RENDERERS[style](c, s, components)
    else:
        _empty(c, s)
    _footer(c, s, components)
# VBWR E
