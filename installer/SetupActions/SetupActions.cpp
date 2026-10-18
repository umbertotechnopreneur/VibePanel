// VBWR B
// Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

#include <windows.h>
#include <shlobj.h>
#include <shellapi.h>
#include <sddl.h>
#include <tlhelp32.h>
#include <algorithm>
#include <cerrno>
#include <cstdint>
#include <cwchar>
#include <cwctype>
#include <exception>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace {
constexpr wchar_t ServiceName[] = L"WinRing0_1_2_0";
constexpr wchar_t DriverName[] = L"OpenHardwareMonitorLib.sys";
constexpr DWORD GetDriverReferenceCount = (40000u << 16) | (0x801u << 2);
constexpr DWORD FailureExit = 1603;
constexpr DWORD CancelExit = 1602;

struct Failure {
    DWORD code;
    std::wstring message;
    bool canElevate = true;
};

[[noreturn]] void Fail(const std::wstring& message, DWORD code = GetLastError()) {
    throw Failure{ code, message };
}

class Handle {
public:
    Handle() = default;
    explicit Handle(HANDLE value) : value_(value == INVALID_HANDLE_VALUE ? nullptr : value) {}
    ~Handle() { reset(); }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    Handle(Handle&& other) noexcept : value_(std::exchange(other.value_, nullptr)) {}
    Handle& operator=(Handle&& other) noexcept {
        if (this != &other) { reset(); value_ = std::exchange(other.value_, nullptr); }
        return *this;
    }
    HANDLE get() const { return value_; }
    explicit operator bool() const { return value_ != nullptr; }
    void reset() { if (value_) { CloseHandle(value_); value_ = nullptr; } }
private:
    HANDLE value_ = nullptr;
};

class ServiceHandle {
public:
    explicit ServiceHandle(SC_HANDLE value = nullptr) : value_(value) {}
    ~ServiceHandle() { reset(); }
    ServiceHandle(const ServiceHandle&) = delete;
    ServiceHandle& operator=(const ServiceHandle&) = delete;
    SC_HANDLE get() const { return value_; }
    explicit operator bool() const { return value_ != nullptr; }
    void reset() { if (value_) { CloseServiceHandle(value_); value_ = nullptr; } }
private:
    SC_HANDLE value_;
};

// Keep writes to user folders at the original user's privilege level even
// when only the service-control portion needed administrator permission.
class OriginalUser {
public:
    explicit OriginalUser(HANDLE token) {
        HANDLE previous = nullptr;
        if (OpenThreadToken(GetCurrentThread(), TOKEN_QUERY | TOKEN_IMPERSONATE, TRUE, &previous)) {
            previous_ = Handle(previous);
        }
        else if (GetLastError() != ERROR_NO_TOKEN) { Fail(L"Cannot preserve setup thread identity"); }
        if (!ImpersonateLoggedOnUser(token)) { Fail(L"Cannot use the original user's file permissions"); }
        active_ = true;
    }
    ~OriginalUser() {
        if (active_ && !(previous_ ? SetThreadToken(nullptr, previous_.get()) : RevertToSelf())) {
            // Continuing under an unexpected token is unsafe. This is our own
            // setup helper, never a product or unrelated process.
            TerminateProcess(GetCurrentProcess(), FailureExit);
            std::terminate();
        }
    }
    OriginalUser(const OriginalUser&) = delete;
    OriginalUser& operator=(const OriginalUser&) = delete;
private:
    Handle previous_;
    bool active_ = false;
};

bool Equal(const std::wstring& left, const std::wstring& right) {
    return CompareStringOrdinal(left.c_str(), -1, right.c_str(), -1, TRUE) == CSTR_EQUAL;
}

bool StartsWithPath(const std::wstring& path, const std::wstring& root) {
    return path.size() > root.size() && path[root.size()] == L'\\'
        && Equal(path.substr(0, root.size()), root);
}

std::wstring Join(const std::wstring& root, const std::wstring& child) {
    return root + L"\\" + child;
}

bool IsMissing(DWORD error) {
    return error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND;
}

std::wstring FullPath(std::wstring path) {
    if (path.starts_with(L"\\??\\")) { path.erase(0, 4); }
    if (path.starts_with(L"\\\\?\\")) { path.erase(0, 4); }
    std::replace(path.begin(), path.end(), L'/', L'\\');
    if (path.size() < 3 || !iswalpha(path[0]) || path[1] != L':' || path[2] != L'\\') {
        Fail(L"Cleanup requires an absolute local path: " + path, ERROR_INVALID_NAME);
    }
    const DWORD size = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
    if (!size) { Fail(L"Cannot resolve path: " + path); }
    std::wstring result(size, L'\0');
    const DWORD written = GetFullPathNameW(path.c_str(), size, result.data(), nullptr);
    if (!written || written >= size) { Fail(L"Cannot resolve path: " + path); }
    result.resize(written);
    while (result.size() > 3 && result.back() == L'\\') { result.pop_back(); }
    return result;
}

// Check every existing ancestor, including the user's profile. A junction must
// never turn a product-scoped cleanup into deletion outside its release folder.
void CheckNoReparse(const std::wstring& path) {
    for (size_t end = 3; end <= path.size();) {
        const auto part = path.substr(0, end);
        const DWORD attributes = GetFileAttributesW(part.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES) {
            const DWORD error = GetLastError();
            if (IsMissing(error)) { return; }
            Fail(L"Cannot inspect cleanup path: " + part, error);
        }
        if (attributes & FILE_ATTRIBUTE_REPARSE_POINT) {
            Fail(L"Cleanup refuses a link or junction: " + part, ERROR_REPARSE_TAG_INVALID);
        }
        if (end == path.size()) { break; }
        const size_t separator = path.find(L'\\', end + (end == 3 ? 0 : 1));
        end = separator == std::wstring::npos ? path.size() : separator;
    }
}

void EnsureDirectory(const std::wstring& path) {
    CheckNoReparse(path);
    const DWORD attributes = GetFileAttributesW(path.c_str());
    if (attributes != INVALID_FILE_ATTRIBUTES) {
        if (!(attributes & FILE_ATTRIBUTE_DIRECTORY)) { Fail(L"Expected a folder: " + path, ERROR_DIRECTORY); }
        return;
    }
    const DWORD error = GetLastError();
    if (!IsMissing(error)) { Fail(L"Cannot inspect folder: " + path, error); }
    const size_t separator = path.find_last_of(L'\\');
    if (separator > 2) { EnsureDirectory(path.substr(0, separator)); }
    if (!CreateDirectoryW(path.c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS) {
        Fail(L"Cannot create setup log folder: " + path);
    }
    CheckNoReparse(path);
}

std::wstring ProcessPath(HANDLE process) {
    std::wstring path(32768, L'\0');
    DWORD length = static_cast<DWORD>(path.size());
    if (!QueryFullProcessImageNameW(process, 0, path.data(), &length)) { Fail(L"Cannot identify process executable"); }
    path.resize(length);
    return FullPath(path);
}

uint64_t ProcessStart(HANDLE process) {
    FILETIME created{}, exited{}, kernel{}, user{};
    if (!GetProcessTimes(process, &created, &exited, &kernel, &user)) { Fail(L"Cannot identify process creation time"); }
    return (static_cast<uint64_t>(created.dwHighDateTime) << 32) | created.dwLowDateTime;
}

Handle ProcessToken(HANDLE process, DWORD access = TOKEN_QUERY) {
    HANDLE token = nullptr;
    if (!OpenProcessToken(process, access, &token)) { Fail(L"Cannot identify the setup user's token"); }
    return Handle(token);
}

std::vector<BYTE> ReadTokenUser(HANDLE token) {
    DWORD size = 0;
    GetTokenInformation(token, ::TokenUser, nullptr, 0, &size);
    if (!size) { Fail(L"Cannot identify the setup user's SID"); }
    std::vector<BYTE> bytes(size);
    if (!GetTokenInformation(token, ::TokenUser, bytes.data(), size, &size)) { Fail(L"Cannot read the setup user's SID"); }
    return bytes;
}

bool SameUser(HANDLE process, const std::vector<BYTE>& expected) {
    auto token = ProcessToken(process);
    const auto actual = ReadTokenUser(token.get());
    return EqualSid(reinterpret_cast<const TOKEN_USER*>(expected.data())->User.Sid,
        reinterpret_cast<const TOKEN_USER*>(actual.data())->User.Sid) != FALSE;
}

bool IsAdministrator() {
    auto token = ProcessToken(GetCurrentProcess());
    TOKEN_ELEVATION elevation{};
    DWORD size = 0;
    if (!GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size)) {
        Fail(L"Cannot inspect setup elevation");
    }
    return elevation.TokenIsElevated != 0;
}

struct Scope {
    Handle originalProcess;
    Handle token;
    std::vector<BYTE> user;
    std::wstring local;
    std::wstring apps;
    std::wstring backups;
};

Scope MakeScope(DWORD parentId, uint64_t parentStart) {
    Scope scope;
    HANDLE original = GetCurrentProcess();
    if (parentId) {
        scope.originalProcess = Handle(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, parentId));
        if (!scope.originalProcess) { Fail(L"The original setup process is no longer available"); }
        original = scope.originalProcess.get();
        if (WaitForSingleObject(original, 0) != WAIT_TIMEOUT || ProcessStart(original) != parentStart
            || !Equal(ProcessPath(original), ProcessPath(GetCurrentProcess()))) {
            Fail(L"The elevated cleanup request does not match its original setup process", ERROR_INVALID_DATA);
        }
    }
    scope.token = ProcessToken(original, TOKEN_QUERY | TOKEN_IMPERSONATE | TOKEN_DUPLICATE);
    scope.user = ReadTokenUser(scope.token.get());
    PWSTR local = nullptr;
    const HRESULT result = SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DONT_VERIFY, scope.token.get(), &local);
    if (FAILED(result)) { Fail(L"Cannot locate the original user's local application folder", static_cast<DWORD>(result)); }
    const std::wstring path(local);
    CoTaskMemFree(local);
    scope.local = FullPath(path);
    CheckNoReparse(scope.local);
    scope.apps = Join(scope.local, L"Umberto Giacobbi.biz\\Apps\\VibePanel");
    scope.backups = Join(scope.local, L"Umberto Giacobbi.biz\\VibePanel\\Uninstall backups");
    return scope;
}

class Log {
public:
    explicit Log(const Scope& scope) {
        HANDLE retained = nullptr;
        if (!DuplicateHandle(GetCurrentProcess(), scope.token.get(), GetCurrentProcess(), &retained,
            0, FALSE, DUPLICATE_SAME_ACCESS)) { Fail(L"Cannot retain the original setup user's identity"); }
        token_ = Handle(retained);
        OriginalUser user(token_.get());
        const auto directory = Join(scope.local, L"VibePanel\\logs");
        EnsureDirectory(directory);
        path_ = Join(directory, L"setup.log");
        CheckNoReparse(path_);
        Handle file(CreateFileW(path_.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!file) { Fail(L"Cannot open the setup log: " + path_); }
    }
    const std::wstring& path() const { return path_; }
    void Write(const std::wstring& message) const {
        OriginalUser user(token_.get());
        CheckNoReparse(path_);
        Handle file(CreateFileW(path_.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!file) { Fail(L"Cannot write the setup log: " + path_); }
        SYSTEMTIME now{};
        GetSystemTime(&now);
        wchar_t time[48]{};
        swprintf_s(time, L"%04u-%02u-%02uT%02u:%02u:%02uZ ", static_cast<unsigned>(now.wYear),
            static_cast<unsigned>(now.wMonth), static_cast<unsigned>(now.wDay), static_cast<unsigned>(now.wHour),
            static_cast<unsigned>(now.wMinute), static_cast<unsigned>(now.wSecond));
        const auto line = std::wstring(time) + message + L"\r\n";
        const int size = WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, nullptr, 0, nullptr, nullptr);
        if (size <= 1) { return; }
        std::vector<char> bytes(static_cast<size_t>(size));
        WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, bytes.data(), size, nullptr, nullptr);
        DWORD written = 0;
        if (!WriteFile(file.get(), bytes.data(), static_cast<DWORD>(size - 1), &written, nullptr)
            || written != static_cast<DWORD>(size - 1)) { Fail(L"Cannot finish writing the setup log: " + path_); }
    }
private:
    Handle token_;
    std::wstring path_;
};

bool IsHex(const std::wstring& value, size_t size) {
    return value.size() == size && std::all_of(value.begin(), value.end(), [](wchar_t character) {
        return (character >= L'0' && character <= L'9') || (character >= L'a' && character <= L'f')
            || (character >= L'A' && character <= L'F');
    });
}

bool IsReleaseName(const std::wstring& value) {
    return value.size() == 22 && Equal(value.substr(0, 10), L"VibePanel-") && IsHex(value.substr(10), 12);
}

// Recognize a release structurally, even if an old uninstaller moved its files.
// This also identifies a stale service ImagePath whose original directory is gone.
std::optional<std::wstring> ReleaseRoot(const Scope& scope, const std::wstring& path) {
    if (StartsWithPath(path, scope.apps)) {
        const auto rest = path.substr(scope.apps.size() + 1);
        const auto slash = rest.find(L'\\');
        const auto name = rest.substr(0, slash);
        if (Equal(name, L"MSI") || IsReleaseName(name)) { return Join(scope.apps, name); }
    }
    if (StartsWithPath(path, scope.backups)) {
        const auto rest = path.substr(scope.backups.size() + 1);
        const auto slash = rest.find(L'\\');
        if (slash == std::wstring::npos || !IsHex(rest.substr(0, slash), 32)) { return std::nullopt; }
        const auto release = rest.substr(slash + 1);
        const auto name = release.substr(0, release.find(L'\\'));
        if (IsReleaseName(name)) { return Join(Join(scope.backups, rest.substr(0, slash)), name); }
    }
    return std::nullopt;
}

std::optional<std::wstring> OwnedExecutable(const Scope& scope, const std::wstring& path) {
    const auto root = ReleaseRoot(scope, path);
    if (!root || !StartsWithPath(path, *root)) { return std::nullopt; }
    const auto relative = path.substr(root->size() + 1);
    if (Equal(relative, L"VibePanel.exe") || Equal(relative, L"engine\\vp.exe")
        || Equal(relative, L"engine\\VibePanel.Cli.exe") || Equal(relative, L"engine\\python\\python.exe")
        || Equal(relative, L"engine\\python\\pythonw.exe")) {
        CheckNoReparse(path);
        return root;
    }
    return std::nullopt;
}

bool OwnedDriver(const Scope& scope, const std::wstring& path) {
    const auto root = ReleaseRoot(scope, path);
    if (!root || !Equal(path, Join(*root, L"engine\\OpenHardwareMonitorLib.sys"))) { return false; }
    CheckNoReparse(path);
    return true;
}

std::vector<std::wstring> Directories(const std::wstring& directory) {
    CheckNoReparse(directory);
    WIN32_FIND_DATAW data{};
    const HANDLE search = FindFirstFileW(Join(directory, L"*").c_str(), &data);
    if (search == INVALID_HANDLE_VALUE) {
        const DWORD error = GetLastError();
        if (IsMissing(error)) { return {}; }
        Fail(L"Cannot inspect release folders: " + directory, error);
    }
    std::vector<std::wstring> names;
    do {
        if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) && wcscmp(data.cFileName, L".") != 0
            && wcscmp(data.cFileName, L"..") != 0) { names.emplace_back(data.cFileName); }
    } while (FindNextFileW(search, &data));
    const DWORD error = GetLastError();
    FindClose(search);
    if (error != ERROR_NO_MORE_FILES) { Fail(L"Cannot finish release folder inspection: " + directory, error); }
    return names;
}

std::vector<std::wstring> ReleaseFolders(const Scope& scope) {
    std::vector<std::wstring> releases;
    for (const auto& name : Directories(scope.apps)) {
        if (Equal(name, L"MSI") || IsReleaseName(name)) {
            const auto path = Join(scope.apps, name);
            CheckNoReparse(path);
            releases.push_back(path);
        }
    }
    for (const auto& backup : Directories(scope.backups)) {
        if (!IsHex(backup, 32)) { continue; }
        const auto group = Join(scope.backups, backup);
        for (const auto& name : Directories(group)) {
            if (IsReleaseName(name)) {
                const auto path = Join(group, name);
                CheckNoReparse(path);
                releases.push_back(path);
            }
        }
    }
    return releases;
}

struct Process {
    Handle handle;
    DWORD id;
    uint64_t started;
    std::wstring image;
    bool studio;
};

std::vector<PROCESSENTRY32W> ProcessSnapshot() {
    Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
    if (!snapshot) { Fail(L"Cannot inspect running application processes"); }
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    if (!Process32FirstW(snapshot.get(), &entry)) { Fail(L"Cannot read application process snapshot"); }
    std::vector<PROCESSENTRY32W> entries;
    do { entries.push_back(entry); } while (Process32NextW(snapshot.get(), &entry));
    if (GetLastError() != ERROR_NO_MORE_FILES) { Fail(L"Cannot finish application process snapshot"); }
    return entries;
}

bool SeedName(const std::wstring& name) {
    return Equal(name, L"VibePanel.exe") || Equal(name, L"vp.exe") || Equal(name, L"VibePanel.Cli.exe")
        || Equal(name, L"python.exe") || Equal(name, L"pythonw.exe");
}

std::vector<Process> FindProcesses(const Scope& scope) {
    const auto entries = ProcessSnapshot();
    std::vector<Process> selected;
    for (const auto& entry : entries) {
        if (!SeedName(entry.szExeFile)) { continue; }
        Handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, entry.th32ProcessID));
        if (!process || WaitForSingleObject(process.get(), 0) != WAIT_TIMEOUT) { continue; }
        std::wstring path;
        try { path = ProcessPath(process.get()); }
        catch (const Failure&) { continue; } // An unrelated process may be protected or may have exited.
        const auto root = OwnedExecutable(scope, path);
        if (!root || !SameUser(process.get(), scope.user)) { continue; }
        const auto started = ProcessStart(process.get());
        selected.push_back(Process{ std::move(process), entry.th32ProcessID, started, path, Equal(path, Join(*root, L"VibePanel.exe")) });
    }
    // A WebView runtime can live outside our folder. Include only a live,
    // retained parent/child chain of this user, with monotonically increasing
    // creation times; never select the runtime by process name.
    for (bool added = true; added;) {
        added = false;
        for (const auto& entry : entries) {
            if (!SeedName(entry.szExeFile) && !Equal(entry.szExeFile, L"msedgewebview2.exe")) { continue; }
            if (entry.th32ProcessID == GetCurrentProcessId()
                || (scope.originalProcess && entry.th32ProcessID == GetProcessId(scope.originalProcess.get()))) { continue; }
            if (std::any_of(selected.begin(), selected.end(), [&](const Process& item) { return item.id == entry.th32ProcessID; })) { continue; }
            const auto parent = std::find_if(selected.begin(), selected.end(), [&](const Process& item) {
                return item.id == entry.th32ParentProcessID && WaitForSingleObject(item.handle.get(), 0) == WAIT_TIMEOUT;
            });
            if (parent == selected.end()) { continue; }
            const auto parentStart = parent->started;
            Handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, entry.th32ProcessID));
            if (!process || WaitForSingleObject(process.get(), 0) != WAIT_TIMEOUT) { continue; }
            const auto started = ProcessStart(process.get());
            if (started < parentStart || !SameUser(process.get(), scope.user)) { continue; }
            const auto path = ProcessPath(process.get());
            const auto name = path.substr(path.find_last_of(L'\\') + 1);
            if (!OwnedExecutable(scope, path) && !Equal(name, L"msedgewebview2.exe")) { continue; }
            CheckNoReparse(path);
            selected.push_back(Process{ std::move(process), entry.th32ProcessID, started, path, false });
            added = true;
        }
    }
    return selected;
}

void StopProcesses(const Scope& scope, const Log& log) {
    for (unsigned pass = 0; pass != 3; ++pass) {
        auto processes = FindProcesses(scope);
        if (processes.empty()) { return; }
        log.Write(L"Stopping verified Vibe Panel processes; pass " + std::to_wstring(pass + 1));
        for (const auto& process : processes) {
            log.Write(L"Owned process " + std::to_wstring(process.id) + L": " + process.image);
            if (!process.studio) { continue; }
            const auto eventName = L"Global\\UmbertoGiacobbi.VibePanel.SetupClose." + std::to_wstring(process.id)
                + L"." + std::to_wstring(process.started);
            Handle event(OpenEventW(EVENT_MODIFY_STATE, FALSE, eventName.c_str()));
            if (event && !SetEvent(event.get())) { Fail(L"Cannot request Vibe Panel shutdown"); }
        }
        const ULONGLONG deadline = GetTickCount64() + 60000;
        while (GetTickCount64() < deadline
            && std::any_of(processes.begin(), processes.end(), [](const Process& process) {
                return WaitForSingleObject(process.handle.get(), 0) == WAIT_TIMEOUT;
            })) { Sleep(100); }
        std::sort(processes.begin(), processes.end(), [](const Process& left, const Process& right) {
            return left.started > right.started;
        });
        for (const auto& process : processes) {
            if (WaitForSingleObject(process.handle.get(), 0) != WAIT_TIMEOUT) { continue; }
            Handle terminating(OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, process.id));
            if (!terminating) {
                const DWORD error = GetLastError();
                if (WaitForSingleObject(process.handle.get(), 0) != WAIT_TIMEOUT) { continue; }
                Fail(L"Cannot close Vibe Panel process " + std::to_wstring(process.id) + L": " + process.image, error);
            }
            if (ProcessStart(terminating.get()) != process.started || !Equal(ProcessPath(terminating.get()), process.image)
                || !SameUser(terminating.get(), scope.user)) { Fail(L"Application process identity changed during cleanup", ERROR_INVALID_DATA); }
            if (!TerminateProcess(terminating.get(), 0)) {
                const DWORD error = GetLastError();
                if (WaitForSingleObject(terminating.get(), 0) != WAIT_TIMEOUT) { continue; }
                Fail(L"Cannot terminate Vibe Panel process: " + process.image, error);
            }
            if (WaitForSingleObject(terminating.get(), 5000) != WAIT_OBJECT_0) {
                Fail(L"Vibe Panel did not finish closing: " + process.image, ERROR_TIMEOUT);
            }
            log.Write(L"Terminated verified remaining process " + std::to_wstring(process.id));
        }
    }
    if (!FindProcesses(scope).empty()) { Fail(L"Vibe Panel is still restarting. Close it before running setup again.", ERROR_BUSY); }
}

struct DriverConfiguration {
    DWORD type;
    std::wstring path;
};

std::wstring DriverPath(std::wstring path) {
    if (path.size() >= 2 && path.front() == L'"' && path.back() == L'"') {
        path = path.substr(1, path.size() - 2);
    }
    return FullPath(path);
}

DriverConfiguration ReadDriverConfiguration(SC_HANDLE service) {
    DWORD size = 0;
    QueryServiceConfigW(service, nullptr, 0, &size);
    if (!size) { Fail(L"Cannot read hardware driver service configuration"); }
    std::vector<BYTE> bytes(size);
    auto configuration = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(bytes.data());
    if (!QueryServiceConfigW(service, configuration, size, &size)) { Fail(L"Cannot identify the hardware driver path"); }
    return { configuration->dwServiceType, configuration->lpBinaryPathName };
}

SERVICE_STATUS_PROCESS DriverStatus(SC_HANDLE service) {
    SERVICE_STATUS_PROCESS status{};
    DWORD size = 0;
    if (!QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&status), sizeof(status), &size)) {
        Fail(L"Cannot read hardware driver service status");
    }
    return status;
}

void WaitForServiceAbsent(SC_HANDLE manager) {
    const ULONGLONG deadline = GetTickCount64() + 15000;
    do {
        ServiceHandle probe(OpenServiceW(manager, ServiceName, SERVICE_QUERY_STATUS));
        if (!probe) {
            const DWORD error = GetLastError();
            if (error == ERROR_SERVICE_DOES_NOT_EXIST) { return; }
            if (error != ERROR_SERVICE_MARKED_FOR_DELETE) { Fail(L"Cannot verify driver service removal", error); }
        }
        probe.reset();
        Sleep(100);
    } while (GetTickCount64() < deadline);
    Fail(L"The hardware driver service is still pending deletion. Restart Windows before running setup again.", ERROR_SERVICE_MARKED_FOR_DELETE);
}

// A pending-delete service cannot be opened, but its configuration still
// identifies whether this is our service. Reading does not alter SCM state.
std::optional<DriverConfiguration> PendingDriverConfiguration() {
    HKEY key = nullptr;
    const LSTATUS opened = RegOpenKeyExW(HKEY_LOCAL_MACHINE,
        L"SYSTEM\\CurrentControlSet\\Services\\WinRing0_1_2_0", 0, KEY_QUERY_VALUE, &key);
    if (opened == ERROR_FILE_NOT_FOUND) { return std::nullopt; }
    if (opened != ERROR_SUCCESS) { Fail(L"Cannot identify pending driver service", static_cast<DWORD>(opened)); }
    DWORD type = 0, bytes = sizeof(type);
    LSTATUS result = RegGetValueW(key, nullptr, L"Type", RRF_RT_REG_DWORD, nullptr, &type, &bytes);
    DWORD pathBytes = 0;
    if (result == ERROR_SUCCESS) {
        result = RegGetValueW(key, nullptr, L"ImagePath", RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ | RRF_NOEXPAND,
            nullptr, nullptr, &pathBytes);
    }
    std::vector<wchar_t> path(static_cast<size_t>(pathBytes / sizeof(wchar_t)) + 1, L'\0');
    if (result == ERROR_SUCCESS) {
        result = RegGetValueW(key, nullptr, L"ImagePath", RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ | RRF_NOEXPAND,
            nullptr, path.data(), &pathBytes);
    }
    RegCloseKey(key);
    if (result != ERROR_SUCCESS) { Fail(L"Cannot read pending driver service configuration", static_cast<DWORD>(result)); }
    return DriverConfiguration{ type, path.data() };
}

bool IsOurConfiguration(const Scope& scope, const DriverConfiguration& configuration, const Log& log) {
    if (configuration.type != SERVICE_KERNEL_DRIVER) {
        log.Write(L"Preserving foreign WinRing0 service type " + std::to_wstring(configuration.type));
        return false;
    }
    std::wstring path;
    try { path = DriverPath(configuration.path); }
    catch (const Failure&) {
        log.Write(L"Preserving unrecognized WinRing0 ImagePath: " + configuration.path);
        return false;
    }
    if (!OwnedDriver(scope, path)) {
        log.Write(L"Preserving foreign WinRing0 ImagePath: " + path);
        return false;
    }
    log.Write(L"Verified Vibe Panel driver ImagePath: " + path);
    return true;
}

void CheckDriverNotShared() {
    Handle device(CreateFileW(L"\\\\.\\WinRing0_1_2_0", GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
    if (!device) { Fail(L"Cannot verify whether another hardware monitor is using the Vibe Panel driver"); }
    DWORD count = 0, returned = 0;
    if (!DeviceIoControl(device.get(), GetDriverReferenceCount, nullptr, 0, &count, sizeof(count), &returned, nullptr)) {
        Fail(L"Cannot verify hardware driver reference count");
    }
    if (returned != sizeof(count)) { Fail(L"Invalid hardware driver reference count result", ERROR_INVALID_DATA); }
    // This temporary device handle is itself one reference. No sensor IOCTLs
    // are sent, and no service or device is ever installed or started here.
    if (count > 1) { Fail(L"Another hardware monitor is using the Vibe Panel driver. Close that app and run setup again.", ERROR_SHARING_VIOLATION); }
}

void RemoveDriverService(const Scope& scope, const Log& log) {
    ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
    if (!manager) { Fail(L"Cannot inspect hardware driver registration"); }
    ServiceHandle read(OpenServiceW(manager.get(), ServiceName, SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
    if (!read) {
        const DWORD error = GetLastError();
        if (error == ERROR_SERVICE_DOES_NOT_EXIST) { log.Write(L"WinRing0 service is absent."); return; }
        if (error == ERROR_SERVICE_MARKED_FOR_DELETE) {
            const auto configuration = PendingDriverConfiguration();
            if (configuration && !IsOurConfiguration(scope, *configuration, log)) { return; }
            // The registry key can disappear before the SCM object is gone.
            // Only wait in that case; do not mistake pending removal for absence.
            WaitForServiceAbsent(manager.get());
            log.Write(L"Pending Vibe Panel driver service removal completed.");
            return;
        }
        Fail(L"Cannot inspect hardware driver service", error);
    }
    if (!IsOurConfiguration(scope, ReadDriverConfiguration(read.get()), log)) { return; }
    auto status = DriverStatus(read.get());
    log.Write(L"Owned driver service state: " + std::to_wstring(status.dwCurrentState));
    if (status.dwCurrentState == SERVICE_RUNNING) { CheckDriverNotShared(); }
    ServiceHandle service(OpenServiceW(manager.get(), ServiceName,
        SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
    if (!service) { Fail(L"Administrator permission is needed to release the Vibe Panel hardware driver"); }
    // Recheck the actual service configuration immediately before mutating it.
    if (!IsOurConfiguration(scope, ReadDriverConfiguration(service.get()), log)) {
        Fail(L"The hardware driver ownership changed during cleanup", ERROR_INVALID_DATA);
    }
    read.reset();
    status = DriverStatus(service.get());
    if (status.dwCurrentState != SERVICE_STOPPED && status.dwCurrentState != SERVICE_STOP_PENDING) {
        SERVICE_STATUS requested{};
        if (!ControlService(service.get(), SERVICE_CONTROL_STOP, &requested)) {
            const DWORD error = GetLastError();
            if (error != ERROR_SERVICE_NOT_ACTIVE) { Fail(L"Cannot stop the Vibe Panel hardware driver. Close other hardware monitors or restart Windows.", error); }
        }
    }
    const ULONGLONG deadline = GetTickCount64() + 15000;
    while (DriverStatus(service.get()).dwCurrentState != SERVICE_STOPPED) {
        if (GetTickCount64() >= deadline) { Fail(L"The hardware driver did not stop. Restart Windows before running setup again.", ERROR_TIMEOUT); }
        Sleep(100);
    }
    log.Write(L"Owned driver service is stopped.");
    if (!DeleteService(service.get())) {
        const DWORD error = GetLastError();
        if (error != ERROR_SERVICE_MARKED_FOR_DELETE) { Fail(L"Cannot remove Vibe Panel hardware driver registration", error); }
    }
    service.reset(); // An open service handle itself delays actual deletion.
    WaitForServiceAbsent(manager.get());
    log.Write(L"Owned driver service registration is absent.");
}

void RemoveDriverFiles(const Scope& scope, const Log& log) {
    OriginalUser user(scope.token.get());
    for (const auto& release : ReleaseFolders(scope)) {
        const auto path = Join(Join(release, L"engine"), DriverName);
        if (!OwnedDriver(scope, path)) { Fail(L"Driver cleanup escaped its release folder", ERROR_INVALID_DATA); }
        const ULONGLONG deadline = GetTickCount64() + 3000;
        do {
            CheckNoReparse(path);
            const DWORD attributes = GetFileAttributesW(path.c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES) {
                const DWORD error = GetLastError();
                if (IsMissing(error)) { break; }
                Fail(L"Cannot inspect runtime driver file: " + path, error);
            }
            if (attributes & FILE_ATTRIBUTE_DIRECTORY) { Fail(L"The runtime driver path is a directory: " + path, ERROR_DIRECTORY); }
            if (DeleteFileW(path.c_str())) {
                if (GetFileAttributesW(path.c_str()) == INVALID_FILE_ATTRIBUTES && IsMissing(GetLastError())) {
                    log.Write(L"Removed runtime driver file: " + path);
                    break;
                }
                Fail(L"The runtime driver file is still present after deletion: " + path, ERROR_BUSY);
            }
            const DWORD error = GetLastError();
            if (IsMissing(error)) { break; }
            if (error != ERROR_SHARING_VIOLATION || GetTickCount64() >= deadline) {
                Fail(L"Cannot remove the runtime driver file. Restart Windows and run setup again: " + path, error);
            }
            Sleep(100);
        } while (true);
    }
}

void VerifyCleanup(const Scope& scope, const Log& log) {
    if (!FindProcesses(scope).empty()) { Fail(L"Vibe Panel restarted during setup. Close it and run setup again.", ERROR_BUSY); }
    ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
    if (!manager) { Fail(L"Cannot verify final hardware driver registration"); }
    ServiceHandle service(OpenServiceW(manager.get(), ServiceName, SERVICE_QUERY_CONFIG));
    if (service) {
        if (IsOurConfiguration(scope, ReadDriverConfiguration(service.get()), log)) {
            Fail(L"The Vibe Panel hardware driver was registered again during cleanup. Close hardware monitors and retry setup.", ERROR_BUSY);
        }
    }
    else {
        const DWORD error = GetLastError();
        if (error == ERROR_SERVICE_MARKED_FOR_DELETE) {
            const auto configuration = PendingDriverConfiguration();
            if (!configuration || IsOurConfiguration(scope, *configuration, log)) {
                Fail(L"Vibe Panel driver removal is still pending. Restart Windows before running setup again.", error);
            }
        }
        else if (error != ERROR_SERVICE_DOES_NOT_EXIST) { Fail(L"Cannot verify final hardware driver removal", error); }
    }
    OriginalUser user(scope.token.get());
    for (const auto& release : ReleaseFolders(scope)) {
        const auto path = Join(Join(release, L"engine"), DriverName);
        CheckNoReparse(path);
        if (GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES) {
            Fail(L"The runtime driver file is still present. Close hardware monitors and retry setup: " + path, ERROR_BUSY);
        }
        const DWORD error = GetLastError();
        if (!IsMissing(error)) { Fail(L"Cannot verify runtime driver file removal: " + path, error); }
    }
}

void RemoveReleaseTree(const std::wstring& path, bool remove) {
    CheckNoReparse(path);
    WIN32_FIND_DATAW data{};
    const HANDLE search = FindFirstFileW(Join(path, L"*").c_str(), &data);
    if (search == INVALID_HANDLE_VALUE) {
        if (IsMissing(GetLastError())) { return; }
        Fail(L"Cannot inspect old release: " + path);
    }
    std::vector<std::pair<std::wstring, DWORD>> entries;
    do {
        if (wcscmp(data.cFileName, L".") && wcscmp(data.cFileName, L"..")) {
            entries.emplace_back(Join(path, data.cFileName), data.dwFileAttributes);
        }
    } while (FindNextFileW(search, &data));
    const DWORD error = GetLastError();
    FindClose(search);
    if (error != ERROR_NO_MORE_FILES) { Fail(L"Cannot enumerate old release: " + path, error); }
    for (const auto& [child, attributes] : entries) {
        CheckNoReparse(child);
        if (attributes & FILE_ATTRIBUTE_DIRECTORY) { RemoveReleaseTree(child, remove); }
        else if (remove && !DeleteFileW(child.c_str())) { Fail(L"Cannot remove old application file: " + child); }
    }
    if (remove && !RemoveDirectoryW(path.c_str())) { Fail(L"Cannot remove old release folder: " + path); }
}

// The old script wrote a per-user key. Read that user's hive explicitly in
// both registry views; never rely on the MSI service's HKCU mapping.
void RemoveLegacyRegistration(const Scope& scope, const Log& log) {
    LPWSTR sidText = nullptr;
    if (!ConvertSidToStringSidW(reinterpret_cast<const TOKEN_USER*>(scope.user.data())->User.Sid, &sidText)) {
        Fail(L"Cannot resolve the legacy registration owner");
    }
    const std::wstring keyPath = std::wstring(sidText) + L"\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\VibePanel";
    LocalFree(sidText);
    for (const REGSAM view : { KEY_WOW64_64KEY, KEY_WOW64_32KEY }) {
        HKEY key = nullptr;
        LSTATUS result = RegOpenKeyExW(HKEY_USERS, keyPath.c_str(), 0, KEY_QUERY_VALUE | view, &key);
        if (result == ERROR_FILE_NOT_FOUND) { continue; }
        if (result != ERROR_SUCCESS) { Fail(L"Cannot read legacy Vibe Panel registration", result); }
        DWORD managed = 0, bytes = sizeof(managed);
        const auto markerResult = RegGetValueW(key, nullptr, L"VibePanelManaged", RRF_RT_REG_DWORD, nullptr, &managed, &bytes);
        wchar_t location[32768]{};
        bytes = sizeof(location);
        const auto pathResult = RegGetValueW(key, nullptr, L"InstallLocation", RRF_RT_REG_SZ, nullptr, location, &bytes);
        RegCloseKey(key);
        if (markerResult != ERROR_SUCCESS || managed != 1 || pathResult != ERROR_SUCCESS) {
            Fail(L"The legacy Vibe Panel entry is not a recognized managed installation", ERROR_INVALID_DATA);
        }
        const auto path = FullPath(location);
        const auto root = ReleaseRoot(scope, path);
        if (!root || !Equal(path, *root) || !StartsWithPath(path, scope.apps)
            || !IsReleaseName(path.substr(scope.apps.size() + 1))) {
            Fail(L"The legacy registration points outside managed Vibe Panel releases", ERROR_INVALID_DATA);
        }
        // This key has no subkeys. Refuse unexpected children rather than
        // recursively deleting a registration with unknown contents.
        result = RegDeleteKeyExW(HKEY_USERS, keyPath.c_str(), view, 0);
        if (result != ERROR_SUCCESS && result != ERROR_FILE_NOT_FOUND) { Fail(L"Cannot remove legacy registration", result); }
        result = RegOpenKeyExW(HKEY_USERS, keyPath.c_str(), 0, KEY_QUERY_VALUE | view, &key);
        if (result == ERROR_SUCCESS) { RegCloseKey(key); Fail(L"Legacy registration remains after cleanup", ERROR_BUSY); }
        if (result != ERROR_FILE_NOT_FOUND) { Fail(L"Cannot verify legacy registration removal", result); }
        log.Write(L"Removed and verified legacy Vibe Panel registration.");
    }
}

void RemovePreviousReleases(const Scope& scope, const Log& log, bool cleanInstall) {
    OriginalUser user(scope.token.get());
    const auto releases = ReleaseFolders(scope);
    // Preflight the entire selected tree before deleting any file.
    for (const auto& release : releases) {
        if (cleanInstall || !Equal(release, Join(scope.apps, L"MSI"))) { RemoveReleaseTree(release, false); }
    }
    for (const auto& release : releases) {
        if (cleanInstall || !Equal(release, Join(scope.apps, L"MSI"))) {
            RemoveReleaseTree(release, true);
            log.Write(L"Removed previous application folder: " + release);
        }
    }
    RemoveLegacyRegistration(scope, log);
}

void Cleanup(const Scope& scope, const Log& log, bool closeOnly, bool cleanInstall) {
    log.Write(L"Starting scoped cleanup; administrator=" + std::to_wstring(IsAdministrator() ? 1 : 0));
    StopProcesses(scope, log);
    if (closeOnly) { log.Write(L"Verified Vibe Panel processes are closed."); return; }
    RemoveDriverService(scope, log);
    try { RemoveDriverFiles(scope, log); }
    catch (const Failure& error) {
        // Elevation must not turn a denied write in a user folder into an
        // administrator write. These files are always removed as their owner.
        throw Failure{ error.code, error.message, false };
    }
    VerifyCleanup(scope, log);
    try { RemovePreviousReleases(scope, log, cleanInstall); }
    catch (const Failure& error) { throw Failure{ error.code, error.message, false }; }
    log.Write(L"Cleanup completed: verified app processes stopped; owned driver registration and files removed.");
}

uint64_t ParseNumber(const wchar_t* value) {
    if (!value || !*value || !std::all_of(value, value + wcslen(value), [](wchar_t character) {
        return character >= L'0' && character <= L'9';
    })) { Fail(L"Invalid setup helper argument", ERROR_INVALID_PARAMETER); }
    errno = 0;
    wchar_t* end = nullptr;
    const uint64_t number = _wcstoui64(value, &end, 10);
    if (errno == ERANGE || !end || *end) { Fail(L"Invalid setup helper argument", ERROR_INVALID_PARAMETER); }
    return number;
}

DWORD Elevate(int uiLevel, const Log& log, bool closeOnly, bool cleanInstall) {
    const auto executable = ProcessPath(GetCurrentProcess());
    const auto parameters = std::wstring(closeOnly ? L"--close-apps" : cleanInstall ? L"--clean-install" : L"--cleanup") + L" --ui-level " + std::to_wstring(uiLevel) + L" --parent "
        + std::to_wstring(GetCurrentProcessId()) + L" --parent-start " + std::to_wstring(ProcessStart(GetCurrentProcess()));
    SHELLEXECUTEINFOW launch{};
    launch.cbSize = sizeof(launch);
    launch.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI | SEE_MASK_CLASSNAME;
    launch.lpVerb = L"runas";
    launch.lpClass = L"exefile"; // MSI Binary-table EXEs can be extracted with a .tmp suffix.
    launch.lpFile = executable.c_str();
    launch.lpParameters = parameters.c_str();
    launch.nShow = SW_HIDE;
    log.Write(L"Requesting administrator permission for scoped cleanup.");
    if (!ShellExecuteExW(&launch)) {
        const DWORD error = GetLastError();
        if (error == ERROR_CANCELLED) { log.Write(L"Administrator permission was cancelled."); return CancelExit; }
        Fail(L"Cannot request administrator permission for Vibe Panel cleanup", error);
    }
    Handle child(launch.hProcess);
    if (!child) { Fail(L"The elevated cleanup process is unavailable", ERROR_INVALID_HANDLE); }
    if (WaitForSingleObject(child.get(), 120000) != WAIT_OBJECT_0) {
        TerminateProcess(child.get(), FailureExit);
        WaitForSingleObject(child.get(), 5000);
        Fail(L"Elevated cleanup timed out. Restart Windows before running setup again.", ERROR_TIMEOUT);
    }
    DWORD code = FailureExit;
    if (!GetExitCodeProcess(child.get(), &code)) { Fail(L"Cannot read elevated cleanup completion"); }
    log.Write(L"Elevated cleanup exit code: " + std::to_wstring(code));
    return code;
}

void Report(const Failure& error, int uiLevel, const Log* log) {
    const auto detail = error.message + L"\nWindows error: " + std::to_wstring(error.code);
    if (log) {
        try { log->Write(L"FAILED: " + detail); }
        catch (...) {} // Preserve the original failure if logging also fails.
    }
    if (uiLevel >= 3) {
        const auto message = L"Vibe Panel setup could not finish cleanup.\n\n" + detail
            + (log ? L"\n\nDetails: " + log->path() : L"");
        MessageBoxW(nullptr, message.c_str(), L"Vibe Panel Setup", MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
    }
}
} // namespace

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int argumentCount = 0;
    auto arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    int uiLevel = 2;
    bool closeOnly = false;
    bool cleanInstall = false;
    DWORD parentId = 0;
    uint64_t parentStart = 0;
    std::optional<Log> log;
    try {
        if (!arguments || (argumentCount != 4 && argumentCount != 8)
            || (wcscmp(arguments[1], L"--cleanup") != 0 && wcscmp(arguments[1], L"--close-apps") != 0 && wcscmp(arguments[1], L"--clean-install") != 0)
            || wcscmp(arguments[2], L"--ui-level") != 0) {
            Fail(L"Usage: VibePanel.SetupActions.exe <--cleanup|--close-apps> --ui-level <2..5>", ERROR_INVALID_PARAMETER);
        }
        closeOnly = wcscmp(arguments[1], L"--close-apps") == 0;
        cleanInstall = wcscmp(arguments[1], L"--clean-install") == 0;
        const auto level = ParseNumber(arguments[3]);
        if (level < 2 || level > 5) { Fail(L"Invalid installer UI level", ERROR_INVALID_PARAMETER); }
        uiLevel = static_cast<int>(level);
        if (argumentCount == 8) {
            if (wcscmp(arguments[4], L"--parent") != 0 || wcscmp(arguments[6], L"--parent-start") != 0) {
                Fail(L"Invalid elevated cleanup request", ERROR_INVALID_PARAMETER);
            }
            const auto id = ParseNumber(arguments[5]);
            parentStart = ParseNumber(arguments[7]);
            if (id == 0 || id > MAXDWORD || parentStart == 0 || !IsAdministrator()) {
                Fail(L"Invalid elevated cleanup identity", ERROR_INVALID_PARAMETER);
            }
            parentId = static_cast<DWORD>(id);
        }
        LocalFree(arguments);
        arguments = nullptr;
        auto scope = MakeScope(parentId, parentStart);
        log.emplace(scope);
        try { Cleanup(scope, *log, closeOnly, cleanInstall); }
        catch (const Failure& error) {
            if (error.code != ERROR_ACCESS_DENIED || !error.canElevate || parentId || IsAdministrator()) { throw; }
            log->Write(L"Permission needed: " + error.message);
            if (uiLevel < 3) {
                Fail(L"Silent cleanup requires administrator permission. Run setup elevated or close the elevated app and release its driver first.",
                    ERROR_ACCESS_DENIED);
            }
            return static_cast<int>(Elevate(uiLevel, *log, closeOnly, cleanInstall));
        }
        return 0;
    }
    catch (const Failure& error) { Report(error, uiLevel, log ? &*log : nullptr); }
    catch (const std::exception&) {
        Report(Failure{ ERROR_UNHANDLED_EXCEPTION, L"Unexpected setup cleanup failure. Restart Windows and run setup again." },
            uiLevel, log ? &*log : nullptr);
    }
    if (arguments) { LocalFree(arguments); }
    return static_cast<int>(FailureExit);
}
