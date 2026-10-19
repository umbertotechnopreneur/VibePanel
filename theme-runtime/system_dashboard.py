# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Paged instruments for measured hardware; no synthetic history or missing-value filler."""
from __future__ import annotations

import math
from collections import OrderedDict

import skia
from canvas import blend, clean, optional_number, clip, progress_color
from hardware import _components, _record, _points, _text
from system_details import HARDWARE_KINDS, cooling_values, detail_rows, detail_step, memory_view, reading_rows, sensor_groups, spd_groups, value_text

NAMES = {"thermal": "THERMAL ATLAS", "orbit": "AIRFLOW", "terminal": "MATRIX"}
_FAN_ROTATIONS = OrderedDict()


def readings(raw, kind):
    return [(clean(item.get("name")), value) for item in raw.get("readings", [])
            if isinstance(item, dict) and item.get("type") == kind
            and (value := optional_number(item.get("value"))) is not None]


def measured(raw, kind, name):
    return next((value for label, value in readings(raw, kind) if label == name), None)


def devices(s):
    raw = s.sensors
    result = []
    if isinstance(raw.get("cpu"), dict):
        result.append(("cpu", "CPU", raw["cpu"]))
    gpus = [gpu for gpu in raw.get("gpus", []) if isinstance(gpu, dict)]
    if gpus:
        index = s.page_cycle % len(gpus)
        result.append((f"gpu-{index}", "GPU" if len(gpus) == 1 else f"GPU {index + 1}/{len(gpus)}", gpus[index]))
    if isinstance(raw.get("memory"), dict):
        result.append(("memory", "RAM", raw["memory"]))
    return result


def short_name(key, raw):
    if key == "memory":
        modules = raw.get("modules") or []
        return " · ".join(value for value in (clean(raw.get("type")), f"{len(modules)} DIMMs" if modules else "") if value)
    return clean(raw.get("name"))


def metadata(key, raw):
    result = []
    if key == "memory":
        used, total = optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB"))
        if used is not None and total is not None:
            result.append(("USED / TOTAL", f"{used:.1f} / {total:.1f} GiB"))
        elif total is not None:
            result.append(("TOTAL", f"{total:.1f} GiB"))
        elif used is not None:
            result.append(("USED", f"{used:.1f} GiB"))
        if (speed := optional_number(raw.get("configuredSpeedMTs"))) is not None:
            result.append(("CONFIGURED", f"{speed:.0f} MT/s"))
    else:
        if (clock := optional_number(raw.get("clockMHz"))) is not None:
            result.append(("CLOCK", f"{clock / 1000:.2f} GHz"))
        if (temp := optional_number(raw.get("temperatureC"))) is not None:
            result.append(("TEMPERATURE", f"{temp:.1f}°C"))
    return result


def arc(c, x, y, radius, start, sweep, tint, width=7, opacity=1, segments=False):
    # Piecewise arcs use the same CPU Skia primitives at all output sizes.
    count = max(1, int(abs(sweep) / 3))
    for index in range(count):
        if segments and index % 4 == 3:
            continue
        a = math.radians(start + sweep * index / count)
        b = math.radians(start + sweep * (index + 1) / count)
        c.line(x + math.cos(a) * radius, y + math.sin(a) * radius,
               x + math.cos(b) * radius, y + math.sin(b) * radius, tint, width, opacity)


def dial(c, s, x, y, radius, value, unit, maximum, tint, style):
    start, sweep = (135, 270) if style in ("blueprint", "thermal", "ledger") else (-90, 360)
    segments = style in ("terminal", "signal", "blocks")
    arc(c, x, y, radius, start, sweep, tint, 7, .13, segments)
    if value is not None:
        active = progress_color(tint, value / maximum, s.light)
        arc(c, x, y, radius, start, sweep * clip(value / maximum), active, 7, 1, segments)
    if style in ("blueprint", "orbit", "thermal"):
        for index in range(21):
            angle = math.radians(start + sweep * index / 20)
            inner = radius + 11
            outer = inner + (6 if index % 5 == 0 else 3)
            c.line(x + math.cos(angle) * inner, y + math.sin(angle) * inner,
                   x + math.cos(angle) * outer, y + math.sin(angle) * outer, tint, 1, .4)
    if value is not None:
        _text(c, f"{value:.0f}" if unit == "%" else f"{value:.1f}", x, y + min(10, radius * .2), radius * .57, radius * 1.7, s.fg, True, "center")
        _text(c, unit, x, y + max(24, radius * .52), 15, radius * 1.5, active, align="center")
    if sweep < 360:
        _text(c, "0", x - radius * .7, y + radius + 20, 11, 50, s.muted, align="center")
        _text(c, str(maximum), x + radius * .7, y + radius + 20, 11, 50, s.muted, align="center")


def background(c, s, style):
    c.gradient(0, 0, c.width, 480, [blend(s.base, s.palette[0], .13), s.base, blend(s.base, s.palette[1], .10)], vertical=True)
    if style in ("blueprint", "terminal"):
        for x in range(0, int(c.width), 28):
            c.line(x, 0, x, 480, s.palette[0], opacity=.045)
        for y in range(0, 480, 28):
            c.line(0, y, c.width, y, s.palette[0], opacity=.045)
    if style == "terminal":
        # Static decorative glyphs stay outside the data regions.
        for index in range(24):
            x = 12 + index * (c.width - 24) / 24
            _text(c, f"{index * 7 % 16:X}", x, 454, 10, 15, s.palette[0])
    elif style in ("orbit", "pulse", "signal"):
        for radius in (140, 210, 280):
            c.circle(c.width - 65, 60, radius, s.palette[1], opacity=.07, stroke=1)
    elif style == "horizon":
        for index in range(7):
            c.line(0, 260 + index * 26, c.width, 160 + index * 26, s.palette[1], 2, .07)


def panel(c, s, box, tint, style):
    x, y, w, h = box
    c.gradient(x, y, w, h, [blend(s.base, tint, .14), blend(s.base, tint, .025)], vertical=True, radius=0 if style == "blueprint" else 12)
    if style in ("blueprint", "terminal"):
        for px in (x, x + w - 12):
            c.line(px, y, px + 12, y, tint, 2, .7)
    else:
        c.line(x + 16, y, x + w - 16, y, tint, 2, .55)


def frame(c, s, style):
    margin = 24 if c.compact else 40
    title = NAMES.get(style, style.upper())
    _text(c, title, margin, 31, 18, c.width - margin * 2 - 100, s.fg, True)
    _text(c, s.now.strftime("%H:%M"), c.width - margin, 31, 20, 90, s.muted, align="right")
    _text(c, s.page_title.upper(), margin, 52, 11, c.width - 100, s.palette[0])
    bottom = 441
    c.line(margin, bottom, c.width - margin, bottom, s.fg, opacity=.12)
    _text(c, "VIBE PANEL", margin, 468, 11, 120, s.muted)
    for index in range(s.page_count):
        x = c.width / 2 - s.page_count * 12 + index * 24
        c.rect(x, 459, 17, 3, s.palette[0], radius=1, opacity=1 if index == s.page_index else .2)
        if index == s.page_index:
            c.rect(x, 459, 17 * s.page_progress, 3, progress_color(s.palette[0], s.page_progress, s.light), radius=1)
    _text(c, f"{s.page_index + 1:02d} / {s.page_count:02d}", c.width - margin, 468, 11, 90, s.muted, align="right")


def overview(c, s, style, items):
    count = len(items)
    if not count:
        return
    gap, margin = (10, 24) if c.compact else (22, 40)
    for index, (key, label, raw) in enumerate(items):
        tint = s.palette[index % 3]
        load = optional_number(raw.get("usagePercent"))
        name = short_name(key, raw)
        info = metadata(key, raw)
        if c.compact:
            height = (354 - (count - 1) * gap) / count
            y = 72 + index * (height + gap)
            panel(c, s, (margin, y, 432, height), tint, style)
            if load is not None:
                dial(c, s, 83, y + height / 2, min(37, height * .32), load, "%", 100, tint, style)
            _text(c, label + (" · " + name if name else ""), 142, y + 25, 14, 292, s.fg, True)
            for row, (_, value) in enumerate(info):
                _text(c, value, 142, y + 57 + row * 25, 18, 292, tint if row == 0 else s.muted)
        else:
            width = (1200 - gap * (count - 1)) / count
            x = margin + index * (width + gap)
            panel(c, s, (x, 77, width, 345), tint, style)
            _text(c, label, x + 22, 104, 14, width - 44, tint, True)
            _text(c, name, x + 22, 130, 17, width - 44, s.fg, True)
            if load is not None:
                dial(c, s, x + width / 2, 238, 75, load, "%", 100, tint, style)
            for column, (title, value) in enumerate(info):
                half = (width - 44) / max(1, len(info))
                xx = x + 22 + column * half
                _text(c, title, xx, 374, 10, half - 8, s.muted)
                value_size = 21 if key != "memory" else 15 if len(info) > 1 else 17
                _text(c, value, xx, 400, value_size, half - 8, tint, True)


def history(c, s, component, box, tint):
    x, y, w, h = box
    metric = component.metric("usagePercent") if component else None
    if metric is None:
        return
    for fraction, label in ((0, "100"), (.5, "50"), (1, "0")):
        yy = y + fraction * h
        c.line(x + 25, yy, x + w, yy, tint, opacity=.13)
        _text(c, label, x + 19, yy + 4, 9, 24, s.muted, align="right")
    points = _points(component, metric)
    if not points:
        return
    oldest, newest = points[0][0], points[-1][0]
    span = max(10, newest - oldest)
    plotted = [(x + 27 + (1 - (newest - stamp) / span) * (w - 30), y + h * (1 - clip(value / 100))) for stamp, value in points]
    if len(plotted) > 1:
        c.polygon([(plotted[0][0], y + h), *plotted, (plotted[-1][0], y + h)], tint, .07)
        for start, end in zip(plotted, plotted[1:]):
            c.line(*start, *end, tint, 2.5)
    c.circle(*plotted[-1], 3, tint)
    _text(c, f"{span:.0f}s", x + 27, y + h + 17, 10, 50, s.muted)
    _text(c, "NOW", x + w, y + h + 17, 10, 45, s.muted, align="right")


def aggregate_activity(c, s, raw, box, tint):
    x, y, w, h = box
    rows = reading_rows(raw, ("Clock",), aggregate=True)
    if rows:
        (label, value, unit), = instrument_page(rows, detail_step(s), 1)[0]
        _text(c, label, x, y + 13, 12, w, s.muted)
        _text(c, value_text(value, unit), x, y + 53, 27, w, tint, True)


def occupancy(c, s, used, total, box, tint, label):
    if used is None or total is None or total <= 0:
        return
    x, y, w, h = box
    _text(c, label, x, y + 13, 11, w, s.muted)
    _text(c, f"{used:.1f} / {total:.1f} GiB", x, y + 44, 23, w, tint, True)
    active = progress_color(tint, used / total, s.light)
    for index in range(32):
        filled = (index + 1) / 32 <= clip(used / total)
        c.rect(x + index * w / 32, y + h - 19, w / 32 - 3, 16, active if filled else tint, radius=2,
               opacity=1 if filled else .14)


def activity(c, s, style, items, components):
    count = len(items)
    if not count:
        return
    for index, (key, label, raw) in enumerate(items):
        tint = s.palette[index % 3]
        component = next((item for item in components if item.key == key), None)
        load = optional_number(raw.get("usagePercent"))
        if c.compact:
            h = 348 / count
            y = 75 + index * h
            _text(c, label, 24, y + 13, 13, 200, tint, True)
            if load is not None:
                _text(c, f"{load:.0f}%", 455, y + 13, 20, 100, s.fg, True, "right")
            history(c, s, component, (24, y + 30, 430, h - 58), tint)
        else:
            w = (1200 - 22 * (count - 1)) / count
            x = 40 + index * (w + 22)
            panel(c, s, (x, 77, w, 345), tint, style)
            _text(c, label, x + 18, 104, 13, w - 36, tint, True)
            if load is not None:
                _text(c, f"{load:.0f}%", x + w - 18, 111, 30, 140, s.fg, True, "right")
            _text(c, short_name(key, raw), x + 18, 139, 12, w - 36, s.muted)
            history(c, s, component, (x + 14, 166, w - 32, 115), tint)
            if key == "cpu":
                aggregate_activity(c, s, raw, (x + 20, 320, w - 40, 80), tint)
            elif key == "memory":
                occupancy(c, s, optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB")), (x + 20, 320, w - 40, 80), tint, "PHYSICAL RAM")
            else:
                used, total = measured(raw, "SmallData", "GPU Memory Used"), measured(raw, "SmallData", "GPU Memory Total")
                occupancy(c, s, used / 1024 if used is not None else None, total / 1024 if total is not None else None,
                          (x + 20, 320, w - 40, 80), tint, "VRAM")


def instrument_page(rows, cycle, count):
    start = (cycle % math.ceil(len(rows) / count)) * count
    return rows[start:start + count], start


def fan_angle(label, rpm, now):
    previous = _FAN_ROTATIONS.get(label)
    angle = 0
    if previous and now >= previous[0]:
        # A readable emblem, not a simulation of the rotor's physical speed.
        speed = 30 + min(35, rpm / 100) if rpm > 0 else 0
        angle = (previous[1] + min(2, now - previous[0]) * speed) % 360
    _FAN_ROTATIONS[label] = (now, angle)
    _FAN_ROTATIONS.move_to_end(label)
    while len(_FAN_ROTATIONS) > 32:
        _FAN_ROTATIONS.popitem(last=False)
    return angle


def fan_icon(c, x, y, radius, tint, angle):
    c.rect(x - radius, y - radius, radius * 2, radius * 2, tint,
           radius=radius * .22, opacity=.35, stroke=1.2)
    c.circle(x, y, radius * .82, tint, opacity=.25, stroke=1)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        c.circle(x + dx * radius * .77, y + dy * radius * .77, radius * .035, tint, .55)
    blade = skia.Path()
    blade.moveTo(.16, -.12)
    blade.cubicTo(.20, -.42, .40, -.73, .62, -.56)
    blade.cubicTo(.85, -.35, .56, -.05, .20, .12)
    blade.close()
    c.native.save()
    c.native.translate(x, y)
    c.native.rotate(angle)
    c.native.scale(radius, radius)
    for _ in range(4):
        c.native.drawPath(blade, c.paint(tint, .9, 1.6 / radius))
        c.native.rotate(90)
    c.native.restore()
    c.circle(x, y, radius * .16, tint, stroke=1.6)


def power_instruments(c, s, style, rows, box):
    x, y, w, h = box
    visible, start = instrument_page(rows, s.page_cycle, 1 if c.compact else 2)
    gap = 14
    width = (w - gap * (len(visible) - 1)) / len(visible)
    for index, (label, value, unit) in enumerate(visible):
        xx = x + index * (width + gap)
        panel(c, s, (xx, y, width, h), s.palette[0], style)
        if c.compact:
            _text(c, label, xx + 16, y + 23, 13, width * .56, s.muted)
            _text(c, f"{value:.0f} {unit}", xx + width - 16, y + 38, 29, width * .38, s.palette[0], True, "right")
        else:
            _text(c, "POWER DRAW", xx + 20, y + 27, 12, width - 40, s.palette[0], True)
            _text(c, label, xx + 20, y + 53, 14, width - 40, s.muted)
            bolt = [(8, 0), (-8, 21), (1, 21), (-5, 38), (13, 14), (4, 14), (8, 0)]
            for a, b in zip(bolt, bolt[1:]):
                c.line(xx + 31 + a[0], y + h - 59 + a[1], xx + 31 + b[0], y + h - 59 + b[1], s.palette[0], 1.8, .8)
            baseline = y + (h * .63 if h > 180 else h - 18)
            _text(c, f"{value:.0f} {unit}", xx + width - 20, baseline, 64 if h > 180 else 42, width - 100, s.fg, True, "right")
        if len(rows) > len(visible):
            _text(c, f"{start + index + 1}/{len(rows)}", xx + 16 if c.compact else xx + width - 16,
                  y + h - 8 if c.compact else y + 23, 10, 45, s.muted, align="left" if c.compact else "right")


def fan_instruments(c, s, style, rows, box):
    x, y, w, h = box
    count = 2 if c.compact else 3
    if len(rows) > count and rows[0][1] > 0:
        # Keep one running fan visible while the remaining named channels rotate.
        # A late page containing only stopped headers otherwise looks like lost sensors.
        rotating, _ = instrument_page(rows[1:], s.page_cycle, count - 1)
        visible = [rows[0], *rotating]
    else:
        visible, _ = instrument_page(rows, s.page_cycle, count)
    _text(c, "FAN SPEED", x, y + 14, 12, w * .6, s.palette[1], True)
    _text(c, f"{len(rows)} channels", x + w, y + 14, 11, w * .35, s.muted, align="right")
    top = y + 28
    height = h - 28
    if c.compact:
        step = height / len(visible)
        for index, (label, value, unit) in enumerate(visible):
            yy = top + index * step
            fan_icon(c, x + 17, yy + step / 2, min(17, step / 2 - 4), s.palette[1], fan_angle(label, value, s.elapsed_seconds))
            _text(c, label, x + 46, yy + step / 2 + 5, 12, w - 186, s.muted)
            _text(c, f"{value:.0f} {unit}", x + w, yy + step / 2 + 6, 20, 134, s.fg, True, "right")
        return
    gap = 14
    width = (w - gap * (len(visible) - 1)) / len(visible)
    for index, (label, value, unit) in enumerate(visible):
        xx = x + index * (width + gap)
        center = xx + width / 2
        source, _, name = label.partition(" · ")
        panel(c, s, (xx, top, width, height), s.palette[1], style)
        _text(c, source, xx + 16, top + 25, 11, width - 32, s.muted, True)
        fan_icon(c, center, top + height * .36, min(52, height * .16), s.palette[1], fan_angle(label, value, s.elapsed_seconds))
        _text(c, f"{value:.0f}", center, top + height - 45, 30, width - 24, s.fg, True, "center")
        _text(c, unit, center, top + height - 27, 10, width - 24, s.palette[1], align="center")
        _text(c, name, center, top + height - 12, 12, width - 24, s.muted, align="center")


def cooling(c, s, style):
    thermal, powers, fans = cooling_values(s.sensors)
    if thermal:
        thermal, _ = instrument_page(thermal, detail_step(s), 2)
    if c.compact:
        for index, (key, label, raw) in enumerate(thermal[:2]):
            x = 130 + index * 220 if len(thermal) > 1 else 240
            _text(c, label, x, 91, 13, 180, s.palette[index], True, "center")
            dial(c, s, x, 168, 53, raw["temperatureC"], "°C", 120, s.palette[index], style)
        y = 270 if thermal else 90
        if powers:
            power_instruments(c, s, style, powers, (28, y, 424, 55))
            y += 69
        if fans:
            fan_instruments(c, s, style, fans, (28, y, 424, 426 - y))
    else:
        for index, (key, label, raw) in enumerate(thermal[:2]):
            x = 40 + index * 300
            panel(c, s, (x, 77, 278, 345), s.palette[index], style)
            _text(c, label, x + 20, 105, 13, 238, s.palette[index], True)
            _text(c, short_name(key, raw), x + 20, 132, 15, 238, s.fg, True)
            dial(c, s, x + 139, 250, 72, raw["temperatureC"], "°C", 120, s.palette[index], style)
            if (clock := optional_number(raw.get("clockMHz"))) is not None:
                _text(c, f"{clock / 1000:.2f} GHz", x + 139, 396, 23, 230, s.palette[index], True, "center")
        x = 40 + min(2, len(thermal)) * 300
        w = 1240 - x
        if powers:
            power_instruments(c, s, style, powers, (x, 77, w, 128 if fans else 345))
        fan_y = 221 if powers else 77
        if fans:
            fan_instruments(c, s, style, fans, (x, fan_y, w, 422 - fan_y))


def detail_panel(c, s, style, group, box, tint, step):
    _, label, name, rows = group
    x, y, w, h = box
    panel(c, s, box, tint, style)
    _text(c, label, x + 20, y + 27, 13, w - 40, tint, True)
    _text(c, name, x + 20, y + 53, 14, w - 40, s.fg, True)
    visible, start = detail_rows(rows, step)
    row_height = (h - 93) / max(1, len(visible))
    for index, (name, value, unit) in enumerate(visible):
        yy = y + 80 + index * row_height
        _text(c, name, x + 20, yy, 12, w - 40, s.muted)
        value_tint = progress_color(tint, value / 120, s.light) if unit == "°C" else tint
        _text(c, value_text(value, unit), x + 20, yy + 27, 25, w - 40, value_tint, True)
        if unit == "°C":
            c.rect(x + 20, yy + 37, w - 40, 2, tint, opacity=.13)
            c.rect(x + 20, yy + 37, (w - 40) * clip(value / 120), 2, value_tint)
        elif index < len(visible) - 1:
            c.line(x + 20, yy + 37, x + w - 20, yy + 37, tint, opacity=.12)
    if any(unit == "°C" for _, _, unit in visible):
        _text(c, "SCALE 0–120°C", x + 20, y + h - 8, 10, (w - 40) / 2, s.muted)
    if len(rows) > len(visible):
        _text(c, f"{start + 1}–{start + len(visible)} / {len(rows)}", x + w - 20, y + h - 8,
              10, w - 40, s.muted, align="right")


def detail_groups(c, s, style, groups, columns=3, step=None):
    if not groups:
        return
    step = detail_step(s) if step is None else step
    group_count = 1 if c.compact else columns
    bank_count = math.ceil(len(groups) / group_count)
    visible, _ = instrument_page(groups, step, group_count)
    # Rotate readings independently of the group bank; every channel stays reachable.
    row_step = step // bank_count
    gap, margin = (16, 24) if c.compact else (22, 40)
    width = (c.width - margin * 2 - gap * (len(visible) - 1)) / len(visible)
    for index, group in enumerate(visible):
        key = group[0]
        tint = s.palette[0 if key == "cpu" else 1 if key.startswith("gpu-") else 2]
        height = 93 + 63 * len(detail_rows(group[3], row_step)[0])
        detail_panel(c, s, style, group, (margin + index * (width + gap), 77, width, height), tint, row_step)


def details(c, s, style, kinds):
    detail_groups(c, s, style, sensor_groups(s.sensors, kinds))


def memory_page(c, s, style, step):
    raw = s.sensors.get("memory") or {}
    summary = metadata("memory", raw)
    if memory_type := clean(raw.get("type")):
        summary.insert(0, ("MEMORY TYPE", memory_type))
    if (clock := optional_number(raw.get("clockMHz"))) is not None:
        summary.append(("MEMORY CLOCK", f"{clock:.0f} MHz"))
    margin, gap = (24, 16) if c.compact else (40, 22)
    width = c.width - margin * 2
    if c.compact and summary:
        # Keep every measured summary reachable on square displays too.
        summary, _ = instrument_page(summary, step, 2)
    for index, (label, value) in enumerate(summary):
        cell = width / len(summary)
        x = margin + index * cell
        _text(c, label, x, 89, 10, cell - 12, s.muted)
        _text(c, value, x, 118, 16 if c.compact else 27, cell - 12, s.palette[2], True)
    modules = [module for module in raw.get("modules", []) or [] if isinstance(module, dict)]
    if not modules:
        occupancy(c, s, optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB")),
                  (margin, 220, width, 120), s.palette[2], "PHYSICAL RAM")
        return
    visible, start = instrument_page(modules, step, 1 if c.compact else 2)
    cell = (width - gap * (len(visible) - 1)) / len(visible)
    for index, module in enumerate(visible):
        x = margin + index * (cell + gap)
        tint = s.palette[2]
        panel(c, s, (x, 149, cell, 273), tint, style)
        maker, part = clean(module.get("manufacturer")), clean(module.get("partNumber"))
        _text(c, maker or "RAM MODULE", x + 20, 178, 14, cell - 100, tint, True)
        _text(c, f"{start + index + 1} / {len(modules)}", x + cell - 20, 178, 11, 65, s.muted, align="right")
        _text(c, part or clean(module.get("name")), x + 20, 212, 25 if c.compact else 30, cell - 40, s.fg, True)
        if serial := clean(module.get("serialNumber")):
            _text(c, "S/N  " + serial, x + 20, 237, 12, cell - 40, s.muted)
        if (capacity := optional_number(module.get("capacityGiB"))) is not None:
            _text(c, "CAPACITY", x + 20, 263, 10, cell - 160, s.muted)
            _text(c, f"{capacity:g} GiB", x + 20, 293, 30, cell - 160, tint, True)
        memory_outline(c, x + cell - 123, 260, tint)
        identity = [("SLOT", clean(module.get("deviceLocator"))), ("BANK", clean(module.get("bankLabel")))]
        for row, (label, value) in enumerate((item for item in identity if item[1])):
            yy = 322 + row * 24
            _text(c, label, x + 20, yy, 10, 58, s.muted)
            _text(c, value, x + 84, yy, 13, cell - 104, s.fg)
        module_type = clean(module.get("type"))
        speed = optional_number(module.get("configuredSpeedMTs"))
        if module_type or speed is not None:
            c.line(x + 20, 367, x + cell - 20, 367, tint, opacity=.16)
            if module_type:
                _text(c, module_type, x + 20, 399, 22, (cell - 40) / 2, tint, True)
            if speed is not None:
                _text(c, "CONFIGURED", x + cell - 20, 384, 9, (cell - 40) / 2, s.muted, align="right")
                _text(c, f"{speed:.0f} MT/s", x + cell - 20, 406, 18, (cell - 40) / 2, s.fg, True, "right")


def memory_outline(c, x, y, tint):
    # A small DIMM symbol, not a diagram of the installed module's chip layout.
    c.line(x, y, x + 100, y, tint, 1.5, .7)
    c.line(x, y, x, y + 29, tint, 1.5, .7)
    c.line(x + 100, y, x + 100, y + 29, tint, 1.5, .7)
    c.line(x, y + 29, x + 43, y + 29, tint, 1.5, .7)
    c.line(x + 49, y + 29, x + 100, y + 29, tint, 1.5, .7)
    for offset in (8, 37, 66):
        c.rect(x + offset, y + 7, 23, 14, tint, radius=2, opacity=.17)
    for offset in range(7, 100, 7):
        if offset != 49:
            c.line(x + offset, y + 30, x + offset, y + 34, tint, 2, .6)


def render(c, s, style):
    components = _components(s.sensors)
    _record(components, clean(s.sensors.get("updatedAtUtc")))
    items = devices(s)
    background(c, s, style)
    frame(c, s, style)
    if s.page_id == "overview":
        overview(c, s, style, items)
    elif s.page_id == "activity":
        activity(c, s, style, items, components)
    elif s.page_id == "cooling":
        cooling(c, s, style)
    elif s.page_id == "electrical":
        details(c, s, style, HARDWARE_KINDS)
    elif s.page_id == "temperatures":
        details(c, s, style, ("Temperature",))
    elif s.page_id == "memory":
        section, step = memory_view(s.sensors, detail_step(s))
        if section == "spd":
            detail_groups(c, s, style, spd_groups(s.sensors), columns=2, step=step)
        elif section == "modules":
            memory_page(c, s, style, step)
    else:
        raise ValueError("Unknown System page: " + s.page_id)
# VBWR E
