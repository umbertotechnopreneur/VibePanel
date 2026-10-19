# Local dependency scan evidence

19 October 2026. Targeted Microsoft Defender custom scans completed with exit 0 and no detections for the inputs listed below. The command for each path was `MpCmdRun.exe -Scan -ScanType 3 -File <staged path> -DisableRemediation`. Archives and extracted files were scanned before build or packaging use. This is a point-in-time scan, not a complete security review.

## Scan scopes

Python inputs: official CPython 3.13.16 embeddable x64, skia-python 144.0.post2,
numpy 2.4.3 and pybind11 3.0.2 from PyPI. Final MSI 0.1.8 SHA-256:
`F345883849F179BE9BA5CFAD31DDA79A147A601D2A82619CD752BC85EA0C0A46`.

- https://api.nuget.org/v3/index.json; restored package archives and extracted dependencies — 2056 files; Completed without detections (exit 0).
- https://github.com/tryphotino/photino.Native/commit/d790e5be3127ec628d0a9f1f9fac68e14f339c2a — 1 files; Completed without detections (exit 0).
- Photino.Native source d790e5be3127ec628d0a9f1f9fac68e14f339c2a — 55 files; Completed without detections (exit 0).
- https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.1462.37/microsoft.web.webview2.1.0.1462.37.nupkg — 1 files; Completed without detections (exit 0).
- https://api.nuget.org/v3-flatcontainer/microsoft.windows.implementationlibrary/1.0.220914.1/microsoft.windows.implementationlibrary.1.0.220914.1.nupkg — 1 files; Completed without detections (exit 0).
- Official NuGet: Microsoft.Web.WebView2 1.0.1462.37; Microsoft.Windows.ImplementationLibrary 1.0.220914.1 — 118 files; Completed without detections (exit 0).
- https://www.python.org/ftp/python/3.13.16/ and https://pypi.org/; pinned requirements and SHA-256 manifest — 5 files; Completed without detections (exit 0).
- Extracted Python 3.13.16 and pinned PyPI wheels — 1050 files; Completed without detections (exit 0).
- https://api.nuget.org/v3/index.json; wix 4.0.6 — 48 files; Completed without detections (exit 0).
- https://api.nuget.org/v3-flatcontainer/wixtoolset.ui.wixext/4.0.6/wixtoolset.ui.wixext.4.0.6.nupkg — 1 files; Completed without detections (exit 0).
- https://api.nuget.org/v3-flatcontainer/wixtoolset.ui.wixext/4.0.6/wixtoolset.ui.wixext.4.0.6.nupkg — 8 files; Completed without detections (exit 0).
- Vibe Panel 0.1.8 fresh payload — 1626 files; Completed without detections (exit 0).
- Vibe Panel 0.1.8 local MSI — 1 files; Completed without detections (exit 0).

## NuGet input archives

Official source: https://api.nuget.org/v3/index.json. The archive names include exact versions.

| Archive | SHA-256 |
| --- | --- |
| blacksharp.core.1.0.4.nupkg | 6D1BB863EFE7B50E18A96E99DDCE18175F3DE6EB8AFBC791C50FFBDE176C4ED7 |
| blacksharp.core.1.0.7.nupkg | 4F713FE18EE7C85A7C49F28309ADAA815714333326AA81FD4D4A81619ECAA0AB |
| diskinfotoolkit.1.1.2.nupkg | A997F0B3701E20B2AB9296E07EE4F74F877E75015277FB6BD314ADE1E2C4DB13 |
| hidsharp.2.6.4.nupkg | CF55D4B4D7B42DC1D8496B7B592BA00D1ECD48A70BC07BD8DABD3E9976D108E7 |
| librehardwaremonitorlib.0.9.6.nupkg | 60DB5E0D9135F4658B385CAE139D3B9EAEAC0A8E867D322D20B5DBE4C4039197 |
| microsoft.aspnetcore.app.runtime.win-x64.10.0.13.nupkg | A0DBA5EE37A07A173BC2739F01FEE4C530C686E00BF3A3F7508972D6592511F3 |
| microsoft.extensions.objectpool.8.0.10.nupkg | B850C78C7949FC5A928C13251196D2197E3A33F8A3ACA12829370011A46A6A88 |
| microsoft.jsinterop.3.1.28.nupkg | F3C2841B977DED9D3E8526769E7806576C93844D5FBC4EC3C1B52DC73C61EDD8 |
| microsoft.netcore.app.runtime.win-x64.10.0.13.nupkg | C66885D464697BE75676B4D1FBCA38B8806F3AF01D65D27205B3F06ADD094444 |
| microsoft.win32.systemevents.10.0.10.nupkg | 594440D8D212A19CC3043307ED89C971CFE2238EE29EA249B47323E341E15491 |
| microsoft.windows.sdk.net.ref.10.0.19041.57.nupkg | 90B18F2CF7D7A23F5B0BFE2B86D8DDA45881481F677CE36ECCAF6E9AB50B6B4B |
| microsoft.windowsdesktop.app.runtime.win-x64.10.0.13.nupkg | AF044758D6BC23ECA96B60C9CE13B38F211C3E736EEF9458DE3CADBBD3E20FF2 |
| mono.posix.netstandard.1.0.0.nupkg | FC5EB593B318FDFBB615C7D6EC29328EE52BA0AC256005CF40555E0ECD509276 |
| naudio.core.2.2.1.nupkg | 794645DBFD30E4880663D52D9D9224D55C301FA54228FF623C95D572C7887347 |
| naudio.wasapi.2.2.1.nupkg | C8396B8DBAB86A2619EDFD9158BEAF18C21CB86677099B5594950FD8651B9D1A |
| opensilver.3.3.0.nupkg | E31F7294CDBB0C97BD41C9B3E33D80B5B79EE6FCF1BE18949611D8A81C7CF0CA |
| opensilver.3.3.3.nupkg | C96CAFE6CB340A6A85267369D24716B3C519CD7C9359C97CACEB9F3F207AC824 |
| opensilver.photino.3.3.3.nupkg | DCEF95A4636F140E046639DEB936A4883C37D98F4F8CAA8E99A5783E73D98670 |
| opensilver.themes.modern.3.3.0.nupkg | EBF10BE7F85F367651C0CEB3713BD8EAA37820462EF10B00A0C48435E80F0E98 |
| photino.native.3.2.3.nupkg | 78D3FA9B96588F058D18EF3EC96C510A0B986CE1EB8EB125796274C21BFEB95B |
| photino.net.3.2.3.nupkg | 43380C2945D279D5B5C5238A4A63F0C6B0CDC7D20B409D83DB9D743D179F7803 |
| ramspdtoolkit-ndd.1.4.2.nupkg | 57C893824A68C71538055F242586616D4205A671C300897C21B2A6F88270CE64 |
| runtime.android-arm.runtime.native.system.io.ports.10.0.3.nupkg | 0A8B1A2F06B724E5D5602EE3EDB67182703F68D0005FC7CB8FBF390C850937F0 |
| runtime.android-arm64.runtime.native.system.io.ports.10.0.3.nupkg | 17AAFF1F630E8DE6D89CB4A32CF6C7075738BCA6F1A19629F98AD322AD2B2E9F |
| runtime.android-x64.runtime.native.system.io.ports.10.0.3.nupkg | A20E9C5E662E3AE864C28E9F41ADFBA2C682FDCFFAF2CC4D838B02C571667556 |
| runtime.android-x86.runtime.native.system.io.ports.10.0.3.nupkg | 621E5129B3907361204695AC6DAD682F66F37386E8859AD2A8C8C5FB898DFBD4 |
| runtime.linux-arm.runtime.native.system.io.ports.10.0.3.nupkg | B871A7CD2B774F0EAD6F7BB10B9A109DC3B4F516195D03F370FF930C861C65E2 |
| runtime.linux-arm64.runtime.native.system.io.ports.10.0.3.nupkg | AA9DD706457F28E1AE1A4E25DB7DF51174E5CDDA49553B2376C55A885411F0B5 |
| runtime.linux-bionic-arm64.runtime.native.system.io.ports.10.0.3.nupkg | 61316CD25A8F50E84BEB960A79F15DE0B330D3C3538F25CB0F6FE9597B35C372 |
| runtime.linux-bionic-x64.runtime.native.system.io.ports.10.0.3.nupkg | CC2D813AE36FABCC09463D1FAF81195B63029C8BD79AF48D43F333629ADC9370 |
| runtime.linux-musl-arm.runtime.native.system.io.ports.10.0.3.nupkg | 3001737D92EF5843C001A4C2B06EAD81AD07CCBC0A37F1CD17702E1010527AFE |
| runtime.linux-musl-arm64.runtime.native.system.io.ports.10.0.3.nupkg | 06F67D5A24E91BAAFCB351B27561964DCA3A470F7ED7F4E5AEA1DCA5DE4BBD99 |
| runtime.linux-musl-x64.runtime.native.system.io.ports.10.0.3.nupkg | 6F9C21E2B91A9EA193E2F3D311016862199F677BD6939C9022E25E82A4E954EA |
| runtime.linux-x64.runtime.native.system.io.ports.10.0.3.nupkg | 8987ADD3544E011BF6923E3DACD2B6CB8A642FB3E39D9F4D65086780CEAD507D |
| runtime.maccatalyst-arm64.runtime.native.system.io.ports.10.0.3.nupkg | 3753382A067728FD60E8E392F91B4E7F17209E6A10093E2E8EE21941ED7ECA39 |
| runtime.maccatalyst-x64.runtime.native.system.io.ports.10.0.3.nupkg | 0D2F08B5FD92FE8A199D413B565617969ED325188CE51AFF1E37D71619A565F2 |
| runtime.native.system.io.ports.10.0.3.nupkg | 1E0DD52C841574B4DE3D1027C794727DDEE536836FDC5DAC4C2B65D2411AAF4E |
| runtime.osx-arm64.runtime.native.system.io.ports.10.0.3.nupkg | 7A19CEBD630B7734D0317EB2438FDA348DBCC328E3077B8824781305F2454D91 |
| runtime.osx-x64.runtime.native.system.io.ports.10.0.3.nupkg | B2F0B56F001CE9069150F47E7E804AEF8FEA3CE31FAB2A3CE82662D117E8449A |
| spectre.console.0.57.2.nupkg | 3912DAE8D5646AC922D1760FE0C83E06DC4EEE4B8864D38E1851B5B891EBC30C |
| spectre.console.ansi.0.57.2.nupkg | A4BF214836D6D02CBD8E9AFD1CAB1F50FCD8D56BD63FB974567770A711D68C2C |
| system.codedom.10.0.10.nupkg | C66BA86BB7A3C2B36C85AEA15CB9B4CEC6A01AB52CA027BC0DDFB9C4588D5AF2 |
| system.componentmodel.annotations.5.0.0.nupkg | D29493D541E0A5E13AC49AD87F947E53B0302251F7AD50B74A982E8A523F3008 |
| system.componentmodel.composition.6.0.0.nupkg | EC9C986EA5A5D4D1D334C256D7683A4ED61F91E988E769CE9D7ECE1D6BE9EE9B |
| system.drawing.common.10.0.10.nupkg | 9072478088456BDF8737CEA29FCF58D8D0F1CB90B3FEBDAC48E82E1508572ADD |
| system.io.filesystem.accesscontrol.5.0.0.nupkg | 73D3250CA25F8FADD846F97B6EB44136CE7DA25AE66CBF86D40EA84D2F32B447 |
| system.io.ports.10.0.3.nupkg | 89F0D0DBA1F964831C12D0D05DA84CC80821568C8AF682BCFC2647393E9DAC0A |
| system.management.10.0.0.nupkg | 361DCA3A5B2FE80C4692B5A21E1BAE4DB46E222DADA75DCDADB07081080AB928 |
| system.management.10.0.10.nupkg | 140C8C43AA2EC69B79FED755BFADA5369FF786251B57CB8023862828C9A3DA81 |
| system.management.10.0.2.nupkg | BB40E7E33254BD2A5FB66B6F476E1E9EA28F0F1C403172696AFDBFB1F8B06221 |
| system.security.cryptography.pkcs.10.0.13.nupkg | 54ED24578EAD34B5B50D259084243432F11733E2754B8B196E799D3418D304A7 |
| system.security.cryptography.xml.10.0.13.nupkg | 6EC31ECBC9F51102D9FB7FF0E9607AE7699BF203299A4702A31D5273322A4E89 |
| system.security.cryptography.xml.8.0.2.nupkg | F530A6572301E3E072FE2A54F2F7580ED4A7C35B6491E6E75D7551753EFCFB6F |
| system.servicemodel.primitives.8.1.0.nupkg | 7B0F720BA484B6C7763082342D62BEFBFABB3041E58EA23D83B59AE54108266B |

The separate shared-cache scan detected VulnerableDriver:WinNT/Winring0 in OpenHardwareMonitor 0.9.6. That cache was not used. The build used a new isolated cache with LibreHardwareMonitorLib 0.9.6. No Defender exclusion, allowlist, restoration, protection change or driver installation was performed.
