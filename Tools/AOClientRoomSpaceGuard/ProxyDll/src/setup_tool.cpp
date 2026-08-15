#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>

#include <array>
#include <cstdio>
#include <string>
#include <vector>

namespace AORebirthClientPatchDeploy
{
    int InstallEmbedded(const std::wstring& clientRoot, const std::wstring& packageRoot);
}

namespace
{
    struct Payload
    {
        int resourceId;
        const wchar_t* fileName;
    };

    constexpr Payload Payloads[] =
    {
        { 101, L"AORebirthAnarchyLauncher.url" },
        { 102, L"AORebirthDimensionServer.url" },
        { 103, L"version.dll" }
    };

    constexpr wchar_t DialogTitle[] = L"AORebirth Client Patch";

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

    std::wstring Combine(const std::wstring& root, const wchar_t* name)
    {
        if (!root.empty() && (root.back() == L'\\' || root.back() == L'/'))
        {
            return root + name;
        }
        return root + L"\\" + name;
    }

    bool IsRegularFile(const std::wstring& path)
    {
        DWORD attributes = GetFileAttributesW(path.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES &&
               (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 &&
               (attributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0;
    }

    bool TryUseClientRoot(const std::wstring& candidate, std::wstring& clientRoot)
    {
        if (candidate.empty())
        {
            return false;
        }

        if (!IsRegularFile(Combine(candidate, L"AnarchyOnline.exe")))
        {
            return false;
        }

        clientRoot = candidate;
        while (clientRoot.size() > 3 &&
               (clientRoot.back() == L'\\' || clientRoot.back() == L'/'))
        {
            clientRoot.pop_back();
        }
        return true;
    }

    bool DetectClientRoot(int argc, wchar_t** argv, std::wstring& clientRoot)
    {
        if (argc > 1)
        {
            return TryUseClientRoot(argv[1], clientRoot);
        }

        wchar_t currentDirectory[MAX_PATH + 1] = {};
        DWORD currentLength = GetCurrentDirectoryW(
            static_cast<DWORD>(std::size(currentDirectory)),
            currentDirectory);
        if (currentLength > 0 &&
            currentLength < std::size(currentDirectory) &&
            TryUseClientRoot(currentDirectory, clientRoot))
        {
            return true;
        }

        if (TryUseClientRoot(L"C:\\Funcom\\Anarchy Online", clientRoot))
        {
            return true;
        }

        wchar_t programFilesX86[MAX_PATH + 1] = {};
        DWORD x86Length = GetEnvironmentVariableW(
            L"ProgramFiles(x86)",
            programFilesX86,
            static_cast<DWORD>(std::size(programFilesX86)));
        if (x86Length > 0 &&
            x86Length < std::size(programFilesX86) &&
            TryUseClientRoot(Combine(programFilesX86, L"Funcom\\Anarchy Online"), clientRoot))
        {
            return true;
        }

        wchar_t programFiles[MAX_PATH + 1] = {};
        DWORD programFilesLength = GetEnvironmentVariableW(
            L"ProgramFiles",
            programFiles,
            static_cast<DWORD>(std::size(programFiles)));
        return programFilesLength > 0 &&
               programFilesLength < std::size(programFiles) &&
               TryUseClientRoot(Combine(programFiles, L"Funcom\\Anarchy Online"), clientRoot);
    }

    bool IsSilentMode()
    {
        wchar_t value[8] = {};
        DWORD length = GetEnvironmentVariableW(
            L"AO_REBIRTH_SETUP_SILENT",
            value,
            static_cast<DWORD>(std::size(value)));
        return length == 1 && value[0] == L'1';
    }

    void ShowInfo(const std::wstring& message)
    {
        if (!IsSilentMode())
        {
            MessageBoxW(nullptr, message.c_str(), DialogTitle, MB_OK | MB_ICONINFORMATION);
        }
    }

    void ShowError(const std::wstring& message)
    {
        if (!IsSilentMode())
        {
            MessageBoxW(nullptr, message.c_str(), DialogTitle, MB_OK | MB_ICONERROR);
        }
    }

    bool EnsureDirectory(const std::wstring& path)
    {
        DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes != INVALID_FILE_ATTRIBUTES)
        {
            return (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
        }
        return CreateDirectoryW(path.c_str(), nullptr) != FALSE ||
               GetLastError() == ERROR_ALREADY_EXISTS;
    }

    std::wstring GetSetupLogPath()
    {
        wchar_t localAppData[MAX_PATH + 1] = {};
        DWORD length = GetEnvironmentVariableW(
            L"LOCALAPPDATA",
            localAppData,
            static_cast<DWORD>(std::size(localAppData)));
        if (length == 0 || length >= std::size(localAppData))
        {
            return L"";
        }

        std::wstring directory = Combine(localAppData, L"AORebirthClientPatch");
        if (!EnsureDirectory(directory))
        {
            return L"";
        }
        return Combine(directory, L"AORebirthClientPatchSetup.log");
    }

    bool WideToUtf8(const std::wstring& input, std::string& output)
    {
        output.clear();
        int required = WideCharToMultiByte(
            CP_UTF8,
            0,
            input.c_str(),
            static_cast<int>(input.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (required <= 0)
        {
            return input.empty();
        }

        output.assign(static_cast<size_t>(required), '\0');
        return WideCharToMultiByte(
            CP_UTF8,
            0,
            input.c_str(),
            static_cast<int>(input.size()),
            &output[0],
            required,
            nullptr,
            nullptr) == required;
    }

    void WriteSetupLog(const std::wstring& path, const std::wstring& text)
    {
        if (path.empty())
        {
            return;
        }

        std::string utf8;
        if (!WideToUtf8(text, utf8))
        {
            return;
        }

        UniqueHandle file(CreateFileW(
            path.c_str(),
            GENERIC_WRITE,
            FILE_SHARE_READ,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr));
        if (!file.IsValid())
        {
            return;
        }

        DWORD written = 0;
        if (!utf8.empty())
        {
            WriteFile(
                file.Get(),
                utf8.data(),
                static_cast<DWORD>(utf8.size()),
                &written,
                nullptr);
        }
    }

    int CALLBACK BrowseCallbackProc(
        HWND window,
        UINT message,
        LPARAM,
        LPARAM data)
    {
        if (message == BFFM_INITIALIZED && data != 0)
        {
            SendMessageW(window, BFFM_SETSELECTIONW, TRUE, data);
        }
        return 0;
    }

    bool BrowseForClientRoot(
        const std::wstring& initialDirectory,
        std::wstring& clientRoot)
    {
        for (;;)
        {
            wchar_t selectedPath[MAX_PATH + 1] = {};
            std::vector<wchar_t> initial(initialDirectory.begin(), initialDirectory.end());
            initial.push_back(L'\0');

            BROWSEINFOW browse = {};
            browse.hwndOwner = nullptr;
            browse.pszDisplayName = selectedPath;
            browse.lpszTitle =
                L"Select your Anarchy Online installation folder.\n\n"
                L"Choose the folder that contains AnarchyOnline.exe.";
            browse.ulFlags =
                BIF_RETURNONLYFSDIRS |
                BIF_NEWDIALOGSTYLE |
                BIF_NONEWFOLDERBUTTON;
            browse.lpfn = BrowseCallbackProc;
            browse.lParam = initialDirectory.empty()
                ? 0
                : reinterpret_cast<LPARAM>(initial.data());

            PIDLIST_ABSOLUTE item = SHBrowseForFolderW(&browse);
            if (!item)
            {
                ShowInfo(L"AORebirth Client Patch installation was cancelled.");
                return false;
            }

            bool havePath = SHGetPathFromIDListW(item, selectedPath) != FALSE;
            CoTaskMemFree(item);
            if (!havePath)
            {
                ShowError(L"The selected folder could not be read.");
                continue;
            }

            if (TryUseClientRoot(selectedPath, clientRoot))
            {
                return true;
            }

            int retry = MessageBoxW(
                nullptr,
                L"That folder does not contain AnarchyOnline.exe.\n\n"
                L"Select the main Anarchy Online folder, not cd_image or a launcher subfolder.",
                DialogTitle,
                MB_RETRYCANCEL | MB_ICONERROR);
            if (retry != IDRETRY)
            {
                return false;
            }
        }
    }

    bool SelectClientRoot(int argc, wchar_t** argv, std::wstring& clientRoot)
    {
        if (argc > 1)
        {
            if (TryUseClientRoot(argv[1], clientRoot))
            {
                return true;
            }

            ShowError(
                L"The supplied folder does not contain AnarchyOnline.exe.\n\n"
                L"Run the installer again and select the main Anarchy Online folder.");
            return false;
        }

        std::wstring initialDirectory;
        DetectClientRoot(argc, argv, initialDirectory);
        return BrowseForClientRoot(initialDirectory, clientRoot);
    }

    bool WriteAll(HANDLE file, const void* data, DWORD size)
    {
        const BYTE* cursor = static_cast<const BYTE*>(data);
        DWORD remaining = size;
        while (remaining > 0)
        {
            DWORD written = 0;
            if (!WriteFile(file, cursor, remaining, &written, nullptr) ||
                written == 0)
            {
                return false;
            }

            cursor += written;
            remaining -= written;
        }

        return FlushFileBuffers(file) != FALSE;
    }

    bool CreateUniqueExtractionRoot(std::wstring& root)
    {
        wchar_t temporaryRoot[MAX_PATH + 1] = {};
        DWORD temporaryRootLength = GetTempPathW(
            static_cast<DWORD>(std::size(temporaryRoot)),
            temporaryRoot);
        if (temporaryRootLength == 0 ||
            temporaryRootLength >= std::size(temporaryRoot))
        {
            return false;
        }

        for (unsigned int attempt = 0; attempt < 64; ++attempt)
        {
            wchar_t name[160] = {};
            if (swprintf_s(
                    name,
                    L"AORebirthClientPatchSetup-%lu-%llu-%u",
                    GetCurrentProcessId(),
                    static_cast<unsigned long long>(GetTickCount64()),
                    attempt) < 0)
            {
                return false;
            }

            root = Combine(std::wstring(temporaryRoot, temporaryRootLength), name);
            if (CreateDirectoryW(root.c_str(), nullptr))
            {
                return true;
            }

            if (GetLastError() != ERROR_ALREADY_EXISTS)
            {
                return false;
            }
        }

        return false;
    }

    bool ExtractPayload(const std::wstring& root, const Payload& payload)
    {
        HRSRC resource = FindResourceW(
            nullptr,
            MAKEINTRESOURCEW(payload.resourceId),
            RT_RCDATA);
        if (!resource)
        {
            return false;
        }

        HGLOBAL loaded = LoadResource(nullptr, resource);
        DWORD size = SizeofResource(nullptr, resource);
        const void* data = LockResource(loaded);
        if (!loaded || size == 0 || !data)
        {
            return false;
        }

        UniqueHandle file(CreateFileW(
            Combine(root, payload.fileName).c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_NEW,
            FILE_ATTRIBUTE_NORMAL,
            nullptr));
        return file.IsValid() && WriteAll(file.Get(), data, size);
    }

    bool ExtractPayloads(const std::wstring& root)
    {
        for (const Payload& payload : Payloads)
        {
            if (!ExtractPayload(root, payload))
            {
                return false;
            }
        }

        return true;
    }

    bool QuoteCommandArgument(const std::wstring& argument, std::wstring& output)
    {
        output.push_back(L'"');
        size_t backslashes = 0;
        for (wchar_t character : argument)
        {
            if (character == L'\\')
            {
                ++backslashes;
                continue;
            }

            if (character == L'"')
            {
                output.append(backslashes * 2U + 1U, L'\\');
                output.push_back(character);
                backslashes = 0;
                continue;
            }

            output.append(backslashes, L'\\');
            backslashes = 0;
            output.push_back(character);
        }

        output.append(backslashes * 2U, L'\\');
        output.push_back(L'"');
        return true;
    }

    void AppendBytesAsWide(const char* buffer, DWORD length, std::wstring& output)
    {
        if (length == 0)
        {
            return;
        }

        int required = MultiByteToWideChar(
            CP_ACP,
            MB_PRECOMPOSED,
            buffer,
            static_cast<int>(length),
            nullptr,
            0);
        if (required > 0)
        {
            size_t offset = output.size();
            output.resize(offset + static_cast<size_t>(required));
            MultiByteToWideChar(
                CP_ACP,
                MB_PRECOMPOSED,
                buffer,
                static_cast<int>(length),
                &output[offset],
                required);
            return;
        }

        for (DWORD index = 0; index < length; ++index)
        {
            char value = buffer[index];
            if (value >= 0)
            {
                output.push_back(static_cast<wchar_t>(value));
            }
        }
    }

    void ReadPipeToEnd(HANDLE pipe, std::wstring& output)
    {
        std::array<char, 4096> buffer = {};
        for (;;)
        {
            DWORD bytesRead = 0;
            if (!ReadFile(
                    pipe,
                    buffer.data(),
                    static_cast<DWORD>(buffer.size()),
                    &bytesRead,
                    nullptr) ||
                bytesRead == 0)
            {
                break;
            }
            AppendBytesAsWide(buffer.data(), bytesRead, output);
        }
    }

    void ReadFileToWide(const std::wstring& path, std::wstring& output)
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
            return;
        }

        std::array<char, 4096> buffer = {};
        for (;;)
        {
            DWORD bytesRead = 0;
            if (!ReadFile(
                    file.Get(),
                    buffer.data(),
                    static_cast<DWORD>(buffer.size()),
                    &bytesRead,
                    nullptr) ||
                bytesRead == 0)
            {
                break;
            }
            AppendBytesAsWide(buffer.data(), bytesRead, output);
        }
    }

    int RunEmbeddedInstaller(
        const std::wstring& root,
        const std::wstring& clientRoot,
        std::wstring& installerOutput)
    {
        const std::wstring outputPath = Combine(root, L"AORebirthClientPatchDeploy.log");
        FILE* stdoutFile = nullptr;
        FILE* stderrFile = nullptr;
        _wfreopen_s(&stdoutFile, outputPath.c_str(), L"w", stdout);
        _wfreopen_s(&stderrFile, outputPath.c_str(), L"a", stderr);

        int result = AORebirthClientPatchDeploy::InstallEmbedded(clientRoot, root);
        fflush(stdout);
        fflush(stderr);

        ReadFileToWide(outputPath, installerOutput);
        DeleteFileW(outputPath.c_str());
        return result;
    }

    void CleanupExtractionRoot(const std::wstring& root)
    {
        for (const Payload& payload : Payloads)
        {
            DeleteFileW(Combine(root, payload.fileName).c_str());
        }
        RemoveDirectoryW(root.c_str());
    }
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, wchar_t*, int)
{
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv)
    {
        ShowError(L"Installer startup failed.");
        return 1;
    }

    if (argc > 2)
    {
        LocalFree(argv);
        ShowError(L"Too many command line arguments were supplied.");
        return 2;
    }

    HRESULT comResult = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    const bool uninitializeCom = SUCCEEDED(comResult);

    std::wstring clientRoot;
    if (!SelectClientRoot(argc, argv, clientRoot))
    {
        LocalFree(argv);
        if (uninitializeCom)
        {
            CoUninitialize();
        }
        return 2;
    }
    LocalFree(argv);

    std::wstring extractionRoot;
    if (!CreateUniqueExtractionRoot(extractionRoot))
    {
        if (uninitializeCom)
        {
            CoUninitialize();
        }
        ShowError(L"Could not create the temporary installer directory.");
        return 1;
    }

    if (!ExtractPayloads(extractionRoot))
    {
        CleanupExtractionRoot(extractionRoot);
        if (uninitializeCom)
        {
            CoUninitialize();
        }
        ShowError(L"Could not extract the installer payload.");
        return 1;
    }

    std::wstring installerOutput;
    int result = RunEmbeddedInstaller(extractionRoot, clientRoot, installerOutput);
    const std::wstring setupLogPath = GetSetupLogPath();
    std::wstring setupLog =
        L"AORebirth Client Patch setup result\r\n"
        L"Selected AO folder: " +
        clientRoot +
        L"\r\nExit code: " +
        std::to_wstring(result) +
        L"\r\n\r\nInstaller output:\r\n" +
        installerOutput;
    WriteSetupLog(setupLogPath, setupLog);
    CleanupExtractionRoot(extractionRoot);
    if (uninitializeCom)
    {
        CoUninitialize();
    }
    if (result == 0)
    {
        std::wstring message =
            L"AORebirth Client Patch installed successfully.\n\n"
            L"Selected AO folder:\n" +
            clientRoot +
            L"\n\n"
            L"Install report:\n" +
            installerOutput +
            L"\nSetup log:\n" +
            setupLogPath +
            L"\n\n"
            L"\nYou can now start Anarchy Online normally and choose AORebirth or the official servers from the launcher.";
        ShowInfo(message);
    }
    else
    {
        std::wstring message =
            L"AORebirth Client Patch did not install.\n\n"
            L"Selected AO folder:\n" +
            clientRoot +
            L"\n\n"
            L"Exit code: " +
            std::to_wstring(result) +
            L"\n\n"
            L"Close every AnarchyOnline.exe process, make sure you selected the main AO folder, and check for an existing version.dll conflict.";
        if (!installerOutput.empty())
        {
            message += L"\n\nInstaller details:\n";
            message += installerOutput;
        }
        else
        {
            message += L"\n\nInstaller details:\nNo deploy-helper output was captured.";
        }
        if (!setupLogPath.empty())
        {
            message += L"\n\nSetup log:\n";
            message += setupLogPath;
        }
        ShowError(message);
    }
    return result;
}
