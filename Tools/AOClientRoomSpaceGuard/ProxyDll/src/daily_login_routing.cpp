#include "daily_login_routing.h"

#include "logging.h"
#include "login_key_patch.h"
#include "roomspace_fix.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <tlhelp32.h>

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

namespace aorf
{
    namespace
    {
        using FnGetAddrInfoA = int(WSAAPI*)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
        using FnGetAddrInfoW = int(WSAAPI*)(PCWSTR, PCWSTR, const ADDRINFOW*, PADDRINFOW*);
        using FnGetHostByName = hostent*(WSAAPI*)(const char*);
        using FnSend = int(WSAAPI*)(SOCKET, const char*, int, int);
        using FnGetProcAddress = FARPROC(WINAPI*)(HMODULE, LPCSTR);
        using FnWSASend = int(WSAAPI*)(
            SOCKET,
            LPWSABUF,
            DWORD,
            LPDWORD,
            DWORD,
            LPWSAOVERLAPPED,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE);

        LONG WorkerStarted = 0;
        bool RoutingEnabled = false;
        LoginServerSelection RoutingTarget = {};
        HMODULE Ws2 = nullptr;
        FnGetAddrInfoA RealGetAddrInfoA = nullptr;
        FnGetAddrInfoW RealGetAddrInfoW = nullptr;
        FnGetHostByName RealGetHostByName = nullptr;
        FnSend RealSend = nullptr;
        FnWSASend RealWSASend = nullptr;
        FnGetProcAddress RealGetProcAddressKernel32 = nullptr;
        FnGetProcAddress RealGetProcAddressKernelBase = nullptr;

        int WSAAPI HookGetAddrInfoA(
            PCSTR nodeName,
            PCSTR serviceName,
            const ADDRINFOA* hints,
            PADDRINFOA* result);
        int WSAAPI HookGetAddrInfoW(
            PCWSTR nodeName,
            PCWSTR serviceName,
            const ADDRINFOW* hints,
            PADDRINFOW* result);
        hostent* WSAAPI HookGetHostByName(const char* name);
        int WSAAPI HookSend(SOCKET socket, const char* buffer, int length, int flags);
        FARPROC WINAPI HookGetProcAddress(HMODULE module, LPCSTR procName);
        int WSAAPI HookWSASend(
            SOCKET socket,
            LPWSABUF buffers,
            DWORD bufferCount,
            LPDWORD bytesSent,
            DWORD flags,
            LPWSAOVERLAPPED overlapped,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE completionRoutine);

        std::string ToLower(std::string value)
        {
            std::transform(
                value.begin(),
                value.end(),
                value.begin(),
                [](unsigned char character)
                {
                    return static_cast<char>(std::tolower(character));
                });
            return value;
        }

        bool IsDailyLoginHost(const std::string& host)
        {
            const std::string lower = ToLower(host);
            return (!lower.empty() && lower == RoutingTarget.originalDailyHost) ||
                std::find(RoutingTarget.dailyHosts.begin(), RoutingTarget.dailyHosts.end(), lower) !=
                    RoutingTarget.dailyHosts.end();
        }

        bool IsOrdinalProcedureName(LPCSTR procName)
        {
            return !procName ||
                ((reinterpret_cast<uintptr_t>(procName) & ~static_cast<uintptr_t>(0xffff)) == 0);
        }

        bool IsWs2Module(HMODULE module)
        {
            return module &&
                (module == Ws2 || module == GetModuleHandleW(L"ws2_32.dll"));
        }

        bool IsHttpRequestPrefix(const std::string& value)
        {
            return value.rfind("GET ", 0) == 0 ||
                value.rfind("POST ", 0) == 0 ||
                value.rfind("HEAD ", 0) == 0;
        }

        std::string RightPaddedHostReplacement(const std::string& host)
        {
            std::string replacement = host;
            replacement.append(RoutingTarget.originalDailyHost.size() - replacement.size(), ' ');
            return replacement;
        }

        bool ReplaceAll(std::string& value, const std::string& from, const std::string& to)
        {
            bool replaced = false;
            size_t offset = 0;
            while ((offset = value.find(from, offset)) != std::string::npos)
            {
                value.replace(offset, from.size(), to);
                offset += to.size();
                replaced = true;
            }
            return replaced;
        }

        bool RewriteDailyLoginHttpRequest(
            const char* buffer,
            int length,
            std::vector<char>& rewritten)
        {
            if (!buffer || length <= 0 || length > 65536)
            {
                return false;
            }

            std::string request(buffer, buffer + length);
            if (!IsHttpRequestPrefix(request))
            {
                return false;
            }

            if (RoutingTarget.originalDailyHost.empty() || RoutingTarget.dailyHosts.empty())
            {
                return false;
            }
            const std::string& targetHost = RoutingTarget.dailyHosts.front();
            const std::string& originalHost = RoutingTarget.originalDailyHost;
            const std::string lower = ToLower(request);
            const bool matchedDailyRewards = lower.find(originalHost) != std::string::npos;
            const bool matchedAlias = std::any_of(
                RoutingTarget.dailyHosts.begin(), RoutingTarget.dailyHosts.end(), [&](const std::string& host)
                { return lower.find(host) != std::string::npos; });
            if (!matchedDailyRewards && !matchedAlias)
            {
                return false;
            }

            bool changed = false;
            changed |= ReplaceAll(
                request,
                "Host: " + originalHost,
                "Host: " + RightPaddedHostReplacement(targetHost));
            changed |= ReplaceAll(
                request,
                "host: " + originalHost,
                "host: " + RightPaddedHostReplacement(targetHost));
            changed |= ReplaceAll(
                request,
                "http://" + originalHost,
                "http://" + targetHost);
            for (size_t i = 1; i < RoutingTarget.dailyHosts.size(); ++i)
            {
                changed |= ReplaceAll(request, RoutingTarget.dailyHosts[i], targetHost);
            }

            if (!changed)
            {
                return false;
            }

            rewritten.assign(request.begin(), request.end());
            Log(
                "DAILYLOGIN route=http_rewrite hostDailyRewards=%s hostAlias=%s bytesIn=%lu bytesOut=%lu",
                matchedDailyRewards ? "true" : "false",
                matchedAlias ? "true" : "false",
                static_cast<unsigned long>(length),
                static_cast<unsigned long>(rewritten.size()));
            return true;
        }

        bool RewriteDailyLoginHttpRequestFromWsabufs(
            LPWSABUF buffers,
            DWORD bufferCount,
            std::vector<char>& rewritten)
        {
            if (!buffers || bufferCount == 0)
            {
                return false;
            }

            if (bufferCount == 1)
            {
                return RewriteDailyLoginHttpRequest(
                    buffers[0].buf,
                    static_cast<int>(buffers[0].len),
                    rewritten);
            }

            DWORD totalLength = 0;
            for (DWORD index = 0; index < bufferCount; ++index)
            {
                if (!buffers[index].buf ||
                    buffers[index].len > 65536 ||
                    totalLength > 65536 - buffers[index].len)
                {
                    return false;
                }

                totalLength += buffers[index].len;
            }

            std::vector<char> combined;
            combined.reserve(totalLength);
            for (DWORD index = 0; index < bufferCount; ++index)
            {
                combined.insert(
                    combined.end(),
                    buffers[index].buf,
                    buffers[index].buf + buffers[index].len);
            }

            return RewriteDailyLoginHttpRequest(
                combined.data(),
                static_cast<int>(combined.size()),
                rewritten);
        }

        FARPROC ResolveWs2(const char* name)
        {
            if (!Ws2)
            {
                Ws2 = LoadLibraryW(L"ws2_32.dll");
            }

            if (!Ws2)
            {
                return nullptr;
            }

            if (RealGetProcAddressKernel32)
            {
                return RealGetProcAddressKernel32(Ws2, name);
            }

            return GetProcAddress(Ws2, name);
        }

        bool ResolveKernelFunctions()
        {
            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
            HMODULE kernelBase = GetModuleHandleW(L"kernelbase.dll");

            RealGetProcAddressKernel32 = kernel32
                ? reinterpret_cast<FnGetProcAddress>(GetProcAddress(kernel32, "GetProcAddress"))
                : nullptr;
            RealGetProcAddressKernelBase = kernelBase
                ? reinterpret_cast<FnGetProcAddress>(GetProcAddress(kernelBase, "GetProcAddress"))
                : nullptr;

            return RealGetProcAddressKernel32 != nullptr;
        }

        bool ResolveWs2Functions()
        {
            if (!ResolveKernelFunctions())
            {
                return false;
            }

            RealGetAddrInfoA = reinterpret_cast<FnGetAddrInfoA>(ResolveWs2("getaddrinfo"));
            RealGetAddrInfoW = reinterpret_cast<FnGetAddrInfoW>(ResolveWs2("GetAddrInfoW"));
            RealGetHostByName = reinterpret_cast<FnGetHostByName>(ResolveWs2("gethostbyname"));
            RealSend = reinterpret_cast<FnSend>(ResolveWs2("send"));
            RealWSASend = reinterpret_cast<FnWSASend>(ResolveWs2("WSASend"));
            return RealGetAddrInfoA && RealGetAddrInfoW && RealGetHostByName && RealSend && RealWSASend;
        }

        bool PatchImport(HMODULE module, const char* importModule, const char* name, void* original, void* replacement)
        {
            __try
            {
                if (!module || !original || !replacement)
                {
                    return false;
                }

                auto* base = reinterpret_cast<unsigned char*>(module);
                auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
                if (dos->e_magic != IMAGE_DOS_SIGNATURE)
                {
                    return false;
                }

                auto* nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
                if (nt->Signature != IMAGE_NT_SIGNATURE)
                {
                    return false;
                }

                IMAGE_DATA_DIRECTORY importDirectory =
                    nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
                if (importDirectory.VirtualAddress == 0)
                {
                    return false;
                }

                auto* descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
                    base + importDirectory.VirtualAddress);
                bool patched = false;
                for (; descriptor->Name; ++descriptor)
                {
                    const char* moduleName = reinterpret_cast<const char*>(base + descriptor->Name);
                    if (_stricmp(moduleName, importModule) != 0)
                    {
                        continue;
                    }

                    auto* thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                        base + descriptor->FirstThunk);
                    auto* originalThunk = descriptor->OriginalFirstThunk
                        ? reinterpret_cast<IMAGE_THUNK_DATA*>(base + descriptor->OriginalFirstThunk)
                        : nullptr;
                    for (; thunk->u1.Function; ++thunk)
                    {
                        if (originalThunk &&
                            (originalThunk->u1.Ordinal & IMAGE_ORDINAL_FLAG))
                        {
                            ++originalThunk;
                            continue;
                        }

                        if (originalThunk)
                        {
                            auto* importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                                base + originalThunk->u1.AddressOfData);
                            if (std::strcmp(reinterpret_cast<const char*>(importByName->Name), name) != 0)
                            {
                                ++originalThunk;
                                continue;
                            }
                            ++originalThunk;
                        }

                        if (reinterpret_cast<void*>(thunk->u1.Function) != original)
                        {
                            continue;
                        }

                        DWORD oldProtection = 0;
                        if (!VirtualProtect(
                                &thunk->u1.Function,
                                sizeof(thunk->u1.Function),
                                PAGE_READWRITE,
                                &oldProtection))
                        {
                            continue;
                        }

                        thunk->u1.Function = reinterpret_cast<ULONG_PTR>(replacement);
                        DWORD ignored = 0;
                        VirtualProtect(
                            &thunk->u1.Function,
                            sizeof(thunk->u1.Function),
                            oldProtection,
                            &ignored);
                        patched = true;
                    }
                }

                return patched;
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                return false;
            }
        }

        using AweJsCallback = void(__cdecl*)(void*, const void*, const void*, const void*);
        using AweFinishCallback = void(__cdecl*)(void*);
        using FnSetJsCallback = void(__cdecl*)(void*, AweJsCallback);
        using FnSetFinishCallback = void(__cdecl*)(void*, AweFinishCallback);
        using FnAweDestroy = void(__cdecl*)(void*);
        using FnAweGetUrl = void*(__cdecl*)(void*);
        using FnAweString = void*(__cdecl*)(const char*, unsigned int);
        using FnAweStringDestroy = void(__cdecl*)(void*);
        using FnAweToUtf8 = unsigned int(__cdecl*)(const void*, char*, unsigned int);
        using FnAweCreateObject = void(__cdecl*)(void*, const void*);
        using FnAweObjectCallback = void(__cdecl*)(void*, const void*, const void*);
        using FnAweArraySize = unsigned int(__cdecl*)(const void*);
        using FnInterfaceInstance = void*(__cdecl*)();
        using FnRequestClaims = void(__thiscall*)(void*);

        struct BridgeModule
        {
            const wchar_t* name;
            const char* hash;
            HMODULE handle;
        };
        BridgeModule BridgeModules[] =
        {
            { L"GUI.dll", "e485384721e2fe13972e840dfb6a9fe29b1ba4eb71b42cc049e5097a570b6de1", nullptr },
            { L"Interfaces.dll", "3aa79a44e76c3413543404058c5d07323bd3b69f4c3493c7b136befa1a55b0a7", nullptr },
            { L"Gamecode.dll", "654969a6b65946cb161f0e60aed8589260fc5eca1795488f66bb56f8fff73726", nullptr },
            { L"Awesomium.dll", "4965938928358ad7e99f7465806324336f49ca51e467ed0ac36a4ba65139b2d0", nullptr }
        };
        LONG NativeBridgeReady = 0;
        bool NativeBridgeRejected = false;
        bool NativeBridgeEndpoint = false;
        FnSetJsCallback RealSetJsCallback = nullptr;
        FnSetFinishCallback RealSetFinishCallback = nullptr;
        FnAweDestroy RealAweDestroy = nullptr;
        FnAweGetUrl AweGetUrl = nullptr;
        FnAweString AweString = nullptr;
        FnAweStringDestroy AweStringDestroy = nullptr;
        FnAweToUtf8 AweToUtf8 = nullptr;
        FnAweCreateObject AweCreateObject = nullptr;
        FnAweObjectCallback AweObjectCallback = nullptr;
        FnAweArraySize AweArraySize = nullptr;
        FnInterfaceInstance InterfaceInstance = nullptr;
        FnRequestClaims RequestClaims = nullptr;

        struct BridgeView
        {
            void* view = nullptr;
            DWORD thread = 0;
            AweJsCallback js = nullptr;
            AweFinishCallback finish = nullptr;
            bool registered = false;
            bool destroying = false;
        };
        SRWLOCK BridgeViewsLock = SRWLOCK_INIT;
        BridgeView BridgeViews[32];

        bool AllowedDailyBridgeUrl(const char* url)
        {
            if (!url || RoutingTarget.originalDailyHost.empty())
            {
                return false;
            }
            const std::string lower = ToLower(url);
            const std::string original = "http://" + RoutingTarget.originalDailyHost;
            if (lower == original + "/" || lower == original + "/index.app")
            {
                return true;
            }
            return std::any_of(RoutingTarget.dailyHosts.begin(), RoutingTarget.dailyHosts.end(),
                [&](const std::string& host) { return lower == "http://" + host + "/index.app"; });
        }

        bool CanDispatchDailyBridge(bool ready, bool endpoint, bool origin, const BridgeView& view, DWORD thread)
        {
            return ready && endpoint && origin && view.view && view.registered && !view.destroying &&
                view.thread != 0 && view.thread == thread && view.js && view.finish;
        }

        BridgeView SnapshotBridgeView(void* view)
        {
            BridgeView result;
            AcquireSRWLockShared(&BridgeViewsLock);
            for (const auto& entry : BridgeViews) if (entry.view == view) { result = entry; break; }
            ReleaseSRWLockShared(&BridgeViewsLock);
            return result;
        }

        bool RecordBridgeView(void* view, AweJsCallback js, AweFinishCallback finish)
        {
            AcquireSRWLockExclusive(&BridgeViewsLock);
            BridgeView* selected = nullptr;
            for (auto& entry : BridgeViews) if (entry.view == view) { selected = &entry; break; }
            if (!selected) for (auto& entry : BridgeViews) if (!entry.view) { selected = &entry; break; }
            const bool recorded = selected && (!selected->view || selected->thread == GetCurrentThreadId());
            if (recorded) {
                selected->view = view;
                selected->thread = GetCurrentThreadId();
                if (js) selected->js = js;
                if (finish) selected->finish = finish;
            }
            ReleaseSRWLockExclusive(&BridgeViewsLock);
            return recorded;
        }

        bool NativeBridgeModulesMatch()
        {
            for (const auto& module : BridgeModules)
                if (!module.handle || GetModuleHandleW(module.name) != module.handle ||
                    !VerifyLoadedModuleFileHash(module.handle, module.hash)) return false;
            return true;
        }

        FARPROC BridgeExport(HMODULE module, const char* name)
        {
            FARPROC result = RealGetProcAddressKernel32(module, name);
            MEMORY_BASIC_INFORMATION memory = {};
            return result && VirtualQuery(reinterpret_cast<const void*>(result), &memory, sizeof(memory)) == sizeof(memory) &&
                memory.AllocationBase == module ? result : nullptr;
        }

        bool ReadAweString(const void* text, char* output, unsigned int capacity)
        {
            if (!text || !output || capacity == 0) return false;
            const unsigned int length = AweToUtf8(text, nullptr, 0);
            if (length >= capacity) return false;
            output[0] = 0;
            AweToUtf8(text, output, length + 1);
            output[length] = 0;
            return true;
        }

        bool AllowedBridgeViewOrigin(void* view)
        {
            void* url = AweGetUrl(view); // This C API returns an owned awe_string.
            char text[1024] = {};
            bool allowed = url && ReadAweString(url, text, sizeof(text)) && AllowedDailyBridgeUrl(text);
            if (url) AweStringDestroy(url);
            return allowed;
        }

        void __cdecl HookAweJsCallback(void* view, const void* object, const void* method, const void* arguments)
        {
            BridgeView entry = SnapshotBridgeView(view);
            char objectName[64] = {}, methodName[64] = {};
            const bool ours = ReadAweString(object, objectName, sizeof(objectName)) &&
                ReadAweString(method, methodName, sizeof(methodName)) &&
                std::strcmp(objectName, "AORebirthDaily") == 0 && std::strcmp(methodName, "Claim") == 0;
            if (!ours) { if (entry.js) entry.js(view, object, method, arguments); return; }
            // No worker-thread native calls, alternate packets, or changes to the native guard.
            if (!CanDispatchDailyBridge(InterlockedCompareExchange(&NativeBridgeReady, 0, 0) == 1,
                    NativeBridgeEndpoint, true, entry, GetCurrentThreadId())) {
                Log("DAILYLOGIN native=BLOCKED reason=view_thread"); return;
            }
            if (!NativeBridgeModulesMatch() || !AllowedBridgeViewOrigin(view) ||
                !arguments || AweArraySize(arguments) != 0) {
                Log("DAILYLOGIN native=BLOCKED reason=identity_origin_or_arguments"); return;
            }
            void* instance = InterfaceInstance();
            MEMORY_BASIC_INFORMATION instanceMemory = {};
            if (!instance || VirtualQuery(instance, &instanceMemory, sizeof(instanceMemory)) != sizeof(instanceMemory) ||
                instanceMemory.State != MEM_COMMIT || (instanceMemory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0) {
                Log("DAILYLOGIN native=BLOCKED reason=no_interface"); return;
            }
            RequestClaims(instance);
            Log("DAILYLOGIN native=REQUESTED action=263");
        }

        void __cdecl HookAweFinishLoading(void* view)
        {
            BridgeView entry = SnapshotBridgeView(view);
            if (entry.finish) entry.finish(view);
            entry = SnapshotBridgeView(view); // The original callback may have destroyed the view.
            if (!entry.view || entry.destroying || entry.thread != GetCurrentThreadId() || !entry.js ||
                !NativeBridgeEndpoint || !NativeBridgeModulesMatch() || !AllowedBridgeViewOrigin(view)) return;
            void* object = AweString("AORebirthDaily", 14);
            void* method = AweString("Claim", 5);
            if (object && method) {
                AweCreateObject(view, object);
                AweObjectCallback(view, object, method);
                AcquireSRWLockExclusive(&BridgeViewsLock);
                for (auto& current : BridgeViews) if (current.view == view) current.registered = true;
                ReleaseSRWLockExclusive(&BridgeViewsLock);
                Log("DAILYLOGIN native=READY api=AORebirthDaily.Claim");
            }
            if (method) AweStringDestroy(method);
            if (object) AweStringDestroy(object);
        }

        void __cdecl HookSetAweJsCallback(void* view, AweJsCallback callback)
        {
            const auto expected = reinterpret_cast<AweJsCallback>(reinterpret_cast<unsigned char*>(BridgeModules[0].handle) + 0x10c39e);
            if (view && callback == expected && RecordBridgeView(view, callback, nullptr)) {
                RealSetJsCallback(view, HookAweJsCallback);
            } else RealSetJsCallback(view, callback);
        }

        void __cdecl HookSetAweFinishCallback(void* view, AweFinishCallback callback)
        {
            const auto expected = reinterpret_cast<AweFinishCallback>(reinterpret_cast<unsigned char*>(BridgeModules[0].handle) + 0x10b2b8);
            if (view && callback == expected && RecordBridgeView(view, nullptr, callback)) {
                RealSetFinishCallback(view, HookAweFinishLoading);
            } else RealSetFinishCallback(view, callback);
        }

        void __cdecl HookAweDestroy(void* view)
        {
            AcquireSRWLockExclusive(&BridgeViewsLock);
            for (auto& entry : BridgeViews) if (entry.view == view) entry.destroying = true;
            ReleaseSRWLockExclusive(&BridgeViewsLock);
            RealAweDestroy(view);
            AcquireSRWLockExclusive(&BridgeViewsLock);
            for (auto& entry : BridgeViews) if (entry.view == view) entry = BridgeView{};
            ReleaseSRWLockExclusive(&BridgeViewsLock);
        }

        AweJsCallback BridgeTestInstalled = nullptr;
        unsigned int BridgeTestForwarded = 0;
        void __cdecl BridgeTestSetJs(void*, AweJsCallback callback) { BridgeTestInstalled = callback; }
        void __cdecl BridgeTestOriginalJs(void*, const void*, const void*, const void*) { ++BridgeTestForwarded; }
        unsigned int __cdecl BridgeTestToUtf8(const void* text, char* output, unsigned int capacity)
        {
            const auto length = static_cast<unsigned int>(std::strlen(static_cast<const char*>(text)));
            if (output && capacity > length) std::memcpy(output, text, length + 1);
            return length;
        }

        bool RunNativeBridgeMockSelfTest()
        {
            // Offline fixtures replace only our C API pointers, never a loaded client or native call.
            BridgeView savedViews[32];
            std::memcpy(savedViews, BridgeViews, sizeof(savedViews));
            const HMODULE savedGui = BridgeModules[0].handle;
            const auto savedSetter = RealSetJsCallback;
            const auto savedUtf8 = AweToUtf8;
            const bool savedEndpoint = NativeBridgeEndpoint;
            const LONG savedReady = InterlockedExchange(&NativeBridgeReady, 0);
            BridgeModules[0].handle = reinterpret_cast<HMODULE>(0x10000000);
            RealSetJsCallback = BridgeTestSetJs;
            AweToUtf8 = BridgeTestToUtf8;
            NativeBridgeEndpoint = true;
            BridgeTestForwarded = 0;
            const auto expected = reinterpret_cast<AweJsCallback>(0x1010c39e);
            for (unsigned int i = 0; i < 32; ++i)
                BridgeViews[i] = BridgeView{ reinterpret_cast<void*>(i + 1), GetCurrentThreadId(), BridgeTestOriginalJs, HookAweFinishLoading, true, false };
            HookSetAweJsCallback(reinterpret_cast<void*>(100), expected);
            bool passed = BridgeTestInstalled == expected; // Full table must pass through unchanged.
            BridgeViews[0].thread = GetCurrentThreadId() + 1;
            HookSetAweJsCallback(BridgeViews[0].view, expected);
            passed = passed && BridgeTestInstalled == expected; // Wrong owner must also pass through.
            HookSetAweJsCallback(BridgeViews[0].view, BridgeTestOriginalJs);
            passed = passed && BridgeTestInstalled == BridgeTestOriginalJs;
            BridgeViews[0].thread = GetCurrentThreadId();
            HookAweJsCallback(BridgeViews[0].view, "OtherObject", "OtherMethod", nullptr);
            passed = passed && BridgeTestForwarded == 1;
            HookAweJsCallback(BridgeViews[0].view, "AORebirthDaily", "Claim", nullptr);
            passed = passed && BridgeTestForwarded == 1; // An unready bridge cannot dispatch native work.
            std::memcpy(BridgeViews, savedViews, sizeof(savedViews));
            BridgeModules[0].handle = savedGui;
            RealSetJsCallback = savedSetter;
            AweToUtf8 = savedUtf8;
            NativeBridgeEndpoint = savedEndpoint;
            InterlockedExchange(&NativeBridgeReady, savedReady);
            return passed;
        }

        bool ResolveNativeBridge()
        {
            if (InterlockedCompareExchange(&NativeBridgeReady, 0, 0) == 1) return true;
            if (NativeBridgeRejected) return false;
            for (auto& module : BridgeModules) {
                module.handle = GetModuleHandleW(module.name);
                if (!module.handle) return false;
            }
            NativeBridgeEndpoint = RoutingTarget.matchedLaunchEndpoint;
            if (!NativeBridgeEndpoint || !NativeBridgeModulesMatch()) {
                NativeBridgeRejected = true; Log("DAILYLOGIN native=BLOCKED reason=unsupported_endpoint_or_modules"); return false;
            }
            HMODULE awe = BridgeModules[3].handle;
            RealSetJsCallback = reinterpret_cast<FnSetJsCallback>(BridgeExport(awe, "awe_webview_set_callback_js_callback"));
            RealSetFinishCallback = reinterpret_cast<FnSetFinishCallback>(BridgeExport(awe, "awe_webview_set_callback_finish_loading"));
            RealAweDestroy = reinterpret_cast<FnAweDestroy>(BridgeExport(awe, "awe_webview_destroy"));
            AweGetUrl = reinterpret_cast<FnAweGetUrl>(BridgeExport(awe, "awe_webview_get_url"));
            AweString = reinterpret_cast<FnAweString>(BridgeExport(awe, "awe_string_create_from_ascii"));
            AweStringDestroy = reinterpret_cast<FnAweStringDestroy>(BridgeExport(awe, "awe_string_destroy"));
            AweToUtf8 = reinterpret_cast<FnAweToUtf8>(BridgeExport(awe, "awe_string_to_utf8"));
            AweCreateObject = reinterpret_cast<FnAweCreateObject>(BridgeExport(awe, "awe_webview_create_object"));
            AweObjectCallback = reinterpret_cast<FnAweObjectCallback>(BridgeExport(awe, "awe_webview_set_object_callback"));
            AweArraySize = reinterpret_cast<FnAweArraySize>(BridgeExport(awe, "awe_jsarray_get_size"));
            InterfaceInstance = reinterpret_cast<FnInterfaceInstance>(BridgeExport(BridgeModules[1].handle, "?GetInstance@N3InterfaceModule_t@@SAPAV1@XZ"));
            RequestClaims = reinterpret_cast<FnRequestClaims>(BridgeExport(BridgeModules[1].handle, "?N3Msg_RequestClaims@N3InterfaceModule_t@@QAEXXZ"));
            if (!RealSetJsCallback || !RealSetFinishCallback || !RealAweDestroy || !AweGetUrl || !AweString ||
                !AweStringDestroy || !AweToUtf8 || !AweCreateObject || !AweObjectCallback || !AweArraySize ||
                !InterfaceInstance || !RequestClaims) {
                NativeBridgeRejected = true; Log("DAILYLOGIN native=BLOCKED reason=exports"); return false;
            }
            InterlockedExchange(&NativeBridgeReady, 1);
            return true;
        }

        unsigned long PatchNativeBridge()
        {
            if (!ResolveNativeBridge()) return 0;
            HMODULE gui = BridgeModules[0].handle;
            unsigned long patched = 0;
            patched += PatchImport(gui, "Awesomium.dll", "awe_webview_set_callback_js_callback", reinterpret_cast<void*>(RealSetJsCallback), reinterpret_cast<void*>(HookSetAweJsCallback)) ? 1ul : 0ul;
            patched += PatchImport(gui, "Awesomium.dll", "awe_webview_set_callback_finish_loading", reinterpret_cast<void*>(RealSetFinishCallback), reinterpret_cast<void*>(HookSetAweFinishCallback)) ? 1ul : 0ul;
            patched += PatchImport(gui, "Awesomium.dll", "awe_webview_destroy", reinterpret_cast<void*>(RealAweDestroy), reinterpret_cast<void*>(HookAweDestroy)) ? 1ul : 0ul;
            return patched;
        }

        unsigned long PatchAllLoadedModules()
        {
            HANDLE snapshot = CreateToolhelp32Snapshot(
                TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
                GetCurrentProcessId());
            if (snapshot == INVALID_HANDLE_VALUE)
            {
                return 0;
            }

            MODULEENTRY32W entry = {};
            entry.dwSize = sizeof(entry);
            unsigned long patched = PatchNativeBridge();
            if (Module32FirstW(snapshot, &entry))
            {
                do
                {
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "getaddrinfo", RealGetAddrInfoA, reinterpret_cast<void*>(HookGetAddrInfoA)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "GetAddrInfoW", RealGetAddrInfoW, reinterpret_cast<void*>(HookGetAddrInfoW)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "gethostbyname", RealGetHostByName, reinterpret_cast<void*>(HookGetHostByName)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "send", RealSend, reinterpret_cast<void*>(HookSend)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "WSASend", RealWSASend, reinterpret_cast<void*>(HookWSASend)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "KERNEL32.dll", "GetProcAddress", RealGetProcAddressKernel32, reinterpret_cast<void*>(HookGetProcAddress)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "KERNELBASE.dll", "GetProcAddress", RealGetProcAddressKernelBase, reinterpret_cast<void*>(HookGetProcAddress)) ? 1ul : 0ul;
                }
                while (Module32NextW(snapshot, &entry));
            }

            CloseHandle(snapshot);
            return patched;
        }

        bool ResolvePolicyAllowsRouting()
        {
            char value[32] = {};
            DWORD length = GetEnvironmentVariableA(
                "AO_REBIRTH_DAILYLOGIN_ROUTING",
                value,
                static_cast<DWORD>(sizeof(value)));
            if (length > 0 && _stricmp(value, "Off") == 0)
            {
                Log("DAILYLOGIN route=SKIP reason=policy_off");
                return false;
            }

            const bool always = length > 0 && length < sizeof(value) && _stricmp(value, "Always") == 0;
            const char* failure = nullptr;
            if (!TrySelectLoginServer(always, RoutingTarget, failure))
            {
                Log("DAILYLOGIN route=SKIP reason=%s", failure);
                return false;
            }
            if (RoutingTarget.dailyMode == DailyLoginMode::Off)
            {
                Log("DAILYLOGIN route=SKIP reason=endpoint_has_no_daily_policy");
                return false;
            }

            Log("DAILYLOGIN route=ARMED reason=%s port=%lu",
                RoutingTarget.matchedLaunchEndpoint ? "auto_endpoint_listed" : "policy_always",
                RoutingTarget.port);
            return true;
        }

        DWORD WINAPI DailyLoginRoutingWorker(LPVOID)
        {
            if (!ResolveWs2Functions())
            {
                Log("DAILYLOGIN route=BLOCKED reason=ws2_resolve");
                return 1;
            }

            RoutingEnabled = ResolvePolicyAllowsRouting();
            if (!RoutingEnabled)
            {
                return 0;
            }

            unsigned long totalPatches = 0;
            for (int attempt = 0; attempt < 300; ++attempt)
            {
                totalPatches += PatchAllLoadedModules();
                Sleep(200);
            }

            Log("DAILYLOGIN route=READY importPatchEvents=%lu", totalPatches);
            return 0;
        }

        int WSAAPI HookGetAddrInfoA(
            PCSTR nodeName,
            PCSTR serviceName,
            const ADDRINFOA* hints,
            PADDRINFOA* result)
        {
            if (RoutingEnabled && nodeName && IsDailyLoginHost(nodeName))
            {
                Log("DAILYLOGIN route=dns host=%s target=%s", nodeName, RoutingTarget.address.c_str());
                return RealGetAddrInfoA(RoutingTarget.address.c_str(), serviceName, hints, result);
            }

            return RealGetAddrInfoA(nodeName, serviceName, hints, result);
        }

        int WSAAPI HookGetAddrInfoW(
            PCWSTR nodeName,
            PCWSTR serviceName,
            const ADDRINFOW* hints,
            PADDRINFOW* result)
        {
            if (RoutingEnabled && nodeName)
            {
                char host[256] = {};
                int converted = WideCharToMultiByte(
                    CP_ACP,
                    0,
                    nodeName,
                    -1,
                    host,
                    static_cast<int>(sizeof(host)),
                    nullptr,
                    nullptr);
                if (converted > 0 && IsDailyLoginHost(host))
                {
                    Log("DAILYLOGIN route=dnsw host=%s target=%s", host, RoutingTarget.address.c_str());
                    return RealGetAddrInfoW(RoutingTarget.wideAddress.c_str(), serviceName, hints, result);
                }
            }

            return RealGetAddrInfoW(nodeName, serviceName, hints, result);
        }

        hostent* WSAAPI HookGetHostByName(const char* name)
        {
            if (RoutingEnabled && name && IsDailyLoginHost(name))
            {
                struct HostStorage
                {
                    hostent entry;
                    char name[32];
                    char* aliases[1];
                    unsigned char address[4];
                    char* addressList[2];
                };

                thread_local HostStorage storage = {};
                strcpy_s(storage.name, RoutingTarget.address.c_str());
                storage.aliases[0] = nullptr;
                std::memcpy(storage.address, RoutingTarget.addressBytes.data(), sizeof(storage.address));
                storage.addressList[0] = reinterpret_cast<char*>(storage.address);
                storage.addressList[1] = nullptr;
                storage.entry.h_name = storage.name;
                storage.entry.h_aliases = storage.aliases;
                storage.entry.h_addrtype = AF_INET;
                storage.entry.h_length = 4;
                storage.entry.h_addr_list = storage.addressList;
                Log("DAILYLOGIN route=gethostbyname host=%s target=%s", name, RoutingTarget.address.c_str());
                return &storage.entry;
            }

            return RealGetHostByName(name);
        }

        int WSAAPI HookSend(SOCKET socket, const char* buffer, int length, int flags)
        {
            std::vector<char> rewritten;
            if (RoutingEnabled && RoutingTarget.dailyMode == DailyLoginMode::RewriteHttp &&
                RewriteDailyLoginHttpRequest(buffer, length, rewritten))
            {
                return RealSend(
                    socket,
                    rewritten.data(),
                    static_cast<int>(rewritten.size()),
                    flags);
            }

            return RealSend(socket, buffer, length, flags);
        }

        FARPROC WINAPI HookGetProcAddress(HMODULE module, LPCSTR procName)
        {
            if (RoutingEnabled && IsWs2Module(module) && !IsOrdinalProcedureName(procName))
            {
                if (std::strcmp(procName, "getaddrinfo") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=getaddrinfo");
                    return reinterpret_cast<FARPROC>(HookGetAddrInfoA);
                }

                if (std::strcmp(procName, "GetAddrInfoW") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=GetAddrInfoW");
                    return reinterpret_cast<FARPROC>(HookGetAddrInfoW);
                }

                if (std::strcmp(procName, "gethostbyname") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=gethostbyname");
                    return reinterpret_cast<FARPROC>(HookGetHostByName);
                }

                if (std::strcmp(procName, "send") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=send");
                    return reinterpret_cast<FARPROC>(HookSend);
                }

                if (std::strcmp(procName, "WSASend") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=WSASend");
                    return reinterpret_cast<FARPROC>(HookWSASend);
                }
            }

            return RealGetProcAddressKernel32
                ? RealGetProcAddressKernel32(module, procName)
                : nullptr;
        }

        int WSAAPI HookWSASend(
            SOCKET socket,
            LPWSABUF buffers,
            DWORD bufferCount,
            LPDWORD bytesSent,
            DWORD flags,
            LPWSAOVERLAPPED overlapped,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE completionRoutine)
        {
            std::vector<char> rewritten;
            WSABUF replacement = {};
            if (RoutingEnabled &&
                RoutingTarget.dailyMode == DailyLoginMode::RewriteHttp &&
                RewriteDailyLoginHttpRequestFromWsabufs(buffers, bufferCount, rewritten))
            {
                if (overlapped)
                {
                    char* heapBuffer = reinterpret_cast<char*>(
                        HeapAlloc(GetProcessHeap(), 0, rewritten.size()));
                    if (!heapBuffer)
                    {
                        return RealWSASend(
                            socket,
                            buffers,
                            bufferCount,
                            bytesSent,
                            flags,
                            overlapped,
                            completionRoutine);
                    }

                    std::memcpy(heapBuffer, rewritten.data(), rewritten.size());
                    replacement.buf = heapBuffer;
                    replacement.len = static_cast<ULONG>(rewritten.size());
                    return RealWSASend(
                        socket,
                        &replacement,
                        1,
                        bytesSent,
                        flags,
                        overlapped,
                        completionRoutine);
                }

                replacement.buf = rewritten.data();
                replacement.len = static_cast<ULONG>(rewritten.size());
                return RealWSASend(
                    socket,
                    &replacement,
                    1,
                    bytesSent,
                    flags,
                    nullptr,
                    completionRoutine);
            }

            return RealWSASend(
                socket,
                buffers,
                bufferCount,
                bytesSent,
                flags,
                overlapped,
                completionRoutine);
        }
    }

    bool StartDailyLoginRoutingWorker()
    {
        if (InterlockedCompareExchange(&WorkerStarted, 1, 0) != 0)
        {
            return true;
        }

        HANDLE worker = CreateThread(nullptr, 0, DailyLoginRoutingWorker, nullptr, 0, nullptr);
        if (!worker)
        {
            Log("DAILYLOGIN route=BLOCKED reason=worker_create code=%lu", GetLastError());
            return false;
        }

        CloseHandle(worker);
        return true;
    }

    bool RewriteDailyLoginHttpRequestForTest(
        const std::string& input,
        std::string& output)
    {
        std::vector<char> rewritten;
        if (!RewriteDailyLoginHttpRequest(
                input.data(),
                static_cast<int>(input.size()),
                rewritten))
        {
            return false;
        }

        output.assign(rewritten.begin(), rewritten.end());
        return true;
    }

    bool RunDailyLoginRoutingSelfTest()
    {
        RoutingTarget.originalDailyHost = "dailyrewards.anarchy-online.com";
        RoutingTarget.dailyHosts = { "uwg.daily.icc-rk", "www.daily.icc-rk" };
        if (!RunNativeBridgeMockSelfTest()) return false;
        if (!AllowedDailyBridgeUrl("http://dailyrewards.anarchy-online.com/") ||
            !AllowedDailyBridgeUrl("http://uwg.daily.icc-rk/index.app") ||
            AllowedDailyBridgeUrl("http://uwg.daily.icc-rk.evil/index.app") ||
            AllowedDailyBridgeUrl("http://uwg.daily.icc-rk@evil/index.app") ||
            AllowedDailyBridgeUrl("http://uwg.daily.icc-rk/other.app") ||
            AllowedDailyBridgeUrl("http://127.0.0.1/index.app") ||
            AllowedDailyBridgeUrl("http://uwg.daily.icc-rk/index.app?redirect=evil")) return false;
        BridgeView dispatchView{ reinterpret_cast<void*>(1), 23, HookAweJsCallback, HookAweFinishLoading, true, false };
        if (!CanDispatchDailyBridge(true, true, true, dispatchView, 23) ||
            CanDispatchDailyBridge(false, true, true, dispatchView, 23) ||
            CanDispatchDailyBridge(true, false, true, dispatchView, 23) ||
            CanDispatchDailyBridge(true, true, false, dispatchView, 23) ||
            CanDispatchDailyBridge(true, true, true, dispatchView, 24)) return false;
        dispatchView.registered = false;
        if (CanDispatchDailyBridge(true, true, true, dispatchView, 23)) return false;
        dispatchView.registered = true;
        dispatchView.destroying = true;
        if (CanDispatchDailyBridge(true, true, true, dispatchView, 23)) return false;
        dispatchView.destroying = false;
        dispatchView.js = nullptr;
        if (CanDispatchDailyBridge(true, true, true, dispatchView, 23)) return false;
        dispatchView.js = HookAweJsCallback;
        dispatchView.finish = nullptr;
        if (CanDispatchDailyBridge(true, true, true, dispatchView, 23)) return false;

        if (!IsDailyLoginHost("dailyrewards.anarchy-online.com") ||
            !IsDailyLoginHost("uwg.daily.icc-rk") ||
            !IsDailyLoginHost("www.daily.icc-rk") ||
            IsDailyLoginHost("aomarket.funcom.com"))
        {
            return false;
        }

        std::string rewritten;
        if (!RewriteDailyLoginHttpRequestForTest(
                "GET / HTTP/1.1\r\nHost: dailyrewards.anarchy-online.com\r\n\r\n",
                rewritten) ||
            rewritten.find("GET / HTTP/1.1") == std::string::npos ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos ||
            rewritten.size() != std::strlen("GET / HTTP/1.1\r\nHost: dailyrewards.anarchy-online.com\r\n\r\n"))
        {
            return false;
        }

        if (!RewriteDailyLoginHttpRequestForTest(
                "GET /index.app HTTP/1.1\r\nHost: www.daily.icc-rk\r\n\r\n",
                rewritten) ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos)
        {
            return false;
        }

        if (!RewriteDailyLoginHttpRequestForTest(
                "GET http://www.daily.icc-rk/index.app HTTP/1.1\r\nHost: www.daily.icc-rk\r\n\r\n",
                rewritten) ||
            rewritten.find("http://uwg.daily.icc-rk/index.app") == std::string::npos ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos)
        {
            return false;
        }

        if (RewriteDailyLoginHttpRequestForTest(
                "GET /market/ HTTP/1.1\r\nHost: aomarket.funcom.com\r\n\r\n",
                rewritten))
        {
            return false;
        }

        return true;
    }
}
