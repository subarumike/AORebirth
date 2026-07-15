using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

internal static class Program
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessSynchronize = 0x00100000;
    private const uint ThreadSuspendResume = 0x0002;
    private const uint SnapshotThread = 0x00000004;
    private const uint SnapshotModule = 0x00000008;
    private const uint SnapshotModule32 = 0x00000010;
    private const uint MemCommit = 0x00001000;
    private const uint MemReserve = 0x00002000;
    private const uint MemRelease = 0x00008000;
    private const uint PageReadWrite = 0x04;
    private const uint PageExecuteRead = 0x20;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitTimeout = 0x00000102;
    private const int TelemetrySlotSize = 44;
    private const int TelemetryPageOffset = 0x1000;
    private const int GuardAllocationSize = 0x2000;
    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

    private static readonly PatchProfile[] Profiles =
    {
        new PatchProfile(
            "new-client",
            "E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168",
            new[] { 0x157BC, 0x16144, 0x168E2, 0x168F6, 0x16F98 },
            0xE095,
            0x3AAEA,
            0x5F894,
            0x5F8EC,
            0x154F8,
            0xDEF4),
        new PatchProfile(
            "old-client",
            "8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB",
            new[] { 0x13F2E, 0x148B6, 0x15054, 0x15068, 0x1570A },
            0xC8AA,
            0x3894A,
            0x5B80C,
            0x5B864,
            0x13C6A,
            0xC709)
    };

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AOClientRoomSpaceGuard");

    private static readonly string LogPath = Path.Combine(LogDirectory, "guard.log");

    public static int Main(string[] args)
    {
        try
        {
            if (HasArgument(args, "--self-test"))
            {
                RunSelfTest();
                return 0;
            }

            string clientRoot = GetArgument(args, "--client-root");
            if (string.IsNullOrWhiteSpace(clientRoot))
            {
                throw new ArgumentException("--client-root is required.");
            }

            clientRoot = Path.GetFullPath(clientRoot.Trim().TrimEnd(Path.DirectorySeparatorChar));
            PatchProfile profile = SelectProfile(clientRoot);

            if (HasArgument(args, "--inspect"))
            {
                Log("INSPECT PASS root=" + clientRoot + " profile=" + profile.Name);
                return 0;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\AOClientRoomSpaceGuard-" + profile.Name, out createdNew))
            {
                if (!createdNew)
                {
                    throw new InvalidOperationException("Another " + profile.Name + " guard is already waiting.");
                }

                try
                {
                    int waitSeconds = ParsePositiveInt(GetArgument(args, "--wait-seconds"), 600);
                    var excludedProcessIds = new HashSet<int>();
                    if (HasArgument(args, "--launch-client"))
                    {
                        excludedProcessIds = GetMatchingClientProcessIds(clientRoot);
                        LaunchClient(clientRoot);
                    }

                    TargetProcess target = WaitForTarget(clientRoot, waitSeconds, excludedProcessIds);
                    ApplyTargetedGuard(target, profile);
                    return 0;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        catch (Exception exception)
        {
            Log("ERROR " + exception.Message);
            return 1;
        }
    }

    private static void RunSelfTest()
    {
        const uint sampleModuleBase = 0x60000000;
        const uint sampleWrapperBase = 0x30000000;
        const uint sampleTelemetryBase = sampleWrapperBase + TelemetryPageOffset;

        foreach (PatchProfile profile in Profiles)
        {
            byte[] wrapper = BuildWrapper(
                profile,
                sampleModuleBase,
                sampleWrapperBase,
                sampleTelemetryBase);

            foreach (int callRva in profile.CollisionCallRvas)
            {
                byte[] originalCall = BuildRelativeCall(
                    sampleModuleBase + (uint)callRva,
                    sampleModuleBase + (uint)profile.PosToRoomRva);
                byte[] patchedCall = BuildRelativeCall(
                    sampleModuleBase + (uint)callRva,
                    sampleWrapperBase);
                Require(originalCall.Length == 5 && patchedCall.Length == 5,
                    profile.Name + " call length at 0x" + callRva.ToString("X"));
                Require(DecodeCallTarget(sampleModuleBase + (uint)callRva, originalCall) ==
                    sampleModuleBase + (uint)profile.PosToRoomRva,
                    profile.Name + " original call target at 0x" + callRva.ToString("X"));
                Require(DecodeCallTarget(sampleModuleBase + (uint)callRva, patchedCall) ==
                    sampleWrapperBase,
                    profile.Name + " patched call target at 0x" + callRva.ToString("X"));
            }

            Require(wrapper.Length > 88 && wrapper.Length < TelemetryPageOffset,
                profile.Name + " wrapper length");
            Require(wrapper[6] == 0xFF && wrapper[7] == 0x71 && wrapper[8] == 0x58,
                profile.Name + " exact room-space capture");
            Require(wrapper[23] == 0xFF && wrapper[24] == 0x75 && wrapper[25] == 0xF8,
                profile.Name + " captured room-space cast input");
            Require(wrapper[36] == 0x74 && wrapper[37] == 0x25,
                profile.Name + " null branch");
            Require(wrapper[50] == 0x78 && wrapper[51] == 0x17,
                profile.Name + " invalid-cell branch");
            Require(DecodeRelativeTarget(sampleWrapperBase, wrapper, 26) ==
                sampleModuleBase + (uint)profile.DynamicCastRva, profile.Name + " dynamic-cast target");
            Require(DecodeRelativeTarget(sampleWrapperBase, wrapper, 43) ==
                sampleModuleBase + (uint)profile.GetInsideCellRva, profile.Name + " inside-cell target");
            Require(DecodeRelativeTarget(sampleWrapperBase, wrapper, 55) ==
                sampleModuleBase + (uint)profile.GetZonesRva, profile.Name + " room-list target");
            Require(DecodeRelativeTarget(sampleWrapperBase, wrapper, 75) == sampleWrapperBase + 91,
                profile.Name + " telemetry recorder target");
            Require(BitConverter.ToUInt32(wrapper, 12) == sampleModuleBase + (uint)profile.TargetTypeRva,
                profile.Name + " target RTTI");
            Require(BitConverter.ToUInt32(wrapper, 17) == sampleModuleBase + (uint)profile.SourceTypeRva,
                profile.Name + " source RTTI");
            for (int index = 0; index < profile.CollisionCallRvas.Length; index++)
            {
                uint sampleSlot = sampleTelemetryBase + (uint)(index * TelemetrySlotSize);
                foreach (uint fieldOffset in new uint[]
                {
                    0u, 4u, 8u, 12u, 16u, 20u, 24u, 28u, 32u, 36u, 40u
                })
                {
                    Require(ContainsUInt32(wrapper, sampleSlot + fieldOffset),
                        profile.Name + " telemetry field address " + index + "+" + fieldOffset);
                }
                Require(ContainsUInt32(wrapper,
                    sampleModuleBase + (uint)profile.CollisionCallRvas[index] + 5u),
                    profile.Name + " telemetry return address " + index);
            }

            Require(wrapper[105] == 0x50, profile.Name + " failure result preservation");
            Require(CountSequence(wrapper, new byte[] { 0x83, 0xC4, 0x04, 0xC3 }) ==
                profile.CollisionCallRvas.Length + 1,
                profile.Name + " telemetry stack cleanup");
        }

        Log("SELF-TEST PASS targetedProfiles=" + Profiles.Length);
    }

    private static PatchProfile SelectProfile(string clientRoot)
    {
        string n3Path = Path.Combine(clientRoot, "N3.dll");
        string clientPath = Path.Combine(clientRoot, "anarchyonline.exe");
        string launcherPath = Path.Combine(clientRoot, "Anarchy.exe");

        Require(File.Exists(n3Path), "N3.dll not found: " + n3Path);
        Require(File.Exists(clientPath), "anarchyonline.exe not found: " + clientPath);
        Require(File.Exists(launcherPath), "Anarchy.exe not found: " + launcherPath);

        string hash = Sha256(n3Path);
        PatchProfile profile = Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Hash, hash, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            throw new InvalidOperationException("Unsupported or modified N3.dll SHA-256: " + hash);
        }

        return profile;
    }

    private static HashSet<int> GetMatchingClientProcessIds(string clientRoot)
    {
        string expectedClient = NormalizePath(Path.Combine(clientRoot, "anarchyonline.exe"));
        var processIds = new HashSet<int>();

        foreach (Process process in Process.GetProcessesByName("anarchyonline"))
        {
            using (process)
            {
                string processPath = TryGetProcessPath(process.Id);
                if (string.Equals(NormalizePath(processPath), expectedClient,
                    StringComparison.OrdinalIgnoreCase))
                {
                    processIds.Add(process.Id);
                }
            }
        }

        return processIds;
    }

    private static void LaunchClient(string clientRoot)
    {
        string launcherPath = Path.Combine(clientRoot, "Anarchy.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = clientRoot,
            UseShellExecute = true
        };

        Process launcher = Process.Start(startInfo);
        Require(launcher != null, "Failed to launch " + launcherPath + ".");
        launcher.Dispose();
        Log("LAUNCH PASS root=" + clientRoot);
    }

    private static TargetProcess WaitForTarget(string clientRoot, int waitSeconds,
        ISet<int> excludedProcessIds)
    {
        string expectedClient = NormalizePath(Path.Combine(clientRoot, "anarchyonline.exe"));
        string expectedN3 = NormalizePath(Path.Combine(clientRoot, "N3.dll"));
        DateTime deadline = DateTime.UtcNow.AddSeconds(waitSeconds);

        Log("WAIT targeted root=" + clientRoot + " timeoutSeconds=" + waitSeconds);

        while (DateTime.UtcNow < deadline)
        {
            foreach (Process process in Process.GetProcessesByName("anarchyonline"))
            {
                using (process)
                {
                    if (excludedProcessIds.Contains(process.Id))
                    {
                        continue;
                    }

                    string processPath = TryGetProcessPath(process.Id);
                    if (!string.Equals(NormalizePath(processPath), expectedClient,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ModuleInfo module = TryFindModule(process.Id, expectedN3);
                    if (module != null)
                    {
                        return new TargetProcess(process.Id, module.BaseAddress);
                    }
                }
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("Timed out waiting for " + expectedClient + " and its N3.dll module.");
    }

    private static void ApplyTargetedGuard(TargetProcess target, PatchProfile profile)
    {
        IntPtr process = OpenProcess(
            ProcessQueryInformation | ProcessVmOperation | ProcessVmRead | ProcessVmWrite |
            ProcessSynchronize,
            false,
            target.ProcessId);

        if (process == IntPtr.Zero)
        {
            ThrowLastWin32("OpenProcess failed");
        }

        IntPtr wrapperAddress = IntPtr.Zero;
        try
        {
            uint moduleBase = ToUInt32(target.ModuleBase, "N3.dll base");
            uint[] callAddresses = profile.CollisionCallRvas
                .Select(rva => moduleBase + (uint)rva)
                .ToArray();
            byte[][] originalCalls = callAddresses
                .Select(address => BuildRelativeCall(address, moduleBase + (uint)profile.PosToRoomRva))
                .ToArray();
            for (int index = 0; index < callAddresses.Length; index++)
            {
                byte[] currentCall = ReadExact(process, new IntPtr(callAddresses[index]), 5);
                Require(currentCall.SequenceEqual(originalCalls[index]),
                    "Unexpected collision call bytes at RVA 0x" +
                    profile.CollisionCallRvas[index].ToString("X") + ".");
            }

            wrapperAddress = VirtualAllocEx(process, IntPtr.Zero, new UIntPtr(GuardAllocationSize),
                MemCommit | MemReserve, PageReadWrite);
            if (wrapperAddress == IntPtr.Zero)
            {
                ThrowLastWin32("VirtualAllocEx failed for targeted wrapper");
            }

            uint wrapperBase = ToUInt32(wrapperAddress, "targeted wrapper address");
            uint telemetryBase = wrapperBase + TelemetryPageOffset;
            byte[] wrapper = BuildWrapper(profile, moduleBase, wrapperBase, telemetryBase);
            WriteExact(process, wrapperAddress, wrapper);
            WriteExact(process, new IntPtr(telemetryBase),
                new byte[profile.CollisionCallRvas.Length * TelemetrySlotSize]);

            uint oldWrapperProtection;
            if (!VirtualProtectEx(process, wrapperAddress, new UIntPtr(0x1000),
                PageExecuteRead, out oldWrapperProtection))
            {
                ThrowLastWin32("VirtualProtectEx failed for targeted wrapper");
            }

            if (!FlushInstructionCache(process, wrapperAddress, new UIntPtr((uint)wrapper.Length)))
            {
                ThrowLastWin32("FlushInstructionCache failed for targeted wrapper");
            }

            byte[][] patchedCalls = callAddresses
                .Select(address => BuildRelativeCall(address, wrapperBase))
                .ToArray();
            List<IntPtr> suspendedThreads = SuspendProcessThreads(target.ProcessId);
            var pageProtections = new Dictionary<IntPtr, uint>();

            try
            {
                foreach (IntPtr codePage in callAddresses
                    .Select(address => new IntPtr(address & 0xFFFFF000u))
                    .Distinct())
                {
                    uint oldCodeProtection;
                    if (!VirtualProtectEx(process, codePage, new UIntPtr(0x1000),
                        PageExecuteReadWrite, out oldCodeProtection))
                    {
                        ThrowLastWin32("VirtualProtectEx failed for collision call page");
                    }

                    pageProtections.Add(codePage, oldCodeProtection);
                }

                try
                {
                    for (int index = 0; index < callAddresses.Length; index++)
                    {
                        WriteExact(process, new IntPtr(callAddresses[index]), patchedCalls[index]);
                    }

                    if (!FlushInstructionCache(process, IntPtr.Zero, UIntPtr.Zero))
                    {
                        ThrowLastWin32("FlushInstructionCache failed for collision calls");
                    }

                    for (int index = 0; index < callAddresses.Length; index++)
                    {
                        Require(ReadExact(process, new IntPtr(callAddresses[index]), 5)
                            .SequenceEqual(patchedCalls[index]),
                            "Collision call verification failed at RVA 0x" +
                            profile.CollisionCallRvas[index].ToString("X") + ".");
                    }

                    Require(ReadExact(process, wrapperAddress, wrapper.Length).SequenceEqual(wrapper),
                        "Targeted wrapper verification failed.");
                    Require(ReadExact(process, new IntPtr(telemetryBase),
                        profile.CollisionCallRvas.Length * TelemetrySlotSize)
                        .All(value => value == 0), "Targeted telemetry initialization failed.");
                }
                catch
                {
                    for (int index = 0; index < callAddresses.Length; index++)
                    {
                        WriteExact(process, new IntPtr(callAddresses[index]), originalCalls[index]);
                    }

                    FlushInstructionCache(process, IntPtr.Zero, UIntPtr.Zero);
                    throw;
                }
            }
            finally
            {
                uint ignored;
                foreach (KeyValuePair<IntPtr, uint> page in pageProtections)
                {
                    VirtualProtectEx(process, page.Key, new UIntPtr(0x1000), page.Value, out ignored);
                }

                ResumeProcessThreads(suspendedThreads);
            }

            Log("PATCH PASS targeted pid=" + target.ProcessId + " profile=" + profile.Name +
                " callRvas=" + string.Join(",", profile.CollisionCallRvas
                    .Select(rva => "0x" + rva.ToString("X"))) +
                " wrapper=0x" + wrapperBase.ToString("X8") +
                " telemetry=0x" + telemetryBase.ToString("X8"));
            wrapperAddress = IntPtr.Zero;
            MonitorTelemetry(process, target.ProcessId, profile, telemetryBase);
        }
        finally
        {
            if (wrapperAddress != IntPtr.Zero)
            {
                VirtualFreeEx(process, wrapperAddress, UIntPtr.Zero, MemRelease);
            }

            CloseHandle(process);
        }
    }

    private static byte[] BuildWrapper(PatchProfile profile, uint moduleBase, uint wrapperBase,
        uint telemetryBase)
    {
        var bytes = new List<byte>
        {
            0x55,
            0x8B, 0xEC,
            0x56,
            0x8B, 0xF1,
            0xFF, 0x71, 0x58,
            0x6A, 0x00,
            0x68
        };
        AppendUInt32(bytes, moduleBase + (uint)profile.TargetTypeRva);
        bytes.Add(0x68);
        AppendUInt32(bytes, moduleBase + (uint)profile.SourceTypeRva);
        bytes.AddRange(new byte[]
        {
            0x6A, 0x00,
            0xFF, 0x75, 0xF8,
            0xE8
        });
        bytes.AddRange(RelativeDisplacement(
            wrapperBase + 31,
            moduleBase + (uint)profile.DynamicCastRva));
        bytes.AddRange(new byte[]
        {
            0x83, 0xC4, 0x14,
            0x85, 0xC0,
            0x74, 0x25,
            0xFF, 0x75, 0x08,
            0x8B, 0xC8,
            0xE8
        });
        bytes.AddRange(RelativeDisplacement(
            wrapperBase + 48,
            moduleBase + (uint)profile.GetInsideCellRva));
        bytes.AddRange(new byte[]
        {
            0x85, 0xC0,
            0x78, 0x17,
            0x50,
            0x8B, 0xCE,
            0xE8
        });
        bytes.AddRange(RelativeDisplacement(
            wrapperBase + 60,
            moduleBase + (uint)profile.GetZonesRva));
        bytes.AddRange(new byte[]
        {
            0x5A,
            0x8B, 0x00,
            0x8B, 0x04, 0x90,
            0x8B, 0x75, 0xFC,
            0x8B, 0xE5,
            0x5D,
            0xC2, 0x08, 0x00
        });

        int telemetryCallOffset = bytes.Count;
        bytes.Add(0xE8);
        bytes.AddRange(new byte[4]);
        bytes.AddRange(new byte[]
        {
            0x33, 0xC0,
            0x8B, 0x75, 0xFC,
            0x8B, 0xE5,
            0x5D,
            0xC2, 0x08, 0x00
        });

        int recorderOffset = bytes.Count;
        PatchInt32(bytes, telemetryCallOffset + 1, unchecked((int)(
            wrapperBase + (uint)recorderOffset -
            (wrapperBase + (uint)telemetryCallOffset + 5u))));
        AppendTelemetryRecorder(bytes, profile, moduleBase, wrapperBase, telemetryBase);
        return bytes.ToArray();
    }

    private static void AppendTelemetryRecorder(List<byte> bytes, PatchProfile profile,
        uint moduleBase, uint wrapperBase, uint telemetryBase)
    {
        bytes.AddRange(new byte[]
        {
            0xBA, 0x01, 0x00, 0x00, 0x00,
            0x85, 0xC0,
            0x79, 0x05,
            0xBA, 0x02, 0x00, 0x00, 0x00,
            0x50,
            0x8B, 0x4D, 0x04
        });

        var jumpDisplacements = new List<int>();
        for (int index = 0; index < profile.CollisionCallRvas.Length; index++)
        {
            bytes.AddRange(new byte[] { 0x81, 0xF9 });
            AppendUInt32(bytes,
                moduleBase + (uint)profile.CollisionCallRvas[index] + 5u);
            bytes.AddRange(new byte[] { 0x0F, 0x84 });
            jumpDisplacements.Add(bytes.Count);
            bytes.AddRange(new byte[4]);
        }

        bytes.AddRange(new byte[] { 0x83, 0xC4, 0x04, 0xC3 });

        for (int index = 0; index < profile.CollisionCallRvas.Length; index++)
        {
            int slotOffset = bytes.Count;
            int displacementOffset = jumpDisplacements[index];
            PatchInt32(bytes, displacementOffset, unchecked((int)(
                wrapperBase + (uint)slotOffset -
                (wrapperBase + (uint)displacementOffset + 4u))));

            uint slotAddress = telemetryBase + (uint)(index * TelemetrySlotSize);
            bytes.AddRange(new byte[]
            {
                0x33, 0xC0,
                0xB9, 0x01, 0x00, 0x00, 0x00,
                0xF0, 0x0F, 0xB1, 0x0D
            });
            AppendUInt32(bytes, slotAddress + 4u);
            bytes.AddRange(new byte[] { 0x0F, 0x85 });
            int waitJumpDisplacement = bytes.Count;
            bytes.AddRange(new byte[4]);

            bytes.AddRange(new byte[] { 0x89, 0x35 });
            AppendUInt32(bytes, slotAddress + 8u);
            bytes.AddRange(new byte[] { 0x8B, 0x45, 0xF8, 0xA3 });
            AppendUInt32(bytes, slotAddress + 12u);
            bytes.AddRange(new byte[] { 0x85, 0xC0, 0x74, 0x02, 0x8B, 0x00, 0xA3 });
            AppendUInt32(bytes, slotAddress + 16u);
            bytes.AddRange(new byte[] { 0x89, 0x15 });
            AppendUInt32(bytes, slotAddress + 20u);

            bytes.AddRange(new byte[] { 0x8B, 0x04, 0x24, 0xA3 });
            AppendUInt32(bytes, slotAddress + 24u);
            bytes.AddRange(new byte[] { 0x8B, 0x45, 0x08, 0xA3 });
            AppendUInt32(bytes, slotAddress + 28u);

            bytes.AddRange(new byte[] { 0x83, 0xFA, 0x02, 0x0F, 0x85 });
            int reasonSkipDisplacement = bytes.Count;
            bytes.AddRange(new byte[4]);
            bytes.AddRange(new byte[] { 0x85, 0xC0, 0x0F, 0x84 });
            int nullSkipDisplacement = bytes.Count;
            bytes.AddRange(new byte[4]);

            bytes.AddRange(new byte[] { 0x8B, 0x08, 0x89, 0x0D });
            AppendUInt32(bytes, slotAddress + 32u);
            bytes.AddRange(new byte[] { 0x8B, 0x48, 0x04, 0x89, 0x0D });
            AppendUInt32(bytes, slotAddress + 36u);
            bytes.AddRange(new byte[] { 0x8B, 0x48, 0x08, 0x89, 0x0D });
            AppendUInt32(bytes, slotAddress + 40u);

            int snapshotDoneOffset = bytes.Count;
            PatchInt32(bytes, reasonSkipDisplacement, unchecked((int)(
                wrapperBase + (uint)snapshotDoneOffset -
                (wrapperBase + (uint)reasonSkipDisplacement + 4u))));
            PatchInt32(bytes, nullSkipDisplacement, unchecked((int)(
                wrapperBase + (uint)snapshotDoneOffset -
                (wrapperBase + (uint)nullSkipDisplacement + 4u))));

            bytes.AddRange(new byte[] { 0xC7, 0x05 });
            AppendUInt32(bytes, slotAddress + 4u);
            AppendUInt32(bytes, 2u);
            bytes.Add(0xE9);
            int countJumpDisplacement = bytes.Count;
            bytes.AddRange(new byte[4]);

            int waitOffset = bytes.Count;
            PatchInt32(bytes, waitJumpDisplacement, unchecked((int)(
                wrapperBase + (uint)waitOffset -
                (wrapperBase + (uint)waitJumpDisplacement + 4u))));
            bytes.AddRange(new byte[] { 0x83, 0x3D });
            AppendUInt32(bytes, slotAddress + 4u);
            bytes.Add(0x02);
            bytes.AddRange(new byte[] { 0x0F, 0x85 });
            int waitLoopDisplacement = bytes.Count;
            bytes.AddRange(new byte[4]);
            PatchInt32(bytes, waitLoopDisplacement, unchecked((int)(
                wrapperBase + (uint)waitOffset -
                (wrapperBase + (uint)waitLoopDisplacement + 4u))));

            int countOffset = bytes.Count;
            PatchInt32(bytes, countJumpDisplacement, unchecked((int)(
                wrapperBase + (uint)countOffset -
                (wrapperBase + (uint)countJumpDisplacement + 4u))));
            bytes.AddRange(new byte[] { 0xF0, 0xFF, 0x05 });
            AppendUInt32(bytes, slotAddress);
            bytes.AddRange(new byte[] { 0x83, 0xC4, 0x04, 0xC3 });
        }
    }

    private static void MonitorTelemetry(IntPtr process, int processId, PatchProfile profile,
        uint telemetryBase)
    {
        var lastCounts = new uint[profile.CollisionCallRvas.Length];
        bool telemetryReadWarningLogged = false;
        Log("MONITOR START pid=" + processId + " profile=" + profile.Name);

        while (true)
        {
            uint waitResult = WaitForSingleObject(process, 250);
            if (waitResult == WaitObject0)
            {
                break;
            }

            if (waitResult != WaitTimeout)
            {
                ThrowLastWin32("WaitForSingleObject failed while monitoring guard telemetry");
            }

            byte[] snapshot;
            if (!TryReadExact(process, new IntPtr(telemetryBase),
                profile.CollisionCallRvas.Length * TelemetrySlotSize, out snapshot))
            {
                if (!telemetryReadWarningLogged)
                {
                    Log("MONITOR READ RETRY pid=" + processId + " profile=" + profile.Name);
                    telemetryReadWarningLogged = true;
                }

                continue;
            }

            telemetryReadWarningLogged = false;

            for (int index = 0; index < profile.CollisionCallRvas.Length; index++)
            {
                int offset = index * TelemetrySlotSize;
                uint count = BitConverter.ToUInt32(snapshot, offset);
                if (count == lastCounts[index])
                {
                    continue;
                }

                uint captureState = BitConverter.ToUInt32(snapshot, offset + 4);
                uint playfield = BitConverter.ToUInt32(snapshot, offset + 8);
                uint roomSpace = BitConverter.ToUInt32(snapshot, offset + 12);
                uint vtable = BitConverter.ToUInt32(snapshot, offset + 16);
                uint reason = BitConverter.ToUInt32(snapshot, offset + 20);
                int cellResult = BitConverter.ToInt32(snapshot, offset + 24);
                uint queryVector = BitConverter.ToUInt32(snapshot, offset + 28);
                float queryX = BitConverter.ToSingle(snapshot, offset + 32);
                float queryY = BitConverter.ToSingle(snapshot, offset + 36);
                float queryZ = BitConverter.ToSingle(snapshot, offset + 40);
                if (lastCounts[index] == 0)
                {
                    LogFirstTelemetryHit(process, processId, profile, index, count,
                        captureState, playfield, roomSpace, vtable, reason, cellResult,
                        queryVector, queryX, queryY, queryZ);
                }

                lastCounts[index] = count;
            }
        }

        Log("MONITOR END pid=" + processId + " profile=" + profile.Name +
            " counts=" + string.Join(",", profile.CollisionCallRvas
                .Select((rva, index) => "0x" + rva.ToString("X") + "=" + lastCounts[index])));
    }

    private static void LogFirstTelemetryHit(IntPtr process, int processId,
        PatchProfile profile, int callsiteIndex, uint count, uint captureState,
        uint playfield, uint roomSpace, uint vtable, uint reason, int cellResult,
        uint queryVector, float queryX, float queryY, float queryZ)
    {
        uint currentRoomSpace = 0;
        bool currentReadable = playfield != 0 && TryReadUInt32(
            process, new IntPtr((long)playfield + 0x58), out currentRoomSpace);
        bool queryCaptured = reason == 2 && queryVector != 0;
        Log("GUARD HIT first pid=" + processId + " profile=" + profile.Name +
            " callRva=0x" + profile.CollisionCallRvas[callsiteIndex].ToString("X") +
            " count=" + count +
            " captureState=" + captureState +
            " reason=" + (reason == 2 ? "invalid-cell" : "dynamic-cast") +
            " playfield=" + FormatAddress(playfield) +
            " roomSpace=" + FormatAddress(roomSpace) +
            " currentRoomSpace=" + (currentReadable ? FormatAddress(currentRoomSpace) : "unreadable") +
            " fieldMatch=" + (currentReadable && currentRoomSpace == roomSpace) +
            " eventVtable=" + FormatAddress(vtable) +
            " cellResult=" + (reason == 2 ?
                cellResult.ToString(CultureInfo.InvariantCulture) : "not-called") +
            " queryVector=" + FormatAddress(queryVector) +
            " queryXYZ=" + (queryCaptured ?
                "(" + FormatFloat(queryX) + "," + FormatFloat(queryY) + "," +
                    FormatFloat(queryZ) + ")" : "not-captured"));
    }

    private static string FormatAddress(uint address)
    {
        return "0x" + address.ToString("X8");
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static byte[] BuildRelativeCall(uint callAddress, uint destination)
    {
        var bytes = new List<byte> { 0xE8 };
        bytes.AddRange(RelativeDisplacement(callAddress + 5, destination));
        return bytes.ToArray();
    }

    private static byte[] RelativeDisplacement(uint nextInstruction, uint destination)
    {
        return BitConverter.GetBytes(unchecked((int)(destination - nextInstruction)));
    }

    private static uint DecodeCallTarget(uint callAddress, byte[] bytes)
    {
        Require(bytes.Length == 5 && bytes[0] == 0xE8, "Expected relative call opcode.");
        return unchecked(callAddress + 5u + (uint)BitConverter.ToInt32(bytes, 1));
    }

    private static uint DecodeRelativeTarget(uint blockAddress, byte[] bytes, int opcodeOffset)
    {
        Require(bytes[opcodeOffset] == 0xE8 || bytes[opcodeOffset] == 0xE9,
            "Expected relative branch opcode.");
        return unchecked(blockAddress + (uint)opcodeOffset + 5u +
            (uint)BitConverter.ToInt32(bytes, opcodeOffset + 1));
    }

    private static void AppendUInt32(List<byte> bytes, uint value)
    {
        bytes.AddRange(BitConverter.GetBytes(value));
    }

    private static void PatchInt32(List<byte> bytes, int offset, int value)
    {
        byte[] encoded = BitConverter.GetBytes(value);
        for (int index = 0; index < encoded.Length; index++)
        {
            bytes[offset + index] = encoded[index];
        }
    }

    private static bool ContainsUInt32(byte[] bytes, uint value)
    {
        byte[] encoded = BitConverter.GetBytes(value);
        for (int offset = 0; offset <= bytes.Length - encoded.Length; offset++)
        {
            bool match = true;
            for (int index = 0; index < encoded.Length; index++)
            {
                if (bytes[offset + index] != encoded[index])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSequence(byte[] bytes, byte[] sequence)
    {
        int count = 0;
        for (int offset = 0; offset <= bytes.Length - sequence.Length; offset++)
        {
            bool match = true;
            for (int index = 0; index < sequence.Length; index++)
            {
                if (bytes[offset + index] != sequence[index])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                count++;
            }
        }

        return count;
    }

    private static List<IntPtr> SuspendProcessThreads(int processId)
    {
        var suspended = new List<IntPtr>();
        IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotThread, 0);
        if (snapshot == InvalidHandleValue)
        {
            ThrowLastWin32("Thread snapshot failed");
        }

        try
        {
            var entry = new ThreadEntry32 { Size = (uint)Marshal.SizeOf(typeof(ThreadEntry32)) };
            if (!Thread32First(snapshot, ref entry))
            {
                ThrowLastWin32("Thread32First failed");
            }

            do
            {
                if (entry.OwnerProcessId != (uint)processId)
                {
                    continue;
                }

                IntPtr thread = OpenThread(ThreadSuspendResume, false, entry.ThreadId);
                if (thread == IntPtr.Zero)
                {
                    continue;
                }

                if (SuspendThread(thread) == uint.MaxValue)
                {
                    CloseHandle(thread);
                    continue;
                }

                suspended.Add(thread);
            }
            while (Thread32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        Require(suspended.Count > 0, "No client threads could be suspended safely.");
        return suspended;
    }

    private static void ResumeProcessThreads(IEnumerable<IntPtr> threads)
    {
        foreach (IntPtr thread in threads)
        {
            ResumeThread(thread);
            CloseHandle(thread);
        }
    }

    private static string TryGetProcessPath(int processId)
    {
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var path = new StringBuilder(1024);
            int size = path.Capacity;
            return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static ModuleInfo TryFindModule(int processId, string expectedPath)
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotModule | SnapshotModule32, (uint)processId);
        if (snapshot == InvalidHandleValue)
        {
            return null;
        }

        try
        {
            var entry = new ModuleEntry32 { Size = (uint)Marshal.SizeOf(typeof(ModuleEntry32)) };
            if (!Module32First(snapshot, ref entry))
            {
                return null;
            }

            do
            {
                if (string.Equals(NormalizePath(entry.ExePath), expectedPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new ModuleInfo(entry.BaseAddress);
                }
            }
            while (Module32Next(snapshot, ref entry));

            return null;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private static byte[] ReadExact(IntPtr process, IntPtr address, int count)
    {
        var buffer = new byte[count];
        IntPtr read;
        if (!ReadProcessMemory(process, address, buffer, count, out read) || read.ToInt64() != count)
        {
            ThrowLastWin32("ReadProcessMemory failed at 0x" + address.ToInt64().ToString("X"));
        }

        return buffer;
    }

    private static bool TryReadExact(IntPtr process, IntPtr address, int count,
        out byte[] buffer)
    {
        buffer = new byte[count];
        IntPtr read;
        return ReadProcessMemory(process, address, buffer, count, out read) &&
            read.ToInt64() == count;
    }

    private static bool TryReadUInt32(IntPtr process, IntPtr address, out uint value)
    {
        byte[] buffer;
        if (TryReadExact(process, address, 4, out buffer))
        {
            value = BitConverter.ToUInt32(buffer, 0);
            return true;
        }

        value = 0;
        return false;
    }

    private static void WriteExact(IntPtr process, IntPtr address, byte[] data)
    {
        IntPtr written;
        if (!WriteProcessMemory(process, address, data, data.Length, out written) ||
            written.ToInt64() != data.Length)
        {
            ThrowLastWin32("WriteProcessMemory failed at 0x" + address.ToInt64().ToString("X"));
        }
    }

    private static string Sha256(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        using (SHA256 hash = SHA256.Create())
        {
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path).TrimEnd('\\');
    }

    private static uint ToUInt32(IntPtr value, string description)
    {
        long address = value.ToInt64();
        Require(address >= 0 && address <= uint.MaxValue, description + " is outside the 32-bit address space.");
        return (uint)address;
    }

    private static int ParsePositiveInt(string text, int defaultValue)
    {
        int parsed;
        return int.TryParse(text, out parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private static bool HasArgument(string[] args, string name)
    {
        return args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetArgument(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void ThrowLastWin32(string message)
    {
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    private static void Log(string message)
    {
        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message;
        Console.WriteLine(line);
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Console output remains available if local logging fails.
        }
    }

    private sealed class PatchProfile
    {
        public PatchProfile(string name, string hash, int[] collisionCallRvas, int posToRoomRva,
            int dynamicCastRva, int targetTypeRva, int sourceTypeRva,
            int getInsideCellRva, int getZonesRva)
        {
            Name = name;
            Hash = hash;
            CollisionCallRvas = collisionCallRvas;
            PosToRoomRva = posToRoomRva;
            DynamicCastRva = dynamicCastRva;
            TargetTypeRva = targetTypeRva;
            SourceTypeRva = sourceTypeRva;
            GetInsideCellRva = getInsideCellRva;
            GetZonesRva = getZonesRva;
        }

        public string Name { get; private set; }
        public string Hash { get; private set; }
        public int[] CollisionCallRvas { get; private set; }
        public int PosToRoomRva { get; private set; }
        public int DynamicCastRva { get; private set; }
        public int TargetTypeRva { get; private set; }
        public int SourceTypeRva { get; private set; }
        public int GetInsideCellRva { get; private set; }
        public int GetZonesRva { get; private set; }
    }

    private sealed class TargetProcess
    {
        public TargetProcess(int processId, IntPtr moduleBase)
        {
            ProcessId = processId;
            ModuleBase = moduleBase;
        }

        public int ProcessId { get; private set; }
        public IntPtr ModuleBase { get; private set; }
    }

    private sealed class ModuleInfo
    {
        public ModuleInfo(IntPtr baseAddress)
        {
            BaseAddress = baseAddress;
        }

        public IntPtr BaseAddress { get; private set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThreadEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ThreadId;
        public uint OwnerProcessId;
        public int BasePriority;
        public int DeltaPriority;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ModuleEntry32
    {
        public uint Size;
        public uint ModuleId;
        public uint ProcessId;
        public uint GlobalUsage;
        public uint ProcessUsage;
        public IntPtr BaseAddress;
        public uint BaseSize;
        public IntPtr ModuleHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ModuleName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint access, bool inheritHandle, uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr snapshot, ref ModuleEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr snapshot, ref ModuleEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags,
        StringBuilder path, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr address,
        [Out] byte[] buffer, int size, out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address,
        byte[] buffer, int size, out IntPtr bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address,
        UIntPtr size, uint allocationType, uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address,
        UIntPtr size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(IntPtr process, IntPtr address,
        UIntPtr size, uint newProtection, out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
}
