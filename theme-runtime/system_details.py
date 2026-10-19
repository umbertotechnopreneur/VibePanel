# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Small groups of real readings for System pages, without per-core noise."""
from __future__ import annotations

import re

from canvas import clean, optional_number

_INDIVIDUAL_CORE = re.compile(r"\b(?:core|thread)\s*#?\s*\d+\b", re.IGNORECASE)
_UNITS = {"Voltage": "V", "Current": "A", "Power": "W", "Temperature": "°C", "Clock": "MHz", "Timing": "ns"}


def reading_rows(raw, kinds, aggregate=False):
    rows = []
    for item in (raw.get("readings") or [])[:256]:
        if not isinstance(item, dict) or item.get("type") not in kinds:
            continue
        name, kind = clean(item.get("name")), item["type"]
        value = optional_number(item.get("value"))
        if not name or value is None or (aggregate and _INDIVIDUAL_CORE.search(name)):
            continue
        rows.append((name, value, _UNITS.get(kind, clean(item.get("unit")))))
    return rows


def hardware_devices(sensors):
    candidates = []
    cpu = sensors.get("cpu")
    if isinstance(cpu, dict):
        candidates.append(("cpu", "CPU", cpu))
    gpus = [gpu for gpu in sensors.get("gpus", []) if isinstance(gpu, dict)]
    for index, gpu in enumerate(gpus):
        candidates.append((f"gpu-{index}", "GPU" if len(gpus) == 1 else f"GPU {index + 1}", gpu))
    board = sensors.get("motherboard")
    if isinstance(board, dict):
        candidates.append(("motherboard", "BOARD", board))
    return candidates


def sensor_groups(sensors, kinds):
    result = []
    for key, label, raw in hardware_devices(sensors):
        rows = reading_rows(raw, kinds, aggregate=key == "cpu")
        if "Temperature" in kinds and not rows:
            if key == "motherboard":
                rows = [(clean(t.get("name")), value, "°C") for t in raw.get("temperatures", [])
                        if isinstance(t, dict) and (value := optional_number(t.get("temperatureC"))) is not None]
            elif (value := optional_number(raw.get("temperatureC"))) is not None:
                rows = [(clean(raw.get("temperatureLabel")) or "Temperature", value, "°C")]
        if rows:
            result.append((key, label, clean(raw.get("name")), rows))
    return result


def cooling_values(sensors):
    thermal, powers, fans = [], [], []
    for key, label, raw in hardware_devices(sensors):
        if optional_number(raw.get("temperatureC")) is not None:
            thermal.append((key, label, raw))
        prefix = "CPU " if key == "cpu" else "GPU " if key.startswith("gpu-") else ""
        for name, value, unit in reading_rows(raw, ("Power",), aggregate=key == "cpu"):
            if key != "cpu" or name in ("Package", "CPU Package"):
                powers.append((label + " · " + name.removeprefix(prefix), value, unit))
        for fan in raw.get("fans") or []:
            if isinstance(fan, dict) and (value := optional_number(fan.get("rpm"))) is not None:
                fans.append((label + " · " + clean(fan.get("name")).removeprefix(prefix), value, "RPM"))
    # A measured stop remains visible after the engine has observed this fan running.
    fans.sort(key=lambda item: item[1] == 0)
    return thermal, powers, fans


def spd_groups(sensors):
    memory = sensors.get("memory") or {}
    modules = [module for module in (memory.get("spdModules") or [])[:16] if isinstance(module, dict)]
    groups = []
    for index, module in enumerate(modules):
        rows = reading_rows(module, ("Timing",))
        if rows:
            # Preserve the SPD source name; its SMBus index does not identify a physical slot.
            groups.append((f"memory-spd-{index}", f"SPD LIMITS · SOURCE {index + 1}", clean(module.get("name")), rows))
    return groups


def page_available(page_id, sensors):
    kinds = {"electrical": ("Voltage", "Current", "Power"), "temperatures": ("Temperature",), "clocks": ("Clock",)}
    if page_id in kinds:
        return bool(sensor_groups(sensors, kinds[page_id]))
    if page_id == "memory":
        return bool(sensors.get("memory"))
    if page_id == "memory-spd":
        return bool(spd_groups(sensors))
    if page_id == "cooling":
        return any(cooling_values(sensors))
    return True


def value_text(value, unit):
    digits = 3 if unit == "V" else 2 if unit == "ns" else 1 if unit in ("A", "°C", "GiB") else 0
    return f"{value:.{digits}f} {unit}"


def detail_step(s):
    # At most two groups per visit, with no new timer or sampling request.
    return s.page_cycle * 2 + min(1, int(s.page_progress * 2))
# VBWR E
