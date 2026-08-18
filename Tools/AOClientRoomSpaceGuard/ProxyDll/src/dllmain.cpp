// The version.dll loading pattern is derived from Inorien/AOReloaded (MIT).
// This RoomSpace-only build deliberately excludes AOReloaded's LAA, XML,
// settings, camera, input, and UI modifications.

#include "crash_dump.h"
#include "build_info.h"
#include "daily_login_routing.h"
#include "gui_rect_fix.h"
#include "login_key_patch.h"
#include "logging.h"
#include "randy_color_fix.h"
#include "roomspace_fix.h"

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
        aorf::Log(
            "START product=AORebirthClientPatch version=%s source=%s mode=combined pid=%lu",
            aorf::ClientPatchVersion,
            aorf::ClientPatchSourceSha,
            GetCurrentProcessId());
        const bool loginKeyWorkerStarted = aorf::StartLoginKeyPatchWorker();
        if (!loginKeyWorkerStarted)
        {
            aorf::Log("LOGINKEY patch=BLOCKED reason=start_worker");
        }

        if (!aorf::InstallEarlyRandyExceptionGuard())
        {
            aorf::Log("ERROR early randy31 exception guard was not installed");
        }
        aorf::InstallCrashDumpHandler();

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
                L"AORebirth Client Patch could not find N3.dll. "
                L"The login-key worker may still be active, but the client crash repairs are not.",
                L"AORebirth Client Patch",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        aorf::ClientProfile profile = aorf::GetLoadedN3ClientProfile();
        if (profile == aorf::ClientProfile::Unknown)
        {
            aorf::Log("ERROR client profile was not available before crash mitigation");
            MessageBoxW(
                nullptr,
                L"AORebirth Client Patch could not verify this client profile. "
                L"Close AO and review %LOCALAPPDATA%\\AORebirthClientPatch\\AORebirthClientPatch.log.",
                L"AORebirth Client Patch",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        if (profile == aorf::ClientProfile::OldClient)
        {
            if (!aorf::InstallGuiRectFix())
            {
                aorf::Log("ERROR GUI rectangle repair was not installed");
                MessageBoxW(
                    nullptr,
                    L"AORebirth Client Patch could not install the GUI crash repair. "
                    L"Close AO and review %LOCALAPPDATA%\\AORebirthClientPatch\\AORebirthClientPatch.log.",
                    L"AORebirth Client Patch",
                    MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
                return 1;
            }

            if (!aorf::InstallRandyColorFix())
            {
                aorf::Log("ERROR randy31 renderer repair was not installed");
                MessageBoxW(
                    nullptr,
                    L"AORebirth Client Patch could not install the renderer crash repair. "
                    L"Close AO and review %LOCALAPPDATA%\\AORebirthClientPatch\\AORebirthClientPatch.log.",
                    L"AORebirth Client Patch",
                    MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
                return 1;
            }
        }

        bool clientCrashMitigationInstalled = false;
        for (int attempt = 0; attempt < 10; ++attempt)
        {
            if (aorf::InstallClientCrashMitigation())
            {
                clientCrashMitigationInstalled = true;
                break;
            }

            aorf::Log(
                "WARN RoomSpace repair attempt failed attempt=%d retry=%s",
                attempt + 1,
                attempt + 1 < 10 ? "true" : "false");
            Sleep(250);
        }

        if (!clientCrashMitigationInstalled)
        {
            aorf::Log("ERROR RoomSpace repair was not installed");
            MessageBoxW(
                nullptr,
                L"AORebirth Client Patch could not verify or install the client crash repairs. "
                L"Close AO and review %LOCALAPPDATA%\\AORebirthClientPatch\\AORebirthClientPatch.log.",
                L"AORebirth Client Patch",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
            return 1;
        }

        const bool dailyLoginWorkerStarted = aorf::StartDailyLoginRoutingWorker();
        if (!dailyLoginWorkerStarted)
        {
            aorf::Log("DAILYLOGIN route=BLOCKED reason=start_worker");
        }

        if (profile == aorf::ClientProfile::NewClient)
        {
            if (!aorf::InstallNewClientGuiDrawFix())
            {
                aorf::Log("ERROR new-client GUI draw repair was not installed");
                MessageBoxW(
                    nullptr,
                    L"AORebirth Client Patch could not install the new-client GUI crash repair. "
                    L"Close AO and review %LOCALAPPDATA%\\AORebirthClientPatch\\AORebirthClientPatch.log.",
                    L"AORebirth Client Patch",
                    MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
                return 1;
            }

            aorf::Log("SKIP old-client-only GUI rectangle and renderer repairs");
            aorf::Log(
                "READY loginKeyWorker=%s dailyLoginWorker=%s RoomSpace and new-client GUI draw repairs active",
                loginKeyWorkerStarted ? "started" : "blocked",
                dailyLoginWorkerStarted ? "started" : "blocked");
            return 0;
        }

        aorf::Log(
            "READY loginKeyWorker=%s dailyLoginWorker=%s RoomSpace, GUI rectangle, and renderer repairs active",
            loginKeyWorkerStarted ? "started" : "blocked",
            dailyLoginWorkerStarted ? "started" : "blocked");
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
