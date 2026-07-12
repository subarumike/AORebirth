#include "randy_color_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    uintptr_t FaultAddress = 0;
    uintptr_t ResumeAddress = 0;

    LONG CALLBACK RandyColorExceptionGuard(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->ExceptionAddress !=
                reinterpret_cast<void*>(FaultAddress) ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 0 ||
            exception->ContextRecord->Eax >= 0x10000)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        exception->ContextRecord->Eax = 0;
        exception->ContextRecord->Ebx = 0;
        exception->ContextRecord->Edi = 0;
        exception->ContextRecord->Eip = static_cast<DWORD>(ResumeAddress);
        return EXCEPTION_CONTINUE_EXECUTION;
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
        if (std::memcmp(
                base + 0x6C3A1,
                ExpectedFaultSequence,
                sizeof(ExpectedFaultSequence)) != 0)
        {
            Log("ERROR unsupported randy31 color-read callsite");
            return false;
        }

        FaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C3A1);
        ResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C3AC);
        if (!AddVectoredExceptionHandler(1, RandyColorExceptionGuard))
        {
            Log("ERROR randy31 color-read exception guard installation failed code=%lu",
                GetLastError());
            FaultAddress = 0;
            ResumeAddress = 0;
            return false;
        }

        Log("PATCH PASS randy31 invalid color-pointer guard faultRva=0x6C3A1");
        return true;
    }
}
