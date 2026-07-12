#include "roomspace_fix.h"

#include "logging.h"

#include <windows.h>
#include <tlhelp32.h>
#include <bcrypt.h>

#include <array>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

namespace aorf
{
    namespace
    {
        constexpr size_t CallsiteCount = 4;
        constexpr size_t WrapperSize = 86;

        struct PatchProfile
        {
            const char* name;
            const char* sha256;
            std::array<uint32_t, CallsiteCount> collisionCallRvas;
            uint32_t posToRoomRva;
            uint32_t dynamicCastRva;
            uint32_t targetTypeRva;
            uint32_t sourceTypeRva;
            uint32_t getInsideCellRva;
            uint32_t getZonesRva;
        };

        constexpr PatchProfile Profiles[] =
        {
            {
                "new-client",
                "E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168",
                { 0x157BC, 0x16144, 0x168E2, 0x168F6 },
                0xE095,
                0x3AAEA,
                0x5F894,
                0x5F8EC,
                0x154F8,
                0xDEF4
            },
            {
                "old-client",
                "8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB",
                { 0x13F2E, 0x148B6, 0x15054, 0x15068 },
                0xC8AA,
                0x3894A,
                0x5B80C,
                0x5B864,
                0x13C6A,
                0xC709
            }
        };

        struct SuspendedThreads
        {
            std::array<HANDLE, 256> handles = {};
            std::array<DWORD, 256> threadIds = {};
            size_t count = 0;
        };

        struct PageProtection
        {
            void* page = nullptr;
            DWORD oldProtection = 0;
        };

        void AppendUInt32(std::vector<uint8_t>& bytes, uint32_t value)
        {
            const uint8_t* encoded = reinterpret_cast<const uint8_t*>(&value);
            bytes.insert(bytes.end(), encoded, encoded + sizeof(value));
        }

        int32_t RelativeDisplacement(uint32_t nextInstruction, uint32_t destination)
        {
            return static_cast<int32_t>(destination - nextInstruction);
        }

        void AppendRelativeDisplacement(
            std::vector<uint8_t>& bytes,
            uint32_t nextInstruction,
            uint32_t destination)
        {
            AppendUInt32(
                bytes,
                static_cast<uint32_t>(RelativeDisplacement(nextInstruction, destination)));
        }

        std::array<uint8_t, 5> BuildRelativeCall(uint32_t callAddress, uint32_t destination)
        {
            std::array<uint8_t, 5> bytes = { 0xE8, 0, 0, 0, 0 };
            int32_t displacement = RelativeDisplacement(callAddress + 5, destination);
            std::memcpy(bytes.data() + 1, &displacement, sizeof(displacement));
            return bytes;
        }

        uint32_t DecodeRelativeTarget(
            uint32_t instructionAddress,
            const uint8_t* bytes,
            size_t opcodeOffset)
        {
            int32_t displacement = 0;
            std::memcpy(&displacement, bytes + opcodeOffset + 1, sizeof(displacement));
            return instructionAddress + static_cast<uint32_t>(opcodeOffset) + 5u +
                static_cast<uint32_t>(displacement);
        }

        uint32_t DecodeShortBranchTarget(
            uint32_t instructionAddress,
            const uint8_t* bytes,
            size_t opcodeOffset)
        {
            int8_t displacement = static_cast<int8_t>(bytes[opcodeOffset + 1]);
            return instructionAddress + static_cast<uint32_t>(opcodeOffset) + 2u +
                static_cast<uint32_t>(displacement);
        }

        std::vector<uint8_t> BuildWrapper(
            const PatchProfile& profile,
            uint32_t moduleBase,
            uint32_t wrapperBase)
        {
            std::vector<uint8_t> bytes =
            {
                0x55,
                0x8B, 0xEC,
                0x56,
                0x8B, 0xF1,
                0xFF, 0x71, 0x58,
                0x6A, 0x00,
                0x68
            };
            AppendUInt32(bytes, moduleBase + profile.targetTypeRva);
            bytes.push_back(0x68);
            AppendUInt32(bytes, moduleBase + profile.sourceTypeRva);
            bytes.insert(bytes.end(),
                { 0x6A, 0x00, 0xFF, 0x75, 0xF8, 0xE8 });
            AppendRelativeDisplacement(
                bytes,
                wrapperBase + 31,
                moduleBase + profile.dynamicCastRva);
            bytes.insert(bytes.end(),
                {
                    0x83, 0xC4, 0x14,
                    0x85, 0xC0,
                    0x74, 0x25,
                    0xFF, 0x75, 0x08,
                    0x8B, 0xC8,
                    0xE8
                });
            AppendRelativeDisplacement(
                bytes,
                wrapperBase + 48,
                moduleBase + profile.getInsideCellRva);
            bytes.insert(bytes.end(),
                {
                    0x85, 0xC0,
                    0x78, 0x17,
                    0x50,
                    0x8B, 0xCE,
                    0xE8
                });
            AppendRelativeDisplacement(
                bytes,
                wrapperBase + 60,
                moduleBase + profile.getZonesRva);
            bytes.insert(bytes.end(),
                {
                    0x5A,
                    0x8B, 0x00,
                    0x8B, 0x04, 0x90,
                    0x8B, 0x75, 0xFC,
                    0x8B, 0xE5,
                    0x5D,
                    0xC2, 0x08, 0x00,
                    0x33, 0xC0,
                    0x8B, 0x75, 0xFC,
                    0x8B, 0xE5,
                    0x5D,
                    0xC2, 0x08, 0x00
                });
            return bytes;
        }

        bool Sha256File(const wchar_t* path, std::string& result)
        {
            result.clear();
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

            BCRYPT_ALG_HANDLE algorithm = nullptr;
            BCRYPT_HASH_HANDLE hash = nullptr;
            std::vector<UCHAR> hashObject;
            std::vector<UCHAR> hashBytes;
            bool succeeded = false;

            do
            {
                if (BCryptOpenAlgorithmProvider(
                        &algorithm,
                        BCRYPT_SHA256_ALGORITHM,
                        nullptr,
                        0) < 0)
                {
                    break;
                }

                DWORD objectLength = 0;
                DWORD hashLength = 0;
                DWORD received = 0;
                if (BCryptGetProperty(
                        algorithm,
                        BCRYPT_OBJECT_LENGTH,
                        reinterpret_cast<PUCHAR>(&objectLength),
                        sizeof(objectLength),
                        &received,
                        0) < 0 ||
                    BCryptGetProperty(
                        algorithm,
                        BCRYPT_HASH_LENGTH,
                        reinterpret_cast<PUCHAR>(&hashLength),
                        sizeof(hashLength),
                        &received,
                        0) < 0)
                {
                    break;
                }

                hashObject.resize(objectLength);
                hashBytes.resize(hashLength);
                if (BCryptCreateHash(
                        algorithm,
                        &hash,
                        hashObject.data(),
                        objectLength,
                        nullptr,
                        0,
                        0) < 0)
                {
                    break;
                }

                std::array<UCHAR, 64 * 1024> buffer = {};
                while (true)
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
                        if (BCryptFinishHash(
                                hash,
                                hashBytes.data(),
                                static_cast<ULONG>(hashBytes.size()),
                                0) < 0)
                        {
                            break;
                        }

                        static const char Hex[] = "0123456789ABCDEF";
                        result.reserve(hashBytes.size() * 2);
                        for (UCHAR value : hashBytes)
                        {
                            result.push_back(Hex[value >> 4]);
                            result.push_back(Hex[value & 0x0F]);
                        }
                        succeeded = true;
                        break;
                    }

                    if (BCryptHashData(hash, buffer.data(), bytesRead, 0) < 0)
                    {
                        break;
                    }
                }
            }
            while (false);

            if (hash)
            {
                BCryptDestroyHash(hash);
            }
            if (algorithm)
            {
                BCryptCloseAlgorithmProvider(algorithm, 0);
            }
            CloseHandle(file);
            return succeeded;
        }

        const PatchProfile* SelectProfile(const std::string& sha256)
        {
            for (const PatchProfile& profile : Profiles)
            {
                if (_stricmp(profile.sha256, sha256.c_str()) == 0)
                {
                    return &profile;
                }
            }

            return nullptr;
        }

        const PatchProfile* GetLoadedProfile()
        {
            HMODULE n3 = GetModuleHandleW(L"N3.dll");
            if (!n3)
            {
                Log("ERROR N3.dll is not loaded");
                return nullptr;
            }

            wchar_t n3Path[MAX_PATH] = {};
            DWORD pathLength = GetModuleFileNameW(
                n3,
                n3Path,
                static_cast<DWORD>(sizeof(n3Path) / sizeof(n3Path[0])));
            if (pathLength == 0 || pathLength >= sizeof(n3Path) / sizeof(n3Path[0]))
            {
                Log("ERROR unable to resolve loaded N3.dll path");
                return nullptr;
            }

            std::string hash;
            if (!Sha256File(n3Path, hash))
            {
                Log("ERROR unable to hash loaded N3.dll");
                return nullptr;
            }

            const PatchProfile* profile = SelectProfile(hash);
            if (!profile)
            {
                Log("ERROR unsupported N3.dll sha256=%s", hash.c_str());
                return nullptr;
            }

            return profile;
        }

        bool ContainsThreadId(const SuspendedThreads& suspended, DWORD threadId)
        {
            for (size_t index = 0; index < suspended.count; ++index)
            {
                if (suspended.threadIds[index] == threadId)
                {
                    return true;
                }
            }

            return false;
        }

        bool ResumeOtherThreads(SuspendedThreads& suspended)
        {
            constexpr size_t MaximumResumePasses = 16;

            for (size_t pass = 0;
                 pass < MaximumResumePasses && suspended.count > 0;
                 ++pass)
            {
                size_t retainedCount = 0;
                const size_t priorCount = suspended.count;
                for (size_t index = 0; index < priorCount; ++index)
                {
                    HANDLE thread = suspended.handles[index];
                    DWORD threadId = suspended.threadIds[index];
                    bool released =
                        ResumeThread(thread) != static_cast<DWORD>(-1);
                    if (!released && WaitForSingleObject(thread, 0) == WAIT_OBJECT_0)
                    {
                        released = true;
                    }

                    if (released)
                    {
                        CloseHandle(thread);
                    }
                    else
                    {
                        suspended.handles[retainedCount] = thread;
                        suspended.threadIds[retainedCount] = threadId;
                        ++retainedCount;
                    }
                }

                for (size_t index = retainedCount; index < priorCount; ++index)
                {
                    suspended.handles[index] = nullptr;
                    suspended.threadIds[index] = 0;
                }
                suspended.count = retainedCount;

                if (suspended.count > 0 && pass + 1 < MaximumResumePasses)
                {
                    Sleep(1);
                }
            }

            return suspended.count == 0;
        }

        bool SuspendNewOtherThreads(SuspendedThreads& suspended, size_t& addedCount)
        {
            addedCount = 0;
            HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
            if (snapshot == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            DWORD processId = GetCurrentProcessId();
            DWORD currentThreadId = GetCurrentThreadId();
            THREADENTRY32 entry = {};
            entry.dwSize = sizeof(entry);
            if (!Thread32First(snapshot, &entry))
            {
                CloseHandle(snapshot);
                return false;
            }

            bool ok = true;
            while (true)
            {
                if (entry.th32OwnerProcessID == processId &&
                    entry.th32ThreadID != currentThreadId &&
                    !ContainsThreadId(suspended, entry.th32ThreadID))
                {
                    if (suspended.count == suspended.handles.size())
                    {
                        ok = false;
                        break;
                    }

                    HANDLE thread = OpenThread(
                        THREAD_SUSPEND_RESUME | SYNCHRONIZE,
                        FALSE,
                        entry.th32ThreadID);
                    if (!thread)
                    {
                        ok = false;
                        break;
                    }

                    if (SuspendThread(thread) == static_cast<DWORD>(-1))
                    {
                        CloseHandle(thread);
                        ok = false;
                        break;
                    }

                    suspended.handles[suspended.count] = thread;
                    suspended.threadIds[suspended.count] = entry.th32ThreadID;
                    suspended.count++;
                    addedCount++;
                }

                SetLastError(ERROR_SUCCESS);
                if (!Thread32Next(snapshot, &entry))
                {
                    if (GetLastError() != ERROR_NO_MORE_FILES)
                    {
                        ok = false;
                    }
                    break;
                }
            }

            CloseHandle(snapshot);
            return ok;
        }

        bool SuspendOtherThreads(SuspendedThreads& suspended)
        {
            constexpr size_t MaximumSnapshotPasses = 8;
            constexpr size_t RequiredStablePasses = 2;
            size_t stablePasses = 0;

            for (size_t pass = 0; pass < MaximumSnapshotPasses; ++pass)
            {
                size_t addedCount = 0;
                if (!SuspendNewOtherThreads(suspended, addedCount))
                {
                    ResumeOtherThreads(suspended);
                    return false;
                }

                if (addedCount == 0)
                {
                    stablePasses++;
                    if (stablePasses == RequiredStablePasses)
                    {
                        return true;
                    }
                }
                else
                {
                    stablePasses = 0;
                }
            }

            ResumeOtherThreads(suspended);
            return false;
        }

        bool CallsMatch(
            const std::array<uint8_t*, CallsiteCount>& addresses,
            const std::array<std::array<uint8_t, 5>, CallsiteCount>& expected)
        {
            for (size_t index = 0; index < CallsiteCount; ++index)
            {
                if (std::memcmp(addresses[index], expected[index].data(), 5) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        bool RestorePageProtections(
            std::array<PageProtection, CallsiteCount>& pages,
            size_t pageCount,
            size_t pageSize)
        {
            bool restoredAll = true;
            for (size_t index = 0; index < pageCount; ++index)
            {
                DWORD ignored = 0;
                if (!VirtualProtect(
                        pages[index].page,
                        pageSize,
                        pages[index].oldProtection,
                        &ignored))
                {
                    restoredAll = false;
                }
            }

            return restoredAll;
        }
    }

    ClientProfile GetLoadedN3ClientProfile()
    {
        const PatchProfile* profile = GetLoadedProfile();
        if (!profile)
        {
            return ClientProfile::Unknown;
        }

        if (std::strcmp(profile->name, "old-client") == 0)
        {
            return ClientProfile::OldClient;
        }
        if (std::strcmp(profile->name, "new-client") == 0)
        {
            return ClientProfile::NewClient;
        }

        return ClientProfile::Unknown;
    }

    bool InstallRoomSpaceFix()
    {
        HMODULE n3 = GetModuleHandleW(L"N3.dll");
        const PatchProfile* profile = GetLoadedProfile();
        if (!profile)
        {
            return false;
        }

        uintptr_t moduleAddress = reinterpret_cast<uintptr_t>(n3);
        if (moduleAddress > UINT32_MAX)
        {
            Log("ERROR N3.dll is outside the x86 address space");
            return false;
        }
        uint32_t moduleBase = static_cast<uint32_t>(moduleAddress);

        std::array<uint8_t*, CallsiteCount> callAddresses = {};
        std::array<std::array<uint8_t, 5>, CallsiteCount> originalCalls = {};
        for (size_t index = 0; index < CallsiteCount; ++index)
        {
            uint32_t address = moduleBase + profile->collisionCallRvas[index];
            callAddresses[index] = reinterpret_cast<uint8_t*>(static_cast<uintptr_t>(address));
            originalCalls[index] = BuildRelativeCall(
                address,
                moduleBase + profile->posToRoomRva);
        }

        if (!CallsMatch(callAddresses, originalCalls))
        {
            Log("ERROR collision callsites are already modified profile=%s", profile->name);
            return false;
        }

        void* wrapperMemory = VirtualAlloc(
            nullptr,
            0x1000,
            MEM_COMMIT | MEM_RESERVE,
            PAGE_READWRITE);
        if (!wrapperMemory)
        {
            Log("ERROR VirtualAlloc wrapper failed code=%lu", GetLastError());
            return false;
        }

        uintptr_t wrapperAddress = reinterpret_cast<uintptr_t>(wrapperMemory);
        if (wrapperAddress > UINT32_MAX)
        {
            Log("ERROR wrapper is outside the x86 address space");
            VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            return false;
        }

        std::vector<uint8_t> wrapper = BuildWrapper(
            *profile,
            moduleBase,
            static_cast<uint32_t>(wrapperAddress));
        if (wrapper.size() != WrapperSize)
        {
            Log("ERROR wrapper size=%zu expected=%zu", wrapper.size(), WrapperSize);
            VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            return false;
        }

        std::memcpy(wrapperMemory, wrapper.data(), wrapper.size());
        DWORD oldWrapperProtection = 0;
        if (!VirtualProtect(
                wrapperMemory,
                0x1000,
                PAGE_EXECUTE_READ,
                &oldWrapperProtection))
        {
            Log("ERROR wrapper protection failed code=%lu", GetLastError());
            VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            return false;
        }
        if (!FlushInstructionCache(GetCurrentProcess(), wrapperMemory, wrapper.size()))
        {
            Log("ERROR wrapper instruction-cache flush failed code=%lu", GetLastError());
            VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            return false;
        }

        std::array<std::array<uint8_t, 5>, CallsiteCount> patchedCalls = {};
        for (size_t index = 0; index < CallsiteCount; ++index)
        {
            patchedCalls[index] = BuildRelativeCall(
                static_cast<uint32_t>(reinterpret_cast<uintptr_t>(callAddresses[index])),
                static_cast<uint32_t>(wrapperAddress));
        }

        SuspendedThreads suspended;
        if (!SuspendOtherThreads(suspended))
        {
            bool threadsResumed = ResumeOtherThreads(suspended);
            Log(
                "ERROR unable to suspend client threads threadsResumed=%s",
                threadsResumed ? "true" : "false");
            VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            return false;
        }

        SYSTEM_INFO systemInfo = {};
        GetSystemInfo(&systemInfo);
        size_t pageSize = systemInfo.dwPageSize;
        std::array<PageProtection, CallsiteCount> pages = {};
        size_t pageCount = 0;
        bool protectedPages = true;

        for (uint8_t* address : callAddresses)
        {
            uintptr_t pageAddress = reinterpret_cast<uintptr_t>(address) &
                ~(static_cast<uintptr_t>(pageSize) - 1u);
            bool alreadyAdded = false;
            for (size_t index = 0; index < pageCount; ++index)
            {
                if (reinterpret_cast<uintptr_t>(pages[index].page) == pageAddress)
                {
                    alreadyAdded = true;
                    break;
                }
            }
            if (alreadyAdded)
            {
                continue;
            }

            pages[pageCount].page = reinterpret_cast<void*>(pageAddress);
            if (!VirtualProtect(
                    pages[pageCount].page,
                    pageSize,
                    PAGE_EXECUTE_READWRITE,
                    &pages[pageCount].oldProtection))
            {
                protectedPages = false;
                break;
            }
            pageCount++;
        }

        bool threadsStableForPatch = protectedPages && SuspendOtherThreads(suspended);
        bool installed = false;
        bool rollbackConfirmed = true;
        if (threadsStableForPatch && CallsMatch(callAddresses, originalCalls))
        {
            for (size_t index = 0; index < CallsiteCount; ++index)
            {
                std::memcpy(callAddresses[index], patchedCalls[index].data(), 5);
            }
            bool patchCacheFlushed = FlushInstructionCache(
                GetCurrentProcess(),
                nullptr,
                0) != FALSE;
            installed = patchCacheFlushed &&
                CallsMatch(callAddresses, patchedCalls) &&
                std::memcmp(wrapperMemory, wrapper.data(), wrapper.size()) == 0;

            if (!installed)
            {
                for (size_t index = 0; index < CallsiteCount; ++index)
                {
                    std::memcpy(callAddresses[index], originalCalls[index].data(), 5);
                }
                bool rollbackCacheFlushed = FlushInstructionCache(
                    GetCurrentProcess(),
                    nullptr,
                    0) != FALSE;
                rollbackConfirmed = rollbackCacheFlushed &&
                    CallsMatch(callAddresses, originalCalls);
            }
        }

        bool protectionsRestored = RestorePageProtections(pages, pageCount, pageSize);
        bool threadsResumed = ResumeOtherThreads(suspended);

        if (!installed)
        {
            if (!protectionsRestored)
            {
                Log("ERROR patch transaction did not restore all page protections profile=%s",
                    profile->name);
            }
            if (!threadsResumed)
            {
                Log("ERROR patch transaction did not resume all client threads profile=%s",
                    profile->name);
            }
            if (!rollbackConfirmed)
            {
                Log("ERROR patch rollback is unconfirmed; wrapper retained profile=%s",
                    profile->name);
            }
            else
            {
                VirtualFree(wrapperMemory, 0, MEM_RELEASE);
            }
            Log(
                "ERROR patch transaction failed profile=%s rollbackConfirmed=%s "
                "threadsStableForPatch=%s protectionsRestored=%s threadsResumed=%s "
                "wrapperRetained=%s",
                profile->name,
                rollbackConfirmed ? "true" : "false",
                threadsStableForPatch ? "true" : "false",
                protectionsRestored ? "true" : "false",
                threadsResumed ? "true" : "false",
                rollbackConfirmed ? "false" : "true");
            return false;
        }

        if (protectionsRestored && threadsResumed)
        {
            Log(
                "PATCH PASS profile=%s sha256=%s wrapper=0x%08lX "
                "callRvas=0x%X,0x%X,0x%X,0x%X",
                profile->name,
                profile->sha256,
                static_cast<unsigned long>(wrapperAddress),
                profile->collisionCallRvas[0],
                profile->collisionCallRvas[1],
                profile->collisionCallRvas[2],
                profile->collisionCallRvas[3]);
        }
        else
        {
            Log(
                "ERROR patch active but cleanup is unconfirmed profile=%s "
                "sha256=%s wrapper=0x%08lX "
                "protectionsRestored=%s threadsResumed=%s",
                profile->name,
                profile->sha256,
                static_cast<unsigned long>(wrapperAddress),
                protectionsRestored ? "true" : "false",
                threadsResumed ? "true" : "false");
            return false;
        }
        return true;
    }

    bool RunRoomSpaceFixSelfTest()
    {
        constexpr uint32_t SampleModuleBase = 0x60000000;
        constexpr uint32_t SampleWrapperBase = 0x30000000;

        for (const PatchProfile& profile : Profiles)
        {
            std::vector<uint8_t> wrapper = BuildWrapper(
                profile,
                SampleModuleBase,
                SampleWrapperBase);
            if (wrapper.size() != WrapperSize ||
                wrapper[6] != 0xFF || wrapper[7] != 0x71 || wrapper[8] != 0x58 ||
                wrapper[23] != 0xFF || wrapper[24] != 0x75 || wrapper[25] != 0xF8 ||
                wrapper[36] != 0x74 || wrapper[37] != 0x25 ||
                wrapper[50] != 0x78 || wrapper[51] != 0x17 ||
                DecodeShortBranchTarget(SampleWrapperBase, wrapper.data(), 36) !=
                    SampleWrapperBase + 75u ||
                DecodeShortBranchTarget(SampleWrapperBase, wrapper.data(), 50) !=
                    SampleWrapperBase + 75u ||
                DecodeRelativeTarget(SampleWrapperBase, wrapper.data(), 26) !=
                    SampleModuleBase + profile.dynamicCastRva ||
                DecodeRelativeTarget(SampleWrapperBase, wrapper.data(), 43) !=
                    SampleModuleBase + profile.getInsideCellRva ||
                DecodeRelativeTarget(SampleWrapperBase, wrapper.data(), 55) !=
                    SampleModuleBase + profile.getZonesRva)
            {
                return false;
            }

            constexpr std::array<uint8_t, 11> FailureEpilogue =
            {
                0x33, 0xC0,
                0x8B, 0x75, 0xFC,
                0x8B, 0xE5,
                0x5D,
                0xC2, 0x08, 0x00
            };
            if (std::memcmp(
                    wrapper.data() + 75,
                    FailureEpilogue.data(),
                    FailureEpilogue.size()) != 0)
            {
                return false;
            }

            uint32_t targetType = 0;
            uint32_t sourceType = 0;
            std::memcpy(&targetType, wrapper.data() + 12, sizeof(targetType));
            std::memcpy(&sourceType, wrapper.data() + 17, sizeof(sourceType));
            if (targetType != SampleModuleBase + profile.targetTypeRva ||
                sourceType != SampleModuleBase + profile.sourceTypeRva)
            {
                return false;
            }

            for (uint32_t callRva : profile.collisionCallRvas)
            {
                uint32_t callAddress = SampleModuleBase + callRva;
                std::array<uint8_t, 5> original = BuildRelativeCall(
                    callAddress,
                    SampleModuleBase + profile.posToRoomRva);
                std::array<uint8_t, 5> patched = BuildRelativeCall(
                    callAddress,
                    SampleWrapperBase);
                if (original[0] != 0xE8 || patched[0] != 0xE8 ||
                    DecodeRelativeTarget(callAddress, original.data(), 0) !=
                        SampleModuleBase + profile.posToRoomRva ||
                    DecodeRelativeTarget(callAddress, patched.data(), 0) !=
                        SampleWrapperBase)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
