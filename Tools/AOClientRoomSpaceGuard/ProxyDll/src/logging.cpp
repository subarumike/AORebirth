#include "logging.h"

#include <windows.h>

#include <cstdarg>
#include <cstdio>
#include <cstring>

namespace aorf
{
    namespace
    {
        HANDLE LogFile = INVALID_HANDLE_VALUE;
        CRITICAL_SECTION LogLock;
        LONG LogState = 0;
    }

    void LogInit()
    {
        if (InterlockedCompareExchange(&LogState, 1, 0) != 0)
        {
            return;
        }

        InitializeCriticalSection(&LogLock);

        wchar_t localAppData[MAX_PATH] = {};
        DWORD length = GetEnvironmentVariableW(
            L"LOCALAPPDATA",
            localAppData,
            static_cast<DWORD>(sizeof(localAppData) / sizeof(localAppData[0])));
        if (length == 0 || length >= sizeof(localAppData) / sizeof(localAppData[0]))
        {
            return;
        }

        wchar_t directory[MAX_PATH] = {};
        if (_snwprintf_s(
                directory,
                sizeof(directory) / sizeof(directory[0]),
                _TRUNCATE,
                L"%s\\AORoomSpaceFix",
                localAppData) < 0)
        {
            return;
        }

        if (!CreateDirectoryW(directory, nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
        {
            return;
        }

        wchar_t path[MAX_PATH] = {};
        if (_snwprintf_s(
                path,
                sizeof(path) / sizeof(path[0]),
                _TRUNCATE,
                L"%s\\AORoomSpaceFix.log",
                directory) < 0)
        {
            return;
        }

        LogFile = CreateFileW(
            path,
            FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_DELETE,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (LogFile == INVALID_HANDLE_VALUE)
        {
            return;
        }

        SetFilePointer(LogFile, 0, nullptr, FILE_END);
    }

    void Log(const char* format, ...)
    {
        if (LogFile == INVALID_HANDLE_VALUE)
        {
            return;
        }

        SYSTEMTIME time = {};
        GetLocalTime(&time);

        char buffer[2048] = {};
        int prefixLength = _snprintf_s(
            buffer,
            sizeof(buffer),
            _TRUNCATE,
            "%04u-%02u-%02u %02u:%02u:%02u.%03u ",
            time.wYear,
            time.wMonth,
            time.wDay,
            time.wHour,
            time.wMinute,
            time.wSecond,
            time.wMilliseconds);
        if (prefixLength < 0)
        {
            return;
        }

        va_list arguments;
        va_start(arguments, format);
        int messageLength = _vsnprintf_s(
            buffer + prefixLength,
            sizeof(buffer) - static_cast<size_t>(prefixLength),
            _TRUNCATE,
            format,
            arguments);
        va_end(arguments);
        if (messageLength < 0)
        {
            messageLength = static_cast<int>(std::strlen(buffer + prefixLength));
        }

        int totalLength = prefixLength + messageLength;
        if (totalLength + 2 <= static_cast<int>(sizeof(buffer)))
        {
            buffer[totalLength++] = '\r';
            buffer[totalLength++] = '\n';
        }

        EnterCriticalSection(&LogLock);
        DWORD written = 0;
        WriteFile(LogFile, buffer, static_cast<DWORD>(totalLength), &written, nullptr);
        FlushFileBuffers(LogFile);
        LeaveCriticalSection(&LogLock);
    }
}
