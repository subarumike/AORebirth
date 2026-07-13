// The version.dll loading pattern is derived from Inorien/AOReloaded (MIT).
// This RoomSpace-only build deliberately excludes AOReloaded's LAA, XML,
// settings, camera, input, and UI modifications.

#include "logging.h"
#include "gui_rect_fix.h"
#include "randy_color_fix.h"
#include "roomspace_fix.h"
#include "vehicle_collision_fix.h"

#include <windows.h>

namespace
{
    LONG WorkerStarted = 0;

    bool IsAnarchyOnlineProcess()
    {
        wchar_t path[MAX_PATH] = {};
        DWORD length = GetModuleFileNameW(
            nullptr,
            path,
            static_cast<DWORD>(sizeof(path) / sizeof(path[0])));
        if (length == 0 || length >= sizeof(path) / sizeof(path[0]))
        {
            return false;
        }

        const wchar_t* fileName = path;
        for (const wchar_t* cursor = path; *cursor; ++cursor)
        {
            if (*cursor == L'\\' || *cursor == L'/')
            {
                fileName = cursor + 1;
            }
        }

        return _wcsicmp(fileName, L"AnarchyOnline.exe") == 0;
    }

    DWORD WINAPI DeferredInstall(LPVOID)
    {
        aorf::LogInit();
        aorf::Log("START version=1 pid=%lu", GetCurrentProcessId());

        HMODULE n3 = nullptr;
        for (int attempt = 0; attempt < 300; ++attempt)
        {
            n3 = GetModuleHandleW(L"N3.dll");
            if (n3)
            {
                break;
            }
            Sleep(100);
        }

        if (!n3)
        {
            aorf::Log("ERROR N3.dll did not load within 30 seconds");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not find N3.dll. The client was not protected.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        if (!aorf::InstallRoomSpaceFix())
        {
            aorf::Log("ERROR RoomSpace repair was not installed");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not verify or patch this client. "
                L"Close AO and review %LOCALAPPDATA%\\AORoomSpaceFix\\AORoomSpaceFix.log.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        aorf::ClientProfile profile = aorf::GetLoadedN3ClientProfile();
        if (profile == aorf::ClientProfile::Unknown)
        {
            aorf::Log("ERROR client profile was not available after RoomSpace repair");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not verify this client profile. "
                L"Close AO and review %LOCALAPPDATA%\\AORoomSpaceFix\\AORoomSpaceFix.log.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        if (profile == aorf::ClientProfile::NewClient)
        {
            aorf::Log("SKIP old-client-only GUI rectangle and renderer repairs");
            aorf::Log("READY RoomSpace repair active");
            return 0;
        }

        if (!aorf::InstallGuiRectFix())
        {
            aorf::Log("ERROR GUI rectangle repair was not installed");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not install the GUI crash repair. "
                L"Close AO and review %LOCALAPPDATA%\\AORoomSpaceFix\\AORoomSpaceFix.log.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        if (!aorf::InstallRandyColorFix())
        {
            aorf::Log("ERROR randy31 color-pointer repair was not installed");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not install the renderer crash repair. "
                L"Close AO and review %LOCALAPPDATA%\\AORoomSpaceFix\\AORoomSpaceFix.log.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        if (!aorf::InstallVehicleCollisionFix())
        {
            aorf::Log("ERROR Vehicle collision repair was not installed");
            MessageBoxW(
                nullptr,
                L"AORoomSpaceFix could not install the Vehicle crash repair. "
                L"Close AO and review %LOCALAPPDATA%\\AORoomSpaceFix\\AORoomSpaceFix.log.",
                L"AO RoomSpace Fix",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        aorf::Log("READY RoomSpace, GUI rectangle, renderer, and Vehicle repairs active");
        return 0;
    }

    void OnProcessAttach()
    {
        if (!IsAnarchyOnlineProcess() ||
            InterlockedCompareExchange(&WorkerStarted, 1, 0) != 0)
        {
            return;
        }

        HANDLE worker = CreateThread(nullptr, 0, DeferredInstall, nullptr, 0, nullptr);
        if (worker)
        {
            CloseHandle(worker);
        }
    }
}

BOOL APIENTRY DllMain(HINSTANCE, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        OnProcessAttach();
    }
    return TRUE;
}
