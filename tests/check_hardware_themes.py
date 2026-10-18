# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""OFFLINE checks. Every execution needs explicit owner approval first.

All values below are synthetic fixtures confined to this test file. These checks
never query sensors, launch the studio, load a monitoring driver, or touch USB.
The default checks inspect normalization, bounded history and drawing commands.
Actual offscreen JPEG rendering additionally requires the explicit environment
opt-in VIBEPANEL_OFFLINE_RENDER_CHECKS=1 and the theme-runtime dependencies.
Neither mode establishes physical-device support or visual acceptance.
"""
from __future__ import annotations

import base64
import copy
from datetime import datetime
import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

os.environ["OPENBLAS_NUM_THREADS"] = "1"
os.environ["OMP_NUM_THREADS"] = "1"
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "theme-runtime"))

import hardware
from palette import ThemePalette


STYLES = ("signal", "ledger", "orbit", "terminal", "thermal", "blueprint",
          "horizon", "blocks", "quiet", "pulse")


def fixture_sensors(gpu_count=1):
    """OFFLINE synthetic input only. Never imported by the application runtime."""
    return {
        "cpu": {"name": "OFFLINE fixture CPU", "usagePercent": 37.5,
                "clockMHz": 4200, "temperatureC": 61.2,
                "clockLabel": "Average core clock", "temperatureLabel": "CPU package"},
        "gpus": [{"name": f"OFFLINE fixture GPU {index + 1}", "usagePercent": 43 + index,
                  "clockMHz": 1800 + index * 100, "temperatureC": 49.1 + index,
                  "clockLabel": "Core clock", "temperatureLabel": "GPU core"}
                 for index in range(gpu_count)],
        "memory": {"totalGiB": 32, "usedGiB": 12.5, "usagePercent": 39.0625},
        "updatedAtUtc": "2026-10-17T12:00:00Z",
        "notice": "OFFLINE synthetic fixture",
    }


def fixture_context(sensors, light=False, font_scale=1):
    return SimpleNamespace(
        sensors=sensors, light=light, font_scale=font_scale,
        base="#F3F5EF" if light else "#101820",
        fg="#20342A" if light else "#EDF4F1",
        muted="#5E6E66" if light else "#9AAEA7", accent="#78CEB1",
        palette=ThemePalette(("#78CEB1", "#79C8DE", "#D2C58D", "#A5ABBF"),
                             dict(textPrimary="#EDF4F1", textSecondary="#9AAEA7",
                                  accent="#78CEB1", warning="#FFAA00", info="#79C8DE"), {}, {}, light),
        now=datetime.fromisoformat("2026-10-17T19:00:00+07:00"))


class RecordingCanvas:
    """The public Canvas signatures, recorded without allocating or rendering pixels."""
    def __init__(self, context, compact=False):
        self.ctx, self.compact = context, compact
        self.width, self.height = (480 if compact else 1280), 480
        self.calls = []

    def text(self, value, x, y, size=24, tint=None, width=None, align="left", bold=False):
        if value:
            self.calls.append(("text", {"value": str(value), "x": x, "y": y,
                                        "size": size, "width": width, "align": align}))

    def text_size(self, role="caption"):
        base, minimum, maximum = {"instrument": (16, 16, 18), "caption": (18, 16, 22),
                                  "body": (20, 18, 26)}[role]
        return max(minimum, min(maximum, base * self.ctx.font_scale)) / self.ctx.font_scale

    def rect(self, x, y, width, height, tint, radius=0, opacity=1, stroke=0):
        self.calls.append(("rect", {"x": x, "y": y, "width": width, "height": height}))

    def line(self, x1, y1, x2, y2, tint, width=1, opacity=1):
        self.calls.append(("line", {"x1": x1, "y1": y1, "x2": x2, "y2": y2}))

    def circle(self, x, y, radius, tint, opacity=1, stroke=0):
        self.calls.append(("circle", {"x": x, "y": y, "radius": radius}))

    def gradient(self, x, y, width, height, colors, vertical=False, radius=0):
        self.calls.append(("gradient", {"x": x, "y": y, "width": width, "height": height}))

    def ring(self, x, y, radius, value=None, tint=None, stroke=4, marker=False):
        self.calls.append(("ring", {"x": x, "y": y, "radius": radius, "value": value}))

    def texts(self):
        return [arguments["value"] for kind, arguments in self.calls if kind == "text"]


class SensorNormalizationChecks(unittest.TestCase):
    def test_missing_null_and_empty_components_disappear(self):
        for raw in ({}, {"cpu": None, "gpus": None, "memory": None},
                    {"cpu": {}, "gpus": [None, {}], "memory": {}},
                    {"cpu": {"usagePercent": None}, "memory": {"totalGiB": None}}):
            with self.subTest(raw=raw):
                self.assertEqual(hardware._components(raw), [])

    def test_actual_zero_is_kept_for_every_metric(self):
        zero = {"usagePercent": 0, "clockMHz": 0, "temperatureC": 0}
        components = hardware._components({"cpu": zero, "gpus": [dict(zero)],
                                           "memory": {"totalGiB": 0, "usedGiB": 0, "usagePercent": 0}})
        self.assertEqual([item.label for item in components], ["CPU", "GPU", "RAM"])
        self.assertEqual([len(item.metrics) for item in components], [3, 3, 3])
        for component in components:
            self.assertTrue(all(metric.value == 0 for metric in component.metrics))
            self.assertEqual(component.metric("usagePercent").ratio, 0)

    def test_invalid_numbers_and_negative_usage_or_clock_are_not_data(self):
        invalid = (None, True, False, float("nan"), float("inf"), float("-inf"), "bad", [], {})
        for value in invalid:
            with self.subTest(value=value):
                self.assertEqual(hardware._components({"cpu": {"usagePercent": value,
                                                               "clockMHz": value, "temperatureC": value}}), [])
        self.assertEqual(hardware._components({"cpu": {"usagePercent": -1, "clockMHz": -3}}), [])
        cold_cpu = hardware._components({"cpu": {"temperatureC": -2.5}})[0]
        self.assertEqual(cold_cpu.metric("temperatureC").value, -2.5)

    def test_only_available_fields_survive_with_precise_source_labels(self):
        cpu = hardware._components({"cpu": {"name": "Fixture CPU", "usagePercent": None,
                                              "clockMHz": 3200, "clockLabel": "Average core clock",
                                              "temperatureC": 51.2, "temperatureLabel": "CPU package"}})[0]
        self.assertEqual([item.key for item in cpu.metrics], ["clockMHz", "temperatureC"])
        self.assertEqual(cpu.primary().key, "clockMHz")
        self.assertEqual(cpu.metric("clockMHz").label, "Average core clock")
        self.assertEqual(cpu.metric("temperatureC").label, "CPU package")
        self.assertIn("avg", hardware._inline(cpu.metric("clockMHz")))

    def test_multiple_gpus_keep_their_identity_when_one_is_unavailable(self):
        raw = fixture_sensors(3)
        raw["gpus"][1] = {"name": "Unavailable fixture GPU"}
        gpus = [item for item in hardware._components(raw) if item.key.startswith("gpu-")]
        self.assertEqual([item.key for item in gpus], ["gpu-0", "gpu-2"])
        self.assertEqual([item.label for item in gpus], ["GPU 1", "GPU 3"])
        self.assertIn("fixture GPU 3", gpus[1].name)

    def test_memory_has_only_ram_fields_and_never_derives_swap(self):
        raw = {"memory": {"totalGiB": 32, "usedGiB": 12, "usagePercent": 37.5,
                           "swapUsedGiB": 5, "swapTotalGiB": 8}, "swap": {"usedGiB": 5}}
        memory = hardware._components(raw)[0]
        self.assertEqual(memory.label, "RAM")
        self.assertEqual({metric.key for metric in memory.metrics}, {"totalGiB", "usedGiB", "usagePercent"})
        self.assertEqual(hardware._components({"memory": {"swapUsedGiB": 5}, "swap": {"usedGiB": 5}}), [])


class SensorHistoryChecks(unittest.TestCase):
    def setUp(self):
        hardware._HISTORY.clear()
        self.now = 0.0
        self.clock = patch.object(hardware.time, "monotonic", side_effect=lambda: self.now)
        self.clock.start()

    def tearDown(self):
        self.clock.stop()
        hardware._HISTORY.clear()

    def record_cpu(self, value, stamp):
        component = hardware._components({"cpu": {"usagePercent": value}})[0]
        hardware._record([component], stamp)
        return hardware._samples(component, component.metrics[0])

    def test_same_cached_timestamp_never_creates_new_readings(self):
        self.assertEqual(self.record_cpu(10, "first"), [10])
        self.now = .5
        self.assertEqual(self.record_cpu(90, "first"), [10])
        self.now = 1
        self.assertEqual(self.record_cpu(20, "second"), [10, 20])

    def test_series_contains_only_the_last_sixty_real_samples(self):
        samples = []
        for index in range(200):
            self.now = index * .1
            samples = self.record_cpu(index % 100, str(index))
        self.assertEqual(samples, list(range(40, 100)))
        self.assertEqual(len(samples), 60)

    def test_missing_metric_and_missing_timestamp_drop_the_old_history(self):
        self.record_cpu(10, "first")
        component = hardware._components({"cpu": {"temperatureC": 42}})[0]
        hardware._record([component], "second")
        self.assertTrue(all(key[2] == "temperatureC" for key in hardware._HISTORY))
        hardware._record([component], "")
        self.assertFalse(hardware._HISTORY)
        self.record_cpu(30, "third")
        hardware._record([], "fourth")
        self.assertFalse(hardware._HISTORY)

    def test_source_gap_starts_a_new_trace_and_frozen_snapshot_clears_it(self):
        self.record_cpu(10, "first")
        self.now = 1
        self.record_cpu(20, "second")
        self.now = 12
        self.assertEqual(self.record_cpu(30, "third"), [30])
        self.now = 23
        self.assertEqual(self.record_cpu(30, "third"), [])
        self.now = 24
        self.assertEqual(self.record_cpu(40, "fourth"), [40])

    def test_total_series_retention_is_bounded(self):
        components = hardware._components(fixture_sensors(100))
        hardware._record(components, "first")
        self.assertEqual(len(hardware._HISTORY), 64)
        self.assertTrue(all(len(series["samples"]) == 1 for series in hardware._HISTORY.values()))


class ThemeDrawingContractChecks(unittest.TestCase):
    def setUp(self):
        hardware._HISTORY.clear()

    def tearDown(self):
        hardware._HISTORY.clear()

    def test_all_ten_empty_screens_have_no_data_instruments(self):
        for style in STYLES:
            with self.subTest(style=style):
                context = fixture_context({})
                canvas = RecordingCanvas(context, compact=True)
                hardware.render(canvas, context, style)
                self.assertIn("No sensor data available", canvas.texts())
                self.assertFalse(any(kind in ("ring", "circle", "rect") for kind, _ in canvas.calls))
                self.assertNotIn("CPU", canvas.texts())
                self.assertNotIn("GPU", canvas.texts())
                self.assertNotIn("RAM", canvas.texts())

    def test_zero_is_displayed_and_missing_sections_do_not_leave_labels(self):
        for style in STYLES:
            with self.subTest(style=style):
                context = fixture_context({"cpu": {"usagePercent": 0}})
                canvas = RecordingCanvas(context, compact=True)
                hardware.render(canvas, context, style)
                text = " ".join(canvas.texts())
                self.assertIn("0%", text)
                self.assertIn("CPU", text)
                self.assertNotIn("GPU", text)
                self.assertNotIn("RAM", text)
                self.assertNotIn("Temperature", text)
                self.assertNotIn("Clock", text)

    def test_many_gpus_preserve_secondary_readings_in_compact_layout(self):
        sensors = fixture_sensors(4)
        components = hardware._components(sensors)
        expected = [metric.text for item in components for metric in item.metrics]
        for style in STYLES:
            for scale in (.65, 1, 1.6):
                with self.subTest(style=style, font_scale=scale):
                    context = fixture_context(sensors, font_scale=scale)
                    canvas = RecordingCanvas(context, compact=True)
                    hardware.render(canvas, context, style)
                    text = " ".join(canvas.texts())
                    visible = [value for value in expected
                               if style not in ("thermal", "orbit") or value != "39%"]
                    for value in visible:
                        self.assertIn(value, text)
                    for kind, arguments in canvas.calls:
                        if kind == "text":
                            self.assertGreater(arguments["width"], 0)
                            self.assertLessEqual(arguments["y"], 480)
                        elif kind in ("rect", "gradient"):
                            self.assertGreaterEqual(arguments["width"], 0)
                            self.assertGreaterEqual(arguments["height"], 0)

    def test_a_single_snapshot_cannot_draw_a_fake_history_trace(self):
        context = fixture_context(fixture_sensors())
        components = hardware._components(context.sensors)
        hardware._record(components, context.sensors["updatedAtUtc"])
        cpu = components[0]
        canvas = RecordingCanvas(context)
        hardware._trace(canvas, cpu, cpu.metric("usagePercent"), (40, 80, 300, 60), "#78CEB1")
        self.assertEqual(canvas.calls, [])

    def test_no_usage_never_draws_a_utilization_ring(self):
        context = fixture_context({"cpu": {"clockMHz": 4200, "temperatureC": 61.2}})
        for compact in (False, True):
            with self.subTest(compact=compact):
                canvas = RecordingCanvas(context, compact)
                hardware.render(canvas, context, "orbit")
                self.assertFalse(any(kind == "ring" for kind, _ in canvas.calls))
                self.assertIn("4200 MHz", " ".join(canvas.texts()))

    def test_thermal_themes_show_only_named_measured_fans_and_used_ram(self):
        raw = fixture_sensors()
        raw["cpu"]["fans"] = [{"name": "CPU Fan", "rpm": 0}]
        raw["gpus"][0]["fans"] = [{"name": "GPU Fan", "rpm": 1560}]
        raw["motherboard"] = {"name": "Board", "temperatureC": 39.5,
                              "temperatureLabel": "System",
                              "fans": [{"name": "Chassis Fan", "rpm": 870},
                                       {"name": "Missing Fan", "rpm": None}]}
        for style in ("thermal", "orbit"):
            with self.subTest(style=style):
                context = fixture_context(raw)
                canvas = RecordingCanvas(context)
                hardware.render(canvas, context, style)
                text = " ".join(canvas.texts())
                for value in ("61.2°C", "49.1°C", "39.5°C", "0 RPM", "1560 RPM",
                              "870 RPM", "12.5 GiB", "32.0 GiB"):
                    self.assertIn(value, text)
                self.assertNotIn("Missing Fan", text)
                self.assertNotIn("39%", text)  # RAM load is deliberately omitted.


class SystemPackChecks(unittest.TestCase):
    def test_pack_contains_ten_distinct_sensor_themes_without_spectrum_controls(self):
        root = ROOT / "themes/system"
        pack = json.loads((root / "pack.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(len(pack["themes"]), 10)
        identities = set()
        for relative in pack["themes"]:
            folder = (root / relative).resolve()
            self.assertTrue(folder.is_relative_to(root.resolve()))
            manifest = json.loads((folder / "theme.json").read_text(encoding="utf-8-sig"))
            self.assertEqual(manifest["schema"], 1)
            self.assertEqual(manifest["category"], "system")
            self.assertIn("sensors", manifest["optional"])
            self.assertNotIn("showSpectrum", manifest["settings"])
            self.assertTrue((folder / manifest["entry"]).is_file())
            identities.add(manifest["id"])
        self.assertEqual(identities, {"system." + style for style in STYLES})


@unittest.skipUnless(os.environ.get("VIBEPANEL_OFFLINE_RENDER_CHECKS") == "1",
                     "Owner-approved offscreen rendering requires VIBEPANEL_OFFLINE_RENDER_CHECKS=1")
class OptionalOfflineJpegChecks(unittest.TestCase):
    def test_all_ten_themes_encode_exact_wide_square_and_portrait_frames(self):
        # Imported only inside the optional check. The worker receives only the
        # fixture state; no C# host, sensor DLL or physical device is involved.
        import skia
        import worker

        sparse = {"cpu": {"usagePercent": 0, "clockMHz": None, "temperatureC": None},
                  "gpus": [], "memory": {"totalGiB": 32, "usedGiB": None},
                  "updatedAtUtc": "2026-10-17T12:00:00Z"}
        for style in STYLES:
            for width, height in ((1280, 480), (480, 480), (480, 1280)):
                for light in (False, True):
                    for scenario, sensors in (("full", fixture_sensors()), ("sparse", sparse), ("empty", {})):
                        with self.subTest(style=style, size=(width, height), light=light, scenario=scenario):
                            request = {"version": 1, "method": "render", "id": "offline-fixture",
                                       "themePath": str(ROOT / "themes/system/themes" / style),
                                       "width": width, "height": height, "light": light,
                                       "fontScale": 1.6 if light else .65, "colorFiltersEnabled": False,
                                       "state": {"sensors": copy.deepcopy(sensors),
                                                 "clock": {"iso": "2026-10-17T19:00:00+07:00"}}}
                            result = worker.render(request)
                            raw = base64.b64decode(result["jpegBase64"], validate=True)
                            self.assertTrue(raw.startswith(b"\xff\xd8"))
                            self.assertLessEqual(len(raw), 450_000)
                            self.assertFalse(result["transitionActive"])
                            decoded = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
                            self.assertIsNotNone(decoded)
                            self.assertEqual((decoded.width(), decoded.height()), (width, height))


if __name__ == "__main__":
    unittest.main(verbosity=2)
# VBWR E
