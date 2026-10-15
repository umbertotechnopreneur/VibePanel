# Third-party notices

VibePanel uses the Windows App SDK under its applicable Microsoft license.

The USB protocol facts for Thermalright `0416:5302` were researched from device behaviour, public protocol references, and comparison with `thermalright-trcc-linux`'s implementation. That project is GPL-3.0. VibePanel does not include copied upstream code or assets. Its Windows transport, renderer, and application services are independently authored. See `docs/protocol-notes.md` for the source references and evidence limits.

System.Drawing.Common is used under the MIT license. Windows GDI+ performs local still-image rendering and JPEG encoding.

Spectre.Console and NAudio.Wasapi (including NAudio.Core) are MIT-licensed dependencies. System.Management is part of the MIT-licensed .NET libraries. Their package license information remains available through NuGet.

Windows media-session metadata and artwork come from the current user's local player. No Spotify Web API or Spotify asset bundle is included. NVIDIA metrics use the NVML library already installed with the user's driver; VibePanel does not redistribute it.
