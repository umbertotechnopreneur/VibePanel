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
        self.assertIsNone(cpu.metric("usagePercent"))
        self.assertEqual(cpu.metric("clockMHz").label, "Average core clock")
        self.assertEqual(cpu.metric("temperatureC").label, "CPU package")
        self.assertEqual(cpu.metric("clockMHz").text, "3200 MHz")

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


    def test_board_channels_keep_real_names_without_cpu_mapping(self):
        raw = {"motherboard": {"name": "OFFLINE board", "temperatures": [
            {"name": "Temperature #1", "temperatureC": 44.5},
            {"name": "VRM", "temperatureC": 58},
            {"name": "Invalid channel", "temperatureC": None}]}}
        component = hardware._components(raw)[0]
        self.assertEqual(component.label, "BOARD")
        self.assertEqual([metric.label for metric in component.metrics], ["Temperature #1", "VRM"])
        self.assertEqual([metric.value for metric in component.metrics], [44.5, 58])


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


class ThemeDataContractChecks(unittest.TestCase):
    def test_hardware_page_keeps_all_electrical_and_aggregate_clock_readings(self):
        from system_details import HARDWARE_KINDS, sensor_groups
        cpu = {"readings": [{"name": "Memory", "type": "Clock", "value": 1600},
                            {"name": "Core #1", "type": "Clock", "value": 4200},
                            {"name": "SoC", "type": "Voltage", "value": 1.025},
                            {"name": "Package", "type": "Power", "value": 0},
                            {"name": "TDC", "type": "Current", "value": 38.5}]}
        groups = sensor_groups({"cpu": cpu, "gpus": [{"readings": []}]}, HARDWARE_KINDS)
        self.assertEqual(len(groups), 1)
        self.assertEqual(groups[0][3], [("Package", 0, "W"), ("SoC", 1.025, "V"),
                                        ("TDC", 38.5, "A"), ("Memory", 1600, "MHz")])

    def test_detail_banks_are_balanced_and_keep_every_reading_once(self):
        from system_details import detail_rows
        for count in range(1, 41):
            rows = list(range(count))
            banks = (count + 3) // 4
            pages = [detail_rows(rows, step)[0] for step in range(banks)]
            self.assertEqual([row for page in pages for row in page], rows)
            self.assertLessEqual(max(map(len, pages)), 4)
            self.assertLessEqual(max(map(len, pages)) - min(map(len, pages)), 1)
            self.assertEqual(detail_rows(rows, banks), detail_rows(rows, 0))
        self.assertEqual([len(detail_rows(list(range(5)), step)[0]) for step in range(2)], [3, 2])

    def test_memory_sections_have_independent_counters_and_skip_missing_sections(self):
        from system_details import memory_view, page_available
        spd = {"spdModules": [{"name": "OFFLINE SPD source (#0)",
                               "readings": [{"type": "Timing", "name": "tAA", "value": 14}]}]}
        combined = {"memory": {"modules": [{"partNumber": "OFFLINE model"}], **spd}}
        self.assertEqual([memory_view(combined, step) for step in range(8)],
                         [("modules", 0), ("spd", 0), ("modules", 1), ("spd", 1),
                          ("modules", 2), ("spd", 2), ("modules", 3), ("spd", 3)])
        self.assertEqual(memory_view({"memory": spd}, 3), ("spd", 3))
        self.assertEqual(memory_view({"memory": {"totalGiB": 32}}, 3), ("modules", 3))
        self.assertFalse(page_available("memory", {"memory": {"notice": "Missing"}}))

    def test_multiple_gpu_power_channels_keep_names_and_watts(self):
        from system_details import cooling_values
        raw = {"readings": [{"type": "Power", "name": "GPU Package", "value": 100},
                            {"type": "Power", "name": "GPU Rail", "value": 40}]}
        _, powers, _ = cooling_values({"gpus": [raw]})
        self.assertEqual(powers, [("GPU · Package", 100, "W"), ("GPU · Rail", 40, "W")])

    def test_missing_fields_are_omitted_without_losing_measured_zero(self):
        from system_dashboard import metadata
        self.assertEqual(metadata("cpu", {}), [])
        self.assertEqual(metadata("cpu", {"temperatureC": 0}), [("TEMPERATURE", "0.0°C")])
        self.assertEqual(metadata("memory", {"totalGiB": 32}), [("TOTAL", "32.0 GiB")])


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
            self.assertEqual({page["id"] for page in manifest["pages"]},
                             {"overview", "activity", "cooling", "electrical", "temperatures", "memory"})
            self.assertEqual(len(manifest["pages"]), 6)
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
