// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E
#include "../../native/photino/UtfConversion.h"
#include <Psapi.h>
#include <iostream>
#include <stdexcept>

static SIZE_T PrivateBytes()
{
    PROCESS_MEMORY_COUNTERS_EX counters{};
    counters.cb = sizeof(counters);
    if (!GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&counters), sizeof(counters)))
        throw std::runtime_error("Memory counters unavailable");
    return counters.PrivateUsage;
}

int main()
{
    try
    {
        if (!ThermalrightVibeWareNative::ToUtf16(nullptr).empty() || !ThermalrightVibeWareNative::ToUtf8(nullptr).empty())
            throw std::runtime_error("Null conversion");
        if (!ThermalrightVibeWareNative::ToUtf16("").empty() || !ThermalrightVibeWareNative::ToUtf8(L"").empty())
            throw std::runtime_error("Empty conversion");
        const std::string unicode = "Thermalright VibeWare \xc3\xa8 \xe6\x97\xa5\xe6\x9c\xac \xf0\x9f\x8e\xb5";
        auto first = ThermalrightVibeWareNative::ToUtf16(unicode.c_str());
        auto second = ThermalrightVibeWareNative::ToUtf16("Another title");
        if (ThermalrightVibeWareNative::ToUtf8(first.c_str()) != unicode || second != L"Another title")
            throw std::runtime_error("Independent Unicode storage");
        if (!ThermalrightVibeWareNative::ToUtf16("\xff").empty()) throw std::runtime_error("Invalid UTF-8");

        const std::string frame(600'000, 'A'); // Larger than a maximum-sized JPEG's Base64 payload.
        for (int i = 0; i < 100; ++i) { auto warmup = ThermalrightVibeWareNative::ToUtf16(frame.c_str()); }
        const auto before = PrivateBytes();
        SIZE_T peak = before;
        for (int i = 0; i < 4000; ++i)
        {
            auto converted = ThermalrightVibeWareNative::ToUtf16(frame.c_str());
            auto roundtrip = ThermalrightVibeWareNative::ToUtf8(converted.c_str());
            if (roundtrip != frame) throw std::runtime_error("Large payload roundtrip");
            if (i % 100 == 0) peak = (std::max)(peak, PrivateBytes());
        }
        const auto after = PrivateBytes();
        std::cout << "UTF conversion checks passed. iterations=4000 before=" << before
            << " peak=" << peak << " after=" << after << "\n";
        if (peak > before + 16 * 1024 * 1024 || after > before + 8 * 1024 * 1024)
            throw std::runtime_error("Conversion memory did not stay bounded");
        return 0;
    }
    catch (const std::exception& error) { std::cerr << error.what() << "\n"; return 1; }
}
