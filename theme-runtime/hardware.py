# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""System-theme compositions. Keep measured values and unavailable sensors distinct."""
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
    board = raw.get("motherboard")
    if isinstance(board, dict):
        metrics = _board_temperatures(board)
        if metrics:
            result.append(Component("motherboard", "BOARD", clean(board.get("name")), metrics))
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


def _board_temperatures(raw):
    """Keep board channel names; a generic channel is never called CPU temperature."""
    result = []
    readings = raw.get("temperatures") if isinstance(raw.get("temperatures"), list) else []
    for index, reading in enumerate(readings[:16]):
        if not isinstance(reading, dict):
            continue
        metric = _metric(reading, "temperatureC", reading.get("name") or "Board temperature", "°C", 1)
        if metric and not any(item.label == metric.label and item.value == metric.value for item in result):
            result.append(Metric(f"boardTemperature-{index}", metric.label, metric.value, metric.text))
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
                previous = {"stamp": "", "at": now, "samples": deque(maxlen=_MAX_SAMPLES), "times": deque(maxlen=_MAX_SAMPLES)}
            if previous["stamp"] != stamp:
                # A long gap starts a new trace rather than joining stale readings.
                if now - previous["at"] > 10:
                    previous["samples"].clear()
                    previous["times"].clear()
                previous["samples"].append(metric.value)
                previous["times"].append(now)
                previous["stamp"], previous["at"] = stamp, now
            elif now - previous["at"] > 10:
                previous["samples"].clear()
                previous["times"].clear()
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



def _points(component, metric):
    series = _HISTORY.get((component.key, component.name, metric.key))
    return list(zip(series["times"], series["samples"])) if series else []
# VBWR E
