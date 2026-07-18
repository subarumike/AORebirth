#include "randy_color_fix.h"

#include "logging.h"

#include <windows.h>
#include <tlhelp32.h>

#include <cstdint>
#include <cstring>
#include <vector>

namespace
{
    uintptr_t DrawResourceFaultAddress = 0;
    uintptr_t RenderStateFaultAddress = 0;
    uintptr_t RenderStateResumeAddress = 0;
    uintptr_t ByteColorFaultAddress = 0;
    uintptr_t ByteColorResumeAddress = 0;
    uintptr_t IndirectColorFaultAddress = 0;
    uintptr_t IndirectColorResumeAddress = 0;
    uintptr_t DwordColorFaultAddress = 0;
    uintptr_t DwordColorResumeAddress = 0;
    uintptr_t GuiRenderBatchAddress = 0;
    uintptr_t GuiCallRenderAddress = 0;
    uintptr_t GuiNullDynamicVbFaultAddress = 0;
    uintptr_t GuiDynamicVbGetAddress = 0;
    uintptr_t GuiDynamicVbGetVbAddress = 0;
    uintptr_t GuiResetMaterialAddress = 0;
    uintptr_t GuiStateBlobResetAddress = 0;
    uintptr_t GuiFreeIndexBufferAddress = 0;
    uintptr_t GuiStateBlobArrayAddress = 0;
    uintptr_t GuiStaticIndexBufferAddress = 0;
    uintptr_t GuiTreeFindResumeAddress = 0;
    PVOID volatile EarlyRandyExceptionGuardHandle = nullptr;
    LONG RenderStateVectorSkipCount = 0;
    LONG RenderStateSkipCount = 0;
    LONG IndirectColorSkipCount = 0;
    LONG DriverDrawInputSkipCount = 0;
    LONG DriverDrawExceptionSkipCount = 0;
    LONG GuiBatchExceptionSkipCount = 0;
    LONG GuiCallRenderDepthSkipCount = 0;
    LONG GuiTreeInvalidKeySkipCount = 0;

    __declspec(thread) LONG GuiCallRenderDepth = 0;
    constexpr LONG MaximumGuiCallRenderDepth = 128;

    constexpr uintptr_t RenderStateVectorStartRva = 0x25110;
    constexpr uintptr_t RenderStateEntryFaultRva = 0x25118;
    constexpr uintptr_t RenderStateVectorExitRva = 0x25139;
    constexpr uintptr_t RenderStateEntryResumeRva = 0x25147;
    constexpr uint8_t ExpectedRenderStateFaultSequence[] =
    {
        0x8B, 0x7E, 0x14,
        0x03, 0x7D, 0xF8,
        0x6A, 0x0A,
        0x8B, 0x07,
        0x8B, 0x8C, 0x83, 0xC8, 0x04, 0x00, 0x00,
        0xFF, 0x77, 0x04,
        0x89, 0x4F, 0x08,
        0x50,
        0x8B, 0xCB,
        0xE8, 0x92, 0x6A, 0xFF, 0xFF,
        0xFF, 0x45, 0xFC,
        0x83, 0x45, 0xF8, 0x10,
        0x88, 0x47, 0x0C
    };
    constexpr uint8_t ExpectedRenderStateVectorExitSequence[] =
    {
        0x8B, 0x46, 0x18,
        0x2B, 0x46, 0x14,
        0xC1, 0xF8, 0x04,
        0x39, 0x45, 0xFC,
        0x72, 0xC9,
        0x8B, 0x46, 0x28,
        0x2B, 0x46, 0x24,
        0x6A, 0x14,
        0x99,
        0x59,
        0xF7, 0xF9,
        0x83, 0x65, 0xFC, 0x00,
        0x85, 0xC0,
        0x74, 0x49,
        0x83, 0x65, 0xF8, 0x00
    };

    using DrawIndexedPrimitiveVbFunction = HRESULT (WINAPI*)(
        void*,
        DWORD,
        void*,
        DWORD,
        DWORD,
        WORD*,
        DWORD,
        DWORD);

    using GuiDynamicVbGetFunction = void* (__cdecl*)();
    using GuiDynamicVbGetVbFunction = void* (__thiscall*)(void*, DWORD);
    using GuiThiscallVoidFunction = void (__thiscall*)(void*);
    using GuiFreeFunction = void (__cdecl*)(void*);

    enum class GuiBatchExceptionKind : DWORD
    {
        None = 0,
        NvidiaDeferredFlush = 1,
        NullDynamicVbDestination = 2
    };

    enum class GuardedDrawPhase : LONG
    {
        None = 0,
        InputProbe = 1,
        Resolve = 2,
        DriverCall = 3
    };

    enum class GuardedDrawExceptionKind : DWORD
    {
        None = 0,
        InputProbe = 1,
        Resolve = 2,
        InvalidInitialTarget = 3,
        NvidiaDriver = 4
    };

    __declspec(noreturn) void FailFastPatchRollback(const char* patchName);

    class ScopedOtherThreadSuspension
    {
    public:
        ~ScopedOtherThreadSuspension()
        {
            ResumeAndClose();
        }

        bool Suspend()
        {
            HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
            if (snapshot == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            THREADENTRY32 entry = {};
            entry.dwSize = sizeof(entry);
            DWORD processId = GetCurrentProcessId();
            DWORD currentThreadId = GetCurrentThreadId();
            BOOL hasEntry = Thread32First(snapshot, &entry);
            if (!hasEntry)
            {
                CloseHandle(snapshot);
                return false;
            }
            while (hasEntry)
            {
                if (entry.th32OwnerProcessID == processId &&
                    entry.th32ThreadID != currentThreadId)
                {
                    HANDLE thread = OpenThread(
                        THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT |
                            THREAD_QUERY_LIMITED_INFORMATION,
                        FALSE,
                        entry.th32ThreadID);
                    if (!thread)
                    {
                        DWORD error = GetLastError();
                        if (error != ERROR_INVALID_PARAMETER)
                        {
                            CloseHandle(snapshot);
                            ResumeAndClose();
                            return false;
                        }
                    }
                    else
                    {
                        threads_.push_back(thread);
                    }
                }
                hasEntry = Thread32Next(snapshot, &entry);
            }
            DWORD enumerationError = GetLastError();
            CloseHandle(snapshot);
            if (enumerationError != ERROR_NO_MORE_FILES)
            {
                ResumeAndClose();
                return false;
            }

            for (HANDLE thread : threads_)
            {
                if (SuspendThread(thread) == static_cast<DWORD>(-1))
                {
                    ResumeAndClose();
                    return false;
                }
                ++suspendedCount_;
            }
            return true;
        }

        bool IsAnyThreadExecutingInRange(
            uintptr_t begin,
            uintptr_t end,
            bool* executing) const
        {
            *executing = false;
            for (size_t index = 0; index < suspendedCount_; ++index)
            {
                CONTEXT context = {};
                context.ContextFlags = CONTEXT_CONTROL;
                if (!GetThreadContext(threads_[index], &context))
                {
                    return false;
                }
                if (context.Eip >= begin && context.Eip < end)
                {
                    *executing = true;
                    return true;
                }
            }
            return true;
        }

    private:
        void ResumeAndClose()
        {
            while (suspendedCount_ != 0)
            {
                --suspendedCount_;
                if (ResumeThread(threads_[suspendedCount_]) ==
                    static_cast<DWORD>(-1))
                {
                    FailFastPatchRollback("client thread suspension");
                }
            }
            for (HANDLE thread : threads_)
            {
                CloseHandle(thread);
            }
            threads_.clear();
        }

        std::vector<HANDLE> threads_;
        size_t suspendedCount_ = 0;
    };

    bool IsReadableRange(const void* pointer, size_t size)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (!pointer || VirtualQuery(pointer, &memory, sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        DWORD readable = memory.Protect & 0xFF;
        if (readable != PAGE_READONLY && readable != PAGE_READWRITE &&
            readable != PAGE_WRITECOPY && readable != PAGE_EXECUTE_READ &&
            readable != PAGE_EXECUTE_READWRITE && readable != PAGE_EXECUTE_WRITECOPY)
        {
            return false;
        }

        uintptr_t address = reinterpret_cast<uintptr_t>(pointer);
        uintptr_t regionEnd = reinterpret_cast<uintptr_t>(memory.BaseAddress) +
            memory.RegionSize;
        return regionEnd >= address && regionEnd - address >= size;
    }

    bool IsWritableRange(void* pointer, size_t size)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (!pointer || size == 0 ||
            VirtualQuery(pointer, &memory, sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        DWORD protection = memory.Protect & 0xFF;
        if (protection != PAGE_READWRITE &&
            protection != PAGE_WRITECOPY &&
            protection != PAGE_EXECUTE_READWRITE &&
            protection != PAGE_EXECUTE_WRITECOPY)
        {
            return false;
        }

        uintptr_t address = reinterpret_cast<uintptr_t>(pointer);
        uintptr_t regionEnd = reinterpret_cast<uintptr_t>(memory.BaseAddress) +
            memory.RegionSize;
        return regionEnd >= address && regionEnd - address >= size;
    }

    bool TryGetModuleImageRange(
        HMODULE module,
        uintptr_t* begin,
        uintptr_t* end)
    {
        if (!module)
        {
            *begin = 0;
            *end = 0;
            return true;
        }

        auto base = reinterpret_cast<uint8_t*>(module);
        auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (!IsReadableRange(dos, sizeof(*dos)) ||
            dos->e_magic != IMAGE_DOS_SIGNATURE ||
            dos->e_lfanew <= 0 ||
            !IsReadableRange(base + dos->e_lfanew, sizeof(IMAGE_NT_HEADERS32)))
        {
            return false;
        }
        auto nt = reinterpret_cast<IMAGE_NT_HEADERS32*>(base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE ||
            nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt->OptionalHeader.SizeOfImage == 0)
        {
            return false;
        }

        *begin = reinterpret_cast<uintptr_t>(base);
        *end = *begin + nt->OptionalHeader.SizeOfImage;
        return *end > *begin;
    }

    bool TryResolveEarlyRenderStateResume(
        EXCEPTION_POINTERS* exception,
        uintptr_t* resumeAddress)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (VirtualQuery(
                exception->ExceptionRecord->ExceptionAddress,
                &memory,
                sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            memory.Type != MEM_IMAGE ||
            !memory.AllocationBase)
        {
            return false;
        }

        auto base = reinterpret_cast<const uint8_t*>(memory.AllocationBase);
        uintptr_t imageBegin = reinterpret_cast<uintptr_t>(base);
        IMAGE_DOS_HEADER dos = {};
        if (!IsReadableRange(base, sizeof(dos)))
        {
            return false;
        }
        std::memcpy(&dos, base, sizeof(dos));
        if (dos.e_magic != IMAGE_DOS_SIGNATURE ||
            dos.e_lfanew <= 0 ||
            static_cast<uint32_t>(dos.e_lfanew) > 0x100000 ||
            imageBegin > UINTPTR_MAX - static_cast<uint32_t>(dos.e_lfanew))
        {
            return false;
        }

        auto ntAddress = reinterpret_cast<const uint8_t*>(
            imageBegin + static_cast<uint32_t>(dos.e_lfanew));
        IMAGE_NT_HEADERS32 nt = {};
        if (!IsReadableRange(ntAddress, sizeof(nt)))
        {
            return false;
        }
        std::memcpy(&nt, ntAddress, sizeof(nt));
        constexpr uintptr_t RequiredImageSize =
            RenderStateVectorExitRva +
                sizeof(ExpectedRenderStateVectorExitSequence);
        if (nt.Signature != IMAGE_NT_SIGNATURE ||
            nt.FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC ||
            nt.OptionalHeader.SizeOfImage < RequiredImageSize ||
            imageBegin > UINTPTR_MAX - nt.OptionalHeader.SizeOfImage ||
            reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress) !=
                imageBegin + RenderStateEntryFaultRva)
        {
            return false;
        }

        if (!IsReadableRange(
                base + RenderStateVectorStartRva,
                sizeof(ExpectedRenderStateFaultSequence)) ||
            !IsReadableRange(
                base + RenderStateVectorExitRva,
                sizeof(ExpectedRenderStateVectorExitSequence)) ||
            std::memcmp(
                base + RenderStateVectorStartRva,
                ExpectedRenderStateFaultSequence,
                sizeof(ExpectedRenderStateFaultSequence)) != 0 ||
            std::memcmp(
                base + RenderStateVectorExitRva,
                ExpectedRenderStateVectorExitSequence,
                sizeof(ExpectedRenderStateVectorExitSequence)) != 0)
        {
            return false;
        }

        *resumeAddress = imageBegin + RenderStateEntryResumeRva;
        return true;
    }

    bool IsExactRenderStateVectorEntryFault(
        EXCEPTION_POINTERS* exception,
        uint32_t* vectorBegin,
        uint32_t* byteOffset,
        uint32_t* vectorIndex)
    {
        DWORD stack = exception->ContextRecord->Esp;
        DWORD frame = exception->ContextRecord->Ebp;
        DWORD vectors = exception->ContextRecord->Esi;
        if (frame < 8 || vectors > 0xFFFFFFFFUL - 0x2C ||
            exception->ExceptionRecord->ExceptionInformation[1] !=
                exception->ContextRecord->Edi ||
            !IsReadableRange(
                reinterpret_cast<const void*>(stack),
                sizeof(uint32_t)) ||
            !IsWritableRange(
                reinterpret_cast<void*>(frame - 8),
                sizeof(uint32_t) * 2) ||
            !IsReadableRange(
                reinterpret_cast<const void*>(vectors + 0x14),
                0x18))
        {
            return false;
        }

        uint32_t pushedStateClass = 0;
        uint32_t nextVectorBegin = 0;
        uint32_t nextVectorEnd = 0;
        std::memcpy(
            &pushedStateClass,
            reinterpret_cast<const void*>(stack),
            sizeof(pushedStateClass));
        std::memcpy(
            byteOffset,
            reinterpret_cast<const void*>(frame - 8),
            sizeof(*byteOffset));
        std::memcpy(
            vectorIndex,
            reinterpret_cast<const void*>(frame - 4),
            sizeof(*vectorIndex));
        std::memcpy(
            vectorBegin,
            reinterpret_cast<const void*>(vectors + 0x14),
            sizeof(*vectorBegin));
        std::memcpy(
            &nextVectorBegin,
            reinterpret_cast<const void*>(vectors + 0x24),
            sizeof(nextVectorBegin));
        std::memcpy(
            &nextVectorEnd,
            reinterpret_cast<const void*>(vectors + 0x28),
            sizeof(nextVectorEnd));

        if (pushedStateClass != 0x0A ||
            *vectorIndex > 0x0FFFFFFFUL ||
            *byteOffset != *vectorIndex * 16 ||
            nextVectorEnd < nextVectorBegin ||
            (nextVectorEnd - nextVectorBegin) % 20 != 0)
        {
            return false;
        }

        uint64_t entryAddress =
            static_cast<uint64_t>(*vectorBegin) + *byteOffset;
        return entryAddress <= 0xFFFFFFFFULL &&
            static_cast<uint32_t>(entryAddress) == exception->ContextRecord->Edi;
    }

    bool IsExecutableAddress(const void* pointer)
    {
        MEMORY_BASIC_INFORMATION memory = {};
        if (!pointer || VirtualQuery(pointer, &memory, sizeof(memory)) != sizeof(memory) ||
            memory.State != MEM_COMMIT ||
            (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        DWORD protection = memory.Protect & 0xFF;
        return protection == PAGE_EXECUTE || protection == PAGE_EXECUTE_READ ||
            protection == PAGE_EXECUTE_READWRITE ||
            protection == PAGE_EXECUTE_WRITECOPY;
    }

    __declspec(noreturn) void FailFastPatchRollback(const char* patchName)
    {
        UNREFERENCED_PARAMETER(patchName);
        RaiseFailFastException(nullptr, nullptr, 0);
        TerminateProcess(GetCurrentProcess(), ERROR_INVALID_DATA);
        ExitProcess(ERROR_INVALID_DATA);
    }

    void RestorePatchedBytesOrTerminate(
        uint8_t* callsite,
        const uint8_t* original,
        size_t size,
        DWORD finalProtection,
        const char* patchName)
    {
        DWORD ignored = 0;
        if (!VirtualProtect(
                callsite,
                size,
                PAGE_EXECUTE_READWRITE,
                &ignored))
        {
            FailFastPatchRollback(patchName);
        }

        std::memcpy(callsite, original, size);
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            size) != FALSE;
        bool verified = std::memcmp(callsite, original, size) == 0;
        bool restored = VirtualProtect(
            callsite,
            size,
            finalProtection,
            &ignored) != FALSE;
        if (!flushed || !verified || !restored)
        {
            FailFastPatchRollback(patchName);
        }
    }

    bool TryGetVerifiedNvidiaFault(
        EXCEPTION_POINTERS* exception,
        uint8_t** driverBase,
        uintptr_t* driverRva)
    {
        if (!exception || !exception->ExceptionRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 0 ||
            exception->ExceptionRecord->ExceptionInformation[1] >= 0x10000)
        {
            return false;
        }

        HMODULE nvidia = GetModuleHandleW(L"nvd3dum.dll");
        MEMORY_BASIC_INFORMATION memory = {};
        if (!nvidia ||
            VirtualQuery(
                exception->ExceptionRecord->ExceptionAddress,
                &memory,
            sizeof(memory)) != sizeof(memory) ||
            memory.AllocationBase != nvidia)
        {
            return false;
        }

        auto base = reinterpret_cast<uint8_t*>(nvidia);
        auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (!IsReadableRange(dos, sizeof(*dos)) ||
            dos->e_magic != IMAGE_DOS_SIGNATURE ||
            dos->e_lfanew <= 0 ||
            !IsReadableRange(base + dos->e_lfanew, sizeof(IMAGE_NT_HEADERS32)))
        {
            return false;
        }

        auto nt = reinterpret_cast<IMAGE_NT_HEADERS32*>(base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE ||
            nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt->FileHeader.TimeDateStamp != 0x696F2FCE ||
            nt->OptionalHeader.SizeOfImage != 0x03C76000 ||
            nt->OptionalHeader.CheckSum != 0x03D0ECBD)
        {
            return false;
        }

        *driverBase = base;
        *driverRva = reinterpret_cast<uintptr_t>(
            exception->ExceptionRecord->ExceptionAddress) -
            reinterpret_cast<uintptr_t>(base);
        return true;
    }

    int CaptureNvidiaDrawException(
        EXCEPTION_POINTERS* exception,
        uintptr_t* faultAddress,
        ULONG_PTR* accessAddress)
    {
        uint8_t* base = nullptr;
        uintptr_t driverRva = 0;
        if (!TryGetVerifiedNvidiaFault(exception, &base, &driverRva) ||
            exception->ExceptionRecord->ExceptionInformation[1] != 0x08)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        constexpr uint8_t ExpectedFault172776C[] = { 0x8B, 0x58, 0x08 };
        constexpr uint8_t ExpectedFault173A009[] = { 0x8B, 0x76, 0x08 };
        bool verifiedFault =
            (driverRva == 0x0172776C &&
             exception->ContextRecord &&
             exception->ContextRecord->Eax == 0 &&
             std::memcmp(
                 base + driverRva,
                 ExpectedFault172776C,
                 sizeof(ExpectedFault172776C)) == 0) ||
            (driverRva == 0x0173A009 &&
             exception->ContextRecord &&
             exception->ContextRecord->Esi == 0 &&
             std::memcmp(
                 base + driverRva,
                 ExpectedFault173A009,
                 sizeof(ExpectedFault173A009)) == 0);
        if (!verifiedFault)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        *faultAddress = reinterpret_cast<uintptr_t>(
            exception->ExceptionRecord->ExceptionAddress);
        *accessAddress = exception->ExceptionRecord->ExceptionInformation[1];
        return EXCEPTION_EXECUTE_HANDLER;
    }

    int CaptureGuardedDrawException(
        EXCEPTION_POINTERS* exception,
        LONG phaseValue,
        DrawIndexedPrimitiveVbFunction draw,
        GuardedDrawExceptionKind* exceptionKind,
        uintptr_t* faultAddress,
        ULONG_PTR* accessAddress)
    {
        if (!exception || !exception->ExceptionRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        GuardedDrawPhase phase = static_cast<GuardedDrawPhase>(phaseValue);
        if (phase == GuardedDrawPhase::InputProbe ||
            phase == GuardedDrawPhase::Resolve)
        {
            *exceptionKind = phase == GuardedDrawPhase::InputProbe ?
                GuardedDrawExceptionKind::InputProbe :
                GuardedDrawExceptionKind::Resolve;
            *faultAddress = reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress);
            *accessAddress = exception->ExceptionRecord->ExceptionInformation[1];
            return EXCEPTION_EXECUTE_HANDLER;
        }

        if (phase != GuardedDrawPhase::DriverCall)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        ULONG_PTR drawAddress = reinterpret_cast<ULONG_PTR>(draw);
        if (exception->ExceptionRecord->ExceptionInformation[0] == 8 &&
            exception->ExceptionRecord->ExceptionInformation[1] == drawAddress &&
            reinterpret_cast<ULONG_PTR>(
                exception->ExceptionRecord->ExceptionAddress) == drawAddress)
        {
            *exceptionKind = GuardedDrawExceptionKind::InvalidInitialTarget;
            *faultAddress = static_cast<uintptr_t>(drawAddress);
            *accessAddress = drawAddress;
            return EXCEPTION_EXECUTE_HANDLER;
        }

        if (CaptureNvidiaDrawException(
                exception,
                faultAddress,
                accessAddress) != EXCEPTION_EXECUTE_HANDLER)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        *exceptionKind = GuardedDrawExceptionKind::NvidiaDriver;
        return EXCEPTION_EXECUTE_HANDLER;
    }

    bool TryGetGuiBatchRecoveryState(
        void* batchObject,
        DWORD stackArgument,
        void** stateBlob)
    {
        if (!IsReadableRange(batchObject, 12) ||
            !IsWritableRange(reinterpret_cast<void*>(stackArgument), 0x14) ||
            !IsExecutableAddress(
                reinterpret_cast<const void*>(GuiDynamicVbGetAddress)) ||
            !IsExecutableAddress(
                reinterpret_cast<const void*>(GuiDynamicVbGetVbAddress)) ||
            !IsExecutableAddress(
                reinterpret_cast<const void*>(GuiResetMaterialAddress)) ||
            !IsExecutableAddress(
                reinterpret_cast<const void*>(GuiStateBlobResetAddress)))
        {
            return false;
        }

        DWORD stateIndex = 0;
        std::memcpy(
            &stateIndex,
            reinterpret_cast<const uint8_t*>(batchObject) + 8,
            sizeof(stateIndex));
        if (stateIndex >= 3)
        {
            return false;
        }

        uintptr_t selectedStateBlob =
            GuiStateBlobArrayAddress + static_cast<uintptr_t>(stateIndex) * 0x84;
        if (!IsWritableRange(reinterpret_cast<void*>(selectedStateBlob), 0x84))
        {
            return false;
        }
        *stateBlob = reinterpret_cast<void*>(selectedStateBlob);
        return true;
    }

    int CaptureGuiRenderBatchException(
        EXCEPTION_POINTERS* exception,
        void* batchObject,
        DWORD batchSpan,
        DWORD stackArgument,
        GuiBatchExceptionKind* exceptionKind,
        uintptr_t* faultAddress,
        ULONG_PTR* accessAddress,
        DWORD* accessType,
        void** stateBlob,
        void** indexBuffer)
    {
        uint8_t* nvidiaBase = nullptr;
        uintptr_t nvidiaRva = 0;
        constexpr uint8_t ExpectedNvidiaFault[] =
        {
            0x8B, 0x80, 0x10, 0x00, 0x00, 0x00
        };
        if (TryGetVerifiedNvidiaFault(exception, &nvidiaBase, &nvidiaRva) &&
            nvidiaRva == 0x0170C490 &&
            exception->ContextRecord &&
            exception->ContextRecord->Eax == 0x04 &&
            exception->ExceptionRecord->ExceptionInformation[1] == 0x14 &&
            std::memcmp(
                nvidiaBase + nvidiaRva,
                ExpectedNvidiaFault,
                sizeof(ExpectedNvidiaFault)) == 0 &&
            TryGetGuiBatchRecoveryState(
                batchObject,
                stackArgument,
                stateBlob))
        {
            *exceptionKind = GuiBatchExceptionKind::NvidiaDeferredFlush;
            *faultAddress = reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress);
            *accessAddress = exception->ExceptionRecord->ExceptionInformation[1];
            *accessType = static_cast<DWORD>(
                exception->ExceptionRecord->ExceptionInformation[0]);
            return EXCEPTION_EXECUTE_HANDLER;
        }

        if (!exception || !exception->ExceptionRecord ||
            !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 1 ||
            reinterpret_cast<uintptr_t>(
                exception->ExceptionRecord->ExceptionAddress) !=
                GuiNullDynamicVbFaultAddress ||
            exception->ExceptionRecord->ExceptionInformation[1] !=
                exception->ContextRecord->Edi ||
            exception->ContextRecord->Ecx != 0x1C ||
            exception->ContextRecord->Edx != 0 ||
            exception->ContextRecord->Eax !=
                reinterpret_cast<uintptr_t>(batchObject) ||
            exception->ContextRecord->Ebx != batchSpan ||
            static_cast<LONG>(batchSpan) <= 0 ||
            !IsReadableRange(
                reinterpret_cast<const void*>(exception->ContextRecord->Esi),
                0x70) ||
            !TryGetGuiBatchRecoveryState(
                batchObject,
                stackArgument,
                stateBlob) ||
            !IsExecutableAddress(
                reinterpret_cast<const void*>(GuiFreeIndexBufferAddress)))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        constexpr uint8_t ExpectedFaultInstruction[] = { 0xF3, 0xA5 };
        if (std::memcmp(
                reinterpret_cast<const void*>(GuiNullDynamicVbFaultAddress),
                ExpectedFaultInstruction,
                sizeof(ExpectedFaultInstruction)) != 0)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        uintptr_t frame = exception->ContextRecord->Ebp;
        if (frame < 0x24 ||
            !IsReadableRange(reinterpret_cast<const void*>(frame - 0x24), 0x30))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        DWORD destinationBase = 0;
        DWORD baseVertex = 0;
        DWORD requestedVertexBytes = 0;
        DWORD currentQuad = 0;
        DWORD currentIndex = 0;
        DWORD indexCursor = 0;
        DWORD batchSource = 0;
        DWORD frameStateBlob = 0;
        DWORD frameIndexBuffer = 0;
        DWORD frameViewport = 0;
        std::memcpy(
            &destinationBase,
            reinterpret_cast<const void*>(frame - 0x10),
            sizeof(destinationBase));
        std::memcpy(
            &baseVertex,
            reinterpret_cast<const void*>(frame - 0x1C),
            sizeof(baseVertex));
        std::memcpy(
            &requestedVertexBytes,
            reinterpret_cast<const void*>(frame - 0x18),
            sizeof(requestedVertexBytes));
        std::memcpy(
            &currentQuad,
            reinterpret_cast<const void*>(frame - 0x0C),
            sizeof(currentQuad));
        std::memcpy(
            &currentIndex,
            reinterpret_cast<const void*>(frame - 0x08),
            sizeof(currentIndex));
        std::memcpy(
            &indexCursor,
            reinterpret_cast<const void*>(frame - 0x14),
            sizeof(indexCursor));
        std::memcpy(&batchSource, batchObject, sizeof(batchSource));
        std::memcpy(
            &frameStateBlob,
            reinterpret_cast<const void*>(frame - 0x24),
            sizeof(frameStateBlob));
        std::memcpy(
            &frameIndexBuffer,
            reinterpret_cast<const void*>(frame - 0x04),
            sizeof(frameIndexBuffer));
        std::memcpy(
            &frameViewport,
            reinterpret_cast<const void*>(frame + 0x08),
            sizeof(frameViewport));
        uint64_t nullBaseOffset = static_cast<uint64_t>(baseVertex) * 0x1C;
        uint64_t currentDestination =
            static_cast<uint64_t>(destinationBase) + exception->ContextRecord->Edx;
        uint64_t requestedVertexBytes64 =
            static_cast<uint64_t>(batchSpan) * 4;
        uint64_t requestedIndexBytes64 =
            static_cast<uint64_t>(batchSpan) * 12;
        if (nullBaseOffset > 0xFFFFFFFFull ||
            destinationBase != static_cast<DWORD>(nullBaseOffset) ||
            currentDestination > 0xFFFFFFFFull ||
            static_cast<DWORD>(currentDestination) !=
                exception->ExceptionRecord->ExceptionInformation[1] ||
            requestedVertexBytes64 > 0xFFFFFFFFull ||
            requestedVertexBytes != static_cast<DWORD>(requestedVertexBytes64) ||
            requestedIndexBytes64 > 0xFFFFFFFFull ||
            currentQuad != 0 ||
            currentIndex != 0 ||
            batchSource != exception->ContextRecord->Esi ||
            frameViewport != stackArgument)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        uintptr_t selectedStateBlob = reinterpret_cast<uintptr_t>(*stateBlob);
        bool validIndexBuffer = false;
        if (batchSpan < 0x100)
        {
            validIndexBuffer = frameIndexBuffer == GuiStaticIndexBufferAddress;
        }
        else
        {
            validIndexBuffer =
                frameIndexBuffer != 0 &&
                frameIndexBuffer != GuiStaticIndexBufferAddress &&
                IsWritableRange(
                    reinterpret_cast<void*>(frameIndexBuffer),
                    static_cast<size_t>(requestedIndexBytes64));
        }
        if (frameStateBlob != selectedStateBlob ||
            indexCursor != frameIndexBuffer ||
            !validIndexBuffer)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        *exceptionKind = GuiBatchExceptionKind::NullDynamicVbDestination;
        *faultAddress = reinterpret_cast<uintptr_t>(
            exception->ExceptionRecord->ExceptionAddress);
        *accessAddress = exception->ExceptionRecord->ExceptionInformation[1];
        *accessType = 1;
        *stateBlob = reinterpret_cast<void*>(selectedStateBlob);
        *indexBuffer = reinterpret_cast<void*>(frameIndexBuffer);
        return EXCEPTION_EXECUTE_HANDLER;
    }

    DWORD RecoverGuiRenderBatch(
        void* viewport,
        void* stateBlob,
        void* indexBuffer,
        bool releaseIndexBuffer)
    {
        GuiDynamicVbGetFunction getDynamicVb =
            reinterpret_cast<GuiDynamicVbGetFunction>(GuiDynamicVbGetAddress);
        GuiDynamicVbGetVbFunction getVertexBuffer =
            reinterpret_cast<GuiDynamicVbGetVbFunction>(GuiDynamicVbGetVbAddress);
        void* dynamicVb = getDynamicVb();
        void* vertexBuffer = getVertexBuffer(dynamicVb, 0x144);
        if (!vertexBuffer)
        {
            RaiseException(
                EXCEPTION_ACCESS_VIOLATION,
                EXCEPTION_NONCONTINUABLE,
                0,
                nullptr);
            return 0;
        }

        DWORD recoveryMask = 0x1;
        if (releaseIndexBuffer)
        {
            if (indexBuffer != reinterpret_cast<void*>(GuiStaticIndexBufferAddress))
            {
                GuiFreeFunction freeIndexBuffer =
                    reinterpret_cast<GuiFreeFunction>(GuiFreeIndexBufferAddress);
                freeIndexBuffer(indexBuffer);
            }
            recoveryMask |= 0x2;
        }

        GuiThiscallVoidFunction resetMaterial =
            reinterpret_cast<GuiThiscallVoidFunction>(GuiResetMaterialAddress);
        resetMaterial(viewport);
        recoveryMask |= 0x4;

        GuiThiscallVoidFunction resetStateBlob =
            reinterpret_cast<GuiThiscallVoidFunction>(GuiStateBlobResetAddress);
        resetStateBlob(stateBlob);
        recoveryMask |= 0x8;
        return recoveryMask;
    }

    void InvokeGuiRenderBatch(
        void* batchObject,
        DWORD batchSpan,
        DWORD stackArgument)
    {
        __asm
        {
            push stackArgument
            mov eax, batchSpan
            mov ecx, batchObject
            call dword ptr [GuiRenderBatchAddress]
            add esp, 4
        }
    }

    void __stdcall GuardedGuiRenderBatch(
        void* batchObject,
        DWORD batchSpan,
        DWORD stackArgument)
    {
        uintptr_t faultAddress = 0;
        ULONG_PTR accessAddress = 0;
        DWORD accessType = 0xFFFFFFFFu;
        GuiBatchExceptionKind exceptionKind = GuiBatchExceptionKind::None;
        void* stateBlob = nullptr;
        void* indexBuffer = nullptr;
        __try
        {
            InvokeGuiRenderBatch(batchObject, batchSpan, stackArgument);
        }
        __except (CaptureGuiRenderBatchException(
            GetExceptionInformation(),
            batchObject,
            batchSpan,
            stackArgument,
            &exceptionKind,
            &faultAddress,
            &accessAddress,
            &accessType,
            &stateBlob,
            &indexBuffer))
        {
            bool releaseIndexBuffer =
                exceptionKind == GuiBatchExceptionKind::NullDynamicVbDestination;
            DWORD recoveryMask = RecoverGuiRenderBatch(
                reinterpret_cast<void*>(stackArgument),
                stateBlob,
                indexBuffer,
                releaseIndexBuffer);
            const char* kindName =
                exceptionKind == GuiBatchExceptionKind::NullDynamicVbDestination ?
                "null-dynamic-vb-destination" :
                "nvidia-deferred-flush";
            LONG count = InterlockedIncrement(&GuiBatchExceptionSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT GUI render batch AV contained "
                    "kind=%s fault=0x%08lX accessType=%lu "
                    "accessAddress=0x%08lX batch=0x%08lX span=0x%08lX "
                    "argument=0x%08lX cleanupMask=0x%lX count=%ld",
                    kindName,
                    static_cast<unsigned long>(faultAddress),
                    static_cast<unsigned long>(accessType),
                    static_cast<unsigned long>(accessAddress),
                    static_cast<unsigned long>(
                        reinterpret_cast<uintptr_t>(batchObject)),
                    static_cast<unsigned long>(batchSpan),
                    static_cast<unsigned long>(stackArgument),
                    static_cast<unsigned long>(recoveryMask),
                    static_cast<long>(count));
            }
        }
    }

    __declspec(naked) void GuardedGuiRenderBatchThunk()
    {
        __asm
        {
            mov edx, dword ptr [esp + 4]
            push edx
            push eax
            push ecx
            call GuardedGuiRenderBatch
            ret
        }
    }

    void InvokeGuiCallRenderChild(
        void* childView,
        DWORD argument1,
        DWORD argument2,
        DWORD argument3,
        DWORD argument4,
        DWORD argument5,
        DWORD argument6)
    {
        __asm
        {
            push argument6
            push argument5
            push argument4
            push argument3
            push argument2
            push argument1
            mov ecx, childView
            call dword ptr [GuiCallRenderAddress]
        }
    }

    void __stdcall GuardedGuiCallRenderChild(
        void* childView,
        DWORD argument1,
        DWORD argument2,
        DWORD argument3,
        DWORD argument4,
        DWORD argument5,
        DWORD argument6)
    {
        LONG depth = ++GuiCallRenderDepth;
        if (depth > MaximumGuiCallRenderDepth)
        {
            --GuiCallRenderDepth;
            LONG count = InterlockedIncrement(&GuiCallRenderDepthSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT GUI render recursion depth capped "
                    "child=0x%08lX depth=%ld limit=%ld count=%ld",
                    static_cast<unsigned long>(
                        reinterpret_cast<uintptr_t>(childView)),
                    static_cast<long>(depth),
                    static_cast<long>(MaximumGuiCallRenderDepth),
                    static_cast<long>(count));
            }
            return;
        }

        InvokeGuiCallRenderChild(
            childView,
            argument1,
            argument2,
            argument3,
            argument4,
            argument5,
            argument6);
        --GuiCallRenderDepth;
    }

    __declspec(naked) void GuardedGuiCallRenderChildThunk()
    {
        __asm
        {
            mov edx, esp
            push dword ptr [edx + 18h]
            push dword ptr [edx + 14h]
            push dword ptr [edx + 10h]
            push dword ptr [edx + 0Ch]
            push dword ptr [edx + 08h]
            push dword ptr [edx + 04h]
            push ecx
            call GuardedGuiCallRenderChild
            ret 18h
        }
    }

    __declspec(naked) void GuiTreeFindOriginalTrampoline()
    {
        __asm
        {
            push ebp
            mov ebp, esp
            push ecx
            push esi
            jmp dword ptr [GuiTreeFindResumeAddress]
        }
    }

    void* InvokeGuiTreeFindOriginal(
        void* tree,
        void** output,
        const void* key)
    {
        void* result = nullptr;
        __asm
        {
            push key
            push output
            mov ecx, tree
            call GuiTreeFindOriginalTrampoline
            mov result, eax
        }
        return result;
    }

    void* __stdcall GuardedGuiTreeFind(
        void* tree,
        void** output,
        const void* key)
    {
        // Ordinary tree lookups are a hot GUI path.  Only divert impossible
        // low addresses such as the observed key=0x8; all normal keys go
        // straight to the original implementation without VirtualQuery.
        if (reinterpret_cast<uintptr_t>(key) >= 0x10000u)
        {
            return InvokeGuiTreeFindOriginal(tree, output, key);
        }

        auto sentinelAddress = reinterpret_cast<void*>(
            reinterpret_cast<uintptr_t>(tree) + sizeof(void*));
        if (!IsReadableRange(sentinelAddress, sizeof(void*)) ||
            !IsWritableRange(output, sizeof(void*)))
        {
            return InvokeGuiTreeFindOriginal(tree, output, key);
        }

        void* sentinel = nullptr;
        std::memcpy(&sentinel, sentinelAddress, sizeof(sentinel));
        std::memcpy(output, &sentinel, sizeof(sentinel));

        LONG count = InterlockedIncrement(&GuiTreeInvalidKeySkipCount);
        if (count <= 16 || count % 100 == 0)
        {
            aorf::Log(
                "PATCH HIT GUI tree invalid key treated as not found "
                "tree=0x%08lX output=0x%08lX key=0x%08lX "
                "sentinel=0x%08lX count=%ld",
                static_cast<unsigned long>(
                    reinterpret_cast<uintptr_t>(tree)),
                static_cast<unsigned long>(
                    reinterpret_cast<uintptr_t>(output)),
                static_cast<unsigned long>(
                    reinterpret_cast<uintptr_t>(key)),
                static_cast<unsigned long>(
                    reinterpret_cast<uintptr_t>(sentinel)),
                static_cast<long>(count));
        }
        return output;
    }

    __declspec(naked) void GuardedGuiTreeFindThunk()
    {
        __asm
        {
            push dword ptr [esp + 8]
            push dword ptr [esp + 8]
            push ecx
            call GuardedGuiTreeFind
            ret 8
        }
    }

    HRESULT WINAPI GuardedDrawIndexedPrimitiveVb(
        void* device,
        DWORD primitiveType,
        void* vertexBuffer,
        DWORD startVertex,
        DWORD vertexCount,
        WORD* indices,
        DWORD indexCount,
        DWORD flags)
    {
        uintptr_t deviceAddress = reinterpret_cast<uintptr_t>(device);
        uintptr_t vertexBufferAddress = reinterpret_cast<uintptr_t>(vertexBuffer);
        uintptr_t indexAddress = reinterpret_cast<uintptr_t>(indices);
        uintptr_t lastIndexAddress = indexAddress;
        const char* invalidReason = nullptr;
        if (deviceAddress < 0x10000u)
        {
            invalidReason = "low-device";
        }
        else if (vertexBufferAddress < 0x10000u)
        {
            invalidReason = "low-vertex-buffer";
        }
        else if (indexCount != 0)
        {
            uint64_t lastIndexAddress64 =
                static_cast<uint64_t>(indexAddress) +
                static_cast<uint64_t>(indexCount - 1) * sizeof(WORD);
            if (indexAddress < 0x10000u || lastIndexAddress64 > 0xFFFFFFFEull)
            {
                invalidReason = "invalid-index-span";
            }
            else
            {
                lastIndexAddress = static_cast<uintptr_t>(lastIndexAddress64);
            }
        }

        if (invalidReason)
        {
            LONG count = InterlockedIncrement(&DriverDrawInputSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 invalid DrawIndexedPrimitiveVB input skipped "
                    "reason=%s device=0x%08lX primitive=%lu vertexBuffer=0x%08lX "
                    "start=%lu vertices=%lu indices=0x%08lX indexCount=%lu "
                    "flags=0x%08lX count=%ld",
                    invalidReason,
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(device)),
                    static_cast<unsigned long>(primitiveType),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(vertexBuffer)),
                    static_cast<unsigned long>(startVertex),
                    static_cast<unsigned long>(vertexCount),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(indices)),
                    static_cast<unsigned long>(indexCount),
                    static_cast<unsigned long>(flags),
                    static_cast<long>(count));
            }
            return S_OK;
        }

        void** vtable = nullptr;
        DrawIndexedPrimitiveVbFunction draw = nullptr;
        HRESULT result = S_OK;
        bool completed = false;
        volatile LONG phase = static_cast<LONG>(GuardedDrawPhase::None);
        GuardedDrawExceptionKind exceptionKind = GuardedDrawExceptionKind::None;
        uintptr_t faultAddress = 0;
        ULONG_PTR accessAddress = 0;
        __try
        {
            phase = static_cast<LONG>(GuardedDrawPhase::InputProbe);
            vtable = *reinterpret_cast<void***>(device);
            volatile DWORD vertexBufferProbe =
                *reinterpret_cast<volatile const DWORD*>(vertexBuffer);
            if (indexCount != 0)
            {
                volatile WORD firstIndexProbe =
                    *reinterpret_cast<volatile const WORD*>(indexAddress);
                volatile WORD lastIndexProbe =
                    *reinterpret_cast<volatile const WORD*>(lastIndexAddress);
                vertexBufferProbe ^= firstIndexProbe;
                vertexBufferProbe ^= lastIndexProbe;
            }

            phase = static_cast<LONG>(GuardedDrawPhase::Resolve);
            if (reinterpret_cast<uintptr_t>(vtable) < 0x10000u)
            {
                invalidReason = "low-vtable";
            }
            else
            {
                draw = reinterpret_cast<DrawIndexedPrimitiveVbFunction>(
                    vtable[0x20]);
                if (reinterpret_cast<uintptr_t>(draw) < 0x10000u)
                {
                    invalidReason = "low-draw-target";
                }
            }

            if (!invalidReason)
            {
                phase = static_cast<LONG>(GuardedDrawPhase::DriverCall);
                result = draw(
                    device,
                    primitiveType,
                    vertexBuffer,
                    startVertex,
                    vertexCount,
                    indices,
                    indexCount,
                    flags);
                completed = true;
            }
        }
        __except (CaptureGuardedDrawException(
            GetExceptionInformation(),
            phase,
            draw,
            &exceptionKind,
            &faultAddress,
            &accessAddress))
        {
        }

        if (completed)
        {
            return result;
        }

        if (exceptionKind != GuardedDrawExceptionKind::NvidiaDriver)
        {
            const char* reason = invalidReason;
            if (!reason)
            {
                switch (exceptionKind)
                {
                case GuardedDrawExceptionKind::InputProbe:
                    reason = "input-probe-av";
                    break;
                case GuardedDrawExceptionKind::Resolve:
                    reason = "draw-resolve-av";
                    break;
                case GuardedDrawExceptionKind::InvalidInitialTarget:
                    reason = "initial-target-execute-av";
                    break;
                default:
                    reason = "invalid-draw-input";
                    break;
                }
            }

            LONG count = InterlockedIncrement(&DriverDrawInputSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 invalid DrawIndexedPrimitiveVB input skipped "
                    "reason=%s fault=0x%08lX accessAddress=0x%08lX "
                    "device=0x%08lX primitive=%lu vertexBuffer=0x%08lX "
                    "start=%lu vertices=%lu indices=0x%08lX indexCount=%lu "
                    "flags=0x%08lX count=%ld",
                    reason,
                    static_cast<unsigned long>(faultAddress),
                    static_cast<unsigned long>(accessAddress),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(device)),
                    static_cast<unsigned long>(primitiveType),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(vertexBuffer)),
                    static_cast<unsigned long>(startVertex),
                    static_cast<unsigned long>(vertexCount),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(indices)),
                    static_cast<unsigned long>(indexCount),
                    static_cast<unsigned long>(flags),
                    static_cast<long>(count));
            }
            return S_OK;
        }

        {
            LONG count = InterlockedIncrement(&DriverDrawExceptionSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 NVIDIA DrawIndexedPrimitiveVB AV skipped "
                    "fault=0x%08lX accessAddress=0x%08lX device=0x%08lX "
                    "primitive=%lu vertexBuffer=0x%08lX start=%lu vertices=%lu "
                    "indices=0x%08lX indexCount=%lu flags=0x%08lX count=%ld",
                    static_cast<unsigned long>(faultAddress),
                    static_cast<unsigned long>(accessAddress),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(device)),
                    static_cast<unsigned long>(primitiveType),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(vertexBuffer)),
                    static_cast<unsigned long>(startVertex),
                    static_cast<unsigned long>(vertexCount),
                    static_cast<unsigned long>(reinterpret_cast<uintptr_t>(indices)),
                    static_cast<unsigned long>(indexCount),
                    static_cast<unsigned long>(flags),
                    static_cast<long>(count));
            }
            return S_OK;
        }
    }

    bool PatchDriverDrawCall(uint8_t* callsite)
    {
        constexpr uint8_t ExpectedCall[] = { 0xFF, 0x91, 0x80, 0x00, 0x00, 0x00 };
        if (std::memcmp(callsite, ExpectedCall, sizeof(ExpectedCall)) != 0)
        {
            return false;
        }

        uint8_t patchedCall[sizeof(ExpectedCall)] = { 0xE8, 0, 0, 0, 0, 0x90 };
        uint32_t nextInstruction =
            static_cast<uint32_t>(reinterpret_cast<uintptr_t>(callsite + 5));
        uint32_t destination = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(&GuardedDrawIndexedPrimitiveVb));
        int32_t displacement = static_cast<int32_t>(destination - nextInstruction);
        std::memcpy(patchedCall + 1, &displacement, sizeof(displacement));

        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            return false;
        }

        std::memcpy(callsite, patchedCall, sizeof(patchedCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(patchedCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            patchedCall,
            sizeof(patchedCall)) == 0;
        if (!flushed || !verified)
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "randy31 DrawIndexedPrimitiveVB patch");
            return false;
        }

        DWORD ignored = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                oldProtection,
                &ignored))
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "randy31 DrawIndexedPrimitiveVB patch");
            return false;
        }
        return true;
    }

    void RestoreDriverDrawCall(uint8_t* callsite)
    {
        constexpr uint8_t OriginalCall[] =
        {
            0xFF, 0x91, 0x80, 0x00, 0x00, 0x00
        };
        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(OriginalCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            FailFastPatchRollback("randy31 DrawIndexedPrimitiveVB patch");
        }
        std::memcpy(callsite, OriginalCall, sizeof(OriginalCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(OriginalCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            OriginalCall,
            sizeof(OriginalCall)) == 0;
        DWORD ignored = 0;
        bool restored = VirtualProtect(
            callsite,
            sizeof(OriginalCall),
            oldProtection,
            &ignored) != FALSE;
        if (!flushed || !verified || !restored)
        {
            FailFastPatchRollback("randy31 DrawIndexedPrimitiveVB patch");
        }
    }

    bool PatchGuiRenderBatchCall(uint8_t* callsite)
    {
        constexpr uint8_t ExpectedCall[] =
        {
            0xE8, 0xC9, 0xDF, 0xFF, 0xFF
        };
        if (std::memcmp(callsite, ExpectedCall, sizeof(ExpectedCall)) != 0)
        {
            return false;
        }

        uint8_t patchedCall[sizeof(ExpectedCall)] = { 0xE8, 0, 0, 0, 0 };
        uint32_t nextInstruction = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(callsite + sizeof(patchedCall)));
        uint32_t destination = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(&GuardedGuiRenderBatchThunk));
        int32_t displacement = static_cast<int32_t>(destination - nextInstruction);
        std::memcpy(patchedCall + 1, &displacement, sizeof(displacement));

        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            return false;
        }

        std::memcpy(callsite, patchedCall, sizeof(patchedCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(patchedCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            patchedCall,
            sizeof(patchedCall)) == 0;
        if (!flushed || !verified)
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "GUI render-batch patch");
            return false;
        }

        DWORD ignored = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                oldProtection,
                &ignored))
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "GUI render-batch patch");
            return false;
        }
        return true;
    }

    void RestoreGuiRenderBatchCall(uint8_t* callsite)
    {
        constexpr uint8_t OriginalCall[] =
        {
            0xE8, 0xC9, 0xDF, 0xFF, 0xFF
        };
        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(OriginalCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            FailFastPatchRollback("GUI render-batch patch");
        }
        std::memcpy(callsite, OriginalCall, sizeof(OriginalCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(OriginalCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            OriginalCall,
            sizeof(OriginalCall)) == 0;
        DWORD ignored = 0;
        bool restored = VirtualProtect(
            callsite,
            sizeof(OriginalCall),
            oldProtection,
            &ignored) != FALSE;
        if (!flushed || !verified || !restored)
        {
            FailFastPatchRollback("GUI render-batch patch");
        }
    }

    bool PatchGuiCallRenderChildCall(uint8_t* callsite)
    {
        constexpr uint8_t ExpectedCall[] =
        {
            0xE8, 0x16, 0xFE, 0xFF, 0xFF
        };
        if (std::memcmp(callsite, ExpectedCall, sizeof(ExpectedCall)) != 0)
        {
            return false;
        }

        uint8_t patchedCall[sizeof(ExpectedCall)] = { 0xE8, 0, 0, 0, 0 };
        uint32_t nextInstruction = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(callsite + sizeof(patchedCall)));
        uint32_t destination = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(&GuardedGuiCallRenderChildThunk));
        int32_t displacement = static_cast<int32_t>(destination - nextInstruction);
        std::memcpy(patchedCall + 1, &displacement, sizeof(displacement));

        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            return false;
        }

        std::memcpy(callsite, patchedCall, sizeof(patchedCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(patchedCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            patchedCall,
            sizeof(patchedCall)) == 0;
        if (!flushed || !verified)
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "GUI render-depth call patch");
            return false;
        }

        DWORD ignored = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(patchedCall),
                oldProtection,
                &ignored))
        {
            RestorePatchedBytesOrTerminate(
                callsite,
                ExpectedCall,
                sizeof(ExpectedCall),
                oldProtection,
                "GUI render-depth call patch");
            return false;
        }
        return true;
    }

    void RestoreGuiCallRenderChildCall(uint8_t* callsite)
    {
        constexpr uint8_t OriginalCall[] =
        {
            0xE8, 0x16, 0xFE, 0xFF, 0xFF
        };
        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(OriginalCall),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            FailFastPatchRollback("GUI render-depth call patch");
        }
        std::memcpy(callsite, OriginalCall, sizeof(OriginalCall));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(OriginalCall)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            OriginalCall,
            sizeof(OriginalCall)) == 0;
        DWORD ignored = 0;
        bool restored = VirtualProtect(
            callsite,
            sizeof(OriginalCall),
            oldProtection,
            &ignored) != FALSE;
        if (!flushed || !verified || !restored)
        {
            FailFastPatchRollback("GUI render-depth call patch");
        }
    }

    bool PatchGuiTreeFindEntry(uint8_t* entry)
    {
        constexpr uint8_t ExpectedPrologue[] =
        {
            0x55, 0x8B, 0xEC, 0x51, 0x56
        };
        if (std::memcmp(entry, ExpectedPrologue, sizeof(ExpectedPrologue)) != 0)
        {
            return false;
        }

        uint8_t patchedJump[sizeof(ExpectedPrologue)] = { 0xE9, 0, 0, 0, 0 };
        uint32_t nextInstruction = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(entry + sizeof(patchedJump)));
        uint32_t destination = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(&GuardedGuiTreeFindThunk));
        int32_t displacement = static_cast<int32_t>(destination - nextInstruction);
        std::memcpy(patchedJump + 1, &displacement, sizeof(displacement));

        DWORD oldProtection = 0;
        if (!VirtualProtect(
                entry,
                sizeof(patchedJump),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            return false;
        }

        std::memcpy(entry, patchedJump, sizeof(patchedJump));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            entry,
            sizeof(patchedJump)) != FALSE;
        bool verified = std::memcmp(
            entry,
            patchedJump,
            sizeof(patchedJump)) == 0;
        if (!flushed || !verified)
        {
            RestorePatchedBytesOrTerminate(
                entry,
                ExpectedPrologue,
                sizeof(ExpectedPrologue),
                oldProtection,
                "GUI tree-find patch");
            return false;
        }

        DWORD ignored = 0;
        if (!VirtualProtect(
                entry,
                sizeof(patchedJump),
                oldProtection,
                &ignored))
        {
            RestorePatchedBytesOrTerminate(
                entry,
                ExpectedPrologue,
                sizeof(ExpectedPrologue),
                oldProtection,
                "GUI tree-find patch");
            return false;
        }
        return true;
    }

    LONG CALLBACK EarlyRandyRenderStateExceptionGuard(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 0)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        uintptr_t resumeAddress = 0;
        uint32_t vectorBegin = 0;
        uint32_t byteOffset = 0;
        uint32_t vectorIndex = 0;
        if (!TryResolveEarlyRenderStateResume(exception, &resumeAddress) ||
            !IsExactRenderStateVectorEntryFault(
                exception,
                &vectorBegin,
                &byteOffset,
                &vectorIndex))
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        LONG count = InterlockedIncrement(&RenderStateVectorSkipCount);
        if (count <= 16 || count % 100 == 0)
        {
            aorf::Log(
                "PATCH HIT randy31 corrupt render-state vector skipped "
                "vector=0x%08lX offset=0x%08lX index=%lu "
                "entry=0x%08lX count=%ld",
                static_cast<unsigned long>(vectorBegin),
                static_cast<unsigned long>(byteOffset),
                static_cast<unsigned long>(vectorIndex),
                static_cast<unsigned long>(exception->ContextRecord->Edi),
                static_cast<long>(count));
        }

        exception->ContextRecord->Esp += sizeof(uint32_t);
        exception->ContextRecord->Eax = 0;
        exception->ContextRecord->Eip = static_cast<DWORD>(resumeAddress);
        return EXCEPTION_CONTINUE_EXECUTION;
    }

    LONG CALLBACK RandyColorExceptionGuard(EXCEPTION_POINTERS* exception)
    {
        if (!exception || !exception->ExceptionRecord || !exception->ContextRecord ||
            exception->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION ||
            exception->ExceptionRecord->NumberParameters < 2 ||
            exception->ExceptionRecord->ExceptionInformation[0] != 0)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(DrawResourceFaultAddress) &&
            exception->ContextRecord->Eax < 0x10000 &&
            exception->ExceptionRecord->ExceptionInformation[1] ==
                exception->ContextRecord->Eax)
        {
            DWORD frame = exception->ContextRecord->Ebp;
            if (!IsReadableRange(reinterpret_cast<const void*>(frame), 32))
            {
                return EXCEPTION_CONTINUE_SEARCH;
            }

            DWORD previousFrame = 0;
            DWORD returnAddress = 0;
            std::memcpy(
                &previousFrame,
                reinterpret_cast<const void*>(frame),
                sizeof(previousFrame));
            std::memcpy(
                &returnAddress,
                reinterpret_cast<const void*>(frame + 4),
                sizeof(returnAddress));
            if (returnAddress < 0x10000)
            {
                return EXCEPTION_CONTINUE_SEARCH;
            }

            exception->ContextRecord->Eax = 0;
            exception->ContextRecord->Eip = returnAddress;
            exception->ContextRecord->Esp = frame + 32;
            exception->ContextRecord->Ebp = previousFrame;
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(RenderStateFaultAddress) &&
            exception->ContextRecord->Eax > 0x400 &&
            IsReadableRange(
                reinterpret_cast<const void*>(exception->ContextRecord->Esp),
                sizeof(uint32_t)))
        {
            uint32_t pushedStateClass = 0;
            std::memcpy(
                &pushedStateClass,
                reinterpret_cast<const void*>(exception->ContextRecord->Esp),
                sizeof(pushedStateClass));
            if (pushedStateClass != 0x0A)
            {
                return EXCEPTION_CONTINUE_SEARCH;
            }

            LONG count = InterlockedIncrement(&RenderStateSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 invalid render-state skipped state=0x%08lX "
                    "device=0x%08lX entry=0x%08lX accessAddress=0x%08lX count=%ld",
                    static_cast<unsigned long>(exception->ContextRecord->Eax),
                    static_cast<unsigned long>(exception->ContextRecord->Ebx),
                    static_cast<unsigned long>(exception->ContextRecord->Edi),
                    static_cast<unsigned long>(
                        exception->ExceptionRecord->ExceptionInformation[1]),
                    static_cast<long>(count));
            }

            exception->ContextRecord->Esp += sizeof(uint32_t);
            exception->ContextRecord->Eax = 0;
            exception->ContextRecord->Eip = static_cast<DWORD>(RenderStateResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(ByteColorFaultAddress) &&
            exception->ContextRecord->Eax < 0x10000)
        {
            exception->ContextRecord->Eax = 0;
            exception->ContextRecord->Ebx = 0;
            exception->ContextRecord->Edi = 0;
            exception->ContextRecord->Eip = static_cast<DWORD>(ByteColorResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(IndirectColorFaultAddress) &&
            exception->ContextRecord->Ecx != 0 &&
            exception->ContextRecord->Ecx < 0x10000 &&
            exception->ExceptionRecord->ExceptionInformation[1] ==
                exception->ContextRecord->Ecx &&
            exception->ContextRecord->Edi != 0 &&
            (exception->ContextRecord->Edi &
                (exception->ContextRecord->Edi - 1)) == 0)
        {
            LONG count = InterlockedIncrement(&IndirectColorSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 invalid indirect color sample skipped "
                    "pointer=0x%08lX sampleMask=0x%08lX count=%ld",
                    static_cast<unsigned long>(exception->ContextRecord->Ecx),
                    static_cast<unsigned long>(exception->ContextRecord->Edi),
                    static_cast<long>(count));
            }

            exception->ContextRecord->Ecx = 0;
            exception->ContextRecord->Eip =
                static_cast<DWORD>(IndirectColorResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (exception->ExceptionRecord->ExceptionAddress ==
                reinterpret_cast<void*>(DwordColorFaultAddress) &&
            exception->ContextRecord->Esi < 0x10000)
        {
            exception->ContextRecord->Esi = 0;
            exception->ContextRecord->Eip = static_cast<DWORD>(DwordColorResumeAddress);
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        return EXCEPTION_CONTINUE_SEARCH;
    }
}

namespace aorf
{
    bool InstallEarlyRandyExceptionGuard()
    {
        if (InterlockedCompareExchangePointer(
                &EarlyRandyExceptionGuardHandle,
                nullptr,
                nullptr))
        {
            return true;
        }

        PVOID candidate = AddVectoredExceptionHandler(
            1,
            EarlyRandyRenderStateExceptionGuard);
        if (!candidate)
        {
            Log("ERROR early randy31 render-state exception guard installation "
                "failed code=%lu",
                GetLastError());
            return false;
        }

        PVOID existing = InterlockedCompareExchangePointer(
            &EarlyRandyExceptionGuardHandle,
            candidate,
            nullptr);
        if (existing)
        {
            RemoveVectoredExceptionHandler(candidate);
            return true;
        }

        Log("PATCH PASS early randy31 render-state vector guard active "
            "faultRva=0x25118 resumeRva=0x25147");
        return true;
    }

    bool InstallRandyColorFix()
    {
        if (!InstallEarlyRandyExceptionGuard())
        {
            return false;
        }

        HMODULE randy = GetModuleHandleW(L"randy31.dll");
        HMODULE gui = GetModuleHandleW(L"GUI.dll");
        if (!randy || !gui)
        {
            Log("ERROR old-client renderer modules are not loaded");
            return false;
        }

        auto base = reinterpret_cast<uint8_t*>(randy);
        auto guiBase = reinterpret_cast<uint8_t*>(gui);
        constexpr uint8_t ExpectedFaultSequence[] =
        {
            0x0F, 0xB6, 0x78, 0x02,
            0x0F, 0xB6, 0x58, 0x01,
            0x0F, 0xB6, 0x00
        };
        constexpr uint8_t ExpectedDrawResourceFaultSequence[] =
        {
            0x55, 0x8B, 0xEC,
            0xFF, 0x75, 0x1C,
            0x8B, 0x45, 0x08,
            0xFF, 0x75, 0x18,
            0x8B, 0x00,
            0xFF, 0x75, 0x14,
            0xFF, 0x75, 0x10,
            0xFF, 0x75, 0x0C,
            0xFF, 0x30,
            0xE8, 0xCB, 0xFE, 0xFF, 0xFF,
            0x5D,
            0xC2, 0x18, 0x00
        };
        constexpr uint8_t ExpectedDwordFaultSequence[] =
        {
            0x8B, 0x36,
            0x8B, 0x36,
            0x81, 0xCE, 0x00, 0x00, 0x00, 0xFF
        };
        constexpr uint8_t ExpectedIndirectFaultSequence[] =
        {
            0x8B, 0x09,
            0x8B, 0x09,
            0x85, 0xC9,
            0x75, 0x04,
            0x0B, 0xD7,
            0xEB, 0x03
        };
        constexpr uint8_t ExpectedSharedCreateDeviceCall[] =
        {
            0xE8, 0x5F, 0xD0, 0xFD, 0xFF
        };
        constexpr uint8_t ExpectedGuiBatchPrologue[] =
        {
            0x55, 0x8B, 0xEC,
            0x83, 0xEC, 0x30,
            0x53,
            0x56,
            0x8B, 0xF1
        };
        constexpr uint8_t ExpectedGuiBatchEpilogue[] =
        {
            0x5F, 0x5E, 0x5B, 0xC9, 0xC3
        };
        constexpr uint8_t ExpectedGuiBatchCaller[] =
        {
            0x8B, 0x40, 0x0C,
            0x89, 0x45, 0xF4,
            0x03, 0x4D, 0xF0,
            0xFF, 0x75, 0x08,
            0x8B, 0xC6,
            0x2B, 0x45, 0xF8,
            0xE8, 0xC9, 0xDF, 0xFF, 0xFF,
            0x8B, 0x45, 0xFC,
            0x59
        };
        constexpr uint8_t ExpectedGuiNullDynamicVbFaultSequence[] =
        {
            0x8B, 0x4D, 0xF0,
            0x8B, 0x30,
            0x83, 0x65, 0xF8, 0x00,
            0x8D, 0x3C, 0x0A,
            0x6A, 0x1C,
            0x59,
            0xF3, 0xA5
        };
        constexpr uint8_t ExpectedGuiFreeIndexBufferCall[] =
        {
            0xE8, 0x30, 0x2A, 0x02, 0x00
        };
        uint8_t expectedGuiDynamicVbGetCall[] = { 0xFF, 0x15, 0, 0, 0, 0 };
        uint8_t expectedGuiDynamicVbGetVbCall[] = { 0xFF, 0x15, 0, 0, 0, 0 };
        uint8_t expectedGuiResetMaterialCall[] = { 0xFF, 0x15, 0, 0, 0, 0 };
        uint8_t expectedGuiStateBlobResetCall[] = { 0xFF, 0x15, 0, 0, 0, 0 };
        uint32_t guiDynamicVbGetSlot = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(guiBase + 0x1A865C));
        uint32_t guiDynamicVbGetVbSlot = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(guiBase + 0x1A8664));
        uint32_t guiResetMaterialSlot = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(guiBase + 0x1A8638));
        uint32_t guiStateBlobResetSlot = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(guiBase + 0x1A863C));
        std::memcpy(
            expectedGuiDynamicVbGetCall + 2,
            &guiDynamicVbGetSlot,
            sizeof(guiDynamicVbGetSlot));
        std::memcpy(
            expectedGuiDynamicVbGetVbCall + 2,
            &guiDynamicVbGetVbSlot,
            sizeof(guiDynamicVbGetVbSlot));
        std::memcpy(
            expectedGuiResetMaterialCall + 2,
            &guiResetMaterialSlot,
            sizeof(guiResetMaterialSlot));
        std::memcpy(
            expectedGuiStateBlobResetCall + 2,
            &guiStateBlobResetSlot,
            sizeof(guiStateBlobResetSlot));
        uint32_t guiDynamicVbGetFunction = 0;
        uint32_t guiDynamicVbGetVbFunction = 0;
        uint32_t guiResetMaterialFunction = 0;
        uint32_t guiStateBlobResetFunction = 0;
        std::memcpy(
            &guiDynamicVbGetFunction,
            guiBase + 0x1A865C,
            sizeof(guiDynamicVbGetFunction));
        std::memcpy(
            &guiDynamicVbGetVbFunction,
            guiBase + 0x1A8664,
            sizeof(guiDynamicVbGetVbFunction));
        std::memcpy(
            &guiResetMaterialFunction,
            guiBase + 0x1A8638,
            sizeof(guiResetMaterialFunction));
        std::memcpy(
            &guiStateBlobResetFunction,
            guiBase + 0x1A863C,
            sizeof(guiStateBlobResetFunction));
        constexpr uint8_t ExpectedGuiTreeFindResume[] =
        {
            0xFF, 0x75, 0x0C,
            0x8B, 0xF1,
            0xE8, 0xA5, 0xFD, 0xFF, 0xFF,
            0x8B, 0x76, 0x04,
            0x89, 0x45, 0xFC,
            0x3B, 0xC6,
            0x74, 0x17
        };
        constexpr uint8_t ExpectedGuiTreeFindNotFound[] =
        {
            0x89, 0x75, 0x0C,
            0x8D, 0x45, 0x0C,
            0x8B, 0x08,
            0x8B, 0x45, 0x08,
            0x89, 0x08,
            0x5E,
            0xC9,
            0xC2, 0x08, 0x00
        };
        if (std::memcmp(
                base + 0x6C3A1,
                ExpectedFaultSequence,
                sizeof(ExpectedFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x21A88,
                ExpectedDrawResourceFaultSequence,
                sizeof(ExpectedDrawResourceFaultSequence)) != 0 ||
            std::memcmp(
                base + RenderStateVectorStartRva,
                ExpectedRenderStateFaultSequence,
                sizeof(ExpectedRenderStateFaultSequence)) != 0 ||
            std::memcmp(
                base + RenderStateVectorExitRva,
                ExpectedRenderStateVectorExitSequence,
                sizeof(ExpectedRenderStateVectorExitSequence)) != 0 ||
            std::memcmp(
                base + 0x6C51B,
                ExpectedDwordFaultSequence,
                sizeof(ExpectedDwordFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x6C474,
                ExpectedIndirectFaultSequence,
                sizeof(ExpectedIndirectFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x43BE5,
                ExpectedSharedCreateDeviceCall,
                sizeof(ExpectedSharedCreateDeviceCall)) != 0 ||
            std::memcmp(
                guiBase + 0x150E17,
                ExpectedGuiBatchPrologue,
                sizeof(ExpectedGuiBatchPrologue)) != 0 ||
            std::memcmp(
                guiBase + 0x150FA9,
                ExpectedGuiBatchEpilogue,
                sizeof(ExpectedGuiBatchEpilogue)) != 0 ||
            std::memcmp(
                guiBase + 0x152E38,
                ExpectedGuiBatchCaller,
                sizeof(ExpectedGuiBatchCaller)) != 0 ||
            std::memcmp(
                guiBase + 0x150F13,
                ExpectedGuiNullDynamicVbFaultSequence,
                sizeof(ExpectedGuiNullDynamicVbFaultSequence)) != 0 ||
            std::memcmp(
                guiBase + 0x150E85,
                expectedGuiDynamicVbGetCall,
                sizeof(expectedGuiDynamicVbGetCall)) != 0 ||
            std::memcmp(
                guiBase + 0x150F76,
                expectedGuiDynamicVbGetVbCall,
                sizeof(expectedGuiDynamicVbGetVbCall)) != 0 ||
            std::memcmp(
                guiBase + 0x150F91,
                ExpectedGuiFreeIndexBufferCall,
                sizeof(ExpectedGuiFreeIndexBufferCall)) != 0 ||
            std::memcmp(
                guiBase + 0x150F9A,
                expectedGuiResetMaterialCall,
                sizeof(expectedGuiResetMaterialCall)) != 0 ||
            std::memcmp(
                guiBase + 0x150FA3,
                expectedGuiStateBlobResetCall,
                sizeof(expectedGuiStateBlobResetCall)) != 0 ||
            guiDynamicVbGetFunction != static_cast<uint32_t>(
                reinterpret_cast<uintptr_t>(base + 0x14275)) ||
            guiDynamicVbGetVbFunction != static_cast<uint32_t>(
                reinterpret_cast<uintptr_t>(base + 0x141C5)) ||
            guiResetMaterialFunction != static_cast<uint32_t>(
                reinterpret_cast<uintptr_t>(base + 0x4B724)) ||
            guiStateBlobResetFunction != static_cast<uint32_t>(
                reinterpret_cast<uintptr_t>(base + 0x24D1E)) ||
            !IsExecutableAddress(reinterpret_cast<const void*>(
                static_cast<uintptr_t>(guiDynamicVbGetFunction))) ||
            !IsExecutableAddress(reinterpret_cast<const void*>(
                static_cast<uintptr_t>(guiDynamicVbGetVbFunction))) ||
            !IsExecutableAddress(reinterpret_cast<const void*>(
                static_cast<uintptr_t>(guiResetMaterialFunction))) ||
            !IsExecutableAddress(reinterpret_cast<const void*>(
                static_cast<uintptr_t>(guiStateBlobResetFunction))) ||
            !IsExecutableAddress(guiBase + 0x1739C6) ||
            !IsWritableRange(guiBase + 0x2767C0, 0x18C) ||
            std::memcmp(
                guiBase + 0x4F2F4,
                ExpectedGuiTreeFindResume,
                sizeof(ExpectedGuiTreeFindResume)) != 0 ||
            std::memcmp(
                guiBase + 0x4F31F,
                ExpectedGuiTreeFindNotFound,
                sizeof(ExpectedGuiTreeFindNotFound)) != 0)
        {
            Log("ERROR unsupported randy31 renderer layout");
            return false;
        }

        DrawResourceFaultAddress = reinterpret_cast<uintptr_t>(base + 0x21A94);
        RenderStateFaultAddress = reinterpret_cast<uintptr_t>(base + 0x2511A);
        RenderStateResumeAddress = reinterpret_cast<uintptr_t>(base + 0x2512F);
        ByteColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C3A1);
        ByteColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C3AC);
        IndirectColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C476);
        IndirectColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C478);
        DwordColorFaultAddress = reinterpret_cast<uintptr_t>(base + 0x6C51D);
        DwordColorResumeAddress = reinterpret_cast<uintptr_t>(base + 0x6C51F);
        GuiRenderBatchAddress = reinterpret_cast<uintptr_t>(guiBase + 0x150E17);
        GuiCallRenderAddress = reinterpret_cast<uintptr_t>(guiBase + 0x14D4F5);
        GuiNullDynamicVbFaultAddress = reinterpret_cast<uintptr_t>(
            guiBase + 0x150F22);
        GuiDynamicVbGetAddress = guiDynamicVbGetFunction;
        GuiDynamicVbGetVbAddress = guiDynamicVbGetVbFunction;
        GuiResetMaterialAddress = guiResetMaterialFunction;
        GuiStateBlobResetAddress = guiStateBlobResetFunction;
        GuiFreeIndexBufferAddress = reinterpret_cast<uintptr_t>(
            guiBase + 0x1739C6);
        GuiStateBlobArrayAddress = reinterpret_cast<uintptr_t>(
            guiBase + 0x2767C0);
        GuiStaticIndexBufferAddress = reinterpret_cast<uintptr_t>(
            guiBase + 0x276980);
        GuiTreeFindResumeAddress = reinterpret_cast<uintptr_t>(guiBase + 0x4F2F4);
        uintptr_t d3dimBegin = 0;
        uintptr_t d3dimEnd = 0;
        uintptr_t ddrawBegin = 0;
        uintptr_t ddrawEnd = 0;
        uintptr_t nvidiaBegin = 0;
        uintptr_t nvidiaEnd = 0;
        if (!TryGetModuleImageRange(
                GetModuleHandleW(L"D3DIM700.DLL"),
                &d3dimBegin,
                &d3dimEnd) ||
            !TryGetModuleImageRange(
                GetModuleHandleW(L"DDRAW.dll"),
                &ddrawBegin,
                &ddrawEnd) ||
            !TryGetModuleImageRange(
                GetModuleHandleW(L"nvd3dum.dll"),
                &nvidiaBegin,
                &nvidiaEnd))
        {
            Log("ERROR renderer module range verification failed");
            return false;
        }

        PVOID exceptionGuard = AddVectoredExceptionHandler(1, RandyColorExceptionGuard);
        if (!exceptionGuard)
        {
            Log("ERROR randy31 color-read exception guard installation failed code=%lu",
                GetLastError());
            DrawResourceFaultAddress = 0;
            RenderStateFaultAddress = 0;
            RenderStateResumeAddress = 0;
            ByteColorFaultAddress = 0;
            ByteColorResumeAddress = 0;
            IndirectColorFaultAddress = 0;
            IndirectColorResumeAddress = 0;
            DwordColorFaultAddress = 0;
            DwordColorResumeAddress = 0;
            return false;
        }

        const char* patchFailure = nullptr;
        bool patchTransactionInstalled = false;
        {
            ScopedOtherThreadSuspension suspension;
            if (!suspension.Suspend())
            {
                patchFailure = "could not suspend the other client threads";
            }
            else
            {
                bool executing = false;
                bool unsafeRendererExecution = false;
                bool contextsRead =
                    suspension.IsAnyThreadExecutingInRange(
                        reinterpret_cast<uintptr_t>(base + 0x43B99),
                        reinterpret_cast<uintptr_t>(base + 0x43BF0),
                        &executing);
                unsafeRendererExecution = unsafeRendererExecution || executing;
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        reinterpret_cast<uintptr_t>(base + 0x20C49),
                        reinterpret_cast<uintptr_t>(base + 0x20C80),
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        reinterpret_cast<uintptr_t>(guiBase + 0x14D4F5),
                        reinterpret_cast<uintptr_t>(guiBase + 0x14D8F6),
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        reinterpret_cast<uintptr_t>(guiBase + 0x150E17),
                        reinterpret_cast<uintptr_t>(guiBase + 0x150FAE),
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        reinterpret_cast<uintptr_t>(guiBase + 0x4F2EF),
                        reinterpret_cast<uintptr_t>(guiBase + 0x4F2F4),
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        d3dimBegin,
                        d3dimEnd,
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        ddrawBegin,
                        ddrawEnd,
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }
                if (contextsRead)
                {
                    contextsRead = suspension.IsAnyThreadExecutingInRange(
                        nvidiaBegin,
                        nvidiaEnd,
                        &executing);
                    unsafeRendererExecution =
                        unsafeRendererExecution || executing;
                }

                void* rendererDevice = nullptr;
                if (!contextsRead)
                {
                    patchFailure = "suspended thread context inspection failed";
                }
                else if (unsafeRendererExecution)
                {
                    patchFailure = "renderer initialization was already in flight";
                }
                else if (!IsReadableRange(base + 0x17D318, sizeof(rendererDevice)))
                {
                    patchFailure = "renderer device output was unreadable";
                }
                else
                {
                    std::memcpy(
                        &rendererDevice,
                        base + 0x17D318,
                        sizeof(rendererDevice));
                    if (rendererDevice)
                    {
                        patchFailure = "renderer device was already created";
                    }
                    else if (!PatchDriverDrawCall(base + 0x219B4))
                    {
                        patchFailure = "NVIDIA draw-call patch failed";
                    }
                    else if (!PatchGuiRenderBatchCall(guiBase + 0x152E49))
                    {
                        RestoreDriverDrawCall(base + 0x219B4);
                        patchFailure = "GUI render-batch patch failed";
                    }
                    else if (!PatchGuiCallRenderChildCall(guiBase + 0x14D6DA))
                    {
                        RestoreGuiRenderBatchCall(guiBase + 0x152E49);
                        RestoreDriverDrawCall(base + 0x219B4);
                        patchFailure = "GUI render-depth call patch failed";
                    }
                    else if (!PatchGuiTreeFindEntry(guiBase + 0x4F2EF))
                    {
                        RestoreGuiCallRenderChildCall(guiBase + 0x14D6DA);
                        RestoreGuiRenderBatchCall(guiBase + 0x152E49);
                        RestoreDriverDrawCall(base + 0x219B4);
                        patchFailure = "GUI tree-find patch failed";
                    }
                    else
                    {
                        patchTransactionInstalled = true;
                    }
                }
            }
        }

        if (!patchTransactionInstalled)
        {
            RemoveVectoredExceptionHandler(exceptionGuard);
            DrawResourceFaultAddress = 0;
            RenderStateFaultAddress = 0;
            RenderStateResumeAddress = 0;
            ByteColorFaultAddress = 0;
            ByteColorResumeAddress = 0;
            IndirectColorFaultAddress = 0;
            IndirectColorResumeAddress = 0;
            DwordColorFaultAddress = 0;
            DwordColorResumeAddress = 0;
            GuiRenderBatchAddress = 0;
            GuiCallRenderAddress = 0;
            GuiNullDynamicVbFaultAddress = 0;
            GuiDynamicVbGetAddress = 0;
            GuiDynamicVbGetVbAddress = 0;
            GuiResetMaterialAddress = 0;
            GuiStateBlobResetAddress = 0;
            GuiFreeIndexBufferAddress = 0;
            GuiStateBlobArrayAddress = 0;
            GuiStaticIndexBufferAddress = 0;
            GuiTreeFindResumeAddress = 0;
            Log("ERROR randy31 renderer patch transaction failed: %s",
                patchFailure ? patchFailure : "unknown failure");
            return false;
        }

        Log("PATCH PASS randy31 renderer/color/driver guards "
            "deviceSelector=preserved drawCallRva=0x219B4 "
            "guiRenderDepthCallRva=0x14D6DA guiRenderDepthLimit=128 "
            "guiBatchCallRva=0x152E49 guiNullDynamicVbRva=0x150F22 "
            "guiTreeFindRva=0x4F2EF "
            "faultRvas=0x21A94,0x25118,0x2511A,0x6C3A1,0x6C476,0x6C51D");
        return true;
    }
}
