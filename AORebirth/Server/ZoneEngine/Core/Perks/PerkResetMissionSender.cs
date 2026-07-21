namespace ZoneEngine.Core.Perks
{
    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    /// <summary>
    /// Capture-backed "Full Perk Points Reset Service" mission window entry (20260716-Reset-perks #25).
    /// We take the captured live QuestFullUpdate bytes, patch the recipient identity and the mission
    /// expiry, and send the raw packet so the mission reappears after login/zone (persistence).
    ///
    /// Timer: the mission-window "Remain" countdown is <c>expiry - clientClock</c>, where the expiry is
    /// an absolute timestamp stored in QuestActions[0].UnknownHash1 (raw offset 503). On this server the
    /// client's countdown clock is NOT real time: our constant <c>GameTimeMessage</c> anchors it to a
    /// fixed server epoch (<see cref="ClientClockBaseSeconds"/> ≈ Jan 2008), and the client advances that
    /// value in real time, re-anchoring on every login and zone-in. (That fixed 2008 epoch is why the
    /// unpatched 2026 capture expiry rendered ~161917h ≈ 18.5 years, and why a wall-clock-based expiry
    /// drifted every restart and jumped up on zone change.)
    ///
    /// So we compute the client's current clock as <c>base + (now - lastGameTimeSync)</c> and set
    /// <c>expiry = clientClockNow + remaining</c>. Because both send paths (fresh reset, and login/zone
    /// re-send) run right after a GameTimeMessage re-anchors the clock, this lands on the real remaining
    /// cooldown (48h fresh) and stays stable across restarts and zone changes. The expiry is written as
    /// raw bytes because the DTO stores UnknownHash1 as an ASCII string and would mangle bytes >= 0x80.
    /// </summary>
    public static class PerkResetMissionSender
    {
        /// <summary>Recipient character instance present in the recorded packet; replaced per recipient.</summary>
        private const int CapturedCharacterInstance = unchecked((int)0x7966F05B);

        /// <summary>Raw byte offset of QuestActions[0].UnknownHash1 (the mission expiry) in the capture.</summary>
        private const int ExpiryOffset = 503;

        /// <summary>Mission QuestId in the captured packet (Quest[0].QuestId). Fixed across resends.</summary>
        private const int MissionIdentityType = 0x0000DAC3;

        /// <summary>Mission QuestId instance in the captured packet (Quest[0].QuestId.Instance).</summary>
        private const int MissionInstance = unchecked((int)0x555191D7);

        /// <summary>Free-reset cooldown mirrored in the mission timer (48 hours).</summary>
        private const int MissionDurationSeconds = 48 * 60 * 60;

        /// <summary>
        /// The absolute value (in the client's mission-clock units) that our constant GameTimeMessage
        /// anchors the client countdown clock to at each login/zone-in. Fixed because the GameTimeMessage
        /// constants are fixed. Calibrated from the unpatched 2026 capture rendering ~161917h against a
        /// ~Jan-2008 client clock. To fine-tune: new = old + (displayedRemainSeconds - 172800) when a
        /// fresh reset is done right after zoning to the NPC.
        /// </summary>
        private const long ClientClockBaseSeconds = 1_201_445_827L;

        /// <summary>
        /// Full captured QuestFullUpdate packet (16-byte header + body). Body carries the mission name
        /// "Full Perk Points Reset Service". The recipient identity and expiry are patched before send;
        /// the leading MessageId is overwritten by the transport packet counter.
        /// </summary>
        private const string CapturedQuestFullUpdateHex =
            "03A3000A0001027400000DB67966F05B465A40610000C3507966F05B01000007E20000DAC3555191D70000000F00000000000000000000040246756C6C205065726B20506F696E7473205265736574205365727669636500000000DF46756C6C205065726B20506F696E747320526573657420536572766963653C42523E3C42523E596F7520686176652063686F73656E20746F20756E747261696E20616C6C206F6620796F7572207065726B20706F696E747320726563656E746C792E205468697320736572766963652063616E206F6E6C79206265206163636573736564206F6E636520647572696E67206120706572696F64206F6620343820686F7572732C20617320746869732022717569636B20616E64206469727479222070726F6365737320737472657373657320796F75722073797374656D2E000000C35078A4C5B100000006000000000000000000000000000003F1000003F1000003F1534F395100000000000000000000000000000000000000000000000000000000000000000000C3507966F05B0003BC5200000B4000000B40000007E20000001800000000000000000000000000000000000111D300019534000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5B0089000000000000D2F14D4DD5E300000000000000000000000000000000000000000000000000000000000007E20000C3507966F05B00000001054DD5E3000000000000000000000006000007E20000C3507966F05B0000000000019534000000000000000000000000000000000000000000000007000003F101";

        /// <summary>
        /// Sends the "Full Perk Points Reset Service" cooldown mission to the character's mission window.
        /// </summary>
        /// <param name="character">Recipient.</param>
        /// <param name="remainingSeconds">Cooldown seconds still remaining (clamped to 1..48h).</param>
        public static void SendResetCooldownMission(Character character, int remainingSeconds)
        {
            if (character == null || character.Controller == null)
            {
                return;
            }

            var client = character.Controller.Client as ZoneClient;
            if (client == null)
            {
                return;
            }

            if (remainingSeconds <= 0)
            {
                return;
            }

            if (remainingSeconds > MissionDurationSeconds)
            {
                remainingSeconds = MissionDurationSeconds;
            }

            try
            {
                byte[] packet = HexToBytes(CapturedQuestFullUpdateHex);

                int recipientInstance = character.Identity.Instance;
                ReplaceInstance(packet, CapturedCharacterInstance, recipientInstance);

                // Client mission clock now = fixed epoch anchored at last GameTimeMessage, advanced by the
                // real time elapsed since that sync. Expiry = that clock + remaining, so "Remain" == remaining.
                double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
                if (secondsSinceSync < 0)
                {
                    secondsSinceSync = 0;
                }

                long clientClockNow = ClientClockBaseSeconds + (long)secondsSinceSync;
                long expiry = clientClockNow + remainingSeconds;
                WriteInt32BigEndian(packet, ExpiryOffset, (int)expiry);

                // The client dedupes missions by QuestId and keeps its own countdown, so a plain resend of
                // the same QuestId does not refresh an already-shown mission until a zone clears the list.
                // Delete the existing entry first (capture-backed Quest/Delete, same sequence the live server
                // uses before re-adding) so a paid early reset instantly shows the fresh 48:00 without zoning.
                client.SendCompressed(
                    new QuestMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = QuestAction.Delete,
                        Unknown1 = 0,
                        Mission = new Identity { Type = (IdentityType)MissionIdentityType, Instance = MissionInstance },
                        Unknown2 = 0,
                        Unknown3 = 0
                    });

                // Enqueue (not direct-send) so the raw full-update stays ordered AFTER the queued Delete
                // above; a direct SendCompressed(byte[]) would overtake the queue and the client would add
                // then immediately delete the mission, showing nothing.
                client.EnqueueOutboundCompressedBuffer(packet);

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "PerkResetMission sent reset-cooldown mission char=" + recipientInstance
                    + " remainingSeconds=" + remainingSeconds + " sinceSync=" + (long)secondsSinceSync
                    + " expiry=" + expiry);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }
        }

        /// <summary>Replaces every big-endian 4-byte occurrence of <paramref name="from"/> with <paramref name="to"/>.</summary>
        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            byte f0 = (byte)(from >> 24);
            byte f1 = (byte)(from >> 16);
            byte f2 = (byte)(from >> 8);
            byte f3 = (byte)from;

            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == f0 && packet[i + 1] == f1 && packet[i + 2] == f2 && packet[i + 3] == f3)
                {
                    WriteInt32BigEndian(packet, i, to);
                    i += 3;
                }
            }
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static byte[] HexToBytes(string hex)
        {
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
