#include "gui_rect_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    void* OriginalRectAdd = nullptr;

    bool __stdcall IsReadableRect(const void* pointer)
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
        return regionEnd >= address && regionEnd - address >= 16;
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

        Log("PATCH PASS GUI rectangle pointer guard callsite=GUI+0x14C4A9 target=Utils+0x82E6");
        return true;
    }
}
