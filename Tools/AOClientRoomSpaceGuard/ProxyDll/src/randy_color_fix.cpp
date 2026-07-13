#include "randy_color_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    uintptr_t DrawResourceFaultAddress = 0;
    uintptr_t ByteColorFaultAddress = 0;
    uintptr_t ByteColorResumeAddress = 0;
    uintptr_t DwordColorFaultAddress = 0;
    uintptr_t DwordColorResumeAddress = 0;

    bool IsReadableRange(const void* pointer, size_t size)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (!pointer || VirtualQuery(pointer, &memory, sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        DWORD readable = memory.Protect & 0xFF;
        if (readable != PAGE_READONLY && readable != PAGE_READWRITE &&
            readable != PAGE_WRITECOPY && readable != PAGE_EXECUTE_READ &&
            readable != PAGE_EXECUTE_READWRITE && readable != PAGE_EXECUTE_WRITECOPY)
        {
            return false;
        }

        uintptr_t address = reinterpret_cast<uintptr_t>(pointer);
        uintptr_t regionEnd = reinterpret_cast<uintptr_t>(memory.BaseAddress) +
            memory.RegionSize;
        return regionEnd >= address && regionEnd - address >= size;
    }

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
                reinterpret_cast<void*>(DrawResourceFaultAddress) &&
            exception->ContextRecord->Eax < 0x10000 &&
            exception->ExceptionRecord->ExceptionInformation[1] ==
                exception->ContextRecord->Eax)
        {
            DWORD frame = exception->ContextRecord->Ebp;
            if (!IsReadableRange(reinterpret_cast<const void*>(frame), 32))
            {
                return EXCEPTION_CONTINUE_SEARCH;
            }

            DWORD previousFrame = 0;
            DWORD returnAddress = 0;
            std::memcpy(
                &previousFrame,
                reinterpret_cast<const void*>(frame),
                sizeof(previousFrame));
            std::memcpy(
                &returnAddress,
                reinterpret_cast<const void*>(frame + 4),
                sizeof(returnAddress));
            if (returnAddress < 0x10000)
            {
                return EXCEPTION_CONTINUE_SEARCH;
            }

            exception->ContextRecord->Eax = 0;
            exception->ContextRecord->Eip = returnAddress;
            exception->ContextRecord->Esp = frame + 32;
            exception->ContextRecord->Ebp = previousFrame;
            return EXCEPTION_CONTINUE_EXECUTION;
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
        constexpr uint8_t ExpectedDrawResourceFaultSequence[] =
        {
            0x55, 0x8B, 0xEC,
            0xFF, 0x75, 0x1C,
            0x8B, 0x45, 0x08,
            0xFF, 0x75, 0x18,
            0x8B, 0x00,
            0xFF, 0x75, 0x14,
            0xFF, 0x75, 0x10,
            0xFF, 0x75, 0x0C,
            0xFF, 0x30,
            0xE8, 0xCB, 0xFE, 0xFF, 0xFF,
            0x5D,
            0xC2, 0x18, 0x00
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
                base + 0x21A88,
                ExpectedDrawResourceFaultSequence,
                sizeof(ExpectedDrawResourceFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x6C51B,
                ExpectedDwordFaultSequence,
                sizeof(ExpectedDwordFaultSequence)) != 0)
        {
            Log("ERROR unsupported randy31 renderer/color-read callsite");
            return false;
        }

        DrawResourceFaultAddress = reinterpret_cast<uintptr_t>(base + 0x21A94);
        ByteColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C3A1);
        ByteColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C3AC);
        DwordColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C51D);
        DwordColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C51F);
        if (!AddVectoredExceptionHandler(1, RandyColorExceptionGuard))
        {
            Log("ERROR randy31 color-read exception guard installation failed code=%lu",
                GetLastError());
            DrawResourceFaultAddress = 0;
            ByteColorFaultAddress = 0;
            ByteColorResumeAddress = 0;
            DwordColorFaultAddress = 0;
            DwordColorResumeAddress = 0;
            return false;
        }

        Log("PATCH PASS randy31 invalid renderer/color-pointer guard faultRvas=0x21A94,0x6C3A1,0x6C51D");
        return true;
    }
}
