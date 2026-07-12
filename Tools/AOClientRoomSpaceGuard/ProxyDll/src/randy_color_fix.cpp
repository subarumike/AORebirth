#include "randy_color_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    uintptr_t ByteColorFaultAddress = 0;
    uintptr_t ByteColorResumeAddress = 0;
    uintptr_t DwordColorFaultAddress = 0;
    uintptr_t DwordColorResumeAddress = 0;

    LONG CALLBACK RandyColorExceptionGuard(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 0)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(ByteColorFaultAddress) &&
            exception->ContextRecord->Eax < 0x10000)
        {
            exception->ContextRecord->Eax = 0;
            exception->ContextRecord->Ebx = 0;
            exception->ContextRecord->Edi = 0;
            exception->ContextRecord->Eip = static_cast<DWORD>(ByteColorResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(DwordColorFaultAddress) &&
            exception->ContextRecord->Esi < 0x10000)
        {
            exception->ContextRecord->Esi = 0;
            exception->ContextRecord->Eip = static_cast<DWORD>(DwordColorResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        return EXCEPTION_CONTINUE_SEARCH;
    }
}

namespace aorf
{
    bool InstallRandyColorFix()
    {
        HMODULE randy = GetModuleHandleW(L"randy31.dll");
        if (!randy)
        {
            Log("ERROR randy31.dll is not loaded");
            return false;
        }

        auto base = reinterpret_cast<uint8_t*>(randy);
        constexpr uint8_t ExpectedFaultSequence[] =
        {
            0x0F, 0xB6, 0x78, 0x02,
            0x0F, 0xB6, 0x58, 0x01,
            0x0F, 0xB6, 0x00
        };
        constexpr uint8_t ExpectedDwordFaultSequence[] =
        {
            0x8B, 0x36,
            0x8B, 0x36,
            0x81, 0xCE, 0x00, 0x00, 0x00, 0xFF
        };
        if (std::memcmp(
                base + 0x6C3A1,
                ExpectedFaultSequence,
                sizeof(ExpectedFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x6C51B,
                ExpectedDwordFaultSequence,
                sizeof(ExpectedDwordFaultSequence)) != 0)
        {
            Log("ERROR unsupported randy31 color-read callsite");
            return false;
        }

        ByteColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C3A1);
        ByteColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C3AC);
        DwordColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C51D);
        DwordColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C51F);
        if (!AddVectoredExceptionHandler(1, RandyColorExceptionGuard))
        {
            Log("ERROR randy31 color-read exception guard installation failed code=%lu",
                GetLastError());
            ByteColorFaultAddress = 0;
            ByteColorResumeAddress = 0;
            DwordColorFaultAddress = 0;
            DwordColorResumeAddress = 0;
            return false;
        }

        Log("PATCH PASS randy31 invalid color-pointer guard faultRvas=0x6C3A1,0x6C51D");
        return true;
    }
}
