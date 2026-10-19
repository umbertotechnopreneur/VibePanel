// VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
#include <windows.h>
#include <msi.h>
#include <sddl.h>
#include <string>
#include <vector>

// The MSI custom-action host owns this handle until commit/rollback or process
// exit. An installer crash therefore cannot leave a persistent "installing" flag.
static HANDLE transactionGate = nullptr;

static std::wstring GateName() {
    HANDLE token = nullptr;
    if (!OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token)) {
        if (GetLastError() != ERROR_NO_TOKEN || !OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) { return {}; }
    }
    DWORD bytes = 0;
    GetTokenInformation(token, TokenUser, nullptr, 0, &bytes);
    std::vector<BYTE> data(bytes);
    if (!bytes || !GetTokenInformation(token, TokenUser, data.data(), bytes, &bytes)) { CloseHandle(token); return {}; }
    CloseHandle(token);
    LPWSTR sid = nullptr;
    if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &sid)) { return {}; }
    const std::wstring name = L"Global\\UmbertoGiacobbi.VibePanel.SetupGate." + std::wstring(sid);
    LocalFree(sid);
    return name;
}

extern "C" __declspec(dllexport) UINT __stdcall BeginSetup(MSIHANDLE) {
    try {
        const auto name = GateName();
        if (name.empty()) { return ERROR_INSTALL_FAILURE; }
        HANDLE gate = CreateEventW(nullptr, TRUE, TRUE, name.c_str());
        if (!gate) { return ERROR_INSTALL_FAILURE; }
        if (!SetEvent(gate)) { CloseHandle(gate); return ERROR_INSTALL_FAILURE; }
        transactionGate = gate;
        return ERROR_SUCCESS;
    }
    catch (...) { return ERROR_INSTALL_FAILURE; }
}

extern "C" __declspec(dllexport) UINT __stdcall EndSetup(MSIHANDLE) {
    try {
        const auto name = GateName();
        if (name.empty()) { return ERROR_INSTALL_FAILURE; }
        HANDLE gate = OpenEventW(EVENT_MODIFY_STATE, FALSE, name.c_str());
        if (!gate) { return GetLastError() == ERROR_FILE_NOT_FOUND ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE; }
        const BOOL reset = ResetEvent(gate);
        CloseHandle(gate);
        if (transactionGate) { CloseHandle(transactionGate); transactionGate = nullptr; }
        return reset ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    }
    catch (...) { return ERROR_INSTALL_FAILURE; }
}
