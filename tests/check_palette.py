# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Offline palette contract checks. Run only with owner approval."""
import pathlib
import os
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "theme-runtime"))
from palette import ThemePalette


class PaletteChecks(unittest.TestCase):
    def test_precedence_and_series(self):
        defaults = dict(textPrimary="#FFFFFF", textSecondary="#AAAAAA", accent="#123456", warning="#FFAA00", info="#0099FF")
        manifest = dict(paletteVersion=1, palette=dict(textPrimary="#BBBBBB", dark=dict(textPrimary="#CCCCCC")))
        palette = ThemePalette(("#123456", "#654321"), defaults, manifest, dict(textPrimary="#abcdef", accent=None), False)
        self.assertEqual(palette.textPrimary, "#ABCDEF")
        self.assertEqual(palette.accent, "#123456")
        self.assertEqual(palette[1], "#654321")
        self.assertEqual(ThemePalette(("#123456",), defaults, manifest, {}, False).textPrimary, "#CCCCCC")
        self.assertEqual(ThemePalette(("#123456",), defaults, manifest, {}, True).textPrimary, "#BBBBBB")
        self.assertEqual(defaults["textPrimary"], "#FFFFFF")

    def test_reject_invalid_contract(self):
        for overrides in ({"accent": "red"}, {"warning": 123}, {"unknown": "#FFFFFF"}, []):
            with self.subTest(overrides=overrides):
                with self.assertRaises(ValueError):
                    ThemePalette((), {}, {}, overrides, False)

    @unittest.skipUnless(os.environ.get("THERMALRIGHT_VIBEWARE_OFFLINE_RENDER_CHECKS") == "1", "Requires approved offscreen rendering")
    def test_bundled_themes_render_with_overrides(self):
        import worker
        root = pathlib.Path(__file__).resolve().parents[1]
        colors = dict(textPrimary="#F1E2D3", textSecondary="#A1B2C3", accent="#42AC98", warning="#ED7842", info="#6789DE")
        manifests = list((root / "themes").glob("*/themes/*/theme.json"))
        self.assertEqual(len(manifests), 30)
        for manifest in manifests:
            for light in (False, True):
                with self.subTest(theme=manifest.parent.name, light=light):
                    result = worker.render(dict(version=1, method="render", themePath=str(manifest.parent),
                                                width=1280, height=480, light=light, paletteVersion=1,
                                                palette=colors, colorFiltersEnabled=False, state={}))
                    self.assertTrue(result["ok"])
                    self.assertEqual(result["accentColor"], colors["accent"])


if __name__ == "__main__":
    unittest.main()
# VBWR E
