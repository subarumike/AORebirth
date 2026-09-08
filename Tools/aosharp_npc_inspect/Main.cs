using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Core;
using AOSharp.Core.UI;

namespace NpcInspectProbe
{
    public sealed class Main : AOPluginEntry
    {
        private const string SupportedGamecode = "654969A6B65946CB161F0E60AED8589260FC5ECA1795488F66BB56F8FFF73726";
        private const string InspectExport = "?N3Msg_Inspect@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@@Z";
        private const double WindowSeconds = 15;
        private static Main current;
        private static bool commandRegistered;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private StreamWriter log;
        private string directory;
        private string status = "Not initialized";
        private InspectDelegate inspect;
        private Probe pending;
        private double lastAttempt = -WindowSeconds;
        private int sequence;
        private bool enabled;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeIdentity
        {
            public int Type;
            public int Instance;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void InspectDelegate(IntPtr engine, ref NativeIdentity target);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr GetModuleHandleW(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder path, int capacity);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        private sealed class Probe
        {
            public Identity Target;
            public Identity Actor;
            public string Prefix;
            public double Started;
            public bool Called;
            public bool OutboundSeen;
            public bool ReplySeen;
            public int InboundPackets;
            public int OutboundPackets;
            public int OutboundCharacterActions;
            public int OtherInspectReplies;
            public ProbeCapture Capture;
            public double LastFlush;
            public int FeedbackMessages;
            public int RejectionMessages;
            public bool CaptureWarning;
        }

        public override void Run(string pluginDir)
        {
            if (current != null) { Chat.WriteLine("[NPC Inspect] Already loaded."); return; }
            current = this;
            if (!commandRegistered)
            {
                Chat.RegisterCommand("npcinspect", Dispatch);
                commandRegistered = true;
            }
            try
            {
                directory = Path.Combine(pluginDir, "NpcInspectLogs",
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(directory);
                log = new StreamWriter(Path.Combine(directory, "session.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                Record("PLUGIN_START version=1.2.1 runtime=Mike2022 outboundObserver=pre-connection-framing Core=" + typeof(Game).Assembly.FullName
                    + " Common=" + typeof(Identity).Assembly.FullName);
                if (IntPtr.Size != 4) throw new NotSupportedException("Requires the 32-bit AO client.");
                IntPtr module = GetModuleHandleW("Gamecode.dll");
                if (module == IntPtr.Zero) throw new InvalidOperationException("Gamecode.dll is not loaded.");
                var path = new StringBuilder(32768);
                uint length = GetModuleFileNameW(module, path, path.Capacity);
                if (length == 0 || length >= path.Capacity) throw new IOException("Cannot identify loaded Gamecode.dll.");
                string hash;
                using (var stream = File.OpenRead(path.ToString()))
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                Record("GAMECODE path=" + path + " sha256=" + hash);
                if (hash != SupportedGamecode)
                    throw new NotSupportedException("Gamecode.dll differs from the analyzed build; native Inspect is disabled. See session.log.");
                IntPtr address = GetProcAddress(module, InspectExport);
                if (address == IntPtr.Zero) throw new MissingMethodException("Inspect export was not found.");
                inspect = (InspectDelegate)Marshal.GetDelegateForFunctionPointer(address, typeof(InspectDelegate));
                Game.OnUpdate += OnUpdate;
                Game.TeleportStarted += OnTeleport;
                Network.PacketReceived += OnReceived;
                Network.PacketSent += OnSent;
                enabled = true;
                status = "Ready";
                Say("v1.2.1 Mike2022 ready. Select an NPC and type /npcinspect. /npcinspect help for options.");
                Say("Each probe saves all game-network callbacks for 15 seconds, up to 16 MiB / 20,000 packets. Keep logs private.");
                Say("Logs: " + directory);
            }
            catch (Exception ex) { Fail(ex); }
        }

        // Zam 2022 offers RegisterCommand but no public unregister. Keep a
        // dispatcher inert after teardown; replacing the assembly needs restart.
        private static void Dispatch(string command, string[] args, ChatWindow window)
        {
            Main instance = current;
            if (instance == null) { Chat.WriteLine("[NPC Inspect] Plugin is unloaded; restart to reload."); return; }
            try { instance.Command(args); }
            catch (Exception ex) { instance.Fail(ex); }
        }

        private void Command(string[] args)
        {
            string option = string.Join(" ", args ?? new string[0]).Trim().ToLowerInvariant();
            if (option == "help")
            {
                Say("/npcinspect: selected NPC; /npcinspect control: selected player; /npcinspect status; /npcinspect cancel.");
                Say("One request, 15-second observation window. Do not manually Inspect during a probe. No automatic retries.");
                return;
            }
            if (option == "status") { Say(status + ". Logs: " + directory); return; }
            if (option == "cancel") { Finish("CANCELLED (an already-sent request cannot be recalled)"); return; }
            if (option != "" && option != "control") { Say("Unknown option. Use /npcinspect help."); return; }
            if (!enabled) { Say(status); return; }
            if (pending != null) { Say("A probe is already running; wait or /npcinspect cancel."); return; }
            if (clock.Elapsed.TotalSeconds - lastAttempt < WindowSeconds) { Say("Wait 15 seconds between attempts."); return; }
            if (Game.IsZoning || DynelManager.LocalPlayer == null) { Say("Wait until zoning has finished."); return; }
            SimpleChar target = Targeting.TargetChar;
            if (target == null || target.Identity == DynelManager.LocalPlayer.Identity) { Say("Select another character first."); return; }
            if (option == "control" ? !target.IsPlayer : !target.IsNpc)
            {
                Say(option == "control" ? "Control requires a player target." : "Select an NPC (not a pet). Use /npcinspect control for a player.");
                return;
            }
            pending = new Probe {
                Target = target.Identity, Actor = DynelManager.LocalPlayer.Identity,
                Prefix = (++sequence).ToString("D3"), Started = clock.Elapsed.TotalSeconds
            };
            lastAttempt = pending.Started;
            pending.LastFlush = pending.Started;
            pending.Capture = new ProbeCapture(Path.Combine(directory, pending.Prefix));
            Record("PROBE_QUEUED id=" + pending.Prefix + " mode=" + (option == "control" ? "player-control" : "npc")
                + " target=" + pending.Target + " name=" + target.Name + " actor=" + pending.Actor);
            status = "Queued " + pending.Target;
            Say(status);
        }

        private void OnUpdate(object sender, float deltaTime)
        {
            try
            {
                if (!enabled || pending == null) return;
                if (Game.IsZoning || DynelManager.LocalPlayer == null || DynelManager.LocalPlayer.Identity != pending.Actor)
                { Finish("ABORTED_WORLD_CHANGED"); return; }
                if (clock.Elapsed.TotalSeconds - pending.Started >= WindowSeconds)
                { Finish(pending.ReplySeen ? "MATCHING_REPLY_OBSERVED" : pending.RejectionMessages > 0
                    ? "REJECTION_FEEDBACK_OBSERVED_IN_WINDOW (target correlation is temporal)"
                    : "NO_MATCHING_REPLY_OBSERVED (not proof of rejection or empty gear)"); return; }
                if (clock.Elapsed.TotalSeconds - pending.LastFlush >= 1)
                {
                    pending.Capture.Flush();
                    pending.LastFlush = clock.Elapsed.TotalSeconds;
                }
                if (pending.Called) return;
                SimpleChar target = Targeting.TargetChar;
                if (target == null || target.Identity != pending.Target) { Finish("ABORTED_TARGET_CHANGED_BEFORE_SEND"); return; }
                IntPtr engine = N3Engine_t.GetInstance();
                if (engine == IntPtr.Zero) { Finish("ABORTED_NO_ENGINE"); return; }
                var nativeTarget = new NativeIdentity { Type = (int)pending.Target.Type, Instance = pending.Target.Instance };
                Record("NATIVE_CALL_BEGIN id=" + pending.Prefix + " export=" + InspectExport);
                pending.Called = true; // Never retry, including on an exception.
                inspect(engine, ref nativeTarget);
                Record("NATIVE_CALL_RETURNED id=" + pending.Prefix);
                status = "Waiting for outbound confirmation and matching Inspect reply";
                Say("Inspect routine called once. Observing for 15 seconds.");
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void OnReceived(object sender, byte[] packet)
        {
            try
            {
                if (!InWindow()) return;
                pending.InboundPackets++;
                CapturePacket(false, packet);
                ObserveFeedback(packet);
                if (PacketView.IsInspectReply(packet, (int)pending.Target.Type, pending.Target.Instance))
                {
                    if (pending.ReplySeen) return;
                    File.WriteAllBytes(Path.Combine(directory, pending.Prefix + "-inspect-reply.bin"), packet);
                    Record("MATCHING_INSPECT_REPLY id=" + pending.Prefix + " bytes=" + packet.Length
                        + " inventoryPayloadBytes=" + (packet.Length - 37) + " equipmentDecode=UNVERIFIED");
                    pending.ReplySeen = true;
                    pending.Capture.Flush();
                    status = "Matching Inspect reply received; equipment contents not decoded";
                    Say(status + ". Reply saved; watching until window ends.");
                }
                else if (PacketView.IsN3(packet, PacketView.InspectKey, 29)) pending.OtherInspectReplies++;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void OnSent(object sender, byte[] packet)
        {
            try
            {
                if (!InWindow()) return;
                pending.OutboundPackets++;
                CapturePacket(true, packet);
                if (PacketView.IsOutgoingN3(packet, PacketView.CharacterActionKey, 55))
                    pending.OutboundCharacterActions++;
                if (pending.OutboundSeen) return;
                if (!PacketView.IsInspectRequest(packet, (int)pending.Actor.Type, pending.Actor.Instance,
                    (int)pending.Target.Type, pending.Target.Instance)) return;
                File.WriteAllBytes(Path.Combine(directory, pending.Prefix + "-inspect-request.bin"), packet);
                Record("OUTBOUND_INSPECT_OBSERVED id=" + pending.Prefix + " bytes=" + packet.Length
                    + " headerLength=" + PacketView.HeaderLength(packet) + " observation=before-Connection.Send"
                    + " delivery=UNVERIFIED");
                pending.OutboundSeen = true;
                pending.Capture.Flush();
                Say("Inspect request observed entering Connection.Send (action 261); delivery not yet confirmed.");
            }
            catch (Exception ex) { Fail(ex); }
        }

        private bool InWindow()
        {
            return enabled && pending != null && pending.Called && !Game.IsZoning
                && clock.Elapsed.TotalSeconds - pending.Started < WindowSeconds;
        }

        private void CapturePacket(bool outgoing, byte[] packet)
        {
            pending.Capture.Append(outgoing, packet, DateTime.UtcNow,
                (clock.Elapsed.TotalSeconds - pending.Started) * 1000);
            if (pending.Capture.Truncated && !pending.CaptureWarning)
            {
                pending.CaptureWarning = true;
                Record("CAPTURE_INCOMPLETE id=" + pending.Prefix + " rawLimitReached=True");
                Say("Diagnostic capture limit reached. Results are incomplete; see session.log.");
            }
        }

        private void ObserveFeedback(byte[] packet)
        {
            uint unknown, category, message;
            if (!PacketView.TryFeedback(packet, out unknown, out category, out message)) return;
            pending.FeedbackMessages++;
            bool rejection = PacketView.IsInspectRejection(category, message);
            if (rejection) pending.RejectionMessages++;
            // Complete traffic remains in the bounded archive. Save a bounded
            // convenient standalone copy and description for feedback messages.
            if (pending.FeedbackMessages <= 64)
            {
                string file = pending.Prefix + "-feedback-" + pending.FeedbackMessages.ToString("D3") + ".bin";
                File.WriteAllBytes(Path.Combine(directory, file), packet);
                Record("FEEDBACK id=" + pending.Prefix + " file=" + file + " " + PacketView.Describe(packet)
                    + " unknown1=" + unknown + " category=" + category + " message=0x" + message.ToString("X8")
                    + " trailingBytes=" + (packet.Length - 41) + " inspectRejectionText=" + rejection
                    + " targetCorrelation=TEMPORAL_ONLY reason=UNKNOWN");
            }
            else if (pending.FeedbackMessages == 65)
                Record("FEEDBACK_DETAIL_LIMIT id=" + pending.Prefix + " extra standalone copies omitted; check raw archive completeness");
            if (rejection && pending.RejectionMessages == 1)
                Say("Inspect rejection feedback observed and logged. The reason is not yet decoded.");
            pending.Capture.Flush();
        }

        private void OnTeleport(object sender, EventArgs args)
        {
            try { Finish("ABORTED_ZONING"); }
            catch (Exception ex) { Fail(ex); }
        }

        private void Finish(string reason)
        {
            Probe probe = pending;
            pending = null;
            if (probe == null) return;
            probe.Capture?.Dispose();
            status = reason + "; nativeCalled=" + probe.Called + "; outboundSeen=" + probe.OutboundSeen
                + "; replySeen=" + probe.ReplySeen + "; rejectionFeedbackCount=" + probe.RejectionMessages
                + "; rawCaptureIncomplete=" + (probe.Capture == null || probe.Capture.Truncated);
            Record("PROBE_END id=" + probe.Prefix + " " + status + " inboundPacketCount=" + probe.InboundPackets
                + " outboundPacketCount=" + probe.OutboundPackets + " outboundCharacterActionCount=" + probe.OutboundCharacterActions
                + " otherInspectReplyCount=" + probe.OtherInspectReplies + " feedbackCount=" + probe.FeedbackMessages
                + " rawSaved=" + probe.Capture?.Saved + " rawDropped=" + probe.Capture?.Dropped
                + " archiveBytes=" + probe.Capture?.Bytes);
            Say(status);
        }

        private void Record(string text)
        {
            if (log == null) throw new IOException("Log is not available; refusing an unlogged probe.");
            log.WriteLine(DateTime.UtcNow.ToString("O") + " " + text.Replace('\r', ' ').Replace('\n', ' '));
        }

        private static void Say(string text) { Chat.WriteLine("[NPC Inspect] " + text); }

        private void Fail(Exception ex)
        {
            enabled = false;
            Probe failedProbe = pending;
            pending = null;
            try { failedProbe?.Capture?.Dispose(); } catch { }
            status = "Disabled: " + ex.GetType().Name + ": " + ex.Message;
            try { Record("ERROR captureIncomplete=True probe=" + failedProbe?.Prefix + " " + status); } catch { }
            Say(status);
        }

        public override void Teardown()
        {
            enabled = false;
            Game.OnUpdate -= OnUpdate;
            Game.TeleportStarted -= OnTeleport;
            Network.PacketReceived -= OnReceived;
            Network.PacketSent -= OnSent;
            try { Finish("PLUGIN_UNLOADED"); } catch { }
            try { log?.Dispose(); } catch { }
            log = null;
            if (ReferenceEquals(current, this)) current = null;
        }
    }
}
