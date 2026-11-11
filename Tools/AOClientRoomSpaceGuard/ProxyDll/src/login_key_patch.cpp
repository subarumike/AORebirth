#include "login_key_patch.h"
#include "logging.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <bcrypt.h>
#include <shellapi.h>

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <string>
#include <string_view>
#include <utility>
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
        constexpr wchar_t LoginServersFile[] = L"aorebirth-patch\\servers.txt";
        constexpr LONGLONG MaximumServerConfigBytes = 64ll * 1024ll;

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

        struct LoginServer
        {
            std::string host;
            unsigned long port;
            DailyLoginMode dailyMode;
            bool dailyDefault;
        };

        struct LoginServerConfig
        {
            std::vector<LoginServer> servers;
            std::string originalDailyHost;
            std::vector<std::string> dailyHosts;
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
            if (!text || *text < L'0' || *text > L'9')
            {
                return false;
            }

            unsigned long parsed = 0;
            while (*text >= L'0' && *text <= L'9')
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

        bool TryParseLaunchEndpoint(const wchar_t* commandLine, LaunchEndpoint& endpoint)
        {
            endpoint = {};
            if (!commandLine || *commandLine == L'\0')
            {
                return false;
            }

            int count = 0;
            wchar_t** arguments = CommandLineToArgvW(commandLine, &count);
            if (!arguments)
            {
                return false;
            }

            bool valid = true;
            for (int i = 1; i < count && valid; ++i)
            {
                const wchar_t* argument = arguments[i];
                const wchar_t* end = nullptr;
                if (wcsncmp(argument, L"IA", 2) == 0)
                {
                    bool dotted = ParseDottedIpToken(argument + 2, endpoint.ip, &end);
                    valid = !endpoint.hasIp &&
                        (dotted || ParseUnsignedToken(argument + 2, endpoint.ip, &end)) && *end == L'\0';
                    endpoint.hasIp = valid;
                    endpoint.dottedIp = dotted;
                }
                else if (wcsncmp(argument, L"IP", 2) == 0)
                {
                    valid = !endpoint.hasPort &&
                        ParseUnsignedToken(argument + 2, endpoint.port, &end) && *end == L'\0' &&
                        endpoint.port > 0 && endpoint.port <= 65535;
                    endpoint.hasPort = valid;
                }
            }
            LocalFree(arguments);
            return valid && endpoint.hasIp && endpoint.hasPort;
        }

        bool IsEndpointAddress(const LaunchEndpoint& endpoint, unsigned long hostOrder, unsigned long networkOrder)
        {
            // Dotted input explicitly specifies octet order. Older numeric launchers use either representation.
            return endpoint.ip == hostOrder || (!endpoint.dottedIp && endpoint.ip == networkOrder);
        }

        bool IsServerHost(std::string_view host)
        {
            if (host.empty() || host.size() > 253)
            {
                return false;
            }

            if (host.find_first_not_of("0123456789.") == std::string_view::npos)
            {
                const std::wstring wide(host.begin(), host.end());
                unsigned long ignored = 0;
                const wchar_t* end = nullptr;
                return ParseDottedIpToken(wide.c_str(), ignored, &end) && *end == L'\0';
            }

            while (!host.empty())
            {
                const size_t dot = host.find('.');
                const std::string_view label = host.substr(0, dot);
                if (label.empty() || label.size() > 63 || label.front() == '-' || label.back() == '-' ||
                    label.find_first_not_of("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-") !=
                        std::string_view::npos)
                {
                    return false;
                }

                if (dot == std::string_view::npos)
                {
                    return true;
                }
                host.remove_prefix(dot + 1);
            }
            return false;
        }

        bool ParseDailyHost(std::string_view host, std::string& name)
        {
            if (!IsServerHost(host))
            {
                return false;
            }
            name.assign(host);
            std::transform(name.begin(), name.end(), name.begin(), [](char ch)
            {
                return ch >= 'A' && ch <= 'Z' ? static_cast<char>(ch + 'a' - 'A') : ch;
            });
            return true;
        }

        bool ParseDailyHosts(std::string_view hosts, std::vector<std::string>& names)
        {
            for (;;)
            {
                const size_t comma = hosts.find(',');
                std::string name;
                if (!ParseDailyHost(hosts.substr(0, comma), name) ||
                    std::find(names.begin(), names.end(), name) != names.end())
                {
                    return false;
                }
                names.push_back(name);
                if (comma == std::string_view::npos)
                {
                    return true;
                }
                hosts.remove_prefix(comma + 1);
            }
        }

        bool ParseLoginServers(std::string_view text, LoginServerConfig& config)
        {
            LoginServerConfig parsed;
            bool hasDefault = false;
            bool needsDailyHosts = false;
            if (text.substr(0, 3) == "\xef\xbb\xbf")
            {
                text.remove_prefix(3);
            }
            while (!text.empty())
            {
                const size_t newline = text.find('\n');
                std::string_view line = text.substr(0, newline);
                text.remove_prefix(newline == std::string_view::npos ? text.size() : newline + 1);
                line = line.substr(0, line.find('#'));
                const size_t first = line.find_first_not_of(" \t\r");
                if (first == std::string_view::npos)
                {
                    continue;
                }
                line = line.substr(first, line.find_last_not_of(" \t\r") - first + 1);

                constexpr std::string_view OriginalDailyHostPrefix = "daily-original-host=";
                if (line.substr(0, OriginalDailyHostPrefix.size()) == OriginalDailyHostPrefix)
                {
                    if (!parsed.originalDailyHost.empty() ||
                        !ParseDailyHost(line.substr(OriginalDailyHostPrefix.size()), parsed.originalDailyHost))
                    {
                        return false;
                    }
                    continue;
                }

                constexpr std::string_view DailyHostsPrefix = "daily-hosts=";
                if (line.substr(0, DailyHostsPrefix.size()) == DailyHostsPrefix)
                {
                    if (!parsed.dailyHosts.empty() ||
                        !ParseDailyHosts(line.substr(DailyHostsPrefix.size()), parsed.dailyHosts))
                    {
                        return false;
                    }
                    continue;
                }

                const size_t space = line.find_first_of(" \t");
                const std::string_view endpoint = line.substr(0, space);
                const size_t colon = endpoint.find(':');
                if (colon == std::string_view::npos || !IsServerHost(endpoint.substr(0, colon)))
                {
                    return false;
                }

                const std::string_view digits = endpoint.substr(colon + 1);
                if (digits.empty() || digits.size() > 5 || digits.find_first_not_of("0123456789") != std::string_view::npos)
                {
                    return false;
                }
                unsigned long port = 0;
                for (const char digit : digits)
                {
                    port = port * 10ul + static_cast<unsigned long>(digit - '0');
                }
                if (port == 0 || port > 65535)
                {
                    return false;
                }

                DailyLoginMode dailyMode = DailyLoginMode::Off;
                bool dailyDefault = false;
                if (space != std::string_view::npos)
                {
                    const std::string_view options = line.substr(line.find_first_not_of(" \t", space));
                    const size_t optionEnd = options.find_first_of(" \t");
                    const std::string_view option = options.substr(0, optionEnd);
                    if (option == "rewrite-http")
                    {
                        dailyMode = DailyLoginMode::RewriteHttp;
                    }
                    else if (option == "preserve-http")
                    {
                        dailyMode = DailyLoginMode::PreserveHttp;
                    }
                    else
                    {
                        return false;
                    }
                    needsDailyHosts = true;
                    if (optionEnd != std::string_view::npos)
                    {
                        if (hasDefault || options.substr(options.find_first_not_of(" \t", optionEnd)) != "default")
                        {
                            return false;
                        }
                        dailyDefault = hasDefault = true;
                    }
                }
                const std::string host(endpoint.substr(0, colon));
                if (std::any_of(parsed.servers.begin(), parsed.servers.end(), [&](const LoginServer& row)
                    { return row.port == port && _stricmp(row.host.c_str(), host.c_str()) == 0; }))
                {
                    return false;
                }
                parsed.servers.push_back({ host, port, dailyMode, dailyDefault });
            }
            if (parsed.servers.empty())
            {
                return false;
            }
            if (needsDailyHosts || !parsed.originalDailyHost.empty() || !parsed.dailyHosts.empty())
            {
                // A rewritten Host header must still fit the original request's byte width.
                if (parsed.originalDailyHost.empty() || parsed.dailyHosts.empty() ||
                    std::any_of(parsed.dailyHosts.begin(), parsed.dailyHosts.end(), [&](const std::string& host)
                    {
                        return host == parsed.originalDailyHost || host.size() > parsed.originalDailyHost.size();
                    }))
                {
                    return false;
                }
            }
            config = std::move(parsed);
            return true;
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

        bool ReadFileBytes(const wchar_t* path, std::vector<unsigned char>& bytes,
            LONGLONG maximumSize = 64ll * 1024ll * 1024ll)
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
            if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > maximumSize)
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

        bool ReadLoginServers(const wchar_t* path, LoginServerConfig& config)
        {
            std::vector<unsigned char> bytes;
            return ReadFileBytes(path, bytes, MaximumServerConfigBytes) &&
                ParseLoginServers(
                    std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()), config);
        }

        bool TryLoadLoginServers(LoginServerConfig& config)
        {
            HMODULE module = nullptr;
            wchar_t path[MAX_PATH] = {};
            if (!GetModuleHandleExW(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                    LoginServersFile, &module))
            {
                return false;
            }
            const DWORD length = GetModuleFileNameW(module, path, MAX_PATH);
            if (length == 0 || length >= MAX_PATH)
            {
                return false;
            }
            std::wstring filename(path, length);
            const size_t separator = filename.find_last_of(L"\\/");
            if (separator == std::wstring::npos)
            {
                return false;
            }
            filename.resize(separator + 1);
            filename += LoginServersFile;

            return ReadLoginServers(filename.c_str(), config);
        }

        bool ResolveServerAddress(const LoginServer& server, const LaunchEndpoint* endpoint,
            unsigned long& host, bool& ambiguous)
        {
            ADDRINFOA hints = {};
            hints.ai_family = AF_INET;
            hints.ai_socktype = SOCK_STREAM;
            ADDRINFOA* addresses = nullptr;
            if (getaddrinfo(server.host.c_str(), nullptr, &hints, &addresses) != 0)
            {
                return false;
            }

            bool found = false;
            for (const ADDRINFOA* address = addresses; address; address = address->ai_next)
            {
                if (address->ai_family != AF_INET || address->ai_addrlen < sizeof(sockaddr_in))
                {
                    continue;
                }
                const auto* ipv4 = reinterpret_cast<const sockaddr_in*>(address->ai_addr);
                const unsigned long networkOrder = ipv4->sin_addr.s_addr;
                const unsigned long hostOrder = ntohl(networkOrder);
                if (endpoint && !IsEndpointAddress(*endpoint, hostOrder, networkOrder))
                {
                    continue;
                }
                if (found && host != hostOrder)
                {
                    ambiguous = true;
                    break;
                }
                host = hostOrder;
                found = true;
            }
            freeaddrinfo(addresses);
            return found && !ambiguous;
        }

        bool SelectConfiguredServer(const LoginServerConfig& config, const wchar_t* commandLine,
            bool allowDailyDefault, LoginServerSelection& selection, const char*& failure)
        {
            if (config.servers.empty())
            {
                failure = "server_config_missing_or_invalid";
                return false;
            }

            LaunchEndpoint endpoint = {};
            const bool hasEndpoint = TryParseLaunchEndpoint(commandLine, endpoint);
            if (!hasEndpoint && !allowDailyDefault)
            {
                failure = "auto_endpoint_unavailable";
                return false;
            }

            WSADATA winsock = {};
            if (WSAStartup(MAKEWORD(2, 2), &winsock) != 0)
            {
                failure = "server_resolution_unavailable";
                return false;
            }
            const LoginServer* matched = nullptr;
            const LoginServer* dailyDefault = nullptr;
            unsigned long selectedAddress = 0;
            bool ambiguous = false;
            for (const LoginServer& server : config.servers)
            {
                if (server.dailyDefault)
                {
                    dailyDefault = &server;
                }
                unsigned long address = 0;
                if (hasEndpoint && server.port == endpoint.port)
                {
                    const bool found = ResolveServerAddress(server, &endpoint, address, ambiguous);
                    if (ambiguous || (found && matched))
                    {
                        ambiguous = true;
                        break;
                    }
                    if (found)
                    {
                        matched = &server;
                        selectedAddress = address;
                    }
                }
            }
            const bool matchedEndpoint = matched != nullptr;
            if (!ambiguous && !matched && allowDailyDefault && dailyDefault &&
                ResolveServerAddress(*dailyDefault, nullptr, selectedAddress, ambiguous))
            {
                matched = dailyDefault;
            }
            WSACleanup();
            if (ambiguous || !matched)
            {
                failure = ambiguous ? "ambiguous_server_match" : "auto_endpoint_not_aorebirth";
                return false;
            }

            selection = {};
            for (size_t i = 0; i < selection.addressBytes.size(); ++i)
            {
                const auto octet = static_cast<unsigned char>(selectedAddress >> (24 - i * 8));
                if (i != 0)
                {
                    selection.address += '.';
                }
                selection.address += std::to_string(octet);
                selection.addressBytes[i] = octet;
            }
            selection.wideAddress.assign(selection.address.begin(), selection.address.end());
            selection.dailyMode = matched->dailyMode;
            selection.originalDailyHost = config.originalDailyHost;
            selection.dailyHosts = config.dailyHosts;
            selection.port = matched->port;
            selection.matchedLaunchEndpoint = matchedEndpoint;
            return true;
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
                LoginServerSelection selection = {};
                const char* failure = nullptr;
                if (!TrySelectLoginServer(false, selection, failure))
                {
                    Log("LOGINKEY patch=SKIP reason=%s", failure);
                    return 0;
                }

                Log("LOGINKEY patch=ARMED reason=auto_endpoint_listed port=%lu", selection.port);
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

    bool TrySelectLoginServer(bool allowDailyDefault,
        LoginServerSelection& selection, const char*& failure)
    {
        static const LoginServerConfig config = []()
        {
            LoginServerConfig loaded;
            TryLoadLoginServers(loaded);
            return loaded;
        }();
        struct CachedSelection
        {
            LoginServerSelection target = {};
            const char* failure = nullptr;
            bool found = false;
        };
        // Both hooks use the same match even if DNS changes after one starts installing imports.
        static const CachedSelection launch = []()
        {
            CachedSelection result;
            result.found = SelectConfiguredServer(
                config, GetCommandLineW(), false, result.target, result.failure);
            return result;
        }();
        if (launch.found || !allowDailyDefault ||
            std::strcmp(launch.failure, "ambiguous_server_match") == 0)
        {
            selection = launch.target;
            failure = launch.failure;
            return launch.found;
        }
        static const CachedSelection fallback = []()
        {
            CachedSelection result;
            result.found = SelectConfiguredServer(
                config, nullptr, true, result.target, result.failure);
            return result;
        }();
        selection = fallback.target;
        failure = fallback.failure;
        return fallback.found;
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

        bool RunLoginKeyPatchSelfTest(const wchar_t* packagedServers)
        {
            if (PublicKeyLength != sizeof(AoRebirthPublicKey) - 1)
            {
                return false;
            }

            LoginServerConfig shipped;
            if (!packagedServers || !ReadLoginServers(packagedServers, shipped))
            {
                return false;
            }

            LoginServerConfig config;
            if (!ParseLoginServers(
                    "daily-original-host=original-daily.example\n"
                    "daily-hosts=daily.example,alternate.example\n"
                    "192.0.2.10:7500 rewrite-http default\n"
                    "127.0.0.1:7500 preserve-http\n"
                    "198.51.100.1:7501 # key only\n", config))
            {
                return false;
            }

            const wchar_t* productionCommands[] =
            {
                L"anarchyonline.exe IA192.0.2.10 IP7500 DUprivate",
                L"anarchyonline.exe IA3221225994 IP7500 DUprivate",
                L"anarchyonline.exe IA167903424 IP7500 DUprivate"
            };
            LoginServerSelection selection = {};
            const char* failure = nullptr;
            for (const wchar_t* command : productionCommands)
            {
                if (!SelectConfiguredServer(config, command, false, selection, failure) ||
                    !selection.matchedLaunchEndpoint || selection.address != "192.0.2.10" ||
                    selection.dailyMode != DailyLoginMode::RewriteHttp ||
                    selection.originalDailyHost != "original-daily.example" ||
                    selection.dailyHosts.front() != "daily.example")
                {
                    return false;
                }
            }

            if (!SelectConfiguredServer(config, L"anarchyonline.exe IA127.0.0.1 IP7500", false, selection, failure) ||
                selection.address != "127.0.0.1" || selection.dailyMode != DailyLoginMode::PreserveHttp ||
                !SelectConfiguredServer(config, L"anarchyonline.exe IA198.51.100.1 IP7501", false, selection, failure) ||
                selection.dailyMode != DailyLoginMode::Off)
            {
                return false;
            }

            const wchar_t* rejectedCommands[] =
            {
                L"anarchyonline.exe IA10.2.0.192 IP7500",
                L"anarchyonline.exe IA192.0.2.10 IP7505",
                L"anarchyonline.exe IA192.0.2.10 IP7506",
                L"anarchyonline.exe IA127.0.0.1 IP80",
                L"anarchyonline.exe IA123456 IP7500",
                L"anarchyonline.exe DUmissing",
                L"anarchyonline.exe BADIA192.0.2.10 IP7500",
                L"anarchyonline.exe IA192.0.2.10 IP7500 IP7501"
            };
            for (const wchar_t* command : rejectedCommands)
            {
                if (SelectConfiguredServer(config, command, false, selection, failure))
                {
                    return false;
                }
            }

            LoginServerConfig invalid;
            LoginServerConfig byteOrderCollision;
            if (!SelectConfiguredServer(config, L"anarchyonline.exe DUmissing", true, selection, failure) ||
                selection.matchedLaunchEndpoint || selection.address != "192.0.2.10" ||
                !ParseLoginServers("192.0.2.10:7500\n10.2.0.192:7500\n", byteOrderCollision) ||
                SelectConfiguredServer(byteOrderCollision, productionCommands[1], false, selection, failure) ||
                !SelectConfiguredServer(byteOrderCollision, productionCommands[0], false, selection, failure) ||
                selection.address != "192.0.2.10" ||
                ParseLoginServers("192.0.2.10:7500\nbad:99999\n", invalid) ||
                ParseLoginServers("192.0.2.10:7500 rewrite-http default\n", invalid) ||
                ParseLoginServers("daily-hosts=long.example\n192.0.2.10:7500 rewrite-http\n", invalid) ||
                ParseLoginServers("daily-original-host=a.test\ndaily-hosts=long.example\n192.0.2.10:7500 rewrite-http\n", invalid) ||
                ParseLoginServers("192.0.2.10:7500\n192.0.2.10:7500\n", invalid) ||
                ParseLoginServers("# empty list\n", invalid))
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
