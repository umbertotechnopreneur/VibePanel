# VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Offline page contract checks. Run only with owner approval."""
import sys
import os
import json
import base64
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'theme-runtime'))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from theme_pages import select_page

class PageChecks(unittest.TestCase):
    def context(self, elapsed=0, **values):
        values.setdefault('sensors', {})
        return SimpleNamespace(elapsed_seconds=elapsed, category='system', **values)
    def test_rotation_starts_on_first_page_and_wraps(self):
        manifest={'pages':[{'id':'overview'},{'id':'activity'},{'id':'cooling'}], 'pageSeconds':8}
        for seconds, expected in ((0,'overview'), (7.99,'overview'), (8,'activity'), (16,'cooling'), (24,'overview')):
            self.assertEqual(select_page(self.context(seconds,sensors={'cpu':{'temperatureC':50}}),manifest),expected)
    def test_artist_page_never_borrows_a_different_artist(self):
        context=SimpleNamespace(elapsed_seconds=12,category='multimedia',artist='Current artist',artist_profile={'requestedArtist':'Previous artist'})
        self.assertEqual(select_page(context,{'pages':[{'id':'playback'},{'id':'artist'}],'pageSeconds':12}),'playback')
        self.assertEqual(context.page_count,1)
    def test_page_limits_are_validated(self):
        for manifest in ({'pages':[{'id':'x'},{'id':'x'}]}, {'pages':[{'id':'x'}]*9}, {'pageSeconds':float('nan')}, {'pageSeconds':0}):
            with self.assertRaises(ValueError):select_page(self.context(),manifest)

@unittest.skipUnless(os.environ.get('VIBEPANEL_OFFLINE_RENDER_CHECKS') == '1', 'Explicit offscreen opt-in required')
class PageRenderingChecks(unittest.TestCase):
    def test_every_declared_page_renders_at_both_sizes_and_font_limits(self):
        import skia, worker, canvas
        from check_hardware_themes import fixture_sensors
        root=Path(__file__).resolve().parents[1]
        for pack in ('system','media'):
          for folder in (root/'themes'/pack/'themes').iterdir():
            if not folder.is_dir(): continue
            manifest=json.loads((folder/'theme.json').read_text())
            for width in (480,1280):
              for scale in (.7,1.5):
                for index,page in enumerate(manifest['pages']):
                  with self.subTest(theme=manifest['id'],page=page['id'],width=width,scale=scale):
                    worker.FRAME_TRANSITION.reset(); canvas._ACTIVE_THEME_TIMING=None
                    sensors=fixture_sensors()
                    sensors['cpu']['readings']=[{'type':'Power','name':'CPU Package','value':65,'unit':'W'},
                                                {'type':'Clock','name':'Cores (Average)','value':4200,'unit':'MHz'}]
                    sensors['memory']['spdModules']=[{'name':'OFFLINE SPD source (#0)',
                                                     'readings':[{'type':'Timing','name':'tAA (CAS Latency Time)','value':14,'unit':'ns'}]}]
                    state={'sensors':sensors,'media':{'source':'Offline fixture','artist':'Fixture artist','title':'Fixture track'},
                           'artistProfile':{'requestedArtist':'Fixture artist','name':'Fixture artist','biography':'Offline text used to check the artist layout. '*10,'facts':[],'biographyCredit':'Fixture credit'}}
                    request={'version':1,'method':'render','themePath':str(folder),'width':width,'height':480,'light':scale>1,'fontScale':scale,'colorFiltersEnabled':False,'state':state}
                    with patch('canvas.time.monotonic',return_value=0): worker.render(request)
                    worker.FRAME_TRANSITION.reset()
                    with patch('canvas.time.monotonic',return_value=index*manifest['pageSeconds']+.1): result=worker.render(request)
                    raw=base64.b64decode(result['jpegBase64'])
                    image=skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
                    self.assertEqual((image.width(),image.height()),(width,480))
                    self.assertLessEqual(len(raw),450000)

    def test_page_boundary_starts_fade_without_a_track_change(self):
        import worker, canvas
        from frame_transition import FrameTransition
        root=Path(__file__).resolve().parents[1]
        clock=[0.]
        canvas._ACTIVE_THEME_TIMING=None
        request={'version':1,'method':'render','themePath':str(root/'themes/system/themes/signal'), 'width':480,'height':480,'colorFiltersEnabled':False,'state':{}}
        with patch.object(worker,'FRAME_TRANSITION',FrameTransition(clock=lambda:clock[0])):
            with patch('canvas.time.monotonic',return_value=0.): self.assertFalse(worker.render(request)['transitionActive'])
            clock[0]=8.
            with patch('canvas.time.monotonic',return_value=8.): self.assertTrue(worker.render(request)['transitionActive'])
            clock[0]=8.7
            with patch('canvas.time.monotonic',return_value=8.7): self.assertFalse(worker.render(request)['transitionActive'])

if __name__=='__main__':unittest.main()
# VBWR E
