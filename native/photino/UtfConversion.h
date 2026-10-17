// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
#pragma once
#include <Windows.h>
#include <string>

namespace VibePanelNative
{
    // Owned values survive subsequent conversions and are freed at scope exit.
    inline std::wstring ToUtf16(const char* source)
    {
        if (!source || !*source) return {};
        const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, source, -1, nullptr, 0);
        if (length <= 0) return {};
        std::wstring result(length, L'\0');
        if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, source, -1, &result[0], length)) return {};
        result.resize(length - 1);
        return result;
    }

    inline std::string ToUtf8(const wchar_t* source)
    {
        if (!source || !*source) return {};
        const int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, source, -1, nullptr, 0, nullptr, nullptr);
        if (length <= 0) return {};
        std::string result(length, '\0');
        if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, source, -1, &result[0], length, nullptr, nullptr)) return {};
        result.resize(length - 1);
        return result;
    }
}
