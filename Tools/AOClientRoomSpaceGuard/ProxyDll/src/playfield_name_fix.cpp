#include "playfield_name_fix.h"

#include "logging.h"

#include <windows.h>

#include <cstdint>
#include <cstring>

// n3EngineClientAnarchy_t::N3Msg_GetPFName(uint) (Gamecode export; the LFT window's location column goes through it
// via N3InterfaceModule_t) is, in both client builds:
//
//   55             push ebp
//   8B EC          mov ebp, esp
//   FF 75 08       push [ebp+8]
//   E8 rel32       call <const char* __cdecl lookup(uint)>   ; static map of ~100 names, null when missing
//   59 5D C2 04 00 pop ecx; pop ebp; ret 4
//
// Only the call's rel32 is redirected, to a cdecl function of the same shape that keeps the client's own name and
// falls back to the generated table.

namespace
{
    struct PlayfieldName
    {
        unsigned int id;
        const char* name;
    };

    const PlayfieldName Names[] = {
#include "playfield_names.inc"
    };

    const char GetPFNameExport[] = "?N3Msg_GetPFName@n3EngineClientAnarchy_t@@QBEPBDI@Z";
    const uint8_t ExpectedPrologue[] = { 0x55, 0x8B, 0xEC, 0xFF, 0x75, 0x08, 0xE8 };
    const uint8_t ExpectedEpilogue[] = { 0x59, 0x5D, 0xC2, 0x04, 0x00 };

    using LookupFn = const char*(__cdecl*)(unsigned int);
    LookupFn OriginalLookup = nullptr;

    const char* FindGeneratedName(unsigned int id)
    {
        size_t low = 0;
        size_t high = sizeof(Names) / sizeof(Names[0]);
        while (low < high)
        {
            size_t mid = low + (high - low) / 2;
            if (Names[mid].id < id)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low < sizeof(Names) / sizeof(Names[0]) && Names[low].id == id ? Names[low].name : nullptr;
    }

    const char* __cdecl PlayfieldNameLookup(unsigned int id)
    {
        const char* name = OriginalLookup(id);
        return name ? name : FindGeneratedName(id);
    }
}

namespace aorf
{
    bool InstallPlayfieldNameFix()
    {
        HMODULE gamecode = nullptr;
        for (int attempt = 0; attempt < 300 && !gamecode; ++attempt)
        {
            gamecode = GetModuleHandleW(L"Gamecode.dll");
            if (!gamecode)
            {
                Sleep(100);
            }
        }
        if (!gamecode)
        {
            Log("PFNAME patch=BLOCKED reason=gamecode_not_loaded");
            return false;
        }

        uint8_t* function = reinterpret_cast<uint8_t*>(GetProcAddress(gamecode, GetPFNameExport));
        if (!function ||
            std::memcmp(function, ExpectedPrologue, sizeof(ExpectedPrologue)) != 0 ||
            std::memcmp(function + 11, ExpectedEpilogue, sizeof(ExpectedEpilogue)) != 0)
        {
            Log("PFNAME patch=BLOCKED reason=unsupported_gamecode export=%p", function);
            return false;
        }

        uint8_t* rel = function + 7;
        uint8_t* next = function + 11;
        int32_t originalRel = 0;
        std::memcpy(&originalRel, rel, sizeof(originalRel));
        OriginalLookup = reinterpret_cast<LookupFn>(next + originalRel);
        int32_t hookRel = static_cast<int32_t>(
            reinterpret_cast<uintptr_t>(&PlayfieldNameLookup) - reinterpret_cast<uintptr_t>(next));

        DWORD oldProtection = 0;
        if (!VirtualProtect(rel, sizeof(hookRel), PAGE_EXECUTE_READWRITE, &oldProtection))
        {
            Log("PFNAME patch=BLOCKED reason=protect code=%lu", GetLastError());
            return false;
        }
        std::memcpy(rel, &hookRel, sizeof(hookRel));
        bool flushed = FlushInstructionCache(GetCurrentProcess(), rel, sizeof(hookRel)) != FALSE;
        DWORD ignored = 0;
        bool restored = VirtualProtect(rel, sizeof(hookRel), oldProtection, &ignored) != FALSE;

        Log(
            "PFNAME patch=%s names=%u export=%p original=%p flushed=%s restored=%s",
            flushed && restored ? "ACTIVE" : "PARTIAL",
            static_cast<unsigned>(sizeof(Names) / sizeof(Names[0])),
            function,
            reinterpret_cast<void*>(OriginalLookup),
            flushed ? "true" : "false",
            restored ? "true" : "false");
        return true;
    }
}
