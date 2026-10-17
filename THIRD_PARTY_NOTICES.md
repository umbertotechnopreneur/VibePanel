# Third-party notices

The desktop UI uses OpenSilver and OpenSilver.Photino (MIT, copyright Userware). The launcher uses Photino.NET and Photino.Native (Apache-2.0, TryPhotino); the license is included under `licenses/Photino-Apache-2.0.txt`. Windows App SDK/WinUI are no longer dependencies of this UI.

OpenSilver's bundled browser resources retain their upstream notices. These include Quill, html-to-image, and FileSaver. They are restored from NuGet, not hand-copied or relicensed as VibePanel source.

The Windows build patches Photino.Native's UTF string ownership to release converted web messages. The Apache-2.0 source is pinned to TryPhotino commit `d790e5be3127ec628d0a9f1f9fac68e14f339c2a`; its copyright and license notices remain in the build sources. See `scripts/Build-PhotinoNative.ps1` and [upstream issue 165](https://github.com/tryphotino/photino.Native/issues/165). The C ABI is unchanged. This does not complete a review of the whole native library.

The USB protocol facts for Thermalright `0416:5302` were researched from device behaviour, public protocol references, and comparison with `thermalright-trcc-linux`'s implementation. That project is GPL-3.0. VibePanel does not include copied upstream code or assets. Its Windows transport, renderer, and application services are independently authored. See `docs/protocol-notes.md` for the source references and evidence limits.

System.Drawing.Common is used under the MIT license. Windows GDI+ performs local still-image rendering and JPEG encoding.

Spectre.Console and NAudio.Wasapi (including NAudio.Core) are MIT-licensed dependencies. System.Management is part of the MIT-licensed .NET libraries. Their package license information remains available through NuGet.

Windows media-session metadata and artwork come from the current user's local player. No Spotify Web API or Spotify asset bundle is included. NVIDIA metrics use the NVML library already installed with the user's driver; VibePanel does not redistribute it.

The Windows portable ZIP bundles CPython 3.13.16's official embeddable distribution (Python Software Foundation License 2.0). Its `LICENSE.txt` is copied into `licenses/Python-PSF-2.0.txt`. It also bundles pinned PyPI wheels for skia-python 144.0.post2 (BSD-style), NumPy 2.4.3 (BSD-3-Clause), and pybind11 3.0.2 (BSD-3-Clause). Their wheel metadata and included license files remain under `engine/python/Lib/site-packages/*.dist-info/`. The packaging script verifies the Python archive against the official release SHA-256 and each wheel against its PyPI release SHA-256 before extraction.
