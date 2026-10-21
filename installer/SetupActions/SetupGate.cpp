// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <sddl.h>
#include <shellapi.h>
#include <objbase.h>
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
    const std::wstring name = L"Global\\UmbertoGiacobbi.ThermalrightVibeWare.SetupGate." + std::wstring(sid);
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

static std::wstring SetupProperty(MSIHANDLE installer, const wchar_t* name) {
    DWORD length = 0;
    wchar_t empty = L'\0';
    const UINT status = MsiGetPropertyW(installer, name, &empty, &length);
    if ((status != ERROR_MORE_DATA && status != ERROR_SUCCESS) || length > 32767) { return {}; }
    std::wstring value(static_cast<size_t>(length) + 1, L'\0');
    DWORD capacity = static_cast<DWORD>(value.size());
    if (MsiGetPropertyW(installer, name, value.data(), &capacity) != ERROR_SUCCESS) { return {}; }
    value.resize(capacity);
    return value;
}

static bool CompletionSelected(MSIHANDLE installer, const wchar_t* option) {
    return SetupProperty(installer, L"UILevel") == L"5" &&
        SetupProperty(installer, L"Installed").empty() &&
        SetupProperty(installer, L"REMOVE").empty() &&
        SetupProperty(installer, option) == L"1";
}

static void CompletionWarning(MSIHANDLE installer, const wchar_t* message) {
    MSIHANDLE record = MsiCreateRecord(0);
    if (!record) { return; }
    MsiRecordSetStringW(record, 0, message);
    MsiProcessMessage(installer, INSTALLMESSAGE_WARNING, record);
    MsiCloseHandle(record);
}

static bool OpenCompletionTarget(const std::wstring& target, const wchar_t* directory = nullptr) {
    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.fMask = SEE_MASK_FLAG_NO_UI;
    execute.lpVerb = L"open";
    execute.lpFile = target.c_str();
    execute.lpDirectory = directory;
    execute.nShow = SW_SHOWNORMAL;
    const HRESULT apartment = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    const BOOL opened = ShellExecuteExW(&execute);
    if (SUCCEEDED(apartment)) { CoUninitialize(); }
    return opened != FALSE;
}

extern "C" __declspec(dllexport) UINT __stdcall StartThermalrightVibeWareAfterInstall(MSIHANDLE installer) {
    try {
        if (!CompletionSelected(installer, L"TRV_START_AFTER_INSTALL")) { return ERROR_SUCCESS; }
        std::wstring folder = SetupProperty(installer, L"INSTALLFOLDER");
        if (!folder.empty() && folder.back() != L'\\') { folder += L'\\'; }
        if (folder.empty() || !OpenCompletionTarget(folder + L"ThermalrightVibeWare.exe", folder.c_str())) {
            CompletionWarning(installer, L"Setup finished, but Thermalright VibeWare could not start. Open it from Start.");
        }
    }
    catch (...) { CompletionWarning(installer, L"Setup finished, but Thermalright VibeWare could not start. Open it from Start."); }
    return ERROR_SUCCESS;
}

extern "C" __declspec(dllexport) UINT __stdcall SeeLatestThermalrightVibeWareNews(MSIHANDLE installer) {
    try {
        if (!CompletionSelected(installer, L"TRV_SEE_LATEST_NEWS")) { return ERROR_SUCCESS; }
        // Fixed campaign tags identify this installer link, with no user/session ID.
        const std::wstring url = L"https://github.com/umbertotechnopreneur/thermalright-vibeware/releases"
            L"?utm_source=thermalright-vibeware&utm_medium=installer&utm_campaign=release_news";
        if (!OpenCompletionTarget(url)) {
            CompletionWarning(installer, L"Setup finished, but the browser could not open. Find Thermalright VibeWare releases on GitHub.");
        }
    }
    catch (...) { CompletionWarning(installer, L"Setup finished, but the browser could not open. Find Thermalright VibeWare releases on GitHub."); }
    return ERROR_SUCCESS;
}
