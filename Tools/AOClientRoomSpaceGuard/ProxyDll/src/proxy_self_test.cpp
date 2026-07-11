#include <windows.h>

#include <array>
#include <cstdio>
#include <vector>

namespace
{
    struct ExportExpectation
    {
        const char* name;
        WORD ordinal;
    };

    constexpr ExportExpectation ExpectedExports[] =
    {
        { "GetFileVersionInfoA", 1 },
        { "GetFileVersionInfoByHandle", 2 },
        { "GetFileVersionInfoExA", 3 },
        { "GetFileVersionInfoExW", 4 },
        { "GetFileVersionInfoSizeA", 5 },
        { "GetFileVersionInfoSizeExA", 6 },
        { "GetFileVersionInfoSizeExW", 7 },
        { "GetFileVersionInfoSizeW", 8 },
        { "GetFileVersionInfoW", 9 },
        { "VerFindFileA", 10 },
        { "VerFindFileW", 11 },
        { "VerInstallFileA", 12 },
        { "VerInstallFileW", 13 },
        { "VerLanguageNameA", 14 },
        { "VerLanguageNameW", 15 },
        { "VerQueryValueA", 16 },
        { "VerQueryValueW", 17 }
    };

    using FnGetFileVersionInfoSizeW = DWORD(WINAPI*)(LPCWSTR, LPDWORD);
    using FnGetFileVersionInfoW = BOOL(WINAPI*)(LPCWSTR, DWORD, DWORD, LPVOID);
    using FnVerQueryValueW = BOOL(WINAPI*)(LPCVOID, LPCWSTR, LPVOID*, PUINT);
    using FnGetFileVersionInfoByHandle = BOOL(WINAPI*)(DWORD, HANDLE, LPVOID*, PDWORD);

    bool ValidateExactExportDirectory(HMODULE module)
    {
        const BYTE* base = reinterpret_cast<const BYTE*>(module);
        const IMAGE_DOS_HEADER* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
        if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew <= 0)
        {
            return false;
        }

        const IMAGE_NT_HEADERS32* nt = reinterpret_cast<const IMAGE_NT_HEADERS32*>(
            base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE ||
            nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC)
        {
            return false;
        }

        const IMAGE_DATA_DIRECTORY& directory =
            nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
        if (directory.VirtualAddress == 0 ||
            directory.Size < sizeof(IMAGE_EXPORT_DIRECTORY))
        {
            return false;
        }

        const IMAGE_EXPORT_DIRECTORY* exports =
            reinterpret_cast<const IMAGE_EXPORT_DIRECTORY*>(
                base + directory.VirtualAddress);
        return exports->Base == 1 &&
               exports->NumberOfFunctions ==
                   sizeof(ExpectedExports) / sizeof(ExpectedExports[0]) &&
               exports->NumberOfNames ==
                   sizeof(ExpectedExports) / sizeof(ExpectedExports[0]);
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc != 2)
    {
        std::fwprintf(stderr, L"Usage: ProxyForwardingSelfTest.exe <version.dll>\n");
        return 2;
    }

    HMODULE proxy = LoadLibraryW(argv[1]);
    if (!proxy)
    {
        std::fwprintf(stderr, L"LoadLibrary failed: %lu\n", GetLastError());
        return 1;
    }

    if (!ValidateExactExportDirectory(proxy))
    {
        std::fprintf(stderr, "PE machine or exact export count mismatch.\n");
        return 1;
    }

    for (const ExportExpectation& expected : ExpectedExports)
    {
        FARPROC byName = GetProcAddress(proxy, expected.name);
        FARPROC byOrdinal = GetProcAddress(
            proxy,
            MAKEINTRESOURCEA(expected.ordinal));
        if (!byName || byName != byOrdinal)
        {
            std::fprintf(
                stderr,
                "Export mismatch name=%s ordinal=%u\n",
                expected.name,
                expected.ordinal);
            return 1;
        }
    }

    auto getSize = reinterpret_cast<FnGetFileVersionInfoSizeW>(
        GetProcAddress(proxy, "GetFileVersionInfoSizeW"));
    auto getInfo = reinterpret_cast<FnGetFileVersionInfoW>(
        GetProcAddress(proxy, "GetFileVersionInfoW"));
    auto query = reinterpret_cast<FnVerQueryValueW>(
        GetProcAddress(proxy, "VerQueryValueW"));
    auto getByHandle = reinterpret_cast<FnGetFileVersionInfoByHandle>(
        GetProcAddress(proxy, "GetFileVersionInfoByHandle"));

    wchar_t systemDirectory[MAX_PATH] = {};
    UINT systemLength = GetSystemDirectoryW(
        systemDirectory,
        static_cast<UINT>(sizeof(systemDirectory) / sizeof(systemDirectory[0])));
    if (systemLength == 0 || systemLength >= sizeof(systemDirectory) / sizeof(systemDirectory[0]))
    {
        return 1;
    }

    wchar_t kernelPath[MAX_PATH] = {};
    if (_snwprintf_s(
            kernelPath,
            sizeof(kernelPath) / sizeof(kernelPath[0]),
            _TRUNCATE,
            L"%s\\kernel32.dll",
            systemDirectory) < 0)
    {
        return 1;
    }

    DWORD ignored = 0;
    DWORD size = getSize(kernelPath, &ignored);
    if (size == 0)
    {
        std::fprintf(stderr, "GetFileVersionInfoSizeW failed: %lu\n", GetLastError());
        return 1;
    }

    std::vector<BYTE> versionInfo(size);
    if (!getInfo(kernelPath, 0, size, versionInfo.data()))
    {
        std::fprintf(stderr, "GetFileVersionInfoW failed: %lu\n", GetLastError());
        return 1;
    }

    LPVOID fixedInfo = nullptr;
    UINT fixedInfoSize = 0;
    if (!query(versionInfo.data(), L"\\", &fixedInfo, &fixedInfoSize) ||
        !fixedInfo || fixedInfoSize < sizeof(VS_FIXEDFILEINFO))
    {
        std::fprintf(stderr, "VerQueryValueW failed: %lu\n", GetLastError());
        return 1;
    }

    HANDLE kernelFile = CreateFileW(
        kernelPath,
        GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (kernelFile == INVALID_HANDLE_VALUE)
    {
        return 1;
    }

    LPVOID handleInfo = nullptr;
    DWORD handleInfoSize = 0;
    BOOL byHandleSucceeded = getByHandle(0, kernelFile, &handleInfo, &handleInfoSize);
    CloseHandle(kernelFile);
    if (!byHandleSucceeded || !handleInfo || handleInfoSize == 0)
    {
        if (handleInfo)
        {
            LocalFree(handleInfo);
        }
        std::fprintf(stderr, "GetFileVersionInfoByHandle failed: %lu\n", GetLastError());
        return 1;
    }
    LocalFree(handleInfo);

    std::printf("Proxy forwarding self-test passed: exports=17 functional=4.\n");
    return 0;
}
