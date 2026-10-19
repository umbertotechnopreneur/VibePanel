# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Owner-approved execution only. Synthetic pixels and metadata; no UI, audio or USB."""
from __future__ import annotations

import base64
import os
import pathlib
import sys
import types
import unittest
from unittest.mock import patch

os.environ["OPENBLAS_NUM_THREADS"] = "1"
os.environ["OMP_NUM_THREADS"] = "1"
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "theme-runtime"))

import skia
import worker
from frame_transition import FrameTransition


def solid(color, width=32, height=32):
    surface = skia.Surface(width, height)
    surface.getCanvas().clear(color)
    return surface.makeImageSnapshot()


def pixel(image):
    return tuple(int(value) for value in image.toarray(colorType=skia.ColorType.kRGBA_8888_ColorType)[0, 0])


class FrameTransitionChecks(unittest.TestCase):
    def setUp(self):
        self.now = 0.0
        self.fade = FrameTransition(clock=lambda: self.now)
        self.red, self.blue, self.green = (solid(color) for color in (skia.ColorRED, skia.ColorBLUE, skia.ColorGREEN))
        self.scope = ("theme", 32, 32)

    def assert_rgb(self, image, expected, tolerance=2):
        actual = pixel(image)
        for got, wanted in zip(actual, (*expected, 255)):
            self.assertLessEqual(abs(got - wanted), tolerance, (actual, expected))

    def begin(self):
        self.fade.compose(self.red, self.scope, "A")
        self.now = 1.0
        return self.fade.compose(self.blue, self.scope, "B")

    def test_initial_frame_is_immediate(self):
        image, active = self.fade.compose(self.red, self.scope, "A")
        self.assertIs(image, self.red)
        self.assertFalse(active)

    def test_changed_track_blends_pixels_and_finishes(self):
        image, active = self.begin()
        self.assert_rgb(image, (255, 0, 0))
        self.assertTrue(active)
        self.now = 1.3
        image, active = self.fade.compose(self.blue, self.scope, "B")
        self.assert_rgb(image, (128, 0, 128))
        self.assertTrue(active)
        self.now = 1.7
        image, active = self.fade.compose(self.blue, self.scope, "B")
        self.assertIs(image, self.blue)
        self.assertFalse(active)

    def test_live_frame_updates_do_not_restart_a_track_fade(self):
        self.begin()
        self.now = 1.3
        image, active = self.fade.compose(self.green, self.scope, "B")
        self.assert_rgb(image, (128, 128, 0))
        self.assertTrue(active)
        self.now = 1.7
        image, active = self.fade.compose(self.green, self.scope, "B")
        self.assertIs(image, self.green)
        self.assertFalse(active)

    def test_same_track_updates_without_starting_a_fade(self):
        self.fade.compose(self.red, self.scope, "A")
        image, active = self.fade.compose(self.blue, self.scope, "A")
        self.assertIs(image, self.blue)
        self.assertFalse(active)

    def test_rapid_skip_starts_from_the_visible_mix(self):
        self.begin()
        self.now = 1.3
        mixed, _ = self.fade.compose(self.blue, self.scope, "B")
        self.now = 1.31
        image, active = self.fade.compose(self.green, self.scope, "C")
        self.assertEqual(pixel(image), pixel(mixed))
        self.assertTrue(active)
        self.now = 1.61
        image, _ = self.fade.compose(self.green, self.scope, "C")
        self.assert_rgb(image, (64, 128, 64))

    def test_many_skips_finish_at_the_latest_track_without_a_queue(self):
        self.begin()
        for index in range(32):
            self.now += .1
            self.fade.compose(self.blue, self.scope, str(index))
            self.now += .1
            self.fade.compose(self.blue, self.scope, str(index))
        self.now += 1
        image, active = self.fade.compose(self.green, self.scope, "31")
        self.assertIs(image, self.green)
        self.assertFalse(active)

    def test_theme_or_geometry_change_drops_the_old_image(self):
        self.begin()
        replacement = solid(skia.ColorGREEN, 16, 32)
        image, active = self.fade.compose(replacement, ("other-theme", 16, 32), "C")
        self.assertIs(image, replacement)
        self.assertEqual((image.width(), image.height()), (16, 32))
        self.assertFalse(active)

    def test_elapsed_time_not_request_count_controls_progress(self):
        self.begin()
        self.now = 1.45
        sparse, _ = self.fade.compose(self.blue, self.scope, "B")
        dense = FrameTransition(clock=lambda: self.now)
        self.now = 0
        dense.compose(self.red, self.scope, "A")
        self.now = 1
        dense.compose(self.blue, self.scope, "B")
        for elapsed in (1.1, 1.2, 1.3, 1.4, 1.45):
            self.now = elapsed
            image, _ = dense.compose(self.blue, self.scope, "B")
        self.assertEqual(pixel(image), pixel(sparse))

    def test_worker_fades_with_filters_off_and_filters_the_mix_once(self):
        manifest = {"schema": 1, "id": "test.track", "category": "multimedia"}
        module = types.SimpleNamespace(render=lambda canvas, context: canvas.native.clear(
            skia.ColorRED if context.title == "A" else skia.ColorBLUE))
        request = {"version": 1, "method": "render", "themePath": str(ROOT / "themes/media/themes/aurora"),
                   "width": 128, "height": 128, "colorFiltersEnabled": False,
                   "brightnessPercent": 100, "contrastPercent": 0,
                   "state": {"media": {"source": "fixture", "title": "A"}}}
        with patch.object(worker, "load_theme", return_value=(manifest, module)), patch.object(worker, "FRAME_TRANSITION", self.fade):
            self.assertFalse(worker.render(request)["transitionActive"])
            request["state"]["media"]["title"] = "B"
            self.now = 1
            self.assertTrue(worker.render(request)["transitionActive"])
            self.now = 1.3
            result = worker.render(request)
            decoded = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(base64.b64decode(result["jpegBase64"])))
            self.assert_rgb(decoded, (128, 0, 128), tolerance=4)
            request.update(colorFiltersEnabled=True, brightnessPercent=-50, contrastPercent=100)
            decoded = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(base64.b64decode(worker.render(request)["jpegBase64"])))
            # Filtering each endpoint first would give roughly (64, 0, 64).
            self.assert_rgb(decoded, (0, 0, 0), tolerance=4)

    def test_codex_resets_track_history_and_playback_position_is_not_identity(self):
        manifest = {"schema": 1, "id": "test.track", "category": "multimedia"}
        module = types.SimpleNamespace(render=lambda canvas, context: canvas.native.clear(skia.ColorBLUE))
        request = {"version": 1, "method": "render", "themePath": str(ROOT / "themes/media/themes/aurora"),
                   "width": 128, "height": 128, "colorFiltersEnabled": False,
                   "state": {"media": {"source": "fixture", "title": "A"}}}
        with patch.object(worker, "load_theme", return_value=(manifest, module)), patch.object(worker, "FRAME_TRANSITION", self.fade):
            worker.render(request)
            request["state"]["media"].update(positionSeconds=10, playing=True)
            self.assertFalse(worker.render(request)["transitionActive"])
            request["state"]["media"]["title"] = "B"
            self.assertTrue(worker.render(request)["transitionActive"])
            manifest["category"] = "codex"
            self.assertFalse(worker.render(request)["transitionActive"])
            manifest["category"] = "multimedia"
            self.assertFalse(worker.render(request)["transitionActive"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
# VBWR E
