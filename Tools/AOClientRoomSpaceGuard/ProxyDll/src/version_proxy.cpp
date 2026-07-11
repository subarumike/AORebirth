// Derived from Inorien/AOReloaded's MIT-licensed version.dll proxy.
// The complete upstream license is distributed in LICENSES/AOReloaded-MIT.txt.

#include "version_proxy.h"

#include "logging.h"

#include <windows.h>

#include <cstdio>

namespace
{
    using FnGetFileVersionInfoA = BOOL(WINAPI*)(LPCSTR, DWORD, DWORD, LPVOID);
    using FnGetFileVersionInfoByHandle = BOOL(WINAPI*)(DWORD, HANDLE, LPVOID*, PDWORD);
    using FnGetFileVersionInfoExA = BOOL(WINAPI*)(DWORD, LPCSTR, DWORD, DWORD, LPVOID);
    using FnGetFileVersionInfoExW = BOOL(WINAPI*)(DWORD, LPCWSTR, DWORD, DWORD, LPVOID);
    using FnGetFileVersionInfoSizeA = DWORD(WINAPI*)(LPCSTR, LPDWORD);
    using FnGetFileVersionInfoSizeExA = DWORD(WINAPI*)(DWORD, LPCSTR, LPDWORD);
    using FnGetFileVersionInfoSizeExW = DWORD(WINAPI*)(DWORD, LPCWSTR, LPDWORD);
    using FnGetFileVersionInfoSizeW = DWORD(WINAPI*)(LPCWSTR, LPDWORD);
    using FnGetFileVersionInfoW = BOOL(WINAPI*)(LPCWSTR, DWORD, DWORD, LPVOID);
    using FnVerFindFileA = DWORD(WINAPI*)(
        DWORD, LPCSTR, LPCSTR, LPCSTR, LPSTR, PUINT, LPSTR, PUINT);
    using FnVerFindFileW = DWORD(WINAPI*)(
        DWORD, LPCWSTR, LPCWSTR, LPCWSTR, LPWSTR, PUINT, LPWSTR, PUINT);
    using FnVerInstallFileA = DWORD(WINAPI*)(
        DWORD, LPCSTR, LPCSTR, LPCSTR, LPCSTR, LPCSTR, LPSTR, PUINT);
    using FnVerInstallFileW = DWORD(WINAPI*)(
        DWORD, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPWSTR, PUINT);
    using FnVerLanguageNameA = DWORD(WINAPI*)(DWORD, LPSTR, DWORD);
    using FnVerLanguageNameW = DWORD(WINAPI*)(DWORD, LPWSTR, DWORD);
    using FnVerQueryValueA = BOOL(WINAPI*)(LPCVOID, LPCSTR, LPVOID*, PUINT);
    using FnVerQueryValueW = BOOL(WINAPI*)(LPCVOID, LPCWSTR, LPVOID*, PUINT);

    HMODULE RealVersion = nullptr;
    INIT_ONCE ResolveOnce = INIT_ONCE_STATIC_INIT;
    bool ResolveSucceeded = false;

    FnGetFileVersionInfoA RealGetFileVersionInfoA = nullptr;
    FnGetFileVersionInfoByHandle RealGetFileVersionInfoByHandle = nullptr;
    FnGetFileVersionInfoExA RealGetFileVersionInfoExA = nullptr;
    FnGetFileVersionInfoExW RealGetFileVersionInfoExW = nullptr;
    FnGetFileVersionInfoSizeA RealGetFileVersionInfoSizeA = nullptr;
    FnGetFileVersionInfoSizeExA RealGetFileVersionInfoSizeExA = nullptr;
    FnGetFileVersionInfoSizeExW RealGetFileVersionInfoSizeExW = nullptr;
    FnGetFileVersionInfoSizeW RealGetFileVersionInfoSizeW = nullptr;
    FnGetFileVersionInfoW RealGetFileVersionInfoW = nullptr;
    FnVerFindFileA RealVerFindFileA = nullptr;
    FnVerFindFileW RealVerFindFileW = nullptr;
    FnVerInstallFileA RealVerInstallFileA = nullptr;
    FnVerInstallFileW RealVerInstallFileW = nullptr;
    FnVerLanguageNameA RealVerLanguageNameA = nullptr;
    FnVerLanguageNameW RealVerLanguageNameW = nullptr;
    FnVerQueryValueA RealVerQueryValueA = nullptr;
    FnVerQueryValueW RealVerQueryValueW = nullptr;

    template<typename T>
    bool Resolve(T& destination, const char* name)
    {
        destination = reinterpret_cast<T>(GetProcAddress(RealVersion, name));
        if (!destination)
        {
            aorf::Log("ERROR system version.dll export missing name=%s", name);
            return false;
        }

        return true;
    }

    BOOL CALLBACK ResolveVersionExports(PINIT_ONCE, PVOID, PVOID*)
    {
        wchar_t systemDirectory[MAX_PATH] = {};
        UINT length = GetSystemDirectoryW(
            systemDirectory,
            static_cast<UINT>(sizeof(systemDirectory) / sizeof(systemDirectory[0])));
        if (length == 0 || length >= sizeof(systemDirectory) / sizeof(systemDirectory[0]))
        {
            return TRUE;
        }

        wchar_t path[MAX_PATH] = {};
        if (_snwprintf_s(
                path,
                sizeof(path) / sizeof(path[0]),
                _TRUNCATE,
                L"%s\\version.dll",
                systemDirectory) < 0)
        {
            return TRUE;
        }

        RealVersion = LoadLibraryW(path);
        if (!RealVersion)
        {
            return TRUE;
        }

        bool ok = true;
        ok &= Resolve(RealGetFileVersionInfoA, "GetFileVersionInfoA");
        ok &= Resolve(RealGetFileVersionInfoByHandle, "GetFileVersionInfoByHandle");
        ok &= Resolve(RealGetFileVersionInfoExA, "GetFileVersionInfoExA");
        ok &= Resolve(RealGetFileVersionInfoExW, "GetFileVersionInfoExW");
        ok &= Resolve(RealGetFileVersionInfoSizeA, "GetFileVersionInfoSizeA");
        ok &= Resolve(RealGetFileVersionInfoSizeExA, "GetFileVersionInfoSizeExA");
        ok &= Resolve(RealGetFileVersionInfoSizeExW, "GetFileVersionInfoSizeExW");
        ok &= Resolve(RealGetFileVersionInfoSizeW, "GetFileVersionInfoSizeW");
        ok &= Resolve(RealGetFileVersionInfoW, "GetFileVersionInfoW");
        ok &= Resolve(RealVerFindFileA, "VerFindFileA");
        ok &= Resolve(RealVerFindFileW, "VerFindFileW");
        ok &= Resolve(RealVerInstallFileA, "VerInstallFileA");
        ok &= Resolve(RealVerInstallFileW, "VerInstallFileW");
        ok &= Resolve(RealVerLanguageNameA, "VerLanguageNameA");
        ok &= Resolve(RealVerLanguageNameW, "VerLanguageNameW");
        ok &= Resolve(RealVerQueryValueA, "VerQueryValueA");
        ok &= Resolve(RealVerQueryValueW, "VerQueryValueW");
        ResolveSucceeded = ok;
        return TRUE;
    }
}

bool EnsureRealVersionDllLoaded()
{
    if (!InitOnceExecuteOnce(&ResolveOnce, ResolveVersionExports, nullptr, nullptr))
    {
        return false;
    }

    return ResolveSucceeded;
}

extern "C"
{
    BOOL WINAPI GetFileVersionInfoA(LPCSTR a, DWORD b, DWORD c, LPVOID d)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoA
            ? RealGetFileVersionInfoA(a, b, c, d) : FALSE;
    }

    BOOL WINAPI GetFileVersionInfoByHandle(DWORD a, HANDLE b, LPVOID* c, PDWORD d)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoByHandle
            ? RealGetFileVersionInfoByHandle(a, b, c, d) : FALSE;
    }

    BOOL WINAPI GetFileVersionInfoExA(DWORD a, LPCSTR b, DWORD c, DWORD d, LPVOID e)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoExA
            ? RealGetFileVersionInfoExA(a, b, c, d, e) : FALSE;
    }

    BOOL WINAPI GetFileVersionInfoExW(DWORD a, LPCWSTR b, DWORD c, DWORD d, LPVOID e)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoExW
            ? RealGetFileVersionInfoExW(a, b, c, d, e) : FALSE;
    }

    DWORD WINAPI GetFileVersionInfoSizeA(LPCSTR a, LPDWORD b)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoSizeA
            ? RealGetFileVersionInfoSizeA(a, b) : 0;
    }

    DWORD WINAPI GetFileVersionInfoSizeExA(DWORD a, LPCSTR b, LPDWORD c)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoSizeExA
            ? RealGetFileVersionInfoSizeExA(a, b, c) : 0;
    }

    DWORD WINAPI GetFileVersionInfoSizeExW(DWORD a, LPCWSTR b, LPDWORD c)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoSizeExW
            ? RealGetFileVersionInfoSizeExW(a, b, c) : 0;
    }

    DWORD WINAPI GetFileVersionInfoSizeW(LPCWSTR a, LPDWORD b)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoSizeW
            ? RealGetFileVersionInfoSizeW(a, b) : 0;
    }

    BOOL WINAPI GetFileVersionInfoW(LPCWSTR a, DWORD b, DWORD c, LPVOID d)
    {
        return EnsureRealVersionDllLoaded() && RealGetFileVersionInfoW
            ? RealGetFileVersionInfoW(a, b, c, d) : FALSE;
    }

    DWORD WINAPI VerFindFileA(
        DWORD a, LPCSTR b, LPCSTR c, LPCSTR d, LPSTR e, PUINT f, LPSTR g, PUINT h)
    {
        return EnsureRealVersionDllLoaded() && RealVerFindFileA
            ? RealVerFindFileA(a, b, c, d, e, f, g, h) : 0;
    }

    DWORD WINAPI VerFindFileW(
        DWORD a, LPCWSTR b, LPCWSTR c, LPCWSTR d, LPWSTR e, PUINT f, LPWSTR g, PUINT h)
    {
        return EnsureRealVersionDllLoaded() && RealVerFindFileW
            ? RealVerFindFileW(a, b, c, d, e, f, g, h) : 0;
    }

    DWORD WINAPI VerInstallFileA(
        DWORD a, LPCSTR b, LPCSTR c, LPCSTR d, LPCSTR e, LPCSTR f, LPSTR g, PUINT h)
    {
        return EnsureRealVersionDllLoaded() && RealVerInstallFileA
            ? RealVerInstallFileA(a, b, c, d, e, f, g, h) : 0;
    }

    DWORD WINAPI VerInstallFileW(
        DWORD a, LPCWSTR b, LPCWSTR c, LPCWSTR d, LPCWSTR e, LPCWSTR f, LPWSTR g, PUINT h)
    {
        return EnsureRealVersionDllLoaded() && RealVerInstallFileW
            ? RealVerInstallFileW(a, b, c, d, e, f, g, h) : 0;
    }

    DWORD WINAPI VerLanguageNameA(DWORD a, LPSTR b, DWORD c)
    {
        return EnsureRealVersionDllLoaded() && RealVerLanguageNameA
            ? RealVerLanguageNameA(a, b, c) : 0;
    }

    DWORD WINAPI VerLanguageNameW(DWORD a, LPWSTR b, DWORD c)
    {
        return EnsureRealVersionDllLoaded() && RealVerLanguageNameW
            ? RealVerLanguageNameW(a, b, c) : 0;
    }

    BOOL WINAPI VerQueryValueA(LPCVOID a, LPCSTR b, LPVOID* c, PUINT d)
    {
        return EnsureRealVersionDllLoaded() && RealVerQueryValueA
            ? RealVerQueryValueA(a, b, c, d) : FALSE;
    }

    BOOL WINAPI VerQueryValueW(LPCVOID a, LPCWSTR b, LPVOID* c, PUINT d)
    {
        return EnsureRealVersionDllLoaded() && RealVerQueryValueW
            ? RealVerQueryValueW(a, b, c, d) : FALSE;
    }
}
