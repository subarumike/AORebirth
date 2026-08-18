#include "daily_login_routing.h"

#include "logging.h"

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
        constexpr unsigned long AoRebirthLoginIpHostOrder =
            (2ul << 24) | (24ul << 16) | (96ul << 8) | 30ul;
        constexpr unsigned long AoRebirthLoginIpClientArgumentOrder =
            (30ul << 24) | (96ul << 16) | (24ul << 8) | 2ul;
        constexpr unsigned long AoRebirthLocalIpHostOrder =
            (127ul << 24) | 1ul;
        constexpr unsigned long AoRebirthLocalIpClientArgumentOrder =
            (1ul << 24) | 127ul;
        constexpr unsigned long AoRebirthLoginPort = 7500;
        constexpr const char* AoRebirthDailyLoginIp = "2.24.96.30";
        constexpr const char* AoRebirthDailyLoginHost = "uwg.daily.icc-rk";

        using FnGetAddrInfoA = int(WSAAPI*)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
        using FnGetAddrInfoW = int(WSAAPI*)(PCWSTR, PCWSTR, const ADDRINFOW*, PADDRINFOW*);
        using FnGetHostByName = hostent*(WSAAPI*)(const char*);
        using FnSend = int(WSAAPI*)(SOCKET, const char*, int, int);
        using FnWSASend = int(WSAAPI*)(
            SOCKET,
            LPWSABUF,
            DWORD,
            LPDWORD,
            DWORD,
            LPWSAOVERLAPPED,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE);

        struct LaunchEndpoint
        {
            bool hasIp;
            bool hasPort;
            bool dottedIp;
            unsigned long ip;
            unsigned long port;
        };

        LONG WorkerStarted = 0;
        bool RoutingEnabled = false;
        HMODULE Ws2 = nullptr;
        FnGetAddrInfoA RealGetAddrInfoA = nullptr;
        FnGetAddrInfoW RealGetAddrInfoW = nullptr;
        FnGetHostByName RealGetHostByName = nullptr;
        FnSend RealSend = nullptr;
        FnWSASend RealWSASend = nullptr;

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

        bool ParseUnsignedToken(const wchar_t* text, unsigned long& value, const wchar_t** end)
        {
            if (!text || !iswdigit(*text))
            {
                return false;
            }

            unsigned long parsed = 0;
            while (iswdigit(*text))
            {
                unsigned long digit = static_cast<unsigned long>(*text - L'0');
                if (parsed > (0xfffffffful - digit) / 10ul)
                {
                    return false;
                }

                parsed = parsed * 10ul + digit;
                ++text;
            }

            value = parsed;
            if (end)
            {
                *end = text;
            }

            return true;
        }

        bool ParseDottedIpToken(const wchar_t* text, unsigned long& value, const wchar_t** end)
        {
            unsigned long octets[4] = {};
            const wchar_t* cursor = text;
            for (int index = 0; index < 4; ++index)
            {
                if (!ParseUnsignedToken(cursor, octets[index], &cursor) || octets[index] > 255)
                {
                    return false;
                }

                if (index < 3)
                {
                    if (*cursor != L'.')
                    {
                        return false;
                    }
                    ++cursor;
                }
            }

            value =
                (octets[0] << 24) |
                (octets[1] << 16) |
                (octets[2] << 8) |
                octets[3];
            if (end)
            {
                *end = cursor;
            }
            return true;
        }

        bool IsTokenBoundary(wchar_t value)
        {
            return value == 0 || iswspace(value) || value == L'"';
        }

        bool TryParseLaunchEndpoint(const wchar_t* commandLine, LaunchEndpoint& endpoint)
        {
            endpoint = {};
            if (!commandLine)
            {
                return false;
            }

            const wchar_t* ia = wcsstr(commandLine, L"IA");
            while (ia)
            {
                unsigned long parsed = 0;
                const wchar_t* end = nullptr;
                bool dotted = ParseDottedIpToken(ia + 2, parsed, &end);
                if (!dotted)
                {
                    if (!ParseUnsignedToken(ia + 2, parsed, &end))
                    {
                        ia = wcsstr(ia + 2, L"IA");
                        continue;
                    }
                }

                if (IsTokenBoundary(*end))
                {
                    endpoint.hasIp = true;
                    endpoint.dottedIp = dotted;
                    endpoint.ip = parsed;
                    break;
                }

                ia = wcsstr(ia + 2, L"IA");
            }

            const wchar_t* ip = wcsstr(commandLine, L"IP");
            while (ip)
            {
                unsigned long parsed = 0;
                const wchar_t* end = nullptr;
                if (ParseUnsignedToken(ip + 2, parsed, &end) &&
                    IsTokenBoundary(*end) &&
                    parsed <= 65535)
                {
                    endpoint.hasPort = true;
                    endpoint.port = parsed;
                    break;
                }

                ip = wcsstr(ip + 2, L"IP");
            }

            return endpoint.hasIp && endpoint.hasPort;
        }

        bool IsAoRebirthEndpoint(const LaunchEndpoint& endpoint)
        {
            return endpoint.hasIp &&
                endpoint.hasPort &&
                (endpoint.ip == AoRebirthLoginIpHostOrder ||
                 endpoint.ip == AoRebirthLoginIpClientArgumentOrder ||
                 endpoint.ip == AoRebirthLocalIpHostOrder ||
                 endpoint.ip == AoRebirthLocalIpClientArgumentOrder) &&
                endpoint.port == AoRebirthLoginPort;
        }

        bool IsDailyLoginHost(const std::string& host)
        {
            std::string lower = ToLower(host);
            return lower == "dailyrewards.anarchy-online.com" ||
                lower == "uwg.daily.icc-rk" ||
                lower == "www.daily.icc-rk";
        }

        bool IsHttpRequestPrefix(const std::string& value)
        {
            return value.rfind("GET ", 0) == 0 ||
                value.rfind("POST ", 0) == 0 ||
                value.rfind("HEAD ", 0) == 0;
        }

        std::string RightPaddedHostReplacement()
        {
            constexpr const char* source = "dailyrewards.anarchy-online.com";
            std::string replacement = AoRebirthDailyLoginHost;
            replacement.append(std::strlen(source) - replacement.size(), ' ');
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

            std::string lower = ToLower(request);
            const bool matchedDailyRewards =
                lower.find("dailyrewards.anarchy-online.com") != std::string::npos;
            const bool matchedWww =
                lower.find("www.daily.icc-rk") != std::string::npos;
            const bool matchedUwg =
                lower.find("uwg.daily.icc-rk") != std::string::npos;
            if (!matchedDailyRewards && !matchedWww && !matchedUwg)
            {
                return false;
            }

            bool changed = false;
            changed |= ReplaceAll(
                request,
                "Host: dailyrewards.anarchy-online.com",
                "Host: " + RightPaddedHostReplacement());
            changed |= ReplaceAll(
                request,
                "host: dailyrewards.anarchy-online.com",
                "host: " + RightPaddedHostReplacement());
            changed |= ReplaceAll(
                request,
                "www.daily.icc-rk",
                AoRebirthDailyLoginHost);

            if (!changed)
            {
                return false;
            }

            rewritten.assign(request.begin(), request.end());
            Log(
                "DAILYLOGIN route=http_rewrite hostDailyRewards=%s hostWww=%s hostUwg=%s bytesIn=%lu bytesOut=%lu",
                matchedDailyRewards ? "true" : "false",
                matchedWww ? "true" : "false",
                matchedUwg ? "true" : "false",
                static_cast<unsigned long>(length),
                static_cast<unsigned long>(rewritten.size()));
            return true;
        }

        FARPROC ResolveWs2(const char* name)
        {
            if (!Ws2)
            {
                Ws2 = LoadLibraryW(L"ws2_32.dll");
            }

            return Ws2 ? GetProcAddress(Ws2, name) : nullptr;
        }

        bool ResolveWs2Functions()
        {
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
            unsigned long patched = 0;
            if (Module32FirstW(snapshot, &entry))
            {
                do
                {
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "getaddrinfo", RealGetAddrInfoA, reinterpret_cast<void*>(HookGetAddrInfoA)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "GetAddrInfoW", RealGetAddrInfoW, reinterpret_cast<void*>(HookGetAddrInfoW)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "gethostbyname", RealGetHostByName, reinterpret_cast<void*>(HookGetHostByName)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "send", RealSend, reinterpret_cast<void*>(HookSend)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "WSASend", RealWSASend, reinterpret_cast<void*>(HookWSASend)) ? 1ul : 0ul;
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

            if (length > 0 && _stricmp(value, "Always") == 0)
            {
                Log("DAILYLOGIN route=ARMED reason=policy_always");
                return true;
            }

            LaunchEndpoint endpoint = {};
            if (!TryParseLaunchEndpoint(GetCommandLineW(), endpoint))
            {
                Log("DAILYLOGIN route=SKIP reason=auto_endpoint_unavailable");
                return false;
            }

            if (!IsAoRebirthEndpoint(endpoint))
            {
                Log("DAILYLOGIN route=SKIP reason=auto_endpoint_not_aorebirth port=%lu", endpoint.port);
                return false;
            }

            Log("DAILYLOGIN route=ARMED reason=auto_endpoint_aorebirth port=%lu", endpoint.port);
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
                Log("DAILYLOGIN route=dns host=%s target=%s", nodeName, AoRebirthDailyLoginIp);
                return RealGetAddrInfoA(AoRebirthDailyLoginIp, serviceName, hints, result);
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
                    Log("DAILYLOGIN route=dnsw host=%s target=%s", host, AoRebirthDailyLoginIp);
                    wchar_t target[] = L"2.24.96.30";
                    return RealGetAddrInfoW(target, serviceName, hints, result);
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
                strcpy_s(storage.name, AoRebirthDailyLoginIp);
                storage.aliases[0] = nullptr;
                storage.address[0] = 2;
                storage.address[1] = 24;
                storage.address[2] = 96;
                storage.address[3] = 30;
                storage.addressList[0] = reinterpret_cast<char*>(storage.address);
                storage.addressList[1] = nullptr;
                storage.entry.h_name = storage.name;
                storage.entry.h_aliases = storage.aliases;
                storage.entry.h_addrtype = AF_INET;
                storage.entry.h_length = 4;
                storage.entry.h_addr_list = storage.addressList;
                Log("DAILYLOGIN route=gethostbyname host=%s target=%s", name, AoRebirthDailyLoginIp);
                return &storage.entry;
            }

            return RealGetHostByName(name);
        }

        int WSAAPI HookSend(SOCKET socket, const char* buffer, int length, int flags)
        {
            std::vector<char> rewritten;
            if (RoutingEnabled && RewriteDailyLoginHttpRequest(buffer, length, rewritten))
            {
                return RealSend(
                    socket,
                    rewritten.data(),
                    static_cast<int>(rewritten.size()),
                    flags);
            }

            return RealSend(socket, buffer, length, flags);
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
                overlapped == nullptr &&
                bufferCount == 1 &&
                buffers &&
                RewriteDailyLoginHttpRequest(
                    buffers[0].buf,
                    static_cast<int>(buffers[0].len),
                    rewritten))
            {
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
        LaunchEndpoint endpoint = {};
        if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7500 DUprivate", endpoint) ||
            !IsAoRebirthEndpoint(endpoint))
        {
            return false;
        }

        if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7505 DUofficial", endpoint) ||
            IsAoRebirthEndpoint(endpoint))
        {
            return false;
        }

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
