// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
// Opt-in diagnostic DLL only. Read the rendered WebView through its native API.
#include <Shlwapi.h>
#pragma comment(lib, "Shlwapi.lib")
using PreviewCaptureCallback = void(__cdecl*)(HRESULT);
extern "C" __declspec(dllexport) HRESULT VibePanel_CapturePreview(Photino* instance, const wchar_t* path, PreviewCaptureCallback completed)
{
    wil::com_ptr<IStream> stream;
    const auto result = SHCreateStreamOnFileEx(path, STGM_CREATE | STGM_WRITE | STGM_SHARE_DENY_WRITE,
        FILE_ATTRIBUTE_NORMAL, TRUE, nullptr, &stream);
    if (FAILED(result)) return result;
    return instance->PreviewCaptureWebView()->CapturePreview(COREWEBVIEW2_CAPTURE_PREVIEW_IMAGE_FORMAT_PNG, stream.get(),
        Microsoft::WRL::Callback<ICoreWebView2CapturePreviewCompletedHandler>(
            [stream, completed](HRESULT error) -> HRESULT {
                if (SUCCEEDED(error)) error = stream->Commit(STGC_DEFAULT);
                completed(error);
                return S_OK;
            }).Get());
}
