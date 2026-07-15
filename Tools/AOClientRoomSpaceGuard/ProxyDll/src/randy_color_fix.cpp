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
    uintptr_t GuiTreeFindResumeAddress = 0;
    volatile LONG* RendererDeviceSelectorAddress = nullptr;
    LONG RenderStateSkipCount = 0;
    LONG IndirectColorSkipCount = 0;
    LONG RendererDeviceSelectorSwitchCount = 0;
    LONG DriverDrawInputSkipCount = 0;
    LONG DriverDrawExceptionSkipCount = 0;
    LONG GuiBatchExceptionSkipCount = 0;
    LONG GuiTreeInvalidKeySkipCount = 0;

    using DrawIndexedPrimitiveVbFunction = HRESULT (WINAPI*)(
        void*,
        DWORD,
        void*,
        DWORD,
        DWORD,
        WORD*,
        DWORD,
        DWORD);

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

    DWORD __cdecl NormalizeRendererDeviceSelectorImpl()
    {
        volatile LONG* selector = RendererDeviceSelectorAddress;
        if (!selector)
        {
            return 0;
        }

        LONG selected = InterlockedCompareExchange(selector, 1, 2);
        if (selected == 2)
        {
            LONG count = InterlockedIncrement(&RendererDeviceSelectorSwitchCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 renderer device normalized "
                    "TnLHAL=2 HAL=1 count=%ld",
                    static_cast<long>(count));
            }
            return 1;
        }

        return static_cast<DWORD>(selected);
    }

    __declspec(naked) DWORD NormalizeRendererDeviceSelector()
    {
        __asm
        {
            push ecx
            push edx
            call NormalizeRendererDeviceSelectorImpl
            pop edx
            pop ecx
            ret
        }
    }

    bool PatchRendererDeviceSelectorLoad(
        uint8_t* callsite,
        volatile LONG* selector,
        const void* rendererDeviceOutput)
    {
        constexpr uint8_t ExpectedCompare[] =
        {
            0x83, 0xF8, 0x01,
            0x75, 0x1A
        };
        uintptr_t encodedSelector = 0;
        uintptr_t encodedRendererDeviceOutput = 0;
        if (callsite[0] != 0xA1 ||
            callsite[5] != 0xBE ||
            std::memcmp(
                callsite + 10,
                ExpectedCompare,
                sizeof(ExpectedCompare)) != 0)
        {
            return false;
        }

        std::memcpy(&encodedSelector, callsite + 1, sizeof(uint32_t));
        std::memcpy(
            &encodedRendererDeviceOutput,
            callsite + 6,
            sizeof(uint32_t));
        if (encodedSelector != reinterpret_cast<uintptr_t>(selector) ||
            encodedRendererDeviceOutput !=
                reinterpret_cast<uintptr_t>(rendererDeviceOutput))
        {
            return false;
        }

        uint8_t original[5] = {};
        std::memcpy(original, callsite, sizeof(original));
        uint8_t patchedCall[sizeof(original)] = { 0xE8, 0, 0, 0, 0 };
        uint32_t nextInstruction =
            static_cast<uint32_t>(reinterpret_cast<uintptr_t>(callsite + 5));
        uint32_t destination = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(&NormalizeRendererDeviceSelector));
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
                original,
                sizeof(original),
                oldProtection,
                "randy31 renderer-selector patch");
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
                original,
                sizeof(original),
                oldProtection,
                "randy31 renderer-selector patch");
            return false;
        }
        return true;
    }

    void RestoreRendererDeviceSelectorLoad(
        uint8_t* callsite,
        volatile LONG* selector)
    {
        uint8_t original[5] = { 0xA1, 0, 0, 0, 0 };
        uint32_t selectorAddress = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(selector));
        std::memcpy(original + 1, &selectorAddress, sizeof(selectorAddress));

        DWORD oldProtection = 0;
        if (!VirtualProtect(
                callsite,
                sizeof(original),
                PAGE_EXECUTE_READWRITE,
                &oldProtection))
        {
            FailFastPatchRollback("randy31 renderer-selector patch");
        }

        std::memcpy(callsite, original, sizeof(original));
        bool flushed = FlushInstructionCache(
            GetCurrentProcess(),
            callsite,
            sizeof(original)) != FALSE;
        bool verified = std::memcmp(
            callsite,
            original,
            sizeof(original)) == 0;
        DWORD ignored = 0;
        bool restored = VirtualProtect(
            callsite,
            sizeof(original),
            oldProtection,
            &ignored) != FALSE;
        if (!flushed || !verified || !restored)
        {
            FailFastPatchRollback("randy31 renderer-selector patch");
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

    int CaptureNvidiaGuiBatchException(
        EXCEPTION_POINTERS* exception,
        uintptr_t* faultAddress,
        ULONG_PTR* accessAddress)
    {
        uint8_t* base = nullptr;
        uintptr_t driverRva = 0;
        constexpr uint8_t ExpectedFault170C490[] =
        {
            0x8B, 0x80, 0x10, 0x00, 0x00, 0x00
        };
        if (!TryGetVerifiedNvidiaFault(exception, &base, &driverRva) ||
            driverRva != 0x0170C490 ||
            !exception->ContextRecord ||
            exception->ContextRecord->Eax != 0x04 ||
            exception->ExceptionRecord->ExceptionInformation[1] != 0x14 ||
            std::memcmp(
                base + driverRva,
                ExpectedFault170C490,
                sizeof(ExpectedFault170C490)) != 0)
        {
            return EXCEPTION_CONTINUE_SEARCH;
        }

        *faultAddress = reinterpret_cast<uintptr_t>(
            exception->ExceptionRecord->ExceptionAddress);
        *accessAddress = exception->ExceptionRecord->ExceptionInformation[1];
        return EXCEPTION_EXECUTE_HANDLER;
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
        __try
        {
            InvokeGuiRenderBatch(batchObject, batchSpan, stackArgument);
        }
        __except (CaptureNvidiaGuiBatchException(
            GetExceptionInformation(),
            &faultAddress,
            &accessAddress))
        {
            LONG count = InterlockedIncrement(&GuiBatchExceptionSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT GUI render batch NVIDIA deferred-flush AV skipped "
                    "fault=0x%08lX accessAddress=0x%08lX batch=0x%08lX "
                    "span=0x%08lX argument=0x%08lX count=%ld",
                    static_cast<unsigned long>(faultAddress),
                    static_cast<unsigned long>(accessAddress),
                    static_cast<unsigned long>(
                        reinterpret_cast<uintptr_t>(batchObject)),
                    static_cast<unsigned long>(batchSpan),
                    static_cast<unsigned long>(stackArgument),
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
        if (!IsReadableRange(device, sizeof(void*)) ||
            !IsReadableRange(vertexBuffer, sizeof(void*)) ||
            (indexCount != 0 &&
                (indexCount > UINT32_MAX / sizeof(WORD) ||
                 !IsReadableRange(indices, indexCount * sizeof(WORD)))))
        {
            LONG count = InterlockedIncrement(&DriverDrawInputSkipCount);
            if (count <= 16 || count % 100 == 0)
            {
                aorf::Log(
                    "PATCH HIT randy31 invalid DrawIndexedPrimitiveVB input skipped "
                    "device=0x%08lX primitive=%lu vertexBuffer=0x%08lX "
                    "start=%lu vertices=%lu indices=0x%08lX indexCount=%lu "
                    "flags=0x%08lX count=%ld",
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
        std::memcpy(&vtable, device, sizeof(vtable));
        if (!IsReadableRange(vtable, 0x84))
        {
            return S_OK;
        }

        DrawIndexedPrimitiveVbFunction draw =
            reinterpret_cast<DrawIndexedPrimitiveVbFunction>(vtable[0x20]);
        if (!IsExecutableAddress(reinterpret_cast<const void*>(draw)))
        {
            return S_OK;
        }

        uintptr_t faultAddress = 0;
        ULONG_PTR accessAddress = 0;
        __try
        {
            return draw(
                device,
                primitiveType,
                vertexBuffer,
                startVertex,
                vertexCount,
                indices,
                indexCount,
                flags);
        }
        __except (CaptureNvidiaDrawException(
            GetExceptionInformation(),
            &faultAddress,
            &accessAddress))
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
    bool InstallRandyColorFix()
    {
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
        constexpr uint8_t ExpectedTnlHalGuid[] =
        {
            0x78, 0x9E, 0x04, 0xF5,
            0x61, 0x48,
            0xD2, 0x11,
            0xA4, 0x07,
            0x00, 0xA0, 0xC9, 0x06, 0x29, 0xA8
        };
        constexpr uint8_t ExpectedHalGuid[] =
        {
            0xE0, 0x3D, 0xE6, 0x84,
            0xAA, 0x46,
            0xCF, 0x11,
            0x81, 0x6F,
            0x00, 0x00, 0xC0, 0x20, 0x15, 0x6E
        };
        uint8_t ExpectedDeviceSelectionBranches[] =
        {
            0xA1, 0, 0, 0, 0,
            0x39, 0x18,
            0x74, 0x06,
            0x8B, 0x00,
            0x8B, 0x00,
            0xEB, 0x02,
            0x33, 0xC0,
            0x56,
            0x50,
            0x68, 0, 0, 0, 0,
            0xEB, 0x1D,
            0x83, 0xF8, 0x02,
            0x75, 0x23,
            0xA1, 0, 0, 0, 0,
            0x39, 0x18,
            0x74, 0x06,
            0x8B, 0x00,
            0x8B, 0x00,
            0xEB, 0x02,
            0x33, 0xC0,
            0x56,
            0x50,
            0x68, 0, 0, 0, 0,
            0x8B, 0x0D, 0, 0, 0, 0
        };
        static_assert(
            sizeof(ExpectedDeviceSelectionBranches) == 61,
            "unexpected randy31 device-selection sequence size");
        uint32_t surfaceSourceAddress = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(base + 0x17D2F8));
        uint32_t halGuidAddress = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(base + 0x99718));
        uint32_t tnlHalGuidAddress = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(base + 0x996E8));
        uint32_t renderAddress = static_cast<uint32_t>(
            reinterpret_cast<uintptr_t>(base + 0x16BED0));
        std::memcpy(ExpectedDeviceSelectionBranches + 1,
            &surfaceSourceAddress, sizeof(surfaceSourceAddress));
        std::memcpy(ExpectedDeviceSelectionBranches + 20,
            &halGuidAddress, sizeof(halGuidAddress));
        std::memcpy(ExpectedDeviceSelectionBranches + 32,
            &surfaceSourceAddress, sizeof(surfaceSourceAddress));
        std::memcpy(ExpectedDeviceSelectionBranches + 51,
            &tnlHalGuidAddress, sizeof(tnlHalGuidAddress));
        std::memcpy(ExpectedDeviceSelectionBranches + 57,
            &renderAddress, sizeof(renderAddress));
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
                base + 0x25110,
                ExpectedRenderStateFaultSequence,
                sizeof(ExpectedRenderStateFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x6C51B,
                ExpectedDwordFaultSequence,
                sizeof(ExpectedDwordFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x6C474,
                ExpectedIndirectFaultSequence,
                sizeof(ExpectedIndirectFaultSequence)) != 0 ||
            std::memcmp(
                base + 0x996E8,
                ExpectedTnlHalGuid,
                sizeof(ExpectedTnlHalGuid)) != 0 ||
            std::memcmp(
                base + 0x99718,
                ExpectedHalGuid,
                sizeof(ExpectedHalGuid)) != 0 ||
            std::memcmp(
                base + 0x43BA8,
                ExpectedDeviceSelectionBranches,
                sizeof(ExpectedDeviceSelectionBranches)) != 0 ||
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
        GuiTreeFindResumeAddress = reinterpret_cast<uintptr_t>(guiBase + 0x4F2F4);
        RendererDeviceSelectorAddress = reinterpret_cast<volatile LONG*>(
            base + 0xB772C);
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
                    else if (!PatchRendererDeviceSelectorLoad(
                            base + 0x43B99,
                            RendererDeviceSelectorAddress,
                            base + 0x17D318))
                    {
                        patchFailure = "HAL renderer selector patch failed";
                    }
                    else if (!PatchDriverDrawCall(base + 0x219B4))
                    {
                        RestoreRendererDeviceSelectorLoad(
                            base + 0x43B99,
                            RendererDeviceSelectorAddress);
                        patchFailure = "NVIDIA draw-call patch failed";
                    }
                    else if (!PatchGuiRenderBatchCall(guiBase + 0x152E49))
                    {
                        RestoreDriverDrawCall(base + 0x219B4);
                        RestoreRendererDeviceSelectorLoad(
                            base + 0x43B99,
                            RendererDeviceSelectorAddress);
                        patchFailure = "GUI render-batch patch failed";
                    }
                    else if (!PatchGuiTreeFindEntry(guiBase + 0x4F2EF))
                    {
                        RestoreGuiRenderBatchCall(guiBase + 0x152E49);
                        RestoreDriverDrawCall(base + 0x219B4);
                        RestoreRendererDeviceSelectorLoad(
                            base + 0x43B99,
                            RendererDeviceSelectorAddress);
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
            GuiTreeFindResumeAddress = 0;
            Log("ERROR randy31 renderer patch transaction failed: %s",
                patchFailure ? patchFailure : "unknown failure");
            return false;
        }

        Log("PATCH PASS randy31 renderer/color/driver guards "
            "deviceSelectorRva=0x43B99 drawCallRva=0x219B4 "
            "guiBatchCallRva=0x152E49 guiTreeFindRva=0x4F2EF "
            "faultRvas=0x21A94,0x2511A,0x6C3A1,0x6C476,0x6C51D");
        return true;
    }
}
