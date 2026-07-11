#include <windows.h>
#include <tlhelp32.h>
#include <bcrypt.h>

#include <array>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include <iterator>
#include <string>
#include <vector>

namespace
{
    constexpr wchar_t ProductName[] = L"AORoomSpaceFix";
    constexpr wchar_t ProductVersion[] = L"1";
    constexpr wchar_t ManifestName[] = L"SHA256SUMS.txt";
    constexpr wchar_t MarkerName[] = L"AORoomSpaceFix.install";
    constexpr wchar_t ProxyName[] = L"version.dll";

    constexpr const wchar_t* PayloadNames[] =
    {
        L"AOReloaded-MIT.txt",
        L"AORoomSpaceFixDeploy.exe",
        L"Install.cmd",
        L"README.txt",
        L"Uninstall.cmd",
        L"version.dll"
    };

    constexpr wchar_t NewClientN3Hash[] =
        L"E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168";
    constexpr wchar_t OldClientN3Hash[] =
        L"8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB";

    class UniqueHandle
    {
    public:
        UniqueHandle() noexcept = default;
        explicit UniqueHandle(HANDLE value) noexcept : value_(value) {}
        ~UniqueHandle()
        {
            Reset();
        }

        UniqueHandle(const UniqueHandle&) = delete;
        UniqueHandle& operator=(const UniqueHandle&) = delete;

        HANDLE Get() const noexcept
        {
            return value_;
        }

        bool IsValid() const noexcept
        {
            return value_ != nullptr && value_ != INVALID_HANDLE_VALUE;
        }

        void Reset(HANDLE value = INVALID_HANDLE_VALUE) noexcept
        {
            if (IsValid())
            {
                CloseHandle(value_);
            }
            value_ = value;
        }

    private:
        HANDLE value_ = INVALID_HANDLE_VALUE;
    };

    struct Marker
    {
        std::wstring proxyHash;
        std::wstring n3Hash;
    };

    bool IsApprovedN3Hash(const std::wstring& hash)
    {
        return hash == NewClientN3Hash || hash == OldClientN3Hash;
    }

    bool IsUpperHex64(const std::wstring& value)
    {
        if (value.size() != 64)
        {
            return false;
        }

        for (wchar_t character : value)
        {
            if (!((character >= L'0' && character <= L'9') ||
                  (character >= L'A' && character <= L'F')))
            {
                return false;
            }
        }

        return true;
    }

    std::wstring Combine(const std::wstring& root, const wchar_t* name)
    {
        if (!root.empty() && (root.back() == L'\\' || root.back() == L'/'))
        {
            return root + name;
        }
        return root + L"\\" + name;
    }

    bool NormalizeDirectory(const wchar_t* input, std::wstring& output)
    {
        DWORD required = GetFullPathNameW(input, 0, nullptr, nullptr);
        if (required == 0)
        {
            return false;
        }

        std::vector<wchar_t> buffer(static_cast<size_t>(required) + 1U);
        DWORD written = GetFullPathNameW(
            input,
            static_cast<DWORD>(buffer.size()),
            buffer.data(),
            nullptr);
        if (written == 0 || written >= buffer.size())
        {
            return false;
        }

        output.assign(buffer.data(), written);
        while (output.size() > 3 &&
               (output.back() == L'\\' || output.back() == L'/'))
        {
            output.pop_back();
        }

        DWORD attributes = GetFileAttributesW(output.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES &&
               (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0 &&
               (attributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0;
    }

    bool IsRegularFile(const std::wstring& path)
    {
        DWORD attributes = GetFileAttributesW(path.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES &&
               (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 &&
               (attributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0;
    }

    bool PathExists(const std::wstring& path)
    {
        return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    bool HashHandle(HANDLE file, std::array<BYTE, 32>& digest)
    {
        LARGE_INTEGER zero = {};
        if (!SetFilePointerEx(file, zero, nullptr, FILE_BEGIN))
        {
            return false;
        }

        BCRYPT_ALG_HANDLE algorithm = nullptr;
        BCRYPT_HASH_HANDLE hash = nullptr;
        DWORD objectLength = 0;
        DWORD resultLength = 0;
        bool success = false;

        NTSTATUS status = BCryptOpenAlgorithmProvider(
            &algorithm,
            BCRYPT_SHA256_ALGORITHM,
            nullptr,
            0);
        if (status < 0)
        {
            return false;
        }

        status = BCryptGetProperty(
            algorithm,
            BCRYPT_OBJECT_LENGTH,
            reinterpret_cast<PUCHAR>(&objectLength),
            sizeof(objectLength),
            &resultLength,
            0);
        if (status < 0 || objectLength == 0)
        {
            BCryptCloseAlgorithmProvider(algorithm, 0);
            return false;
        }

        std::vector<BYTE> hashObject(objectLength);
        status = BCryptCreateHash(
            algorithm,
            &hash,
            hashObject.data(),
            static_cast<ULONG>(hashObject.size()),
            nullptr,
            0,
            0);
        if (status >= 0)
        {
            std::array<BYTE, 64 * 1024> buffer = {};
            for (;;)
            {
                DWORD bytesRead = 0;
                if (!ReadFile(
                        file,
                        buffer.data(),
                        static_cast<DWORD>(buffer.size()),
                        &bytesRead,
                        nullptr))
                {
                    break;
                }

                if (bytesRead == 0)
                {
                    status = BCryptFinishHash(
                        hash,
                        digest.data(),
                        static_cast<ULONG>(digest.size()),
                        0);
                    success = status >= 0;
                    break;
                }

                status = BCryptHashData(hash, buffer.data(), bytesRead, 0);
                if (status < 0)
                {
                    break;
                }
            }
        }

        if (hash)
        {
            BCryptDestroyHash(hash);
        }
        BCryptCloseAlgorithmProvider(algorithm, 0);
        return success;
    }

    std::wstring DigestToHex(const std::array<BYTE, 32>& digest)
    {
        constexpr wchar_t Digits[] = L"0123456789ABCDEF";
        std::wstring result;
        result.reserve(digest.size() * 2U);
        for (BYTE value : digest)
        {
            result.push_back(Digits[(value >> 4U) & 0x0FU]);
            result.push_back(Digits[value & 0x0FU]);
        }
        return result;
    }

    bool HashPath(const std::wstring& path, std::wstring& result)
    {
        UniqueHandle file(CreateFileW(
            path.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN,
            nullptr));
        if (!file.IsValid())
        {
            return false;
        }

        std::array<BYTE, 32> digest = {};
        if (!HashHandle(file.Get(), digest))
        {
            return false;
        }

        result = DigestToHex(digest);
        return true;
    }

    bool ReadSmallHandle(HANDLE file, std::string& result)
    {
        LARGE_INTEGER size = {};
        if (!GetFileSizeEx(file, &size) || size.QuadPart < 0 || size.QuadPart > 65536)
        {
            return false;
        }

        LARGE_INTEGER zero = {};
        if (!SetFilePointerEx(file, zero, nullptr, FILE_BEGIN))
        {
            return false;
        }

        result.assign(static_cast<size_t>(size.QuadPart), '\0');
        DWORD total = 0;
        while (total < result.size())
        {
            DWORD remaining = static_cast<DWORD>(result.size() - total);
            DWORD bytesRead = 0;
            if (!ReadFile(file, &result[total], remaining, &bytesRead, nullptr) ||
                bytesRead == 0)
            {
                return false;
            }
            total += bytesRead;
        }

        return true;
    }

    bool ReadSmallPath(const std::wstring& path, std::string& result)
    {
        UniqueHandle file(CreateFileW(
            path.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr));
        return file.IsValid() && ReadSmallHandle(file.Get(), result);
    }

    bool WriteAll(HANDLE file, const std::string& content)
    {
        size_t offset = 0;
        while (offset < content.size())
        {
            size_t remaining = content.size() - offset;
            DWORD request = remaining > MAXDWORD
                ? MAXDWORD
                : static_cast<DWORD>(remaining);
            DWORD written = 0;
            if (!WriteFile(file, content.data() + offset, request, &written, nullptr) ||
                written == 0)
            {
                return false;
            }
            offset += written;
        }
        return FlushFileBuffers(file) != FALSE;
    }

    bool CopyToHandle(const std::wstring& sourcePath, HANDLE destination)
    {
        UniqueHandle source(CreateFileW(
            sourcePath.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN,
            nullptr));
        if (!source.IsValid())
        {
            return false;
        }

        LARGE_INTEGER zero = {};
        if (!SetFilePointerEx(destination, zero, nullptr, FILE_BEGIN) ||
            !SetEndOfFile(destination))
        {
            return false;
        }

        std::array<BYTE, 64 * 1024> buffer = {};
        for (;;)
        {
            DWORD bytesRead = 0;
            if (!ReadFile(
                    source.Get(),
                    buffer.data(),
                    static_cast<DWORD>(buffer.size()),
                    &bytesRead,
                    nullptr))
            {
                return false;
            }
            if (bytesRead == 0)
            {
                return FlushFileBuffers(destination) != FALSE;
            }

            DWORD offset = 0;
            while (offset < bytesRead)
            {
                DWORD written = 0;
                if (!WriteFile(
                        destination,
                        buffer.data() + offset,
                        bytesRead - offset,
                        &written,
                        nullptr) ||
                    written == 0)
                {
                    return false;
                }
                offset += written;
            }
        }
    }

    bool SetDeleteDisposition(HANDLE file, bool remove)
    {
        FILE_DISPOSITION_INFO disposition = {};
        disposition.DeleteFile = remove ? TRUE : FALSE;
        return SetFileInformationByHandle(
            file,
            FileDispositionInfo,
            &disposition,
            sizeof(disposition)) != FALSE;
    }

    bool RenameHandleNoReplace(HANDLE file, const std::wstring& target)
    {
        size_t nameBytes = target.size() * sizeof(wchar_t);
        // FILE_RENAME_INFO reports the path length separately, but FileName is
        // still documented as NUL-terminated. Keep the terminator inside the
        // buffer passed to SetFileInformationByHandle; otherwise the filesystem
        // can consume bytes beyond the allocation and append garbage to the name.
        size_t totalBytes =
            offsetof(FILE_RENAME_INFO, FileName) + nameBytes + sizeof(wchar_t);
        if (nameBytes > MAXDWORD || totalBytes > MAXDWORD)
        {
            return false;
        }

        std::vector<BYTE> buffer(totalBytes, 0);
        FILE_RENAME_INFO* rename = reinterpret_cast<FILE_RENAME_INFO*>(buffer.data());
        rename->ReplaceIfExists = FALSE;
        rename->RootDirectory = nullptr;
        rename->FileNameLength = static_cast<DWORD>(nameBytes);
        std::memcpy(rename->FileName, target.data(), nameBytes);
        return SetFileInformationByHandle(
            file,
            FileRenameInfo,
            rename,
            static_cast<DWORD>(buffer.size())) != FALSE;
    }

    bool CreateUniqueTemporaryFile(
        const std::wstring& root,
        const wchar_t* purpose,
        UniqueHandle& file,
        std::wstring& path)
    {
        for (unsigned int attempt = 0; attempt < 32; ++attempt)
        {
            wchar_t name[160] = {};
            if (swprintf_s(
                    name,
                    L".AORoomSpaceFix.%lu.%llu.%u.%s.tmp",
                    GetCurrentProcessId(),
                    static_cast<unsigned long long>(GetTickCount64()),
                    attempt,
                    purpose) < 0)
            {
                return false;
            }

            path = Combine(root, name);
            HANDLE handle = CreateFileW(
                path.c_str(),
                GENERIC_READ | GENERIC_WRITE | DELETE,
                FILE_SHARE_READ,
                nullptr,
                CREATE_NEW,
                FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN,
                nullptr);
            if (handle != INVALID_HANDLE_VALUE)
            {
                file.Reset(handle);
                return true;
            }
            if (GetLastError() != ERROR_FILE_EXISTS &&
                GetLastError() != ERROR_ALREADY_EXISTS)
            {
                return false;
            }
        }

        return false;
    }

    bool WideToAscii(const std::wstring& value, std::string& result)
    {
        result.clear();
        result.reserve(value.size());
        for (wchar_t character : value)
        {
            if (character < 0x20 || character > 0x7E)
            {
                return false;
            }
            result.push_back(static_cast<char>(character));
        }
        return true;
    }

    std::string MarkerText(
        const std::wstring& proxyHash,
        const std::wstring& n3Hash)
    {
        std::string proxy;
        std::string n3;
        WideToAscii(proxyHash, proxy);
        WideToAscii(n3Hash, n3);
        return
            "Product=AORoomSpaceFix\r\n"
            "Version=1\r\n"
            "ProxySha256=" + proxy + "\r\n"
            "N3Sha256=" + n3 + "\r\n";
    }

    bool ParseMarker(const std::string& text, Marker& marker)
    {
        std::vector<std::string> lines;
        size_t offset = 0;
        while (offset < text.size())
        {
            size_t end = text.find("\r\n", offset);
            if (end == std::string::npos)
            {
                return false;
            }
            lines.push_back(text.substr(offset, end - offset));
            offset = end + 2;
        }

        if (lines.size() != 4 ||
            lines[0] != "Product=AORoomSpaceFix" ||
            lines[1] != "Version=1")
        {
            return false;
        }

        constexpr char ProxyPrefix[] = "ProxySha256=";
        constexpr char N3Prefix[] = "N3Sha256=";
        if (lines[2].compare(0, sizeof(ProxyPrefix) - 1, ProxyPrefix) != 0 ||
            lines[3].compare(0, sizeof(N3Prefix) - 1, N3Prefix) != 0)
        {
            return false;
        }

        std::string proxy = lines[2].substr(sizeof(ProxyPrefix) - 1);
        std::string n3 = lines[3].substr(sizeof(N3Prefix) - 1);
        marker.proxyHash.assign(proxy.begin(), proxy.end());
        marker.n3Hash.assign(n3.begin(), n3.end());
        return IsUpperHex64(marker.proxyHash) &&
               IsUpperHex64(marker.n3Hash) &&
               IsApprovedN3Hash(marker.n3Hash);
    }

    bool IsExpectedPackageName(const wchar_t* name, bool includeManifest)
    {
        for (const wchar_t* expected : PayloadNames)
        {
            if (_wcsicmp(name, expected) == 0)
            {
                return true;
            }
        }
        return includeManifest && _wcsicmp(name, ManifestName) == 0;
    }

    bool VerifyExactDirectorySet(const std::wstring& root, bool includeManifest)
    {
        WIN32_FIND_DATAW data = {};
        std::wstring pattern = Combine(root, L"*");
        HANDLE search = FindFirstFileW(pattern.c_str(), &data);
        if (search == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        size_t count = 0;
        bool valid = true;
        do
        {
            if (wcscmp(data.cFileName, L".") == 0 ||
                wcscmp(data.cFileName, L"..") == 0)
            {
                continue;
            }

            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 ||
                (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0 ||
                !IsExpectedPackageName(data.cFileName, includeManifest))
            {
                valid = false;
                break;
            }
            ++count;
        }
        while (FindNextFileW(search, &data));

        DWORD findError = GetLastError();
        FindClose(search);
        size_t expectedCount =
            (sizeof(PayloadNames) / sizeof(PayloadNames[0])) +
            (includeManifest ? 1U : 0U);
        return valid && findError == ERROR_NO_MORE_FILES && count == expectedCount;
    }

    bool BuildExpectedManifest(const std::wstring& root, std::string& manifest)
    {
        manifest.clear();
        for (const wchar_t* name : PayloadNames)
        {
            std::wstring hash;
            if (!IsRegularFile(Combine(root, name)) ||
                !HashPath(Combine(root, name), hash))
            {
                return false;
            }

            std::string asciiHash;
            std::string asciiName;
            if (!WideToAscii(hash, asciiHash) ||
                !WideToAscii(name, asciiName))
            {
                return false;
            }
            manifest += asciiHash + "  " + asciiName + "\r\n";
        }
        return true;
    }

    bool WriteManifest(const std::wstring& root)
    {
        if (!VerifyExactDirectorySet(root, false) ||
            PathExists(Combine(root, ManifestName)))
        {
            return false;
        }

        std::string manifest;
        if (!BuildExpectedManifest(root, manifest))
        {
            return false;
        }

        UniqueHandle file(CreateFileW(
            Combine(root, ManifestName).c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_NEW,
            FILE_ATTRIBUTE_NORMAL,
            nullptr));
        if (!file.IsValid() || !WriteAll(file.Get(), manifest))
        {
            file.Reset();
            DeleteFileW(Combine(root, ManifestName).c_str());
            return false;
        }
        return true;
    }

    bool VerifyPackage(const std::wstring& root)
    {
        if (!VerifyExactDirectorySet(root, true))
        {
            return false;
        }

        std::string expected;
        std::string actual;
        return BuildExpectedManifest(root, expected) &&
               ReadSmallPath(Combine(root, ManifestName), actual) &&
               actual == expected;
    }

    bool TryAnyAoRunning(bool& running)
    {
        running = false;
        UniqueHandle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
        if (!snapshot.IsValid())
        {
            return false;
        }

        PROCESSENTRY32W entry = {};
        entry.dwSize = sizeof(entry);
        if (!Process32FirstW(snapshot.Get(), &entry))
        {
            return false;
        }

        do
        {
            if (_wcsicmp(entry.szExeFile, L"AnarchyOnline.exe") == 0)
            {
                running = true;
                return true;
            }
        }
        while (Process32NextW(snapshot.Get(), &entry));

        return GetLastError() == ERROR_NO_MORE_FILES;
    }

    bool OpenPinnedFile(const std::wstring& path, UniqueHandle& file)
    {
        file.Reset(CreateFileW(
            path.c_str(),
            GENERIC_READ | DELETE,
            FILE_SHARE_READ,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL |
                FILE_FLAG_SEQUENTIAL_SCAN |
                FILE_FLAG_OPEN_REPARSE_POINT,
            nullptr));
        if (!file.IsValid())
        {
            return false;
        }

        FILE_ATTRIBUTE_TAG_INFO attributes = {};
        if (!GetFileInformationByHandleEx(
                file.Get(),
                FileAttributeTagInfo,
                &attributes,
                sizeof(attributes)) ||
            (attributes.FileAttributes &
                (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)) != 0)
        {
            file.Reset();
            return false;
        }

        return true;
    }

    bool VerifyExistingInstall(
        const std::wstring& versionPath,
        const std::wstring& markerPath,
        const std::wstring& packageProxyHash,
        const std::wstring& currentN3Hash)
    {
        UniqueHandle markerFile;
        UniqueHandle versionFile;
        if (!OpenPinnedFile(markerPath, markerFile) ||
            !OpenPinnedFile(versionPath, versionFile))
        {
            return false;
        }

        std::string markerText;
        Marker marker;
        std::array<BYTE, 32> digest = {};
        return ReadSmallHandle(markerFile.Get(), markerText) &&
               ParseMarker(markerText, marker) &&
               HashHandle(versionFile.Get(), digest) &&
               DigestToHex(digest) == marker.proxyHash &&
               marker.proxyHash == packageProxyHash &&
               marker.n3Hash == currentN3Hash;
    }

    int Install(const std::wstring& clientRoot, const std::wstring& packageRoot)
    {
        if (!VerifyPackage(packageRoot))
        {
            std::fwprintf(stderr, L"ERROR package manifest or payload set is invalid.\n");
            return 1;
        }

        bool aoRunning = false;
        if (!TryAnyAoRunning(aoRunning))
        {
            std::fwprintf(stderr, L"ERROR could not verify whether AO is running.\n");
            return 1;
        }
        if (aoRunning)
        {
            std::fwprintf(stderr, L"ERROR close every Anarchy Online client before installing.\n");
            return 1;
        }

        const std::wstring executablePath = Combine(clientRoot, L"AnarchyOnline.exe");
        const std::wstring n3Path = Combine(clientRoot, L"N3.dll");
        const std::wstring versionPath = Combine(clientRoot, ProxyName);
        const std::wstring markerPath = Combine(clientRoot, MarkerName);
        if (!IsRegularFile(executablePath) || !IsRegularFile(n3Path))
        {
            std::fwprintf(stderr, L"ERROR client root is missing AnarchyOnline.exe or N3.dll.\n");
            return 1;
        }

        std::wstring n3Hash;
        std::wstring proxyHash;
        if (!HashPath(n3Path, n3Hash) || !IsApprovedN3Hash(n3Hash))
        {
            std::fwprintf(stderr, L"ERROR unsupported or unreadable N3.dll.\n");
            return 1;
        }
        if (!HashPath(Combine(packageRoot, ProxyName), proxyHash))
        {
            std::fwprintf(stderr, L"ERROR package version.dll could not be hashed.\n");
            return 1;
        }

        const bool versionExists = PathExists(versionPath);
        const bool markerExists = PathExists(markerPath);
        if (versionExists || markerExists)
        {
            if (versionExists && markerExists &&
                VerifyExistingInstall(
                    versionPath,
                    markerPath,
                    proxyHash,
                    n3Hash))
            {
                std::wprintf(L"PASS AORoomSpaceFix is already installed.\n");
                return 0;
            }

            std::fwprintf(
                stderr,
                L"ERROR CONFLICT existing version.dll or ownership marker. Nothing was overwritten.\n");
            return 1;
        }

        UniqueHandle proxyTemporary;
        UniqueHandle markerTemporary;
        std::wstring proxyTemporaryPath;
        std::wstring markerTemporaryPath;
        if (!CreateUniqueTemporaryFile(
                clientRoot,
                L"proxy",
                proxyTemporary,
                proxyTemporaryPath) ||
            !CopyToHandle(Combine(packageRoot, ProxyName), proxyTemporary.Get()))
        {
            if (proxyTemporary.IsValid())
            {
                SetDeleteDisposition(proxyTemporary.Get(), true);
            }
            std::fwprintf(stderr, L"ERROR could not stage version.dll.\n");
            return 1;
        }

        std::array<BYTE, 32> stagedDigest = {};
        if (!HashHandle(proxyTemporary.Get(), stagedDigest) ||
            DigestToHex(stagedDigest) != proxyHash)
        {
            SetDeleteDisposition(proxyTemporary.Get(), true);
            std::fwprintf(stderr, L"ERROR staged version.dll hash mismatch.\n");
            return 1;
        }

        if (!CreateUniqueTemporaryFile(
                clientRoot,
                L"marker",
                markerTemporary,
                markerTemporaryPath) ||
            !WriteAll(markerTemporary.Get(), MarkerText(proxyHash, n3Hash)))
        {
            SetDeleteDisposition(proxyTemporary.Get(), true);
            if (markerTemporary.IsValid())
            {
                SetDeleteDisposition(markerTemporary.Get(), true);
            }
            std::fwprintf(stderr, L"ERROR could not stage the ownership marker.\n");
            return 1;
        }

        if (!RenameHandleNoReplace(proxyTemporary.Get(), versionPath))
        {
            SetDeleteDisposition(proxyTemporary.Get(), true);
            SetDeleteDisposition(markerTemporary.Get(), true);
            std::fwprintf(
                stderr,
                L"ERROR CONFLICT version.dll appeared during installation. Nothing was overwritten.\n");
            return 1;
        }

        if (!RenameHandleNoReplace(markerTemporary.Get(), markerPath))
        {
            const bool rolledBack = SetDeleteDisposition(proxyTemporary.Get(), true);
            SetDeleteDisposition(markerTemporary.Get(), true);
            if (!rolledBack)
            {
                std::fwprintf(
                    stderr,
                    L"ERROR CRITICAL ownership marker activation failed and version.dll rollback failed.\n");
            }
            else
            {
                std::fwprintf(
                    stderr,
                    L"ERROR ownership marker activation failed; version.dll was rolled back.\n");
            }
            return 1;
        }

        std::wprintf(L"PASS installed AORoomSpaceFix under \"%s\".\n", clientRoot.c_str());
        return 0;
    }

    int Uninstall(const std::wstring& clientRoot, const std::wstring& packageRoot)
    {
        if (!VerifyPackage(packageRoot))
        {
            std::fwprintf(stderr, L"ERROR package manifest or payload set is invalid.\n");
            return 1;
        }

        bool aoRunning = false;
        if (!TryAnyAoRunning(aoRunning))
        {
            std::fwprintf(stderr, L"ERROR could not verify whether AO is running.\n");
            return 1;
        }
        if (aoRunning)
        {
            std::fwprintf(stderr, L"ERROR close every Anarchy Online client before uninstalling.\n");
            return 1;
        }

        if (!IsRegularFile(Combine(clientRoot, L"AnarchyOnline.exe")))
        {
            std::fwprintf(stderr, L"ERROR AnarchyOnline.exe was not found in the client root.\n");
            return 1;
        }

        const std::wstring versionPath = Combine(clientRoot, ProxyName);
        const std::wstring markerPath = Combine(clientRoot, MarkerName);
        UniqueHandle markerFile;
        UniqueHandle versionFile;
        if (!OpenPinnedFile(markerPath, markerFile) ||
            !OpenPinnedFile(versionPath, versionFile))
        {
            std::fwprintf(
                stderr,
                L"ERROR ownership marker or installed version.dll is missing or locked. Nothing was deleted.\n");
            return 1;
        }

        std::string markerText;
        Marker marker;
        std::wstring packageProxyHash;
        std::array<BYTE, 32> installedDigest = {};
        if (!ReadSmallHandle(markerFile.Get(), markerText) ||
            !ParseMarker(markerText, marker) ||
            !HashPath(Combine(packageRoot, ProxyName), packageProxyHash) ||
            !HashHandle(versionFile.Get(), installedDigest) ||
            DigestToHex(installedDigest) != marker.proxyHash ||
            marker.proxyHash != packageProxyHash)
        {
            std::fwprintf(
                stderr,
                L"ERROR strict ownership verification failed. Nothing was deleted.\n");
            return 1;
        }

        if (!SetDeleteDisposition(versionFile.Get(), true))
        {
            std::fwprintf(stderr, L"ERROR could not mark version.dll for removal. Nothing was deleted.\n");
            return 1;
        }
        if (!SetDeleteDisposition(markerFile.Get(), true))
        {
            if (!SetDeleteDisposition(versionFile.Get(), false))
            {
                std::fwprintf(
                    stderr,
                    L"ERROR CRITICAL marker removal failed and version.dll delete cancellation failed.\n");
            }
            else
            {
                std::fwprintf(
                    stderr,
                    L"ERROR could not mark the ownership marker for removal. Nothing was deleted.\n");
            }
            return 1;
        }

        markerFile.Reset();
        versionFile.Reset();
        if (PathExists(markerPath) || PathExists(versionPath))
        {
            std::fwprintf(stderr, L"ERROR removal did not complete.\n");
            return 1;
        }

        std::wprintf(L"PASS removed AORoomSpaceFix from \"%s\".\n", clientRoot.c_str());
        return 0;
    }

    int SelfTest()
    {
        const std::wstring proxyHash(64, L'A');
        Marker marker;
        std::string valid = MarkerText(proxyHash, NewClientN3Hash);
        if (!ParseMarker(valid, marker) ||
            marker.proxyHash != proxyHash ||
            marker.n3Hash != NewClientN3Hash ||
            ParseMarker(valid + "Extra=1\r\n", marker) ||
            ParseMarker("Product=AORoomSpaceFix\nVersion=1\n", marker) ||
            IsUpperHex64(std::wstring(64, L'a')))
        {
            std::fwprintf(stderr, L"ERROR deployment helper self-test failed.\n");
            return 1;
        }

        wchar_t temporaryRoot[MAX_PATH + 1] = {};
        DWORD temporaryRootLength = GetTempPathW(
            static_cast<DWORD>(std::size(temporaryRoot)),
            temporaryRoot);
        if (temporaryRootLength == 0 ||
            temporaryRootLength >= std::size(temporaryRoot))
        {
            std::fwprintf(stderr, L"ERROR deployment helper rename self-test setup failed.\n");
            return 1;
        }

        UniqueHandle temporaryFile;
        std::wstring temporaryPath;
        if (!CreateUniqueTemporaryFile(
                std::wstring(temporaryRoot, temporaryRootLength),
                L"rename-self-test",
                temporaryFile,
                temporaryPath))
        {
            std::fwprintf(stderr, L"ERROR deployment helper rename self-test setup failed.\n");
            return 1;
        }

        wchar_t exactName[128] = {};
        if (swprintf_s(
                exactName,
                L".AORoomSpaceFix.%lu.%llu.rename-exact.tmp",
                GetCurrentProcessId(),
                static_cast<unsigned long long>(GetTickCount64())) < 0)
        {
            SetDeleteDisposition(temporaryFile.Get(), true);
            std::fwprintf(stderr, L"ERROR deployment helper rename self-test setup failed.\n");
            return 1;
        }

        const std::wstring exactPath = Combine(
            std::wstring(temporaryRoot, temporaryRootLength),
            exactName);
        const bool renamed = RenameHandleNoReplace(temporaryFile.Get(), exactPath);
        const bool exactNameExists =
            renamed && PathExists(exactPath) && !PathExists(temporaryPath);
        temporaryFile.Reset();
        UniqueHandle pinnedTemporary;
        const bool pinnedRegularFile =
            exactNameExists && OpenPinnedFile(exactPath, pinnedTemporary);
        const bool deleteMarked =
            pinnedRegularFile && SetDeleteDisposition(pinnedTemporary.Get(), true);
        pinnedTemporary.Reset();
        if (!exactNameExists ||
            !pinnedRegularFile ||
            !deleteMarked ||
            PathExists(exactPath) ||
            PathExists(temporaryPath))
        {
            std::fwprintf(stderr, L"ERROR deployment helper exact-rename self-test failed.\n");
            return 1;
        }

        std::wprintf(L"PASS deployment helper self-test.\n");
        return 0;
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc == 2 && wcscmp(argv[1], L"--self-test") == 0)
    {
        return SelfTest();
    }

    if (argc == 3 && wcscmp(argv[1], L"write-manifest") == 0)
    {
        std::wstring packageRoot;
        if (!NormalizeDirectory(argv[2], packageRoot) || !WriteManifest(packageRoot))
        {
            std::fwprintf(stderr, L"ERROR could not write the exact package manifest.\n");
            return 1;
        }
        std::wprintf(L"PASS package manifest written.\n");
        return 0;
    }

    if (argc == 3 && wcscmp(argv[1], L"verify-package") == 0)
    {
        std::wstring packageRoot;
        if (!NormalizeDirectory(argv[2], packageRoot) || !VerifyPackage(packageRoot))
        {
            std::fwprintf(stderr, L"ERROR package verification failed.\n");
            return 1;
        }
        std::wprintf(L"PASS package verified.\n");
        return 0;
    }

    if (argc == 4 &&
        (wcscmp(argv[1], L"install") == 0 ||
         wcscmp(argv[1], L"uninstall") == 0))
    {
        std::wstring clientRoot;
        std::wstring packageRoot;
        if (!NormalizeDirectory(argv[2], clientRoot) ||
            !NormalizeDirectory(argv[3], packageRoot))
        {
            std::fwprintf(stderr, L"ERROR invalid client or package directory.\n");
            return 1;
        }

        return wcscmp(argv[1], L"install") == 0
            ? Install(clientRoot, packageRoot)
            : Uninstall(clientRoot, packageRoot);
    }

    std::fwprintf(
        stderr,
        L"Usage:\n"
        L"  AORoomSpaceFixDeploy.exe --self-test\n"
        L"  AORoomSpaceFixDeploy.exe write-manifest <package-root>\n"
        L"  AORoomSpaceFixDeploy.exe verify-package <package-root>\n"
        L"  AORoomSpaceFixDeploy.exe install <client-root> <package-root>\n"
        L"  AORoomSpaceFixDeploy.exe uninstall <client-root> <package-root>\n");
    return 2;
}
