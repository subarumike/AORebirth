#include "vehicle_collision_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

namespace
{
    constexpr DWORD CxxExceptionCode = 0xE06D7363;

    int VehicleExceptionFilter(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord ||
            exception->ExceptionRecord->ExceptionCode != CxxExceptionCode)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        return EXCEPTION_EXECUTE_HANDLER;
    }

    using VehicleCall1 = bool(__thiscall*)(void*, void*);
    using VehicleCall6 = bool(__thiscall*)(
        void*,
        void*,
        void*,
        void*,
        void*,
        int,
        void*);

    bool __stdcall SafeVehicleCall1(void* self, void* arg1)
    {
        __try
        {
            void** vtable = *reinterpret_cast<void***>(self);
            auto target = reinterpret_cast<VehicleCall1>(vtable[4]);
            return target(self, arg1);
        }
        __except (VehicleExceptionFilter(GetExceptionInformation()))
        {
            return false;
        }
    }

    bool __stdcall SafeVehicleCall6(
        void* self,
        void* arg1,
        void* arg2,
        void* arg3,
        void* arg4,
        int arg5,
        void* arg6)
    {
        __try
        {
            void** vtable = *reinterpret_cast<void***>(self);
            auto target = reinterpret_cast<VehicleCall6>(vtable[4]);
            return target(self, arg1, arg2, arg3, arg4, arg5, arg6);
        }
        __except (VehicleExceptionFilter(GetExceptionInformation()))
        {
            return false;
        }
    }

    __declspec(naked) void VehicleCall1Guard()
    {
        __asm
        {
            push dword ptr [esp + 4]
            push ecx
            call SafeVehicleCall1
            test al, al
            add esp, 4
            ret
        }
    }

    __declspec(naked) void VehicleCall6Guard()
    {
        __asm
        {
            push dword ptr [esp + 24]
            push dword ptr [esp + 24]
            push dword ptr [esp + 24]
            push dword ptr [esp + 24]
            push dword ptr [esp + 24]
            push dword ptr [esp + 24]
            push ecx
            call SafeVehicleCall6
            test al, al
            add esp, 24
            ret
        }
    }

    bool PatchCallAndTest(uint8_t* site, void* wrapper)
    {
        DWORD oldProtection = 0;
        if (!VirtualProtect(site, 5, PAGE_EXECUTE_READWRITE, &oldProtection))
        {
            aorf::Log(
                "ERROR Vehicle collision callsite protection failed site=0x%p code=%lu",
                site,
                GetLastError());
            return false;
        }

        intptr_t displacement = reinterpret_cast<uint8_t*>(wrapper) - (site + 5);
        site[0] = 0xE8;
        int32_t encoded = static_cast<int32_t>(displacement);
        std::memcpy(site + 1, &encoded, sizeof(encoded));

        bool flushed = FlushInstructionCache(GetCurrentProcess(), site, 5) != FALSE;
        DWORD ignored = 0;
        bool restored = VirtualProtect(site, 5, oldProtection, &ignored) != FALSE;
        if (!flushed || !restored)
        {
            aorf::Log(
                "ERROR Vehicle collision callsite patch incomplete site=0x%p flushed=%s restored=%s",
                site,
                flushed ? "true" : "false",
                restored ? "true" : "false");
            return false;
        }

        return true;
    }
}

namespace aorf
{
    bool InstallVehicleCollisionFix()
    {
        HMODULE vehicle = nullptr;
        for (int attempt = 0; attempt < 300; ++attempt)
        {
            vehicle = GetModuleHandleW(L"Vehicle.dll");
            if (vehicle)
            {
                break;
            }
            Sleep(100);
        }

        if (!vehicle)
        {
            Log("ERROR Vehicle.dll did not load within 30 seconds");
            return false;
        }

        auto base = reinterpret_cast<uint8_t*>(vehicle);
        uint8_t* call1 = base + 0xD832;
        uint8_t* call6a = base + 0xD887;
        uint8_t* call6b = base + 0xD8DC;
        constexpr uint8_t ExpectedCallAndTest[] =
        {
            0xFF, 0x50, 0x10,
            0x84, 0xC0
        };

        if (std::memcmp(call1, ExpectedCallAndTest, sizeof(ExpectedCallAndTest)) != 0 ||
            std::memcmp(call6a, ExpectedCallAndTest, sizeof(ExpectedCallAndTest)) != 0 ||
            std::memcmp(call6b, ExpectedCallAndTest, sizeof(ExpectedCallAndTest)) != 0)
        {
            Log("ERROR unsupported Vehicle collision callsite");
            return false;
        }

        if (!PatchCallAndTest(call1, reinterpret_cast<void*>(&VehicleCall1Guard)) ||
            !PatchCallAndTest(call6a, reinterpret_cast<void*>(&VehicleCall6Guard)) ||
            !PatchCallAndTest(call6b, reinterpret_cast<void*>(&VehicleCall6Guard)))
        {
            return false;
        }

        Log("PATCH PASS Vehicle collision exception guard callRvas=0xD832,0xD887,0xD8DC");
        return true;
    }
}
