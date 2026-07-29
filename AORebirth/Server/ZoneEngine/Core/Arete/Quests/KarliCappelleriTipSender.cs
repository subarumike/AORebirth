namespace ZoneEngine.Core.Arete.Quests

{

    using System;



    using AORebirth.Core.Entities;

    using AORebirth.Core.Network;



    using ZoneEngine.Core.Controllers;



    /// <summary>Capture 20260727-Alien- quest-ncu QuestFullUpdate wire tips (patched player + live AbsoluteTime).</summary>

    internal static class KarliCappelleriTipSender

    {

        public const int CrashedShipTipInstance = unchecked((int)0x5565A09A);



        public const int FindFriendTipInstance = unchecked((int)0x5565A09B);



        private const int CapturedPlayerInstance = unchecked((int)0x7996C028);



        private const int CapturedCrashedShipExpiry = unchecked((int)0x6A6755B8);



        // Find-a-Friend capture AbsoluteTime is 0 (Remain 00:00). Write at fixed offset.

        private const int FindFriendExpiryWriteOffset = 472;



        private const long TipClientClockBaseSeconds = 1_201_445_827L;



        private const int TipMissionDurationSeconds = 48 * 60 * 60;



        public static RexQuestPreviewEmissionResult TrySendCrashedShipTipOnly(ICharacter source)

        {

            return TipOnly(source, SendCrashedShipTip, "KarliCrashedShip", "Mission:5565A09A");

        }



        public static RexQuestPreviewEmissionResult TrySendFindFriendTipOnly(ICharacter source)

        {

            return TipOnly(source, SendFindFriendTip, "KarliFindFriend", "Mission:5565A09B");

        }



        private static void SendCrashedShipTip(ICharacter source)

        {

            TrySendWire(

                source,

                "015E000A0001024D00000DB17996C028465A40610000C3507996C02801000007E20000DAC35565A09A0000000F000000000000000000000102546865204372617368656420416C69656E20536869700000000068546865204372617368656420416C69656E20536869703C42523E3C42523E476F20696E746F204372617368656420416C69656E20536869702061742033392E392C2035352E3320616E64206B696C6C2074686520416C69656E20537069646572202D205A69782E00000111D3595A574900000006000005A20000000000000820000003F1000003F1000003F15738354300000000000000000000000000000000000000000000000000000000000000000000C3507996C02800002C420000021C0000021C000007E200000001000000000000000000000000000000000000C350799D443F000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A6755B8000000000000D2FC1C6D664400009C5000001F490001869F0001869F421FB2743C972DBD425D093D00000BD30000C350641D0C3C0000C3507996C02800000001046D664400000000000000000000000600000BD30000C350641D0C3C0000C3507996C0280000000000019AFB0000C79F00D7C00A0000000000000000000000060000C76D00F73009000000080000C76D00F73009000000040000C76D00F73009000000020000C76D00F7300A000000080000C76D00F7300A000000040000C76D00F7300A0000000200000008000003F101",

                CapturedCrashedShipExpiry);

        }



        private static void SendFindFriendTip(ICharacter source)

        {

            TrySendWire(

                source,

                "01E8000A0001065B00000DB17996C028465A40610000C3507996C02801000007E20000DAC35565A09B0000000F00000000000000000000000246696E64206120467269656E64000000048346696E64206120467269656E643C42523E3C42523E496E206F7264657220746F20666967687420616C69656E7320696E20746865206372617368656420616C69656E20736869702C20796F75206E65656420746F2066696E64206174206C65617374206F6E6520667269656E6420746F2068656C7020796F75206F75742E20596F752063616E207465616D2077697468206F7468657220706C6179657273206279207573696E6720746865205465616D2057696E646F772E20546F206F70656E20746865207465616D2077696E646F772C2070726573732074686520666F6C6C6F77696E67206B6579733A20257B4B45593A57494E444F575F5445414D7D252E20496620796F75206861766520666F756E6420616E6F7468657220706C61796572207468617420697320617070726F78696D6174656C79207468652073616D65206C6576656C20617320796F757273656C662C2073696D706C7920746172676574207468697320706C6179657220616E6420707265737320746865205265637275697420627574746F6E2E3C42523E3C42523E4966206E6F206F7468657220706C61796572732061726520696E20796F757220766963696E69792C20796F752063616E206C6F6F6B20666F72206F7468657220706C617965727320746861742061726520616C736F206C6F6F6B696E6720666F722061207465616D207468726F75676820746865205465616D205365617263682057696E646F772E20546F206F70656E20746865205465616D2066696E6465722C2070726573733A20257B4B45593A57494E444F575F4C46547D252E20596F752063616E20726566696E6520796F75722073656172636820706172616D657465727320627920536964652C204C6F636174696F6E20616E642050726F66657373696F6E2E204D616B65207375726520796F75207069636B207468652073657474696E67732074686174207375697420796F7572206E6565647320616E64207468656E207072657373207468652073656172636820627574746F6E2E20496620796F752066696E64206E6F206F7468657220706C61796572732077686F20617265206C6F6F6B696E6720666F722061207465616D2C20796F752073686F756C642074727920746F20776964656E20796F75722073656172636820706172616D65746572732E204B65657020696E206D696E64207468617420746865205465616D205365617263682057696E646F772077696C6C206F6E6C7920646973706C617920706C61796572732077686F206172652077697468696E20796F7572207465616D206C6576656C2072616E67652E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E46696E6420616E6F7468657220706C6179657220616E6420696E766974652068696D206F722068657220746F20796F7572207465616D2E205768656E20796F752061726520696E207468652073616D65207465616D2C2075736520746865204E616E6F2043616E3A20467269656E646C792042756666206F6E207468697320706C617965722E3C2F666F6E743E000000C350799AD39400000006000009D80000000000000820000003F1000003F100000FC400008FAA00008FB2000000260000000000008FAA00008FB2000000260000000000008FAA00008FB2000000260000000035304B5900000000000000003830433200000026000000000000000000000000000000000000C3507996C0280003BC520000000000000000000007E20000001800000000000000000000000000000000000000111D300019B1E0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D5EAE3000000000000000000000000000000000000000000000000000000000000007E20000C3507996C02800000001055EAE30000000000000000000000006000007E20000C3507996C0280000000000019B1E000000000000000000000000000000000000030000C73D57A94447000000080000C73D57A94447000000040000C73D57A944470000000100000007000003F101",

                0,

                FindFriendExpiryWriteOffset);

        }



        private static RexQuestPreviewEmissionResult TipOnly(

            ICharacter source,

            Action<ICharacter> sendTip,

            string label,

            string questId)

        {

            if (source?.Controller?.Client == null)

            {

                return RexQuestPreviewEmissionResult.Failed(label + " tip skipped: client missing.");

            }



            try

            {

                sendTip(source);

                return RexQuestPreviewEmissionResult.Sent(

                    label + " tip-only. mission=" + questId + " source=20260727-Alien- quest-ncu");

            }

            catch (Exception e)

            {

                return RexQuestPreviewEmissionResult.Failed(label + " tip-only failed: " + e.Message);

            }

        }



        private static void TrySendWire(

            ICharacter source,

            string hex,

            int capturedExpiry,

            int expiryWriteOffset = -1)

        {

            ZoneClient client = source?.Controller?.Client as ZoneClient;

            if (client == null || source.Identity.Instance == 0)

            {

                return;

            }



            byte[] packet = HexToBytes(hex);

            ReplaceInt32Be(packet, CapturedPlayerInstance, source.Identity.Instance);



            int liveExpiry = ComputeLiveTipExpiry(client);

            if (expiryWriteOffset >= 0 && expiryWriteOffset + 4 <= packet.Length)

            {

                WriteInt32Be(packet, expiryWriteOffset, liveExpiry);

            }

            else if (capturedExpiry != 0)

            {

                ReplaceInt32Be(packet, capturedExpiry, liveExpiry);

            }



            client.EnqueueOutboundCompressedBuffer(packet);

        }



        private static int ComputeLiveTipExpiry(ZoneClient client)

        {

            double secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;

            if (secondsSinceSync < 0)

            {

                secondsSinceSync = 0;

            }



            return unchecked(

                (int)(TipClientClockBaseSeconds + (long)secondsSinceSync + TipMissionDurationSeconds));

        }



        private static byte[] HexToBytes(string hex)

        {

            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)

            {

                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

            }



            return bytes;

        }



        private static void WriteInt32Be(byte[] packet, int offset, int value)

        {

            packet[offset] = (byte)(value >> 24);

            packet[offset + 1] = (byte)(value >> 16);

            packet[offset + 2] = (byte)(value >> 8);

            packet[offset + 3] = (byte)value;

        }



        private static void ReplaceInt32Be(byte[] packet, int from, int to)

        {

            byte b0 = (byte)(from >> 24);

            byte b1 = (byte)(from >> 16);

            byte b2 = (byte)(from >> 8);

            byte b3 = (byte)from;

            for (int i = 0; i + 4 <= packet.Length; i++)

            {

                if (packet[i] == b0 && packet[i + 1] == b1 && packet[i + 2] == b2 && packet[i + 3] == b3)

                {

                    packet[i] = (byte)(to >> 24);

                    packet[i + 1] = (byte)(to >> 16);

                    packet[i + 2] = (byte)(to >> 8);

                    packet[i + 3] = (byte)to;

                    i += 3;

                }

            }

        }

    }

}


