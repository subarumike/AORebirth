#include "login_key_patch.h"
#include "logging.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cwctype>
#include <string>
#include <vector>

namespace aorf
{
    namespace
    {
        constexpr char FuncomPublicKey[] =
            "9c32cc23d559ca90fc31be72df817d0e124769e809f936bc14360ff4bed758f260a0d596584eacbbc2b88bdd410416163e11dbf62173393fbc0c6fefb2d855f1a03dec8e9f105bbad91b3437d8eb73fe2f44159597aa4053cf788d2f9d7012fb8d7c4ce3876f7d6cd5d0c31754f4cd96166708641958de54a6def5657b9f2e92";
        constexpr char AoRebirthPublicKey[] =
            "26b5a3b4ac1177f24a2d9de44bafef477ff23ef1cb5f646919b1be26516053030b65d5afb60cef6f49de539958ba0b7922a099319b8016a8673cb27a696ae4b60fdece25ddcdad42e7f0056b87fc35687fe033b242e17e960d79806fd46c4a79cbc64f558660a50cabc1c242dace70de6af452e3433f97e30e202567f187de70";
        constexpr size_t PublicKeyLength = sizeof(FuncomPublicKey) - 1;
        constexpr unsigned long AoRebirthLoginIpHostOrder =
            (2ul << 24) | (24ul << 16) | (96ul << 8) | 30ul;
        constexpr unsigned long AoRebirthLoginIpClientArgumentOrder =
            (30ul << 24) | (96ul << 16) | (24ul << 8) | 2ul;
        constexpr unsigned long AoRebirthLocalIpHostOrder =
            (127ul << 24) | 1ul;
        constexpr unsigned long AoRebirthLocalIpClientArgumentOrder =
            (1ul << 24) | 127ul;
        constexpr unsigned long AoRebirthLoginPort = 7500;
        // More login servers that take the AORebirth key, one "host:port" a line (# comments), in aorebirth-patch\
        // beside this DLL: private servers run from AORebirth (AO-Andersson's setup writes its own server there).
        constexpr wchar_t ListedServersFile[] = L"aorebirth-patch\\servers.txt";

        enum class Policy
        {
            Off,
            Auto,
            Always
        };

        enum class PatchState
        {
            Original,
            Patched,
            Unknown
        };

        struct TargetModule
        {
            const wchar_t* moduleName;
            const char* profileId;
            const char* expectedSha256;
            DWORD expectedFileSize;
        };

        struct MatchAnalysis
        {
            PatchState state;
            std::vector<size_t> funcomOffsets;
            std::vector<size_t> aoRebirthOffsets;
        };

        struct LaunchEndpoint
        {
            bool hasIp;
            bool hasPort;
            bool dottedIp;
            unsigned long ip;
            unsigned long port;
        };

        constexpr TargetModule Targets[] =
        {
            {
                L"GUI.dll",
                "ep2-gui-20230602",
                "ecaa2c686db3e0e17032ac69b14a14f030bc3185c51a10e04beec18ba3ac5306",
                2793472
            },
            {
                L"GUI.dll",
                "ep1-gui-20230602",
                "e485384721e2fe13972e840dfb6a9fe29b1ba4eb71b42cc049e5097a570b6de1",
                2790400
            },
            {
                L"Interfaces.dll",
                "ep2-interfaces-20230602",
                "a75dbe4cb5293778468aa3283bc4ef93efc9573a0cd1c32314176e692c3ec414",
                212480
            },
            {
                L"Interfaces.dll",
                "ep1-interfaces-20230602",
                "3aa79a44e76c3413543404058c5d07323bd3b69f4c3493c7b136befa1a55b0a7",
                211968
            }
        };

        LONG LoginKeyWorkerStarted = 0;

        const char* PolicyName(Policy policy)
        {
            switch (policy)
            {
            case Policy::Off:
                return "Off";
            case Policy::Auto:
                return "Auto";
            case Policy::Always:
                return "Always";
            default:
                return "Unknown";
            }
        }

        Policy ResolvePolicy()
        {
            char value[32] = {};
            DWORD length = GetEnvironmentVariableA(
                "AO_REBIRTH_LOGINKEY_PATCH",
                value,
                static_cast<DWORD>(sizeof(value)));
            if (length == 0)
            {
                return Policy::Auto;
            }

            if (_stricmp(value, "Off") == 0)
            {
                return Policy::Off;
            }

            if (_stricmp(value, "Auto") == 0)
            {
                return Policy::Auto;
            }

            if (_stricmp(value, "Always") == 0)
            {
                return Policy::Always;
            }

            Log("LOGINKEY policy=Auto reason=invalid_env_value");
            return Policy::Auto;
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
            if (!text)
            {
                return false;
            }

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
                    end = nullptr;
                    dotted = false;
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
                if (ParseUnsignedToken(ip + 2, parsed, &end) && IsTokenBoundary(*end) && parsed <= 65535)
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

        struct ListedServer
        {
            std::string host;
            unsigned long port;
        };

        // The lines of servers.txt: "host:port"; blank lines, # comments and anything malformed are skipped.
        std::vector<ListedServer> ParseListedServers(const std::string& text)
        {
            std::vector<ListedServer> servers;
            size_t start = 0;
            while (start < text.size())
            {
                size_t end = text.find('\n', start);
                if (end == std::string::npos)
                {
                    end = text.size();
                }

                std::string line = text.substr(start, end - start);
                start = end + 1;
                const size_t comment = line.find('#');
                if (comment != std::string::npos)
                {
                    line.erase(comment);
                }

                const char* space = " \t\r";
                const size_t first = line.find_first_not_of(space);
                if (first == std::string::npos)
                {
                    continue;
                }

                line = line.substr(first, line.find_last_not_of(space) - first + 1);
                const size_t colon = line.rfind(':');
                if (colon == std::string::npos || colon == 0 || colon + 1 == line.size() ||
                    line.size() - colon - 1 > 5 ||
                    line.find_first_not_of("0123456789", colon + 1) != std::string::npos)
                {
                    continue;
                }

                const unsigned long port = std::strtoul(line.c_str() + colon + 1, nullptr, 10);
                if (port == 0 || port > 65535)
                {
                    continue;
                }

                servers.push_back({ line.substr(0, colon), port });
            }

            return servers;
        }

        bool ReadListedServersFile(std::string& text, std::wstring& path)
        {
            HMODULE self = nullptr;
            if (!GetModuleHandleExW(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                    reinterpret_cast<LPCWSTR>(&ParseListedServers),
                    &self))
            {
                return false;
            }

            wchar_t module[MAX_PATH] = {};
            const DWORD length = GetModuleFileNameW(self, module, MAX_PATH);
            if (length == 0 || length >= MAX_PATH)
            {
                return false;
            }

            path.assign(module, length);
            path.erase(path.find_last_of(L"\\/") + 1);
            path += ListedServersFile;

            HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            char buffer[4096];
            DWORD read = 0;
            text.clear();
            while (text.size() < 65536 && ReadFile(file, buffer, sizeof(buffer), &read, nullptr) && read > 0)
            {
                text.append(buffer, read);
            }

            CloseHandle(file);
            return true;
        }

        // Whether the launch endpoint is one of servers.txt's: the same port, and the host resolving to the address
        // the launcher passed (it resolves the dimension's host itself; either byte order, as above). Winsock is
        // loaded here rather than imported, like daily_login_routing.cpp.
        bool IsListedServerEndpoint(const LaunchEndpoint& endpoint)
        {
            std::string text;
            std::wstring path;
            if (!ReadListedServersFile(text, path))
            {
                return false;
            }

            const std::vector<ListedServer> servers = ParseListedServers(text);
            Log("LOGINKEY listed servers=%zu file=%ls", servers.size(), path.c_str());
            if (servers.empty())
            {
                return false;
            }

            using FnWSAStartup = int(WSAAPI*)(WORD, LPWSADATA);
            using FnGetAddrInfo = int(WSAAPI*)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
            using FnFreeAddrInfo = void(WSAAPI*)(PADDRINFOA);
            HMODULE ws2 = LoadLibraryW(L"ws2_32.dll");
            auto wsaStartup = ws2 ? reinterpret_cast<FnWSAStartup>(GetProcAddress(ws2, "WSAStartup")) : nullptr;
            auto getAddrInfo = ws2 ? reinterpret_cast<FnGetAddrInfo>(GetProcAddress(ws2, "getaddrinfo")) : nullptr;
            auto freeAddrInfo = ws2 ? reinterpret_cast<FnFreeAddrInfo>(GetProcAddress(ws2, "freeaddrinfo")) : nullptr;
            WSADATA wsa = {};
            if (!wsaStartup || !getAddrInfo || !freeAddrInfo || wsaStartup(MAKEWORD(2, 2), &wsa) != 0)
            {
                Log("LOGINKEY listed reason=ws2_unavailable");
                return false;
            }

            // No WSACleanup: winsock stays initialised for the game, which uses it anyway.
            for (const ListedServer& server : servers)
            {
                if (server.port != endpoint.port)
                {
                    continue;
                }

                ADDRINFOA hints = {};
                hints.ai_family = AF_INET;
                hints.ai_socktype = SOCK_STREAM;
                PADDRINFOA result = nullptr;
                const int error = getAddrInfo(server.host.c_str(), nullptr, &hints, &result);
                if (error != 0)
                {
                    Log("LOGINKEY listed host=%s resolve=FAIL code=%d", server.host.c_str(), error);
                    continue;
                }

                bool match = false;
                for (PADDRINFOA address = result; address && !match; address = address->ai_next)
                {
                    const unsigned long network =
                        reinterpret_cast<const sockaddr_in*>(address->ai_addr)->sin_addr.S_un.S_addr;
                    const unsigned long host =
                        ((network & 0xfful) << 24) | ((network & 0xff00ul) << 8) |
                        ((network >> 8) & 0xff00ul) | (network >> 24);
                    match = endpoint.ip == host || endpoint.ip == network;
                }

                freeAddrInfo(result);
                if (match)
                {
                    Log("LOGINKEY listed host=%s port=%lu match=YES", server.host.c_str(), server.port);
                    return true;
                }
            }

            return false;
        }

        std::string Hex(const std::vector<unsigned char>& bytes)
        {
            static constexpr char Digits[] = "0123456789abcdef";
            std::string result;
            result.reserve(bytes.size() * 2);
            for (unsigned char value : bytes)
            {
                result.push_back(Digits[value >> 4]);
                result.push_back(Digits[value & 0x0f]);
            }

            return result;
        }

        bool ReadFileBytes(const wchar_t* path, std::vector<unsigned char>& bytes)
        {
            HANDLE file = CreateFileW(
                path,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            LARGE_INTEGER size = {};
            if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > 64ll * 1024ll * 1024ll)
            {
                CloseHandle(file);
                return false;
            }

            bytes.resize(static_cast<size_t>(size.QuadPart));
            DWORD read = 0;
            bool ok = ReadFile(
                file,
                bytes.data(),
                static_cast<DWORD>(bytes.size()),
                &read,
                nullptr) && read == bytes.size();
            CloseHandle(file);
            return ok;
        }

        bool Sha256(const std::vector<unsigned char>& bytes, std::string& hash)
        {
            BCRYPT_ALG_HANDLE algorithm = nullptr;
            BCRYPT_HASH_HANDLE hasher = nullptr;
            DWORD resultLength = 0;
            DWORD hashLength = 0;
            bool ok = false;

            if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) != 0)
            {
                return false;
            }

            if (BCryptGetProperty(
                    algorithm,
                    BCRYPT_HASH_LENGTH,
                    reinterpret_cast<PUCHAR>(&hashLength),
                    sizeof(hashLength),
                    &resultLength,
                    0) != 0)
            {
                BCryptCloseAlgorithmProvider(algorithm, 0);
                return false;
            }

            std::vector<unsigned char> digest(hashLength);
            if (BCryptCreateHash(algorithm, &hasher, nullptr, 0, nullptr, 0, 0) == 0 &&
                BCryptHashData(
                    hasher,
                    const_cast<PUCHAR>(bytes.data()),
                    static_cast<ULONG>(bytes.size()),
                    0) == 0 &&
                BCryptFinishHash(hasher, digest.data(), static_cast<ULONG>(digest.size()), 0) == 0)
            {
                hash = Hex(digest);
                ok = true;
            }

            if (hasher)
            {
                BCryptDestroyHash(hasher);
            }

            BCryptCloseAlgorithmProvider(algorithm, 0);
            return ok;
        }

        std::vector<size_t> FindAll(const unsigned char* haystack, size_t haystackLength, const char* needle)
        {
            std::vector<size_t> matches;
            if (haystackLength < PublicKeyLength)
            {
                return matches;
            }

            for (size_t offset = 0; offset <= haystackLength - PublicKeyLength; ++offset)
            {
                if (std::memcmp(haystack + offset, needle, PublicKeyLength) == 0)
                {
                    matches.push_back(offset);
                }
            }

            return matches;
        }

        MatchAnalysis AnalyzeBytes(const unsigned char* bytes, size_t length)
        {
            MatchAnalysis analysis = {};
            analysis.funcomOffsets = FindAll(bytes, length, FuncomPublicKey);
            analysis.aoRebirthOffsets = FindAll(bytes, length, AoRebirthPublicKey);

            if (analysis.funcomOffsets.size() == 1 && analysis.aoRebirthOffsets.empty())
            {
                analysis.state = PatchState::Original;
            }
            else if (analysis.funcomOffsets.empty() && analysis.aoRebirthOffsets.size() == 1)
            {
                analysis.state = PatchState::Patched;
            }
            else
            {
                analysis.state = PatchState::Unknown;
            }

            return analysis;
        }

        bool PatchBuffer(std::vector<unsigned char>& bytes)
        {
            std::vector<unsigned char> original = bytes;
            MatchAnalysis analysis = AnalyzeBytes(bytes.data(), bytes.size());
            if (analysis.state != PatchState::Original)
            {
                return false;
            }

            std::memcpy(bytes.data() + analysis.funcomOffsets[0], AoRebirthPublicKey, PublicKeyLength);
            MatchAnalysis after = AnalyzeBytes(bytes.data(), bytes.size());
            if (after.state != PatchState::Patched || after.aoRebirthOffsets[0] != analysis.funcomOffsets[0])
            {
                return false;
            }

            for (size_t index = 0; index < bytes.size(); ++index)
            {
                bool inside = index >= analysis.funcomOffsets[0] &&
                    index < analysis.funcomOffsets[0] + PublicKeyLength;
                if (!inside && bytes[index] != original[index])
                {
                    return false;
                }
            }

            return true;
        }

        DWORD ModuleImageSize(HMODULE module)
        {
            auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(module);
            if (!dos || dos->e_magic != IMAGE_DOS_SIGNATURE)
            {
                return 0;
            }

            auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS*>(
                reinterpret_cast<const unsigned char*>(module) + dos->e_lfanew);
            if (!nt || nt->Signature != IMAGE_NT_SIGNATURE)
            {
                return 0;
            }

            return nt->OptionalHeader.SizeOfImage;
        }

        bool ValidateModuleFile(const TargetModule& target, HMODULE module)
        {
            wchar_t path[MAX_PATH] = {};
            DWORD length = GetModuleFileNameW(module, path, static_cast<DWORD>(sizeof(path) / sizeof(path[0])));
            if (length == 0 || length >= sizeof(path) / sizeof(path[0]))
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=module_path", target.moduleName);
                return false;
            }

            std::vector<unsigned char> fileBytes;
            if (!ReadFileBytes(path, fileBytes))
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=file_read", target.moduleName);
                return false;
            }

            if (fileBytes.size() != target.expectedFileSize)
            {
                Log(
                    "LOGINKEY module=%ls patch=BLOCKED reason=file_size actual=%lu expected=%lu",
                    target.moduleName,
                    static_cast<unsigned long>(fileBytes.size()),
                    target.expectedFileSize);
                return false;
            }

            std::string hash;
            if (!Sha256(fileBytes, hash))
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=file_hash", target.moduleName);
                return false;
            }

            if (_stricmp(hash.c_str(), target.expectedSha256) != 0)
            {
                Log(
                    "LOGINKEY module=%ls patch=BLOCKED reason=unsupported_build sha256=%s",
                    target.moduleName,
                    hash.c_str());
                return false;
            }

            return true;
        }

        bool PatchLoadedModule(const TargetModule& target)
        {
            HMODULE module = GetModuleHandleW(target.moduleName);
            if (!module)
            {
                return false;
            }

            if (!ValidateModuleFile(target, module))
            {
                return true;
            }

            DWORD imageSize = ModuleImageSize(module);
            if (imageSize == 0)
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=image_size", target.moduleName);
                return true;
            }

            auto* base = reinterpret_cast<unsigned char*>(module);
            MatchAnalysis analysis = AnalyzeBytes(base, imageSize);
            Log(
                "LOGINKEY module=%ls profile=%s funcomMatches=%lu aorebirthMatches=%lu",
                target.moduleName,
                target.profileId,
                static_cast<unsigned long>(analysis.funcomOffsets.size()),
                static_cast<unsigned long>(analysis.aoRebirthOffsets.size()));

            if (analysis.state == PatchState::Patched)
            {
                Log("LOGINKEY module=%ls state=Patched patch=PASS reason=already_patched", target.moduleName);
                return true;
            }

            if (analysis.state != PatchState::Original)
            {
                Log("LOGINKEY module=%ls state=Unknown patch=BLOCKED reason=match_count", target.moduleName);
                return true;
            }

            unsigned char* targetAddress = base + analysis.funcomOffsets[0];
            DWORD oldProtection = 0;
            if (!VirtualProtect(targetAddress, PublicKeyLength, PAGE_EXECUTE_READWRITE, &oldProtection))
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=protect code=%lu", target.moduleName, GetLastError());
                return true;
            }

            bool restored = false;
            bool verified = false;
            std::memcpy(targetAddress, AoRebirthPublicKey, PublicKeyLength);
            FlushInstructionCache(GetCurrentProcess(), targetAddress, PublicKeyLength);

            DWORD ignored = 0;
            restored = VirtualProtect(targetAddress, PublicKeyLength, oldProtection, &ignored) != FALSE;
            verified = std::memcmp(targetAddress, AoRebirthPublicKey, PublicKeyLength) == 0;

            if (!restored)
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=restore_protection code=%lu", target.moduleName, GetLastError());
                return true;
            }

            if (!verified)
            {
                Log("LOGINKEY module=%ls patch=BLOCKED reason=verify", target.moduleName);
                return true;
            }

            Log(
                "LOGINKEY module=%ls state=Original rva=0x%lx patch=PASS verify=PASS",
                target.moduleName,
                static_cast<unsigned long>(analysis.funcomOffsets[0]));
            return true;
        }

        DWORD WINAPI LoginKeyPatchWorker(LPVOID)
        {
            Policy policy = ResolvePolicy();
            Log("LOGINKEY policy=%s", PolicyName(policy));

            if (policy == Policy::Off)
            {
                Log("LOGINKEY patch=SKIP reason=policy_off");
                return 0;
            }

            if (policy == Policy::Auto)
            {
                LaunchEndpoint endpoint = {};
                if (!TryParseLaunchEndpoint(GetCommandLineW(), endpoint))
                {
                    Log("LOGINKEY patch=SKIP reason=auto_endpoint_unavailable");
                    return 0;
                }

                if (IsAoRebirthEndpoint(endpoint))
                {
                    Log("LOGINKEY patch=ARMED reason=auto_endpoint_aorebirth port=%lu", endpoint.port);
                }
                else if (IsListedServerEndpoint(endpoint))
                {
                    Log("LOGINKEY patch=ARMED reason=auto_endpoint_listed port=%lu", endpoint.port);
                }
                else
                {
                    Log(
                        "LOGINKEY patch=SKIP reason=auto_endpoint_not_aorebirth ipFormat=%s port=%lu",
                        endpoint.dottedIp ? "dotted" : "numeric",
                        endpoint.port);
                    return 0;
                }
            }

            bool completed[sizeof(Targets) / sizeof(Targets[0])] = {};
            for (int attempt = 0; attempt < 600; ++attempt)
            {
                bool allCompleted = true;
                for (size_t index = 0; index < sizeof(Targets) / sizeof(Targets[0]); ++index)
                {
                    if (!completed[index])
                    {
                        completed[index] = PatchLoadedModule(Targets[index]);
                    }

                    allCompleted = allCompleted && completed[index];
                }

                if (allCompleted)
                {
                    Log("LOGINKEY patch=COMPLETE");
                    return 0;
                }

                Sleep(100);
            }

            for (size_t index = 0; index < sizeof(Targets) / sizeof(Targets[0]); ++index)
            {
                if (!completed[index])
                {
                    Log("LOGINKEY module=%ls patch=BLOCKED reason=module_timeout", Targets[index].moduleName);
                }
            }

            return 1;
        }
    }

    bool StartLoginKeyPatchWorker()
    {
        if (InterlockedCompareExchange(&LoginKeyWorkerStarted, 1, 0) != 0)
        {
            return true;
        }

        HANDLE worker = CreateThread(nullptr, 0, LoginKeyPatchWorker, nullptr, 0, nullptr);
        if (!worker)
        {
            Log("LOGINKEY patch=BLOCKED reason=worker_create code=%lu", GetLastError());
            return false;
        }

        CloseHandle(worker);
        return true;
    }

        bool RunLoginKeyPatchSelfTest()
        {
            if (PublicKeyLength != sizeof(AoRebirthPublicKey) - 1)
            {
                return false;
            }

            LaunchEndpoint endpoint = {};
            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA35151902 IP7500 DUprivate", endpoint) ||
                !IsAoRebirthEndpoint(endpoint) ||
                endpoint.dottedIp)
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA509614082 IP7500 DUprivate", endpoint) ||
                !IsAoRebirthEndpoint(endpoint) ||
                endpoint.dottedIp)
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7500 DUprivate", endpoint) ||
                !IsAoRebirthEndpoint(endpoint) ||
                !endpoint.dottedIp)
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA127.0.0.1 IP7500 DUprivate", endpoint) ||
                !IsAoRebirthEndpoint(endpoint) ||
                !endpoint.dottedIp)
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2130706433 IP7500 DUprivate", endpoint) ||
                !IsAoRebirthEndpoint(endpoint) ||
                endpoint.dottedIp)
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA35151902 IP80 DUprivate", endpoint) ||
                IsAoRebirthEndpoint(endpoint))
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7505 DUofficial", endpoint) ||
                IsAoRebirthEndpoint(endpoint))
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7506 DUofficial", endpoint) ||
                IsAoRebirthEndpoint(endpoint))
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA127.0.0.1 IP80 DUunknown", endpoint) ||
                IsAoRebirthEndpoint(endpoint))
            {
                return false;
            }

            if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA123456 IP7500 DUofficial", endpoint) ||
                IsAoRebirthEndpoint(endpoint))
            {
                return false;
            }

            if (TryParseLaunchEndpoint(L"anarchyonline.exe DUmissing", endpoint))
            {
                return false;
            }

            const std::vector<ListedServer> listed = ParseListedServers(
                "# servers\r\n ao.example.net:7500 \r\n\r\n10.0.0.5:7501 # lan\nbad\nnoport:\n:7500\nhost:99999\nhost:12x\n");
            if (listed.size() != 2 ||
                listed[0].host != "ao.example.net" || listed[0].port != 7500 ||
                listed[1].host != "10.0.0.5" || listed[1].port != 7501)
            {
                return false;
            }

            std::vector<unsigned char> original;
        const char prefix[] = "prefix";
        const char suffix[] = "suffix";
        original.insert(original.end(), prefix, prefix + sizeof(prefix) - 1);
        original.insert(original.end(), FuncomPublicKey, FuncomPublicKey + PublicKeyLength);
        original.insert(original.end(), suffix, suffix + sizeof(suffix) - 1);

        MatchAnalysis originalAnalysis = AnalyzeBytes(original.data(), original.size());
        if (originalAnalysis.state != PatchState::Original ||
            originalAnalysis.funcomOffsets.size() != 1 ||
            !originalAnalysis.aoRebirthOffsets.empty())
        {
            return false;
        }

        std::vector<unsigned char> patched = original;
        if (!PatchBuffer(patched))
        {
            return false;
        }

        MatchAnalysis patchedAnalysis = AnalyzeBytes(patched.data(), patched.size());
        if (patchedAnalysis.state != PatchState::Patched ||
            patchedAnalysis.aoRebirthOffsets.size() != 1)
        {
            return false;
        }

        std::vector<unsigned char> unknown(original.size(), static_cast<unsigned char>('x'));
        if (AnalyzeBytes(unknown.data(), unknown.size()).state != PatchState::Unknown)
        {
            return false;
        }

        std::vector<unsigned char> zeroMatch = unknown;
        if (PatchBuffer(zeroMatch))
        {
            return false;
        }

        std::vector<unsigned char> multi = original;
        multi.insert(multi.end(), FuncomPublicKey, FuncomPublicKey + PublicKeyLength);
        if (AnalyzeBytes(multi.data(), multi.size()).state != PatchState::Unknown)
        {
            return false;
        }

        if (PatchBuffer(multi))
        {
            return false;
        }

        if (ResolvePolicy() == Policy::Off)
        {
            return true;
        }

        return true;
    }
}
