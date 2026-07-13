#include "crash_dump.h"

#include "logging.h"

#include <windows.h>
#include <dbghelp.h>

#include <cstdint>
#include <cstdio>

namespace
{
    LPTOP_LEVEL_EXCEPTION_FILTER PreviousUnhandledFilter = nullptr;
    LONG DumpInProgress = 0;

    bool BuildDumpDirectory(wchar_t* directory, size_t count)
    {
        wchar_t localAppData[MAX_PATH] = {};
        DWORD length = GetEnvironmentVariableW(
            L"LOCALAPPDATA",
            localAppData,
            static_cast<DWORD>(sizeof(localAppData) / sizeof(localAppData[0])));
        if (length == 0 || length >= sizeof(localAppData) / sizeof(localAppData[0]))
        {
            return false;
        }

        wchar_t root[MAX_PATH] = {};
        if (_snwprintf_s(
                root,
                sizeof(root) / sizeof(root[0]),
                _TRUNCATE,
                L"%s\\AORoomSpaceFix",
                localAppData) < 0)
        {
            return false;
        }

        if (!CreateDirectoryW(root, nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
        {
            return false;
        }

        if (_snwprintf_s(directory, count, _TRUNCATE, L"%s\\Dumps", root) < 0)
        {
            return false;
        }

        return CreateDirectoryW(directory, nullptr) ||
            GetLastError() == ERROR_ALREADY_EXISTS;
    }

    bool BuildDumpPath(EXCEPTION_POINTERS* exception, wchar_t* path, size_t count)
    {
        wchar_t directory[MAX_PATH] = {};
        if (!BuildDumpDirectory(directory, sizeof(directory) / sizeof(directory[0])))
        {
            return false;
        }

        SYSTEMTIME time = {};
        GetLocalTime(&time);

        DWORD exceptionCode = 0;
        uintptr_t exceptionAddress = 0;
        if (exception && exception->ExceptionRecord)
        {
            exceptionCode = exception->ExceptionRecord->ExceptionCode;
            exceptionAddress = reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress);
        }

        return _snwprintf_s(
            path,
            count,
            _TRUNCATE,
            L"%s\\AO-%04u%02u%02u-%02u%02u%02u-%03u-pid%lu-ex%08lX-at%08lX.dmp",
            directory,
            time.wYear,
            time.wMonth,
            time.wDay,
            time.wHour,
            time.wMinute,
            time.wSecond,
            time.wMilliseconds,
            GetCurrentProcessId(),
            static_cast<unsigned long>(exceptionCode),
            static_cast<unsigned long>(exceptionAddress & 0xFFFFFFFFu)) >= 0;
    }

    HMODULE LoadSystemDbgHelp()
    {
        wchar_t systemDirectory[MAX_PATH] = {};
        UINT length = GetSystemDirectoryW(
            systemDirectory,
            static_cast<UINT>(sizeof(systemDirectory) / sizeof(systemDirectory[0])));
        if (length == 0 || length >= sizeof(systemDirectory) / sizeof(systemDirectory[0]))
        {
            return nullptr;
        }

        wchar_t path[MAX_PATH] = {};
        if (_snwprintf_s(
                path,
                sizeof(path) / sizeof(path[0]),
                _TRUNCATE,
                L"%s\\dbghelp.dll",
                systemDirectory) < 0)
        {
            return nullptr;
        }

        return LoadLibraryW(path);
    }

    bool WriteDump(EXCEPTION_POINTERS* exception, wchar_t* dumpPath, size_t dumpPathCount)
    {
        if (!BuildDumpPath(exception, dumpPath, dumpPathCount))
        {
            aorf::Log("ERROR crash dump path creation failed code=%lu", GetLastError());
            return false;
        }

        HANDLE file = CreateFileW(
            dumpPath,
            GENERIC_WRITE,
            FILE_SHARE_READ,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            aorf::Log("ERROR crash dump file creation failed code=%lu", GetLastError());
            return false;
        }

        HMODULE dbghelp = LoadSystemDbgHelp();
        if (!dbghelp)
        {
            CloseHandle(file);
            aorf::Log("ERROR dbghelp.dll load failed code=%lu", GetLastError());
            return false;
        }

        using MiniDumpWriteDumpFn = BOOL(WINAPI*)(
            HANDLE,
            DWORD,
            HANDLE,
            MINIDUMP_TYPE,
            PMINIDUMP_EXCEPTION_INFORMATION,
            PMINIDUMP_USER_STREAM_INFORMATION,
            PMINIDUMP_CALLBACK_INFORMATION);
        auto miniDumpWriteDump = reinterpret_cast<MiniDumpWriteDumpFn>(
            GetProcAddress(dbghelp, "MiniDumpWriteDump"));
        if (!miniDumpWriteDump)
        {
            FreeLibrary(dbghelp);
            CloseHandle(file);
            aorf::Log("ERROR MiniDumpWriteDump export missing");
            return false;
        }

        MINIDUMP_EXCEPTION_INFORMATION exceptionInfo = {};
        exceptionInfo.ThreadId = GetCurrentThreadId();
        exceptionInfo.ExceptionPointers = exception;
        exceptionInfo.ClientPointers = FALSE;

        MINIDUMP_TYPE dumpType = static_cast<MINIDUMP_TYPE>(
            MiniDumpNormal |
            MiniDumpWithDataSegs |
            MiniDumpWithIndirectlyReferencedMemory);
        BOOL written = miniDumpWriteDump(
            GetCurrentProcess(),
            GetCurrentProcessId(),
            file,
            dumpType,
            &exceptionInfo,
            nullptr,
            nullptr);

        DWORD error = GetLastError();
        FreeLibrary(dbghelp);
        CloseHandle(file);

        if (!written)
        {
            aorf::Log("ERROR MiniDumpWriteDump failed code=%lu", error);
            return false;
        }

        return true;
    }

    LONG WINAPI CrashDumpUnhandledFilter(EXCEPTION_POINTERS* exception)
    {
        DWORD exceptionCode = 0;
        uintptr_t exceptionAddress = 0;
        uintptr_t accessAddress = 0;
        unsigned long accessType = 0;
        if (exception && exception->ExceptionRecord)
        {
            exceptionCode = exception->ExceptionRecord->ExceptionCode;
            exceptionAddress = reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress);
            if (exception->ExceptionRecord->NumberParameters >= 2)
            {
                accessType = static_cast<unsigned long>(
                    exception->ExceptionRecord->ExceptionInformation[0]);
                accessAddress = static_cast<uintptr_t>(
                    exception->ExceptionRecord->ExceptionInformation[1]);
            }
        }

        if (InterlockedCompareExchange(&DumpInProgress, 1, 0) == 0)
        {
            wchar_t dumpPath[MAX_PATH] = {};
            bool written = WriteDump(
                exception,
                dumpPath,
                sizeof(dumpPath) / sizeof(dumpPath[0]));
            aorf::Log(
                "CRASH exception=0x%08lX address=0x%08lX accessType=%lu accessAddress=0x%08lX dumpWritten=%s path=\"%ls\"",
                static_cast<unsigned long>(exceptionCode),
                static_cast<unsigned long>(exceptionAddress & 0xFFFFFFFFu),
                accessType,
                static_cast<unsigned long>(accessAddress & 0xFFFFFFFFu),
                written ? "true" : "false",
                written ? dumpPath : L"");
        }

        if (PreviousUnhandledFilter)
        {
            return PreviousUnhandledFilter(exception);
        }

        return EXCEPTION_CONTINUE_SEARCH;
    }
}

namespace aorf
{
    bool InstallCrashDumpHandler()
    {
        PreviousUnhandledFilter = SetUnhandledExceptionFilter(CrashDumpUnhandledFilter);
        Log("PATCH PASS crash dump handler active");
        return true;
    }
}
