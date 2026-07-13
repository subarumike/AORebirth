#include "gui_rect_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    void* OriginalRectAdd = nullptr;
    uintptr_t RectAddStartAddress = 0;
    uintptr_t RectAddEndAddress = 0;
    uintptr_t NewClientDrawHelperAddress = 0;
    LONG NewClientGuiDrawFaultCount = 0;

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

    bool IsSaneFloat(float value)
    {
        uint32_t bits = 0;
        std::memcpy(&bits, &value, sizeof(bits));
        if ((bits & 0x7F800000u) == 0x7F800000u)
        {
            return false;
        }

        return value > -1000000.0f && value < 1000000.0f;
    }

    bool __stdcall IsReadablePoint(const void* pointer)
    {
        if (!IsReadableRange(pointer, 8))
        {
            return false;
        }

        const float* values = reinterpret_cast<const float*>(pointer);
        return IsSaneFloat(values[0]) && IsSaneFloat(values[1]);
    }

    bool __stdcall IsReadableRect(const void* pointer)
    {
        if (!IsReadableRange(pointer, 16))
        {
            return false;
        }

        const float* values = reinterpret_cast<const float*>(pointer);
        return IsSaneFloat(values[0]) && IsSaneFloat(values[1]) &&
            IsSaneFloat(values[2]) && IsSaneFloat(values[3]);
    }

    void WriteEmptyRect(void* pointer)
    {
        if (!pointer)
        {
            return;
        }

        uint32_t zero = 0;
        std::memcpy(pointer, &zero, sizeof(zero));
        std::memcpy(static_cast<uint8_t*>(pointer) + 4, &zero, sizeof(zero));
        std::memcpy(static_cast<uint8_t*>(pointer) + 8, &zero, sizeof(zero));
        std::memcpy(static_cast<uint8_t*>(pointer) + 12, &zero, sizeof(zero));
    }

    bool IsExecutableAddress(uintptr_t address)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (!address ||
            VirtualQuery(
                reinterpret_cast<const void*>(address),
                &memory,
                sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        DWORD executable = memory.Protect & 0xFF;
        return executable == PAGE_EXECUTE ||
            executable == PAGE_EXECUTE_READ ||
            executable == PAGE_EXECUTE_READWRITE ||
            executable == PAGE_EXECUTE_WRITECOPY;
    }

    int __stdcall NewClientGuiDrawExceptionFilter(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        uintptr_t eip = exception->ContextRecord->Eip;
        if (IsExecutableAddress(eip))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        DWORD accessType = exception->ExceptionRecord->NumberParameters >= 1 ?
            static_cast<DWORD>(exception->ExceptionRecord->ExceptionInformation[0]) :
            0xFFFFFFFFu;
        uintptr_t accessAddress = exception->ExceptionRecord->NumberParameters >= 2 ?
            static_cast<uintptr_t>(exception->ExceptionRecord->ExceptionInformation[1]) :
            0;
        LONG count = InterlockedIncrement(&NewClientGuiDrawFaultCount);
        if (count <= 16 || count % 100 == 0)
        {
            aorf::Log(
                "PATCH HIT new-client GUI draw helper skipped faultEip=0x%08lX "
                "accessType=%lu accessAddress=0x%08lX count=%ld",
                static_cast<unsigned long>(eip),
                static_cast<unsigned long>(accessType),
                static_cast<unsigned long>(accessAddress),
                static_cast<long>(count));
        }

        return EXCEPTION_EXECUTE_HANDLER;
    }

    void __stdcall NewClientGuiDrawHelperGuardBody(
        void* self,
        uint32_t arg1,
        uint32_t arg2,
        uint32_t arg3,
        uint32_t arg4,
        uint32_t arg5,
        uint32_t arg6)
    {
        using DrawHelper = void(__thiscall*)(
            void*,
            uint32_t,
            uint32_t,
            uint32_t,
            uint32_t,
            uint32_t,
            uint32_t);
        DrawHelper original = reinterpret_cast<DrawHelper>(NewClientDrawHelperAddress);
        __try
        {
            original(self, arg1, arg2, arg3, arg4, arg5, arg6);
        }
        __except (NewClientGuiDrawExceptionFilter(GetExceptionInformation()))
        {
        }
    }

    __declspec(naked) void NewClientGuiDrawHelperGuard()
    {
        __asm
        {
            push ebp
            mov ebp, esp
            push dword ptr [ebp + 1Ch]
            push dword ptr [ebp + 18h]
            push dword ptr [ebp + 14h]
            push dword ptr [ebp + 10h]
            push dword ptr [ebp + 0Ch]
            push dword ptr [ebp + 08h]
            push ecx
            call NewClientGuiDrawHelperGuardBody
            mov esp, ebp
            pop ebp
            ret 18h
        }
    }

    void BuildRelativeCall(uint8_t* callsite, void* destination, uint8_t (&bytes)[5])
    {
        bytes[0] = 0xE8;
        int32_t displacement = static_cast<int32_t>(
            reinterpret_cast<uintptr_t>(destination) -
            (reinterpret_cast<uintptr_t>(callsite) + 5u));
        std::memcpy(bytes + 1, &displacement, sizeof(displacement));
    }

    LONG CALLBACK RectAddExceptionGuard(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        uintptr_t fault = reinterpret_cast<uintptr_t>(
            exception->ExceptionRecord->ExceptionAddress);
        if (fault < RectAddStartAddress || fault >= RectAddEndAddress)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        DWORD frame = exception->ContextRecord->Ebp;
        if (!IsReadableRange(reinterpret_cast<const void*>(frame), 16))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        DWORD previousFrame = 0;
        DWORD returnAddress = 0;
        DWORD resultPointer = 0;
        std::memcpy(&previousFrame, reinterpret_cast<const void*>(frame), sizeof(previousFrame));
        std::memcpy(&returnAddress, reinterpret_cast<const void*>(frame + 4), sizeof(returnAddress));
        std::memcpy(&resultPointer, reinterpret_cast<const void*>(frame + 8), sizeof(resultPointer));
        if (!IsReadableRange(reinterpret_cast<const void*>(resultPointer), 16))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        WriteEmptyRect(reinterpret_cast<void*>(resultPointer));
        exception->ContextRecord->Eax = resultPointer;
        exception->ContextRecord->Eip = returnAddress;
        exception->ContextRecord->Esp = frame + 16;
        exception->ContextRecord->Ebp = previousFrame;
        return EXCEPTION_CONTINUE_EXECUTION;
    }

    __declspec(naked) void GuiRectAddGuard()
    {
        __asm
        {
            test ecx, ecx
            jz invalid_rect
            push ecx
            push ecx
            call IsReadableRect
            test eax, eax
            pop ecx
            jz invalid_rect
            mov eax, dword ptr [esp + 8]
            push ecx
            push eax
            call IsReadablePoint
            test eax, eax
            pop ecx
            jnz valid_rect
        invalid_rect:
            mov eax, dword ptr [esp + 4]
            xor edx, edx
            mov dword ptr [eax], edx
            mov dword ptr [eax + 4], edx
            mov dword ptr [eax + 8], edx
            mov dword ptr [eax + 12], edx
            ret 8
        valid_rect:
            jmp dword ptr [OriginalRectAdd]
        }
    }
}

namespace aorf
{
    bool InstallGuiRectFix()
    {
        HMODULE gui = GetModuleHandleW(L"GUI.dll");
        HMODULE utils = GetModuleHandleW(L"Utils.dll");
        if (!gui || !utils)
        {
            Log("ERROR GUI rectangle repair modules are not loaded");
            return false;
        }

        auto guiBase = reinterpret_cast<uint8_t*>(gui);
        auto utilsBase = reinterpret_cast<uint8_t*>(utils);
        void** importSlot = reinterpret_cast<void**>(guiBase + 0x1A83D0);
        void* expectedTarget = utilsBase + 0x82E6;
        constexpr uint8_t ExpectedCallerPrefix[] =
        {
            0x8B, 0x4D, 0x0C, 0x50, 0x8D, 0x45, 0xB8, 0x50, 0xFF, 0x15
        };
        constexpr uint8_t ExpectedTarget[] =
        {
            0x55, 0x8B, 0xEC, 0x8B, 0x55, 0x0C, 0xD9, 0x02,
            0x8B, 0x45, 0x08, 0xD8, 0x01
        };

        uint32_t importedSlotOperand = 0;
        std::memcpy(&importedSlotOperand, guiBase + 0x14C4AB, sizeof(importedSlotOperand));
        if (std::memcmp(
                guiBase + 0x14C4A1,
                ExpectedCallerPrefix,
                sizeof(ExpectedCallerPrefix)) != 0 ||
            importedSlotOperand != reinterpret_cast<uint32_t>(importSlot) ||
            std::memcmp(expectedTarget, ExpectedTarget, sizeof(ExpectedTarget)) != 0 ||
            *importSlot != expectedTarget)
        {
            Log("ERROR unsupported GUI rectangle callsite");
            return false;
        }

        OriginalRectAdd = expectedTarget;
        RectAddStartAddress = reinterpret_cast<uintptr_t>(expectedTarget);
        RectAddEndAddress = RectAddStartAddress + sizeof(ExpectedTarget);
        if (!AddVectoredExceptionHandler(1, RectAddExceptionGuard))
        {
            Log("ERROR GUI rectangle exception guard installation failed code=%lu",
                GetLastError());
            OriginalRectAdd = nullptr;
            RectAddStartAddress = 0;
            RectAddEndAddress = 0;
            return false;
        }

        DWORD oldProtection = 0;
        if (!VirtualProtect(importSlot, sizeof(*importSlot), PAGE_READWRITE, &oldProtection))
        {
            Log("ERROR GUI rectangle import protection failed code=%lu", GetLastError());
            return false;
        }

        InterlockedExchangePointer(importSlot, reinterpret_cast<void*>(&GuiRectAddGuard));
        DWORD ignored = 0;
        bool restored = VirtualProtect(importSlot, sizeof(*importSlot), oldProtection, &ignored) != FALSE;
        bool installed = *importSlot == reinterpret_cast<void*>(&GuiRectAddGuard);
        if (!restored || !installed)
        {
            Log("ERROR GUI rectangle repair transaction failed restored=%s installed=%s",
                restored ? "true" : "false", installed ? "true" : "false");
            return false;
        }

        Log("PATCH PASS GUI rectangle data guard callsite=GUI+0x14C4A9 target=Utils+0x82E6");
        return true;
    }

    bool InstallNewClientGuiDrawFix()
    {
        HMODULE gui = GetModuleHandleW(L"GUI.dll");
        if (!gui)
        {
            Log("ERROR new-client GUI draw repair module is not loaded");
            return false;
        }

        auto guiBase = reinterpret_cast<uint8_t*>(gui);
        uint8_t* firstCallsite = guiBase + 0x14CC5F;
        uint8_t* secondCallsite = guiBase + 0x157234;
        void* expectedTarget = guiBase + 0x14CA77;
        constexpr uint8_t ExpectedFirstCall[] =
        {
            0xE8, 0x13, 0xFE, 0xFF, 0xFF
        };
        constexpr uint8_t ExpectedSecondCall[] =
        {
            0xE8, 0x3E, 0x58, 0xFF, 0xFF
        };
        constexpr uint8_t ExpectedHelperPrefix[] =
        {
            0xB8
        };
        constexpr uint8_t ExpectedHelperAfterRelocatedImmediate[] =
        {
            0xE8, 0x53, 0x6B, 0x02, 0x00,
            0x81, 0xEC, 0x80, 0x00, 0x00, 0x00,
            0x53, 0x8B, 0xD9
        };
        constexpr uint8_t ExpectedHelperEpilogue[] =
        {
            0x5F, 0x5E,
            0x8B, 0x4D, 0xF4,
            0x5B,
            0x64, 0x89, 0x0D, 0x00, 0x00, 0x00, 0x00,
            0xC9,
            0xC2, 0x18, 0x00
        };

        uint32_t relocatedSehCookie = 0;
        std::memcpy(
            &relocatedSehCookie,
            guiBase + 0x14CA78,
            sizeof(relocatedSehCookie));
        if (std::memcmp(firstCallsite, ExpectedFirstCall, sizeof(ExpectedFirstCall)) != 0 ||
            std::memcmp(secondCallsite, ExpectedSecondCall, sizeof(ExpectedSecondCall)) != 0 ||
            std::memcmp(
                guiBase + 0x14CA77,
                ExpectedHelperPrefix,
                sizeof(ExpectedHelperPrefix)) != 0 ||
            relocatedSehCookie != reinterpret_cast<uint32_t>(guiBase + 0x1A15C3) ||
            std::memcmp(
                guiBase + 0x14CA7C,
                ExpectedHelperAfterRelocatedImmediate,
                sizeof(ExpectedHelperAfterRelocatedImmediate)) != 0 ||
            std::memcmp(
                guiBase + 0x14CEED,
                ExpectedHelperEpilogue,
                sizeof(ExpectedHelperEpilogue)) != 0)
        {
            Log("ERROR unsupported new-client GUI draw helper callsite");
            return false;
        }

        uint8_t patchedFirstCall[5] = {};
        uint8_t patchedSecondCall[5] = {};
        BuildRelativeCall(
            firstCallsite,
            reinterpret_cast<void*>(&NewClientGuiDrawHelperGuard),
            patchedFirstCall);
        BuildRelativeCall(
            secondCallsite,
            reinterpret_cast<void*>(&NewClientGuiDrawHelperGuard),
            patchedSecondCall);

        DWORD firstOldProtection = 0;
        DWORD secondOldProtection = 0;
        if (!VirtualProtect(
                firstCallsite,
                sizeof(patchedFirstCall),
                PAGE_EXECUTE_READWRITE,
                &firstOldProtection))
        {
            Log("ERROR new-client GUI draw first-call protection failed code=%lu",
                GetLastError());
            return false;
        }
        if (!VirtualProtect(
                secondCallsite,
                sizeof(patchedSecondCall),
                PAGE_EXECUTE_READWRITE,
                &secondOldProtection))
        {
            DWORD ignored = 0;
            VirtualProtect(
                firstCallsite,
                sizeof(patchedFirstCall),
                firstOldProtection,
                &ignored);
            Log("ERROR new-client GUI draw second-call protection failed code=%lu",
                GetLastError());
            return false;
        }

        NewClientDrawHelperAddress = reinterpret_cast<uintptr_t>(expectedTarget);
        std::memcpy(firstCallsite, patchedFirstCall, sizeof(patchedFirstCall));
        std::memcpy(secondCallsite, patchedSecondCall, sizeof(patchedSecondCall));
        bool flushed = FlushInstructionCache(GetCurrentProcess(), nullptr, 0) != FALSE;
        bool firstRestored = false;
        bool secondRestored = false;
        DWORD ignored = 0;
        firstRestored = VirtualProtect(
            firstCallsite,
            sizeof(patchedFirstCall),
            firstOldProtection,
            &ignored) != FALSE;
        secondRestored = VirtualProtect(
            secondCallsite,
            sizeof(patchedSecondCall),
            secondOldProtection,
            &ignored) != FALSE;

        bool installed = flushed &&
            firstRestored &&
            secondRestored &&
            std::memcmp(firstCallsite, patchedFirstCall, sizeof(patchedFirstCall)) == 0 &&
            std::memcmp(secondCallsite, patchedSecondCall, sizeof(patchedSecondCall)) == 0;
        if (!installed)
        {
            Log(
                "ERROR new-client GUI draw repair transaction failed flushed=%s "
                "firstRestored=%s secondRestored=%s",
                flushed ? "true" : "false",
                firstRestored ? "true" : "false",
                secondRestored ? "true" : "false");
            return false;
        }

        Log("PATCH PASS new-client GUI draw helper guard callRvas=0x14CC5F,0x157234 target=GUI+0x14CA77");
        return true;
    }
}
