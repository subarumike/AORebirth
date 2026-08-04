namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    #endregion

    public static class SafeQuestFullUpdateSender
    {
        private const int MissionIdentityType = 0x0000DAC3;

        private const int B18CInstance = unchecked((int)0x5514B18C);

        private const int B18DInstance = unchecked((int)0x5514B18D);

        private const int B18EInstance = unchecked((int)0x5514B18E);

        private const int B18FInstance = unchecked((int)0x5514B18F);

        private const int B194Instance = unchecked((int)0x5514B194);

        private const int B196Instance = unchecked((int)0x5514B196);

        private const int FlintInstance = unchecked((int)0x5514B198);

        private const int B199Instance = unchecked((int)0x5514B199);

        private const int B19AInstance = unchecked((int)0x5514B19A);

        private const int FindBioInstance = unchecked((int)0x5514B19B);

        private const int DeliverBioInstance = unchecked((int)0x5514B19C);

        // Capture 20260720-074847 / 105157 tip QuestId instances (client Mission window).
        // Server MissionRuntime keys stay Mission:5514B19D..A0; tip wire must match live AO.
        private const int SurveillanceUplinkInstance = unchecked((int)0x555A4A49);

        private const int PlantBugInstance = unchecked((int)0x555A4E3B);

        private const int DeliverHc12BillInstance = unchecked((int)0x555A4E3C);

        private const int KneecappingInstance = unchecked((int)0x555A4E3D);
        private const int ReportToAlexInstance = unchecked((int)0x555B4365);
        private const int TalkToStanInstance = unchecked((int)0x555B4366);

        private const int BuyLockpickInstance = unchecked((int)0x555BD124);

        private const int StrongboxContentsInstance = unchecked((int)0x555BE9C5);

        private const int DeliverAntonioFactoryInstance = unchecked((int)0x555BE9F2);

        private const int TalkToSarahGreeneInstance = unchecked((int)0x555BE9F3);

        private const int BuyNanoProgramsInstance = unchecked((int)0x555BE9F4);

        private const int FindTheThiefInstance = unchecked((int)0x555BE9F5);

        private const int DeliverDnaLockedArmorInstance = unchecked((int)0x555BE9F6);

        private const int SpeakToVernonGodfrayInstance = unchecked((int)0x555BE9F7);

        private const int HackingSkillsInstance = unchecked((int)0x555BE9F8);

        private const int GiveHackedTechnicalLibraryInstance = unchecked((int)0x555BE9F9);

        private const int CargoLiftingInstance = unchecked((int)0x555BE9FA);

        // Capture 20260721-Vernon-Godfray Cargo Lifting QFU shell before instance patch.
        private const int CargoLiftingCapturedWireInstance = unchecked((int)0x555CF577);

        private const int ReturnToVernonGodfrayInstance = unchecked((int)0x555BE9FB);

        private const int TalkToDoctorMasonInstance = unchecked((int)0x555BE9FC);

        private const int TradeskillNanoSensorInstance = unchecked((int)0x555B4367);

        private const int TradeskillBasicBrainInstance = unchecked((int)0x555B4368);

        private const int TradeskillPersonalizedBrainInstance = unchecked((int)0x555B4369);

        private const int TradeskillShowBrainInstance = unchecked((int)0x555B436A);

        // Prior private-server tip IDs (wrong); still Action59+Delete so stuck Remain 00:00 tips clear.
        private const int LegacySurveillanceUplinkInstance = unchecked((int)0x5514B19D);

        private const int LegacyPlantBugInstance = unchecked((int)0x5514B19E);

        private const int LegacyDeliverHc12BillInstance = unchecked((int)0x5514B19F);

        private const int LegacyKneecappingInstance = unchecked((int)0x5514B1A0);

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const int B18CUnknownActionIdType = 0x00001999;

        private const int B18CUnknownActionIdInstance = 0x4D4C4345;

        private const int B18CUnknownActionId7Type = 0x0000D2FC;

        private const int B18CUnknownActionId7Instance = 0x1C50D8CE;

        private const int B18DUnknownActionId2Type = 0x000111D3;

        private const int B18DUnknownActionId2Instance = 0x00019A8F;

        private const int B18DUnknownActionId7Type = 0x0000D2F1;

        private const int B18DUnknownActionId7Instance = 0x4D167F39;

        private const int B18EUnknownActionId2Type = 0x000111D3;

        private const int B18EUnknownActionId2Instance = 0x52454C53;

        private const int B18EUnknownActionId7Type = 0x0000D2F1;

        private const int B18EUnknownActionId7Instance = 0x4D167F3A;

        private const int B18FUnknownActionId2Type = 0x000111D3;

        private const int B18FUnknownActionId2Instance = 0x00019A50;

        private const int B18FUnknownActionId7Type = 0x0000D2F1;

        private const int B18FUnknownActionId7Instance = 0x4D167F3B;

        private const int B194UnknownActionId2Type = 0x000111D3;

        private const int B194UnknownActionId2Instance = 0x000199EB;

        private const int B194UnknownActionId7Type = 0x0000D2F1;

        private const int B194UnknownActionId7Instance = 0x4D167F40;

        private const string B18CShortInfo = "Terminate 5 Malfunctioning C...";

        private const string B18CLongInfo =
            "Terminate 5 Malfunctioning Cleaning Robots<BR><br>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<br><BR>"
            + "Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, "
            + "he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then "
            + "open the package with brand new cleaning robots and set them to work.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Kill 5 Malfunctining Cleaning Robots.</font>";

        private const string B18DShortInfo = "Open the Cargo Box";

        private const string B18DLongInfo =
            "Open the Cargo Box<BR><BR>"
            + "Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, "
            + "he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then "
            + "open the Cargo Box with brand new cleaning robots and set them to work.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Use (Right Click) the Cargo Box to open it.</font>";

        private const string B18EShortInfo = "Return to Rex Larsson";

        private const string B18ELongInfo =
            "Return to Rex Larsson<BR><BR>"
            + "Return to Rex Larsson to inform him of the great cleaning success.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Rex Larsson.</font>";

        private const string B18FShortInfo = "Talk to Marcus Stone";

        private const string B18FLongInfo =
            "Talk to Marcus Stone<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Rex Larsson told you to spreak with Marcus Stone, an overseer for arriving cargo in the area, "
            + "might be able to aid in getting your license issue settled.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Marcus Stone.</font>";

        private const string B194ShortInfo = "Extinguish the Gas Fire";

        private const string B194LongInfo =
            "Extinguish the Gas Fire<BR><BR>"
            + "Marcus Stone mentioned that he may be able to assist you with your lack of identity on Rubi-Ka, "
            + "but at a price. A recent accident on one of his landing pads has left cargo damaged and people "
            + "injured. Bodies can heal while cargo cannot. Extinguish one of the Gas Fires that has errupted "
            + "on the landing pad.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "(Left Click) the <a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a> "
            + "in your inventory to lift it up, then Left Click the Gas Fire to apply the fire suppressant.</font>";

        private const string B196ShortInfo = "Return to Marcus";

        private const string B196LongInfo =
            "Return to Marcus<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Marcus Stone and hand him the "
            + "<a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a>.</font>";

        private const string FlintShortInfo = "Talk to Flint Novak";

        private const string FlintLongInfo =
            "Talk to Flint Novak<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Flint Novak.</font>";

        private const string FindBioShortInfo = "Find a Bio Analyzing Computer";

        private const string FindBioLongInfo =
            "Find a Bio Analyzing Computer<BR><BR>"
            + "At the request of Flint Novak you must  find a Bio Analyzing Computer. "
            + "You may find one of these computers by taking out the malfunctioning robots in the nearby junkyard."
            + "<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Kill 7 Robots in the junkyard.</font>";

        private const string DeliverBioShortInfo = "Deliver the Bio Analyzing Co...";

        private const string DeliverBioLongInfo =
            "Deliver the Bio Analyzing Computer to Alex Gibbs<BR><BR>"
            + "After killing a few junk robots you finally found a Bio Analyzing Computer. "
            + "Flint Novak told you to give this to Alex Gibbs, a local roboticist."
            + "<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Give the <a href='itemref://156020/156021/1'>Bio Analyzing Computer</a> to Alex Gibbs.</font>";

        private const string SurveillanceUplinkShortInfo = "Surveillance Uplink";

        private const string SurveillanceUplinkLongInfo =
            "Surveillance Uplink<BR><BR>"
            + "Alex Gibbs has provided you with a contraption that will be able to hook into the video feed "
            + "one of Desmond Calitri's Surveillance Droids.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Target the Surveillance Droid and use (Right Click) the "
            + "<a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor in your inventory.</a></font>";

        private const string PlantBugShortInfo = "Plant a Bug";

        private const string PlantBugLongInfo =
            "Plant a Bug<BR><BR>"
            + "To further incriminate Desmond Calitri, a remote audio recording device is to be placed "
            + "within his office.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Find a suitable location in Desmond Calitri's office to hide the bug. Pick up (Left Click) the "
            + "<a href='itemref://295801/295801/1'>RC-P Audio Recording Device</a> in your inventory and drop "
            + "it (Left Click) in a suitable location.</font>";

        private const string DeliverHc12BillShortInfo = "Deliver the Rebuilt HC-12 Se...";

        private const string DeliverHc12BillLongInfo =
            "Deliver the Rebuilt HC-12 SecTec Monitor<BR><BR>"
            + "With the Surveillance Droid feed uplink and a hidden audio recording device in Desmond Calitri's "
            + "office, it is time to deliver this potential evidence to one of Alex's friend ICC Immigration "
            + "Officer Bill.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Give the <a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor</a> to ICC Immigration "
            + "Officer Bill.</font>";

        private const string KneecappingShortInfo = "Kneecapping a Kneebreaker";

        private const string KneecappingLongInfo =
            "Kneecapping a Kneebreaker<BR><BR>"
            + "While monitoring the audio and video feeds of Desmond Calitri, it became clear that he intends "
            + "to send \"The Kneebreaker\", Alfonzo Rizzolo, to deal with an upstart Dockworker who is fighting "
            + "for fair working conditions.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Kill \"The Kneebreaker\".</font>";

        private const string ReportToAlexShortInfo = "Report to Alex";

        private const string ReportToAlexLongInfo =
            "Report to Alex<BR><BR>"
            + "You have put a major dent in Demond Caltiri's plans. Since Bill doesn't want to talk to you about "
            + "this matter, you decided to update Alex on your progress. She did promise you a reward for your "
            + "efforts...<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Alex Gibbs.</font>";

        private const string TalkToStanShortInfo = "Talk to Stan Goodman";

        private const string TalkToStanLongInfo =
            "Talk to Stan Goodman<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission "
            + "is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Alex told you to go talk to Stan Goodman, a local 'purveyer of recently used merchandise'. He should "
            + "be able to help with aquiring more parts for your ID card.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Stan Goodman.</font>";

        private const string BuyLockpickShortInfo = "Buy a Lockpick";

        private const string BuyLockpickLongInfo =
            "Buy a Lockpick<BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetected, but in order "
            + "to do so you need to buy a <a href='itemref://95577/95577/1'>Lock Pick</a>.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Find the <a href='itemref://297290/297290/3'>ICC Tech Supplies</a> vending machine and buy a "
            + "<a href='itemref://95577/95577/1'>Lock Pick</a>.</font>";

        private const string StrongboxContentsShortInfo = "Take the contents of the Str...";

        private const string StrongboxContentsLongInfo =
            "Take the contents of the Strongbox <BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetected. "
            + "Now that you have bought a <a href='itemref://95577/95577/1'>Lock Pick</a>, this should be an easy task."
            + "<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Pick up (Left Click) your <a href='itemref://95577/95577/1'>Lock Pick</a> from your inventory and "
            + "drop it (Left Click) on the <a href='itemref://295604/295604/1'>Merchant's Strongbox</a>.</font>";

        // Capture 20260801-102913 QFU Mission:5574F01A ShortInfo truncation.
        private const string DeliverAntonioFactoryShortInfo = "Deliver Antonio's Adaptation...";

        private const string DeliverAntonioFactoryLongInfo =
            "Deliver Antonio's Adaptation Factory to Stan Goodman.<BR><BR>"
            + "Stan told you to Pick the Lock on the Strongbox in the Merchant's Storage undetected. "
            + "Now that you have found <a href='itemref://248306/248306/1'>Antonio's Adaptation Factory</a>, "
            + "bring it back to Stan.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>"
            + "Bring <a href='itemref://248306/248306/1'>Antonio's Adaptation Factory</a> to Stan Goodman.</font>";

        private const string TalkToSarahGreeneShortInfo = "Talk to Sarah Greene";

        private const string TalkToSarahGreeneLongInfo =
            "Talk to Sarah Greene<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Stab told you that Sarah Greene, a local armorsmith, should be able to help you with aquiring more "
            + "parts needed for your ID card.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Sarah Greene.</font>";

        private const string BuyNanoProgramsShortInfo = "Buy some Nano Programs";

        private const string BuyNanoProgramsLongInfo =
            "Buy some Nano Programs<BR><BR>"
            + "Stanley Goodman told you to go talk to Marco Spida to buy a Nanoprogram Container.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective: Talk to Marco Spida and buy a Nanoprogram Container "
            + "for your profession. Open the Container to complete your mission.</font>";

        // Capture 20260721-sara QuestFullUpdate short/long after Talk to Sarah accept.
        private const string FindTheThiefShortInfo = "Find the thief";

        private const string FindTheThiefLongInfo =
            "Find the thief<BR><BR>"
            + "Sarah recently had one of her custom-built suits of armor stolen from her. The thief was last seen "
            + "in the underground.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Locate the thief and recover the DNA-Locked Armor.</font>";

        private const string DeliverDnaLockedArmorShortInfo = "Deliver DNA-Locked Armor to ...";

        private const string DeliverDnaLockedArmorLongInfo =
            "Deliver DNA-Locked Armor to Sarah Greene<BR><BR>"
            + "You have found the stolen suit of armor. <BR><BR>"
            + "<a href='itemref://295618/295618/1'><img src=\"rdb://88053\"></a><BR>"
            + "<font color=\"#FFFFFF\">Return the DNA-Locked Armor to Sarah Greene.</font>";

        private const string SpeakToVernonGodfrayShortInfo = "Speak to Vernon Godfray";

        private const string SpeakToVernonGodfrayLongInfo =
            "Speak to Vernon Godfray<BR><BR>"
            + "<font color=\"#63ad63\">Identity Crisis:</font><BR>"
            + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
            + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
            + "Sarah told you to speak to Vernon Godfray, a local hacker, who should be able to help with aquiring "
            + "more parts needed for your ID card.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Talk to Vernon Godfray.</font>";

        private const string GiveHackedTechnicalLibraryShortInfo = "Give the Hacked Technical Li...";

        private const string GiveHackedTechnicalLibraryLongInfo =
            "Give the Hacked Technical Library to Vernon Godfray<BR><BR>"
            + "You successfully hacked the OT Technical Library. You should return it to Vernon Godfray.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Give the <a href='itemref://295756/295756/1'>Hacked Technical Library</a> to Vernon Godfray.</font>";

        private const string HackingSkillsShortInfo = "Hacking Skills";

        private const string HackingSkillsLongInfo =
            "Hacking Skills<BR><BR>"
            + "Vernon Godfray told you to hack the OT Technical Library to allow it to be worn by anyone.<BR><BR>"
            + "Use the <a href='itemref://87810/87810/1'>Hacker Tool</a> to hack the "
            + "<a href='itemref://248377/248377/1'> Omni-Tek Technical Library</a> to create the "
            + "<a href='itemref://295756/295756/1'>Hacked Technical Library</a>.<BR>"
            + "<a href='itemref://87810/87810/1'><img src=\"rdb://99282\"></a> + "
            + "<a href='itemref://248377/248377/1'><img src=\"rdb://130564\"></a> = "
            + "<a href='itemref://295756/295756/1'><img src=\"rdb://130561\"></a><BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit %{KEY:WINDOW_TS}%, place the "
            + "<a href='itemref://87810/87810/1'>Hacker Tool</a> as the Source and the "
            + "<a href='itemref://248377/248377/1'> Omni-Tek Technical Library</a> as the Target, then press Build.</font>";

        private const string TradeskillNanoSensorShortInfo = "Tradeskilling (1/4): Assembl...";

        private const string TradeskillNanoSensorLongInfo =
            "Tradeskilling (1/4): Assemble a Nano Sensor<BR><BR>"
            + "<font color=\"#FF0000\">WARNING: If you are interested in learning tradeskilling, this mission will "
            + "help you learn the basics. However, only Engineers and Traders are equipped with profession tools to "
            + "help them master the art of tradeskilling.</font><BR><BR>"
            + "Alex Gibbs has provided you with the recipe for creating a "
            + "<a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>. Once this mission has been "
            + "completed, allow her to inspect it. <BR><BR>"
            + "<font color=\"#FFFFFF\">1. Buy the following item from the "
            + "<a href='itemref://297281/297281/1'>Junk Shop</a>:<BR>"
            + "<a href='itemref://150922/150922/1'><img src=\"rdb://151011\"> Screwdriver</a><BR><BR>"
            + "2. Find <a href='itemref://42620/42620/1'>Robot Junk</a>.<BR>"
            + "Do so by killing and looting a robot.<BR><BR>"
            + "3. Modify the <a href='itemref://42620/42620/1'>Robot Junk</a> with the "
            + "<a href='itemref://150922/150922/1'>Screwdriver</a> to create a "
            + "<a href='itemref://150923/150924/1'>Nano Sensor</a>.<BR>"
            + "<a href='itemref://150922/150922/1'><img src=\"rdb://151011\"></a> + "
            + "<a href='itemref://42620/42620/1'><img src=\"rdb://290417\"></a> = "
            + "<a href='itemref://150923/150923/1'><img src=\"rdb://149940\"></a><BR></font><BR>"
            + "<font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit %{KEY:WINDOW_TS}%, place the "
            + "<a href='itemref://150922/150922/1'>Screwdriver</a> as the Source and the "
            + "<a href='itemref://42620/42620/1'>Robot Junk</a> as the Target, then press Build.</font>";

        private const string TradeskillBasicBrainShortInfo = "Tradeskilling (2/4): Assembl...";

        private const string TradeskillBasicBrainLongInfo =
            "Tradeskilling (2/4): Assemble a Basic Robot Brain<BR><BR>"
            + "Alex Gibbs has provided you with the recipe for creating a "
            + "<a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>. Once it has been completed, "
            + "allow her to inspect it. <BR><BR>"
            + "<font color=\"#FFFFFF\">1. Buy the following item from the "
            + "<a href='itemref://297281/297281/1'>Junk Shop</a>:<BR>"
            + "<a href='itemref://156020/156021/5'><img src=\"rdb://156084\"> Bio Analyzing Computer</a><BR><BR>"
            + "2. Apply the <a href='itemref://156020/156021/1'>Bio Analyzing Computer</a> onto the "
            + "<a href='itemref:// 150923/150923/1'> Nano Sensor</a> to create a "
            + "<a href='itemref://156022/156022/1'>Basic Robot Brain</a>.</font><BR>"
            + "<a href='itemref://156020/156021/5'><img src=\"rdb://156084\"></a> + "
            + "<a href='itemref://150923/150923/1'><img src=\"rdb://149940\"></a> = "
            + "<a href='itemref://156022/156022/1'><img src=\"rdb://156085\"></a><BR><BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit (SHIFT+T), place the "
            + "<a href='itemref://156020/156021/5'>Bio Analyzing Computer</a> as the Source and the "
            + "<a href='itemref:// 150923/150923/1'> Nano Sensor</a> as the Target, then press Build.</font>";

        private const string TradeskillPersonalizedBrainShortInfo = "Tradeskilling (3/4): Assembl...";

        private const string TradeskillPersonalizedBrainLongInfo =
            "Tradeskilling (3/4): Assemble a Personalized Basic Robot Brain<BR><BR>"
            + "Alex Gibbs has provided you with the recipe for creating a "
            + "<a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>. Once it has been completed, "
            + "allow her to inspect it. <BR><BR>"
            + "<font color=\"#FFFFFF\">1. Buy the following item from the Junk Shop:<BR>"
            + "<a href='itemref://156024/ 156025/5'><img src=\"rdb://11618\"> MasterComm - Personalization Device</a>."
            + "<BR><BR>"
            + "2. Attach the <a href='itemref://156024/ 156025/5'>MasterComm - Personalization Device</a> to the "
            + "<a href='itemref://156022/156022/1'>Basic Robot Brain</a> to create the "
            + "<a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>.<BR>"
            + "<a href='itemref://156024/156024/1'><img src=\"rdb://11618\"></a> + "
            + "<a href='itemref://156022/156022/1'><img src=\"rdb://156085\"></a> = "
            + "<a href='itemref://156026/156026/1'><img src=\"rdb://156087\"></a><BR></font><BR> <BR>"
            + "<font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit (SHIFT+T), place the "
            + "<a href='itemref://156024/ 156025/5'>MasterComm - Personalization Device</a> as the Source and the "
            + "<a href='itemref://156022/156022/1'>Basic Robot Brain</a> as the Target, then press Build.</font>";

        private const string TradeskillShowBrainShortInfo = "Tradeskilling (4/4): Show th...";

        private const string TradeskillShowBrainLongInfo =
            "Tradeskilling (4/4): Show the Personalized Computer Brain to Alex<BR><BR>"
            + "Allow Alex Gibbs to inspect the <a href='itemref://156026/156026/1'>Personalized Computer Brain</a> "
            + "you assembled!<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Give the <a href='itemref://156026/156026/1'>Personalized Basic Robot Brain</a> to Alex Gibbs.</font>";

        private const string B199ShortInfo = "Use the Stim on a Wounded Do...";

        private const string B199LongInfo =
            "Use the Stim on a Wounded Dockworker<BR><BR>"
            + "Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR> "
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Target a Wounded Dockworker and use the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a> (Right-Click).</font>";

        private const string B19AShortInfo = "Return to Marcus Stone";

        private const string B19ALongInfo =
            "Return to Marcus Stone<BR><BR>"
            + "Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR>"
            + "<font color=\"#FF0000\">Mission Objective:<BR>"
            + "Return to Marcus Stone and hand him the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a>.</font>";

        public static RexQuestPreviewEmissionResult TrySendB18CPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18CPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18C QuestFullUpdate DTO preview sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18C rawReplay=false noPersistence=true noRewards=true "
                    + "noQuestDelete=true noCompletion=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18C QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18C "
                    + "rawReplay=false noPersistence=true noRewards=true noInventory=true noXpCredits=true "
                    + "noQuestDelete=true noCompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18DPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18D QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18D QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB18DPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B18D QuestFullUpdate preview resent. mission=Mission:5514B18D");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18D QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static bool TrySendB18CCompletionHandoff(ICharacter source)
        {
            if (source == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source character missing.");
                return false;
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source client missing.");
                return false;
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff skipped: source identity is invalid.");
                return false;
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB18CAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18CQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18DPreviewMessage(source.Identity));

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18C completion handoff sent character="
                    + source.Identity.ToString(true)
                    + " action59=Mission:5514B18C questDelete=Mission:5514B18C "
                    + "nextQuestFullUpdate=Mission:5514B18D capture=20260614-194454/events.log:5919-5926 "
                    + "packetHandoffOnly=true noRewards=true noInventory=true noXpCredits=true "
                    + "noDbWrites=true noPersistence=true noCargoBox=true");
                return true;
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18C completion handoff failed: " + e.Message);
                return false;
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18DQuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18D Quest Delete DTO cleanup sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18D source=20260614-194454/packets.hex.log:5765 "
                    + "rawReplay=false noAction59=true b18dWindowCleanupOnly=true noCompletionSemantics=true "
                    + "noPersistence=true noRewards=true noInventory=true noXpCredits=true noB18ECompletion=true");

                source.Controller.Client.SendCompressed(CreateB18DQuestDeleteMessage(source.Identity));

                return RexQuestPreviewEmissionResult.Sent(
                    "B18D Quest Delete sent using DTO serializer. mission=Mission:5514B18D "
                    + "source=20260614-194454/packets.hex.log:5765 rawReplay=false noAction59=true "
                    + "b18dWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true "
                    + "noInventory=true noXpCredits=true noB18ECompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18D Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18D Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18EPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18EPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18E QuestFullUpdate DTO preview sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5767 "
                    + "rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noRewards=true "
                    + "noInventory=true noXpCredits=true noCompletion=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18E QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18E "
                    + "source=20260614-194454/packets.hex.log:5767 rawReplay=false noAction59=true "
                    + "noQuestDelete=true noPersistence=true noRewards=true noInventory=true noXpCredits=true "
                    + "noCompletion=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18E QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18EQuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18E Quest Delete DTO cleanup sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5947 "
                    + "rawReplay=false noAction59=true b18eWindowCleanupOnly=true noCompletionSemantics=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true noDbWrites=true");

                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));

                return RexQuestPreviewEmissionResult.Sent(
                    "B18E Quest Delete sent using DTO serializer. mission=Mission:5514B18E "
                    + "source=20260614-194454/packets.hex.log:5947 rawReplay=false noAction59=true "
                    + "b18eWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noCredits=true "
                    + "noItems=true noInventory=true noDbWrites=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18E Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260614-194454: Quest Delete B18E then QuestFullUpdate B18F (Talk to Marcus).
        /// Always send both — flag-gated delete-only left Return to Rex stuck beside Marcus.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB18EToB18FHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source identity is invalid.");
            }

            try
            {
                // Capture packets #5495 then #5497 (and a live duplicate delete). No Action59 on this swap.
                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18FPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B18E→B18F handoff sent delete+delete+Talk to Marcus Stone. "
                    + "source=20260614-194454/packets.hex.log:5947-5949");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18E→B18F handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18E→B18F handoff failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18FPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB18FPreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Rex B18F QuestFullUpdate DTO handoff sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18F source=20260614-194454/packets.hex.log:5949 "
                    + "nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true noMarcusStoneImplementation=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B18F QuestFullUpdate sent using DTO serializer. mission=Mission:5514B18F "
                    + "source=20260614-194454/packets.hex.log:5949 title=\"Talk to Marcus Stone\" "
                    + "nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true "
                    + "noPersistence=true noCredits=true noItems=true noInventory=true "
                    + "noMarcusStoneImplementation=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Rex B18F QuestFullUpdate DTO handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F QuestFullUpdate failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18FToB194Handoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source identity is invalid.");
            }

            try
            {
                // Same capture pattern as B18C→B18D and B194→B196: Action59 + Delete + next QFU.
                source.Controller.Client.SendCompressed(CreateB18FAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18FQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB194PreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B18F→B194 handoff sent action59+delete+Extinguish the Gas Fire. "
                    + "source=20260719-Rex-Markus-stone/mission-flow.log:8-10");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B18F→B194 handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F→B194 handoff failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB18FQuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Marcus B18F Quest Delete DTO cleanup sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B18F source=20260614-195107/events.log:1645-1646 "
                    + "rawReplay=false b18fWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true "
                    + "noRewards=true noInventory=true noXpCredits=true");

                source.Controller.Client.SendCompressed(CreateB18FQuestDeleteMessage(source.Identity));

                return RexQuestPreviewEmissionResult.Sent(
                    "B18F Quest Delete sent using DTO serializer. mission=Mission:5514B18F "
                    + "source=20260614-195107/events.log:1645-1646 rawReplay=false "
                    + "b18fWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true "
                    + "noInventory=true noXpCredits=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B18F Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B18F Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB194Preview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B194 QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B194 QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B194 QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateB194PreviewMessage(source.Identity);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "Arete Marcus B194 QuestFullUpdate DTO preview sending character="
                    + source.Identity.ToString(true)
                    + " mission=Mission:5514B194 source=20260614-195107/packets.hex.log:1407 "
                    + "trigger=marcus_195107_b18f_002:0 rawReplay=false noPersistence=true noRewards=true "
                    + "noInventory=true item296780Deferred=true noFollowUpMission=true noTrade=true");

                source.Controller.Client.SendCompressed(message);

                return RexQuestPreviewEmissionResult.Sent(
                    "B194 QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B194 "
                    + "source=20260614-195107/packets.hex.log:1407 title=\"Extinguish the Gas Fire\" "
                    + "rawReplay=false noPersistence=true noRewards=true noInventory=true "
                    + "item296780Deferred=true noFollowUpMission=true noTrade=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B194 QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B194 QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB194QuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                // Capture 20260719-Rex-Markus-stone events.log:11002-11006:
                // CharacterAction 59 targeting the B194 mission, then Quest Delete.
                source.Controller.Client.SendCompressed(CreateB194Action59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB194QuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B194 Action59+Quest Delete sent. mission=Mission:5514B194 "
                    + "source=20260719-Rex-Markus-stone/events.log:11002-11006");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B194 Quest Delete DTO cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B194 Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Capture-backed B194 → B196 handoff: remove Extinguish (+ leftover Talk to Marcus), then Return to Marcus.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB194ToB196Handoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB194Action59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB194QuestDeleteMessage(source.Identity));
                // Leftover Talk to Marcus (B18F) must not stay beside Return to Marcus.
                source.Controller.Client.SendCompressed(CreateB18FQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB196PreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B194→B196 handoff sent action59+delete B194/B18F/B18E + Return to Marcus. "
                    + "source=20260719-Rex-Markus-stone/events.log:11002-11008");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B194→B196 handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B194→B196 handoff failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB196Preview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B196 QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B196 QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB196PreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B196 QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B196 "
                    + "title=\"Return to Marcus\" source=20260614-195107/packets.hex.log:1773");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B196 QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B196 QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB196QuestDelete(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source identity is invalid.");
            }

            try
            {
                // Delete-only (no Action59) — Action59 mid-dialogue was aborting the client transport.
                source.Controller.Client.SendCompressed(CreateB196QuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B196 Quest Delete sent. mission=Mission:5514B196");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B196 Quest Delete failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B196 Quest Delete failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Finish Return to Marcus: remove B196 and every leftover Marcus/Rex fire-chain mission from the client.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB196CompletionCleanup(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source identity is invalid.");
            }

            try
            {
                // Delete-only — do not send Action59 here (client abort / ZoneClient IOException).
                source.Controller.Client.SendCompressed(CreateB196QuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB194QuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18FQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B196 completion cleanup deleted B196/B194/B18F/B18E from mission window.");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B196 completion cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B196 completion cleanup failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260719-185137 / Rex-Markus-stone events.log:12204-12210:
        /// Action59 on Return to Marcus → Quest Delete → QuestFullUpdate Talk to Flint Novak.
        /// Leftover mission deletes after the live tip so dirty clients cannot keep Extinguish/Talk Marcus.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB196ToFlintHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB196Action59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB196QuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB194QuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18FQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB18EQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateFlintPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B196→Flint handoff Action59+Delete leftovers + Talk to Flint Novak. "
                    + "mission=Mission:5514B198 source=20260719-185137/events.log:12204-12210");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B196→Flint handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B196→Flint handoff failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendFlintPreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint QuestFullUpdate preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Flint QuestFullUpdate preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Flint QuestFullUpdate preview failed: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateFlintPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "Flint QuestFullUpdate preview sent. mission=Mission:5514B198 title=\"Talk to Flint Novak\"");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Flint QuestFullUpdate DTO preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "Flint QuestFullUpdate preview failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Accept wounded-workers side quest: ADD Use Stim (B199) without removing Talk to Flint Novak.
        /// Flint is the main tip; heal workers is optional and stacks beside it.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendFlintToB199Handoff(ICharacter source)
        {
            // Kept name for call-site compatibility; stacks B199 beside Flint (does not delete Flint).
            return TrySendB199Preview(source);
        }

        /// <summary>
        /// Capture 20260719-224226: stim use → Action59+Delete B199 + QFU Return to Marcus Stone (B19A).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB199ToB19AHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB199Action59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateB199QuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB19APreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B199→B19A handoff Action59+Delete + Return to Marcus Stone. mission=Mission:5514B19A "
                    + "source=20260719-224226/events.log");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B199→B19A handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B199→B19A handoff failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Stim return finished: remove Return to Marcus Stone (B19A) from the mission window.
        /// Delete-only mid-dialogue — Action59 can abort transport before Delete is applied.
        /// Does not touch Talk to Flint Novak.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB19ACompletionCleanup(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB19AQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateB19AQuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B19A completion cleanup Delete×2 (no Action59). mission=Mission:5514B19A "
                    + "keepFlint=true");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B19A cleanup failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B19A cleanup failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Soft-remove Flint tip without Action59 (stacked-tip cleanup on Marcus reopen).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendFlintQuestDeleteOnly(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateFlintQuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "Flint Quest Delete only. mission=Mission:5514B198");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus Flint delete failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "Flint delete failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB199Preview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB199PreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B199 QuestFullUpdate preview sent. mission=Mission:5514B199 "
                    + "title=\"Use the Stim on a Wounded Dockworker\"");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B199 preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B199 preview failed during DTO serialization/send: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendB19APreview(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB19APreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B19A QuestFullUpdate preview sent. mission=Mission:5514B19A "
                    + "title=\"Return to Marcus Stone\"");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B19A preview failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B19A preview failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Soft-remove Return to Marcus Stone tip without Action59 (stacked-tip cleanup while Use Stim is active).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendB19AQuestDeleteOnly(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source character missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source identity is invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateB19AQuestDeleteMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "B19A Quest Delete only. mission=Mission:5514B19A");
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Arete Marcus B19A delete failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed(
                    "B19A delete failed during DTO serialization/send: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-072904: Action59+Delete Talk to Flint + QFU Find a Bio Analyzing Computer.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendFlintToFindBioHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: source missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: identity invalid.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateFlintAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateFlintQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateFindBioPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "Flint→FindBio Action59+Delete + Find a Bio Analyzing Computer. mission=Mission:5514B19B "
                    + "source=20260720-072904/mission-flow.log:2-3");
            }
            catch (Exception e)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Arete Flint→FindBio handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff failed: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendFindBioPreview(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("FindBio preview skipped: source/client missing.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateFindBioPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "FindBio QuestFullUpdate preview sent. mission=Mission:5514B19B");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("FindBio preview failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-072904: TemplateAction 156020 + Action59+Delete Find + QFU Deliver.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendFindBioToDeliverHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: source missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: identity invalid.");
            }

            try
            {
                // Grant already sent TemplateAction 156020 — tip wire only.
                // Also clear leftover Talk to Flint ghosts from earlier handoffs.
                FlintKneecappingTipWire.TryDeleteTip(source, FlintInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, FindBioInstance);
                source.Controller.Client.SendCompressed(CreateFindBioAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateFindBioQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateDeliverBioPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "FindBio→Deliver Action59+Delete + Deliver tip. mission=Mission:5514B19C "
                    + "source=20260720-flint/mission-flow.log");
            }
            catch (Exception e)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Arete FindBio→Deliver handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff failed: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendDeliverBioPreview(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("DeliverBio preview skipped: source/client missing.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateDeliverBioPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "DeliverBio QuestFullUpdate preview sent. mission=Mission:5514B19C");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("DeliverBio preview failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260731-184635: Action59+Delete Deliver + QFU Surveillance Uplink
        /// (+ Blank Info Chip / HC-12 granted before tip).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendDeliverBioToSurveillanceUplinkHandoff(ICharacter source)
        {
            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: source missing.");
            }

            if (source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: client missing.");
            }

            if (source.Identity.Type != IdentityType.CanbeAffected || source.Identity.Instance == 0)
            {
                return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: identity invalid.");
            }

            try
            {
                FlintKneecappingTipWire.TryDeleteTip(source, FlintInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, FindBioInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, DeliverBioInstance);
                source.Controller.Client.SendCompressed(CreateDeliverBioAction59Message(source.Identity));
                source.Controller.Client.SendCompressed(CreateDeliverBioQuestDeleteMessage(source.Identity));
                source.Controller.Client.SendCompressed(CreateSurveillanceUplinkPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "Deliver→SurveillanceUplink Action59+Delete + tip. mission=Mission:5514B19D "
                    + "source=20260731-184635/mission-flow.log");
            }
            catch (Exception e)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Arete Deliver→Uplink handoff failed: " + e.Message);
                return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff failed: " + e.Message);
            }
        }

        public static RexQuestPreviewEmissionResult TrySendSurveillanceUplinkPreview(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Uplink preview skipped: source/client missing.");
            }

            try
            {
                source.Controller.Client.SendCompressed(CreateSurveillanceUplinkPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "SurveillanceUplink QuestFullUpdate preview sent. mission=Mission:5514B19D");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Uplink preview failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-105157: Action59+Delete Surveillance Uplink (555A4A49) + QFU Plant a Bug (555A4E3B).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendUplinkToPlantBugHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Uplink→PlantBug handoff skipped: client missing.");
            }

            try
            {
                // Capture: Action59 (Int16) + Quest/Delete Uplink, then QFU Plant a Bug.
                FlintKneecappingTipWire.TryDeleteTip(source, SurveillanceUplinkInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, LegacySurveillanceUplinkInstance);
                source.Controller.Client.SendCompressed(CreatePlantBugPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "Uplink→PlantBug Action59+Delete + tip. mission=Mission:555A4E3B "
                    + "source=20260720-105157");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Uplink→PlantBug handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-105157: Action59+Delete Plant a Bug + QFU Deliver HC-12 to Bill.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendPlantBugToDeliverBillHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("PlantBug→DeliverBill handoff skipped: client missing.");
            }

            try
            {
                // Also wipe Uplink if a prior handoff left it stuck (Remain 00:00).
                FlintKneecappingTipWire.TryDeleteTip(source, SurveillanceUplinkInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, LegacySurveillanceUplinkInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, PlantBugInstance);
                FlintKneecappingTipWire.TryDeleteTip(source, LegacyPlantBugInstance);
                source.Controller.Client.SendCompressed(CreateDeliverHc12BillPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "PlantBug→DeliverBill Action59+Delete + tip. mission=Mission:555A4E3C "
                    + "source=20260720-105157");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("PlantBug→DeliverBill handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-105157: FinishTrade / "I will take care of it."
        /// Action59+Delete prior tips, then Kneecapping QFU (serializer path).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendDeliverBillToKneecappingHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverBill→Kneecapping handoff skipped: client missing.");
            }

            try
            {
                int[] tipsToDelete =
                    {
                        SurveillanceUplinkInstance,
                        LegacySurveillanceUplinkInstance,
                        PlantBugInstance,
                        LegacyPlantBugInstance,
                        DeliverHc12BillInstance,
                        LegacyDeliverHc12BillInstance,
                        KneecappingInstance,
                        LegacyKneecappingInstance
                    };

                for (int i = 0; i < tipsToDelete.Length; i++)
                {
                    FlintKneecappingTipWire.TryDeleteTip(source, tipsToDelete[i]);
                }

                source.Controller.Client.SendCompressed(CreateKneecappingPreviewMessage(source.Identity));
                return RexQuestPreviewEmissionResult.Sent(
                    "DeliverBill→Kneecapping Action59+Delete + tip. mission=Mission:555A4E3D "
                    + "source=20260720-105157");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverBill→Kneecapping handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Legacy name — Bill turn-in clears Deliver and grants Kneecapping tip (capture).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendBillTurnInClearTips(ICharacter source)
        {
            return TrySendDeliverBillToKneecappingHandoff(source);
        }

        /// <summary>
        /// Idempotent Kneecapping tip send (dialogue safety / re-send).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendKneecappingTip(ICharacter source)
        {
            return TrySendDeliverBillToKneecappingHandoff(source);
        }

        /// <summary>
        /// Capture 20260720-171317: kill Kneebreaker → Delete Kneecapping + QFU Report to Alex.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendKneecappingToReportAlexHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Kneecapping→ReportAlex handoff skipped: client missing.");
            }

            try
            {
                ZoneClient client = source.Controller.Client as ZoneClient;
                Character character = source as Character;
                if (client != null && character != null)
                {
                    FlintKneecappingTipWire.ClearChainTips(client, character);
                }
                else
                {
                    SendTipAction59AndDelete(source, KneecappingInstance);
                    SendTipAction59AndDelete(source, LegacyKneecappingInstance);
                }

                QuestFullUpdateMessage reportTip = CreateReportToAlexPreviewMessage(source.Identity);
                ApplyLiveTipExpiry(reportTip, source);
                source.Controller.Client.SendCompressed(reportTip);
                return RexQuestPreviewEmissionResult.Sent(
                    "Kneecapping→ReportAlex tip. mission=Mission:555B4365 source=20260720-171317");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Kneecapping→ReportAlex handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-171317: Alex Calitri report → Delete Report + QFU Talk to Stan.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendReportAlexToTalkStanHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "ReportAlex→TalkStan handoff skipped: client missing.");
            }

            try
            {
                ZoneClient client = source.Controller.Client as ZoneClient;
                Character character = source as Character;
                if (client != null && character != null)
                {
                    FlintKneecappingTipWire.ClearChainTips(client, character);
                }
                else
                {
                    SendTipAction59AndDelete(source, ReportToAlexInstance);
                }

                SendTalkToStanTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "ReportAlex→TalkStan tip. mission=Mission:555B4366 source=20260720-171317");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("ReportAlex→TalkStan handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Soft re-emit Talk to Stan tip (no ClearChainTips). Keeps main quest beside Tip 4/4.
        /// Capture 20260720-190432: Talk to Stan + Tradeskilling tips coexist.
        /// </summary>
        public static RexQuestPreviewEmissionResult TryRefreshTalkToStanTip(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Talk to Stan refresh skipped: client missing.");
            }

            try
            {
                SendTalkToStanTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "Talk to Stan tip refreshed. mission=Mission:555B4366");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Talk to Stan refresh failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-goldman: accept Stan job → Action59+Delete TalkStan + QFU Buy a Lockpick.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTalkStanToBuyLockpickHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "TalkStan→BuyLockpick handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, TalkToStanInstance);
                SendBuyLockpickTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "TalkStan→BuyLockpick tip. mission=Mission:555BD124 source=20260720-goldman");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("TalkStan→BuyLockpick handoff failed: " + e.Message);
            }
        }

        private static void SendBuyLockpickTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateBuyLockpickPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-lockpick packets.hex #542-#544:
        /// Action59+Quest/Delete BuyLockpick (555BD124) then QFU Strongbox (555BE9C5).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendBuyLockpickToStrongboxHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "BuyLockpick→Strongbox handoff skipped: client missing.");
            }

            try
            {
                // Capture-exact delete wire first (generic tip delete left Buy Lockpick stuck).
                SendCapturedBuyLockpickTipDelete(source);
                SendTipAction59AndDelete(source, BuyLockpickInstance);
                SendStrongboxContentsTip(source);
                // Second delete after Strongbox QFU — client tip list sometimes kept the old tip.
                SendCapturedBuyLockpickTipDelete(source);
                SendTipAction59AndDelete(source, BuyLockpickInstance);
                return RexQuestPreviewEmissionResult.Sent(
                    "BuyLockpick→Strongbox tip. mission=Mission:555BE9C5 source=20260721-lockpick");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("BuyLockpick→Strongbox handoff failed: " + e.Message);
            }
        }

        private static void SendCapturedBuyLockpickTipDelete(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return;
            }

            // Capture 20260721-lockpick #542 CharacterAction Action=59 Target=Mission:555BD124
            const string action59Hex =
                "206D000A0001003700000DC1797E30295E4777700000C350797E3029000000003B000000000000DAC3555BD1240000DAC3555BD1240000";
            // Capture 20260721-lockpick #543 Quest Action=Delete Mission:555BD124
            const string questDeleteHex =
                "206E000A0001003500000DC1797E3029212C487A0000C350797E30290000000001000000000000DAC3555BD1240000000000000000";
            const int capturedPlayerInstance = unchecked((int)0x797E3029);

            byte[] action59 = HexToBytes(action59Hex);
            ReplaceInt32Be(action59, capturedPlayerInstance, source.Identity.Instance);
            byte[] questDelete = HexToBytes(questDeleteHex);
            ReplaceInt32Be(questDelete, capturedPlayerInstance, source.Identity.Instance);

            client.EnqueueOutboundCompressedBuffer(action59);
            client.EnqueueOutboundCompressedBuffer(questDelete);
        }

        private static void SendStrongboxContentsTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateStrongboxContentsPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-afgter dog lockpick goodman: strongbox pick → Action59+Delete Strongbox +
        /// QFU Deliver Antonio's Adaptation Factory (555BE9F2).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendStrongboxToDeliverFactoryHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Strongbox→DeliverFactory handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, StrongboxContentsInstance);
                SendDeliverAntonioFactoryTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "Strongbox→DeliverFactory tip. mission=Mission:555BE9F2 source=20260721-afgter dog lockpick goodman");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Strongbox→DeliverFactory handoff failed: " + e.Message);
            }
        }

        private static void SendDeliverAntonioFactoryTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateDeliverAntonioFactoryPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-afgter dog lockpick goodman: Stan Accept → Action59+Delete Deliver +
        /// QFU Talk to Sarah Greene (555BE9F3) + QFU Buy some Nano Programs (555BE9F4).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendDeliverFactoryToSarahAndNanoTipsHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverFactory→Sarah/Nano handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, DeliverAntonioFactoryInstance);
                SendTalkToSarahGreeneTip(source);
                SendBuyNanoProgramsTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "DeliverFactory→Sarah+Nano tips. missions=Mission:555BE9F3,Mission:555BE9F4 "
                    + "source=20260721-afgter dog lockpick goodman");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverFactory→Sarah/Nano handoff failed: " + e.Message);
            }
        }

        private static void SendTalkToSarahGreeneTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateTalkToSarahGreenePreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        private static void SendBuyNanoProgramsTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateBuyNanoProgramsPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Login heal for Active Buy Nano tip — re-send tip only (do not re-run factory handoff).
        /// </summary>
        internal static void SendBuyNanoProgramsTipForLogin(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            SendBuyNanoProgramsTip(source);
        }

        /// <summary>
        /// Capture 20260721-sara: Talk Sarah accept → Action59+Delete TalkSarah + QFU Find the thief.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTalkSarahToFindThiefHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "TalkSarah→FindThief handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, TalkToSarahGreeneInstance);
                SendFindTheThiefTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "TalkSarah→FindThief tip. mission=Mission:555BE9F5 source=20260721-sara");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "TalkSarah→FindThief handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-sara: Use Remains of Shop Thief → Action59+Delete FindThief + QFU Deliver.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendFindThiefToDeliverArmorHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "FindThief→DeliverArmor handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, FindTheThiefInstance);
                SendDeliverDnaLockedArmorTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "FindThief→DeliverArmor tip. mission=Mission:555BE9F6 source=20260721-sara");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "FindThief→DeliverArmor handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-sara: Sarah Accept → Action59+Delete Deliver + QFU Speak to Vernon.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendDeliverArmorToVernonHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverArmor→Vernon handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, DeliverDnaLockedArmorInstance);
                SendSpeakToVernonGodfrayTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "DeliverArmor→Vernon tip. mission=Mission:555BE9F7 source=20260721-sara");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "DeliverArmor→Vernon handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: "... well what?" → Action59+Delete SpeakVernon + QFU Hacking Skills.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendSpeakVernonToHackingSkillsHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "SpeakVernon→HackingSkills handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, SpeakToVernonGodfrayInstance);
                SendHackingSkillsTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "SpeakVernon→HackingSkills tip. mission=Mission:555BE9F8 source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "SpeakVernon→HackingSkills handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: combine → Action59+Delete Hacking Skills + QFU Give Hacked Library.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendHackingSkillsToGiveLibraryHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "HackingSkills→GiveLibrary handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, HackingSkillsInstance);
                SendGiveHackedTechnicalLibraryTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "HackingSkills→GiveLibrary tip. mission=Mission:555BE9F9 source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "HackingSkills→GiveLibrary handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Login/zone resync: emit Give Hacked Library tip without deleting prior tips.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendGiveHackedTechnicalLibraryTipOnly(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "GiveLibrary tip skipped: client missing.");
            }

            try
            {
                SendGiveHackedTechnicalLibraryTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "GiveLibrary tip-only. mission=Mission:555BE9F9 source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "GiveLibrary tip-only failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: Hacked Library Accept → Delete Give tip + QFU Cargo Lifting.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendGiveLibraryToCargoLiftingHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "GiveLibrary→CargoLifting handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, GiveHackedTechnicalLibraryInstance);
                SendCargoLiftingTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "GiveLibrary→CargoLifting tip. mission=Mission:555BE9FA source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "GiveLibrary→CargoLifting handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Login/zone resync: Cargo Lifting tip without Action59 delete.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendCargoLiftingTipOnly(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "CargoLifting tip skipped: client missing.");
            }

            try
            {
                SendCargoLiftingTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "CargoLifting tip-only. mission=Mission:555BE9FA source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "CargoLifting tip-only failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260801-105429 / 20260721-Vernon-Godfray: Re-route →
        /// Action59+Quest Delete Cargo Lifting, then QFU Return to Vernon.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendCargoLiftingToReturnVernonHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "CargoLifting→ReturnVernon handoff skipped: client missing.");
            }

            try
            {
                // Fixed tip id + capture-source id (wire shell Mission:555CF577 before patch).
                // Deleting only 555BE9FA left Cargo stuck when the client tip used the shell id.
                SendTipAction59AndDelete(source, CargoLiftingInstance);
                SendTipAction59AndDelete(source, CargoLiftingCapturedWireInstance);
                SendReturnToVernonGodfrayTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "CargoLifting→ReturnVernon tip. mission=Mission:555BE9FB source=20260801-105429");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "CargoLifting→ReturnVernon handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Login/zone resync: Return to Vernon tip without Action59 delete.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendReturnToVernonGodfrayTipOnly(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "ReturnVernon tip skipped: client missing.");
            }

            try
            {
                // Stale Cargo Lifting tips survive if an earlier handoff missed Action59/Delete.
                SendTipAction59AndDelete(source, CargoLiftingInstance);
                SendTipAction59AndDelete(source, CargoLiftingCapturedWireInstance);
                SendReturnToVernonGodfrayTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "ReturnVernon tip-only. mission=Mission:555BE9FB source=20260801-105429");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "ReturnVernon tip-only failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: return chip Accept → Delete Return tip + QFU Talk to Doctor Mason.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendReturnVernonToDoctorMasonHandoff(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "ReturnVernon→DoctorMason handoff skipped: client missing.");
            }

            try
            {
                SendTipAction59AndDelete(source, ReturnToVernonGodfrayInstance);
                SendTalkToDoctorMasonTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "ReturnVernon→DoctorMason tip. mission=Mission:555BE9FC source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "ReturnVernon→DoctorMason handoff failed: " + e.Message);
            }
        }

        /// <summary>
        /// Login/zone resync: Talk to Doctor Mason tip without Action59 delete.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTalkToDoctorMasonTipOnly(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "TalkDoctorMason tip skipped: client missing.");
            }

            try
            {
                SendTalkToDoctorMasonTip(source);
                return RexQuestPreviewEmissionResult.Sent(
                    "TalkDoctorMason tip-only. mission=Mission:555BE9FC source=20260721-Vernon-Godfray");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "TalkDoctorMason tip-only failed: " + e.Message);
            }
        }

        private static void SendHackingSkillsTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateHackingSkillsPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        private static void SendGiveHackedTechnicalLibraryTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            // Capture 20260721-Vernon-Godfray #304: replay wire QFU (DTO rebuild crashed client on login).
            if (TrySendGiveHackedTechnicalLibraryTipWire(source))
            {
                return;
            }

            QuestFullUpdateMessage message = CreateGiveHackedTechnicalLibraryPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray IN #304 QuestFullUpdate (live Mission:555CF576).
        /// Patches player instance + fixed mission id Mission:555BE9F9.
        /// </summary>
        private static bool TrySendGiveHackedTechnicalLibraryTipWire(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return false;
            }

            const string giveLibraryQfuHex =
                "4625000A000102C200000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555CF5760000000F0000000000000000000000024769766520746865204861636B656420546563686E6963616C204C692E2E2E000000012C4769766520746865204861636B656420546563686E6963616C204C69627261727920746F205665726E6F6E20476F64667261793C42523E3C42523E596F75207375636365737366756C6C79206861636B656420746865204F5420546563686E6963616C204C6962726172792E20596F752073686F756C642072657475726E20697420746F205665726E6F6E20476F64667261792E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E4769766520746865203C6120687265663D276974656D7265663A2F2F3239353735362F3239353735362F31273E4861636B656420546563686E6963616C204C6962726172793C2F613E20746F205665726E6F6E20476F64667261792E3C2F666F6E743E000000C35078E0FC63000000060000052800000000000008B5000003F1000003F1000003F14F47493800000000000000000000000000000000000000000000000000000000000000000000C350797E306A00026ADD0000000000000000000007E200000006000111D3484154430000000000000000000111D3565254520000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D578B3000009C5000001999000186A0000186A04556A00000000000444E4000000007E20000C350797E306A0000000105578B30000000000000000000000006000007E20000C350797E306A0000000000019A8B000000000000000000000000000000000000000000000007000003F101";

            const int capturedPlayerInstance = unchecked((int)0x797E306A);
            const int capturedMissionInstance = unchecked((int)0x555CF576);

            byte[] packet = HexToBytes(giveLibraryQfuHex);
            ReplaceInt32Be(packet, capturedPlayerInstance, source.Identity.Instance);
            ReplaceInt32Be(packet, capturedMissionInstance, GiveHackedTechnicalLibraryInstance);
            client.EnqueueOutboundCompressedBuffer(packet);
            return true;
        }

        private static void SendCargoLiftingTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            if (TrySendCargoLiftingTipWire(source))
            {
                return;
            }

            QuestFullUpdateMessage message = CreateCargoLiftingPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray IN #429 QuestFullUpdate (live Mission:555CF577).
        /// Patches player instance + fixed mission id Mission:555BE9FA.
        /// </summary>
        private static bool TrySendCargoLiftingTipWire(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return false;
            }

            const string cargoLiftingQfuHex =
                "46A2000A000102FE00000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555CF5770000000F000000000000000000000002436172676F204C696674696E67000000017A436172676F204C696674696E673C42523E3C42523E5665726E6F6E206D656E74696F6E6564207468617420686520776F756C64206C696B6520746F20676574206869732068616E6473206F6E2074686520646174612066726F6D206F6E65206F6620746865205368697070696E67204D616E6966657374205465726D696E616C73206C6F636174656420696E2074686520696E647573747269616C206469737472696374206F66207468652073687574746C65706F72742E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E4F70656E2061206469616C6F67207769746820746865205368697070696E67204D616E6966657374205465726D696E616C20616E64206170706C7920746865203C6120687265663D276974656D7265663A2F2F38373831302F38373831302F31273E4861636B657220546F6F6C3C2F613E206966206163636573732069732064656E6965642E3C2F666F6E743E000000C35078E0FC6300000006000000000000000000000000000003F1000003F1000003F14850554E00000000000000000000000000000000000000000000000000000000000000000000C350797E306A0003BC520000000000000000000007E20000001800000000000000000000000000000000000111D3000199D60000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D578B3100009C5000001999000186A0000186A0455DE0000000000044504000000007E20000C350797E306A0000000105578B31000000000000000000000006000007E20000C350797E306A00000000000199D6000000000000000000000000000000000000000000000007000003F101";

            const int capturedPlayerInstance = unchecked((int)0x797E306A);
            const int capturedMissionInstance = unchecked((int)0x555CF577);

            byte[] packet = HexToBytes(cargoLiftingQfuHex);
            ReplaceInt32Be(packet, capturedPlayerInstance, source.Identity.Instance);
            ReplaceInt32Be(packet, capturedMissionInstance, CargoLiftingInstance);
            client.EnqueueOutboundCompressedBuffer(packet);
            return true;
        }

        private static void SendReturnToVernonGodfrayTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            if (TrySendReturnToVernonGodfrayTipWire(source))
            {
                return;
            }

            QuestFullUpdateMessage message = CreateReturnToVernonGodfrayPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray IN #1494 QuestFullUpdate (live Mission:555CF578).
        /// Patches player instance + fixed mission id Mission:555BE9FB.
        /// Unknown8 patched 0x08B5 (2229) → 0x0A24 (2596) per capture 20260801-105429 turn-in.
        /// </summary>
        private static bool TrySendReturnToVernonGodfrayTipWire(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return false;
            }

            const string returnVernonQfuHex =
                "4ACB000A000102C600000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555CF5780000000F00000000000000000000000252657475726E20746F205665726E6F6E20476F6466726179000000012752657475726E20746F205665726E6F6E20476F64667261793C42523E3C42523E41667465722066696E697368696E6720746865206861636B206A6F622C2072657475726E20746F205665726E6F6E20616E64206865206D696768742068656C7020796F75207769746820796F75722049442070726F626C656D2E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E54616C6B20746F205665726E6F6E20476F646672617920616E6420676976652068696D20746865203C6120687265663D276974656D7265663A2F2F3239363537322F3239363537322F31273E556E70726F6772616D6D6564204964656E74696669636174696F6E20436869703C2F613E2E3C2F666F6E743E000000C35078E0FC6300000006000005500000000000000A24000003F1000003F1000007E20004867F0004867F0000000100000000595A464900000000000000003132593800000009000000000000000000000000000000000000C350797E306A00026ADD0000000000000000000007E200000006000111D3554E49440000000000000000000111D3565254520000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D578B3600009C5000001999000186A0000186A04556A00000000000444E4000000007E20000C350797E306A0000000105578B36000000000000000000000006000007E20000C350797E306A00000000000199F3000000000000000000000000000000000000000000000007000003F101";

            const int capturedPlayerInstance = unchecked((int)0x797E306A);
            const int capturedMissionInstance = unchecked((int)0x555CF578);

            byte[] packet = HexToBytes(returnVernonQfuHex);
            ReplaceInt32Be(packet, capturedPlayerInstance, source.Identity.Instance);
            ReplaceInt32Be(packet, capturedMissionInstance, ReturnToVernonGodfrayInstance);
            client.EnqueueOutboundCompressedBuffer(packet);
            return true;
        }

        private static void SendTalkToDoctorMasonTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            if (TrySendTalkToDoctorMasonTipWire(source))
            {
                return;
            }

            QuestFullUpdateMessage message = CreateTalkToDoctorMasonPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray QuestFullUpdate (live Mission:555CF579).
        /// Patches player instance + fixed mission id Mission:555BE9FC.
        /// </summary>
        private static bool TrySendTalkToDoctorMasonTipWire(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null || source.Identity.Instance == 0)
            {
                return false;
            }

            const string doctorMasonQfuHex =
                "4EAA000A0001036400000DC1797E306A465A40610000C350797E306A01000007E20000DAC3555CF5790000000F00000000000000000000000254616C6B20746F20446F63746F72204D61736F6E00000001D954616C6B20746F20446F63746F72204D61736F6E3C42523E3C42523E3C666F6E7420636F6C6F723D2223363361643633223E4964656E74697479204372697369733A3C2F666F6E743E3C42523E496E206F7264657220746F206C65617665204172657465204C616E64696E6720616E64206265636F6D65206120636974697A656E206F6620527562692D4B612C20796F75206E65656420616E206964656E746974792E20596F7572206D697373696F6E20697320746F2063726561746520612066616B65204944204361726420746F20796F752063616E206C65617665207468697320706C6163652E2E3C42523E3C42523E41667465722068656C70696E67205665726E6F6E2C206865206761766520796F75206120426C616E6B2049434320494420436869702E20486520736169642074686174204472204D61736F6E20776F756C642062652061626C6520746F2068656C7020796F75206F7574206675727468657220746F20696D7072696E7420796F757220444E4120696E20746F2074686520636869702E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E54616C6B20746F20446F63746F72204D61736F6E2E3C2F666F6E743E000000C35078E0FC6800000006000000000000000000000000000003F1000003F1000003F15352374200000000000000000000000000000000000000000000000000000000000000000000C350797E306A0003BC520000000000000000000007E20000001800000000000000000000000000000000000111D300019A5A0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2F14D578B3700009C5000001999000186A0000186A0455670000000000044474000000007E20000C350797E306A0000000105578B37000000000000000000000006000007E20000C350797E306A0000000000019A5A000000000000000000000000000000000000000000000007000003F101";

            const int capturedPlayerInstance = unchecked((int)0x797E306A);
            const int capturedMissionInstance = unchecked((int)0x555CF579);

            byte[] packet = HexToBytes(doctorMasonQfuHex);
            ReplaceInt32Be(packet, capturedPlayerInstance, source.Identity.Instance);
            ReplaceInt32Be(packet, capturedMissionInstance, TalkToDoctorMasonInstance);
            client.EnqueueOutboundCompressedBuffer(packet);
            return true;
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

        private static void SendFindTheThiefTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateFindTheThiefPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        private static void SendDeliverDnaLockedArmorTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateDeliverDnaLockedArmorPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        private static void SendSpeakToVernonGodfrayTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateSpeakToVernonGodfrayPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        private static void SendTalkToStanTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
            QuestFullUpdateMessage message = CreateTalkToStanPreviewMessage(source.Identity);
            ApplyLiveTipExpiry(message, source);
            source.Controller.Client.SendCompressed(message);
        }

        internal static void ReanchorGameTimeForWireTip(ICharacter source)
        {
            ReanchorGameTimeForTipJournal(source);
        }

        private static void ReanchorGameTimeForTipJournal(ICharacter source)
        {
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client == null)
            {
                return;
            }

            client.SendCompressed(
                new GameTimeMessage
                {
                    Identity = source.Identity,
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                });
            client.LastGameTimeSyncUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Capture 20260720-171317: Alex robot-brain option → Tradeskilling (1/4) tip.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTradeskillNanoSensorTip(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "Tradeskill tip skipped: client missing.");
            }

            try
            {
                QuestFullUpdateMessage message = CreateTradeskillNanoSensorPreviewMessage(source.Identity);
                ApplyLiveTipExpiry(message, source);
                source.Controller.Client.SendCompressed(message);
                if (IsTalkToStanMissionOpen(source))
                {
                    TryRefreshTalkToStanTip(source);
                }

                return RexQuestPreviewEmissionResult.Sent(
                    "Tradeskill Nano Sensor tip. mission=Mission:555B4367 source=20260720-171317");
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Tradeskill tip failed: " + e.Message);
            }
        }

        /// <summary>
        /// Capture 20260720-190432: after Nano Sensor combine → Tradeskilling (2/4).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTradeskillBasicBrainTip(ICharacter source)
        {
            return TrySendTradeskillStepTip(
                source,
                CreateTradeskillBasicBrainPreviewMessage,
                "Tradeskill Basic Robot Brain tip. mission=Mission:555B4368 source=20260720-190432");
        }

        /// <summary>
        /// Capture 20260720-190432: after Basic Robot Brain combine → Tradeskilling (3/4).
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTradeskillPersonalizedBrainTip(ICharacter source)
        {
            return TrySendTradeskillStepTip(
                source,
                CreateTradeskillPersonalizedBrainPreviewMessage,
                "Tradeskill Personalized Brain tip. mission=Mission:555B4369 source=20260720-190432");
        }

        /// <summary>
        /// Capture 20260720-190432: after Personalized combine → Tradeskilling (4/4) show to Alex.
        /// </summary>
        public static RexQuestPreviewEmissionResult TrySendTradeskillShowBrainTip(ICharacter source)
        {
            return TrySendTradeskillStepTip(
                source,
                CreateTradeskillShowBrainPreviewMessage,
                "Tradeskill Show Brain tip. mission=Mission:555B436A source=20260720-190432");
        }

        private static RexQuestPreviewEmissionResult TrySendTradeskillStepTip(
            ICharacter source,
            Func<Identity, QuestFullUpdateMessage> createMessage,
            string successDetail)
        {
            if (source?.Controller?.Client == null)
            {
                return RexQuestPreviewEmissionResult.Failed("Tradeskill tip skipped: client missing.");
            }

            try
            {
                QuestFullUpdateMessage message = createMessage(source.Identity);
                ApplyLiveTipExpiry(message, source);
                source.Controller.Client.SendCompressed(message);

                // Capture 20260720-190432: Tip 4/4 stacks beside Talk to Stan — refresh main tip.
                if (IsTalkToStanMissionOpen(source))
                {
                    TryRefreshTalkToStanTip(source);
                }

                return RexQuestPreviewEmissionResult.Sent(successDetail);
            }
            catch (Exception e)
            {
                return RexQuestPreviewEmissionResult.Failed("Tradeskill tip failed: " + e.Message);
            }
        }

        private static bool IsTalkToStanMissionOpen(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:555B4366");
            return mission != null
                   && (mission.State == ZoneEngine.Core.Missions.MissionLifecycleState.Active
                       || mission.State == ZoneEngine.Core.Missions.MissionLifecycleState.Offered);
        }

        internal static void SendTipAction59AndDelete(ICharacter source, int missionInstance)
        {
            if (source?.Controller?.Client == null || missionInstance == 0)
            {
                return;
            }

            // Typed CharacterAction Action=59 is Int32-shaped and leaves Remain 00:00 stuck tips.
            // Capture wire: Int16 Action59 + Quest/Delete (same Thrak / Bill tip shells).
            FlintKneecappingTipWire.TryDeleteTip(source, missionInstance);
        }

        private static CharacterActionMessage CreateTipAction59Message(Identity characterIdentity, int missionInstance)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, missionInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = missionInstance,
                       Unknown2 = 0
                   };
        }

        private static QuestMessage CreateTipQuestDeleteMessage(Identity characterIdentity, int missionInstance)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, missionInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestFullUpdateMessage CreateB18CPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18CInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18CShortInfo,
                                   LongInfo = B18CLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1112496696,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 11330,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 20,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = Identity.None,
                                               UnknownId3 = IdentityFromRaw(
                                                   B18CUnknownActionIdType,
                                                   B18CUnknownActionIdInstance),
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(
                                                   B18CUnknownActionId7Type,
                                                   B18CUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3614, 0, 779)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 72407246 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 5,
                                   Unknown24 = 105102,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateB18DPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18DInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18DShortInfo,
                                   LongInfo = B18DLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1145587534,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18DUnknownActionId2Type,
                                                   B18DUnknownActionId2Instance),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(
                                                   B18DUnknownActionId7Type,
                                                   B18DUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3621, 0, 782)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360441 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105103,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateB18EPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18EInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18EShortInfo,
                                   LongInfo = B18ELongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1040,
                                   Unknown7 = 0,
                                   Unknown8 = 1281,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 861490233,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = "5UFZ",
                                   Unknown14 = 1,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 23,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18EUnknownActionId2Type,
                                                   B18EUnknownActionId2Instance),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(
                                                   B18EUnknownActionId7Type,
                                                   B18EUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3621, 0, 790)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360442 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105104,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateB18FPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B18FInstance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B18FShortInfo,
                                   LongInfo = B18FLongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1212436295,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B18FUnknownActionId2Type,
                                                   B18FUnknownActionId2Instance),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(
                                                   B18FUnknownActionId7Type,
                                                   B18FUnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3638, 0, 830)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360443 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateB194PreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B194Instance);
            Identity rexIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B194ShortInfo,
                                   LongInfo = B194LongInfo,
                                   UnknownId1 = rexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1229076054,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(
                                                   B194UnknownActionId2Type,
                                                   B194UnknownActionId2Instance),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(
                                                   B194UnknownActionId7Type,
                                                   B194UnknownActionId7Instance),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3604, 0, 833)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360448 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 104939,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateB18FAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B18FInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B18FInstance,
                       Unknown2 = 0
                   };
        }

        internal static CharacterActionMessage CreateB18CAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B18CInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B18CInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB18CQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18CInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestMessage CreateB18DQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18DInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestMessage CreateB18EQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18EInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestMessage CreateB18FQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B18FInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static CharacterActionMessage CreateB194Action59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B194Instance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B194Instance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB194QuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B194Instance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static CharacterActionMessage CreateB196Action59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B196Instance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B196Instance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB196QuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B196Instance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestFullUpdateMessage CreateB196PreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B196Instance);
            Identity marcusIdentity = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = MarcusStoneInstance
                                      };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B196ShortInfo,
                                   LongInfo = B196LongInfo,
                                   UnknownId1 = marcusIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1080,
                                   Unknown7 = 0,
                                   Unknown8 = 2076,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1229076054,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 158429,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360448 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 104939,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        private const long TipClientClockBaseSeconds = 1_201_445_827L;

        private const int TipMissionDurationSeconds = 48 * 60 * 60;

        /// <summary>
        /// Remain = expiry - clientClock. Client clock = GameTime anchor + seconds since sync
        /// (same math as MissionAcceptService / PerkResetMissionSender).
        /// </summary>
        private static int ComputeLiveTipExpiry(ICharacter source)
        {
            double secondsSinceSync = 0;
            ZoneClient client = source?.Controller?.Client as ZoneClient;
            if (client != null)
            {
                secondsSinceSync = (DateTime.UtcNow - client.LastGameTimeSyncUtc).TotalSeconds;
                if (secondsSinceSync < 0)
                {
                    secondsSinceSync = 0;
                }
            }

            return unchecked(
                (int)(TipClientClockBaseSeconds + (long)secondsSinceSync + TipMissionDurationSeconds));
        }

        private static void ApplyLiveTipExpiry(QuestFullUpdateMessage message, ICharacter source)
        {
            if (message?.Quests == null || message.Quests.Length == 0)
            {
                return;
            }

            message.Quests[0].Unknown11 = ComputeLiveTipExpiry(source);
        }

        internal static QuestFullUpdateMessage CreateFlintPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, FlintInstance);
            Identity marcusIdentity = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = MarcusStoneInstance
                                      };

            // AbsoluteTime expiry must be clientClockNow + duration or Remain can hide the tip.
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = FlintShortInfo,
                                   LongInfo = FlintLongInfo,
                                   UnknownId1 = marcusIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(0x000111D3, 0x00019A52),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(0x0000D2F1, 0x4D167F3C),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3598, 0, 863)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateFindBioPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, FindBioInstance);
            Identity flintIdentity = new Identity
                                     {
                                         Type = IdentityType.CanbeAffected,
                                         Instance = unchecked((int)0x78E0FC64)
                                     };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = FindBioShortInfo,
                                   LongInfo = FindBioLongInfo,
                                   UnknownId1 = flintIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 11330,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 20,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = Identity.None,
                                               UnknownId3 = IdentityFromRaw(0x00001999, 0x4D424957),
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(0x0000D2FC, unchecked((int)0x1C69BEF2)),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3598, 0, 863)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateDeliverBioPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, DeliverBioInstance);
            Identity alexIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC61)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = DeliverBioShortInfo,
                                   LongInfo = DeliverBioLongInfo,
                                   UnknownId1 = alexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1120,
                                   Unknown7 = 0,
                                   Unknown8 = 2229,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 158429,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 6,
                                               Action = IdentityFromRaw(0x000111D3, 0x4249414E),
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(0x000111D3, 0x414C4749),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(0x0000D2F1, unchecked((int)0x4D55E6F5)),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3521, 0, 857)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateFindBioAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, FindBioInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = FindBioInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateFindBioQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Mission = IdentityFromRaw(MissionIdentityType, FindBioInstance)
                   };
        }

        internal static CharacterActionMessage CreateDeliverBioAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, DeliverBioInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = DeliverBioInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateDeliverBioQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Mission = IdentityFromRaw(MissionIdentityType, DeliverBioInstance)
                   };
        }

        internal static QuestFullUpdateMessage CreateSurveillanceUplinkPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, SurveillanceUplinkInstance);
            Identity alexIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC61)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = SurveillanceUplinkShortInfo,
                                   LongInfo = SurveillanceUplinkLongInfo,
                                   UnknownId1 = alexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions =
                                       new[]
                                       {
                                           new QuestActionInfo
                                           {
                                               Version = 24,
                                               Action = Identity.None,
                                               UnknownId1 = Identity.None,
                                               UnknownId2 = IdentityFromRaw(0x000111D3, 0x000199D3),
                                               UnknownId3 = Identity.None,
                                               UnknownId4 = Identity.None,
                                               Unknown1 = 0,
                                               Unknown2 = 0,
                                               Unknown3 = 0,
                                               Unknown4 = 0,
                                               UnknownId5 = Identity.None,
                                               Unknown5 = 0,
                                               Unknown6 = 0,
                                               Unknown7 = 0,
                                               Unknown8 = 0,
                                               UnknownId6 = Identity.None,
                                               UnknownHash1 = string.Empty,
                                               Unknown9 = 0,
                                               UnknownId7 = IdentityFromRaw(0x0000D2F1, unchecked((int)0x4D55E7EA)),
                                               PlayfieldId = new Identity
                                                             {
                                                                 Type = IdentityType.Playfield2,
                                                                 Instance = 6553
                                                             },
                                               Unknown10 = 100000,
                                               Unknown11 = 100000,
                                               Position = new Vector3(3521, 0, 857)
                                           }
                                       },
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateSurveillanceUplinkAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, SurveillanceUplinkInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = SurveillanceUplinkInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateSurveillanceUplinkQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Mission = IdentityFromRaw(MissionIdentityType, SurveillanceUplinkInstance)
                   };
        }

        internal static QuestFullUpdateMessage CreatePlantBugPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, PlantBugInstance);
            Identity droidIdentity = new Identity
                                     {
                                         Type = IdentityType.CanbeAffected,
                                         Instance = unchecked((int)0x78E0FC8A)
                                     };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = PlantBugShortInfo,
                                   LongInfo = PlantBugLongInfo,
                                   UnknownId1 = droidIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 11342,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreatePlantBugAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, PlantBugInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = PlantBugInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreatePlantBugQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Mission = IdentityFromRaw(MissionIdentityType, PlantBugInstance)
                   };
        }

        internal static QuestFullUpdateMessage CreateDeliverHc12BillPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, DeliverHc12BillInstance);
            Identity billIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC66)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = DeliverHc12BillShortInfo,
                                   LongInfo = DeliverHc12BillLongInfo,
                                   UnknownId1 = billIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 158429,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateDeliverHc12BillAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, DeliverHc12BillInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = DeliverHc12BillInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateDeliverHc12BillQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Mission = IdentityFromRaw(MissionIdentityType, DeliverHc12BillInstance)
                   };
        }

        internal static QuestFullUpdateMessage CreateKneecappingPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, KneecappingInstance);
            Identity alfonzIdentity = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = unchecked((int)0x78E0FC63)
                                      };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = KneecappingShortInfo,
                                   LongInfo = KneecappingLongInfo,
                                   UnknownId1 = alfonzIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 11330,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateReportToAlexPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, ReportToAlexInstance);
            Identity alexIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC61)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = ReportToAlexShortInfo,
                                   LongInfo = ReportToAlexLongInfo,
                                   UnknownId1 = alexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateTalkToStanPreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, TalkToStanInstance);
            // Capture 20260720-171317 Talk-to-Stan QFU UnknownId1 = SimpleChar:78E0FC63.
            Identity stanIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC63)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = TalkToStanShortInfo,
                                   LongInfo = TalkToStanLongInfo,
                                   UnknownId1 = stanIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateBuyLockpickPreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                BuyLockpickInstance,
                BuyLockpickShortInfo,
                BuyLockpickLongInfo);
        }

        internal static QuestFullUpdateMessage CreateStrongboxContentsPreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                StrongboxContentsInstance,
                StrongboxContentsShortInfo,
                StrongboxContentsLongInfo);
        }

        internal static QuestFullUpdateMessage CreateDeliverAntonioFactoryPreviewMessage(Identity characterIdentity)
        {
            // Capture 20260801-102913 QFU Mission:5574F01A:
            // icon=158429, Unknown6=1240 credits, Unknown8=2596 XP, MissionItemData=296572@1.
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                DeliverAntonioFactoryInstance,
                DeliverAntonioFactoryShortInfo,
                DeliverAntonioFactoryLongInfo,
                158429,
                unchecked((int)0x78E0FC63),
                unknown6: 1240,
                unknown8: 2596,
                missionItemData:
                    new[]
                    {
                        new MissionItemReward
                        {
                            LowId = 296572,
                            HighId = 296572,
                            Ql = 1,
                            Unknown = 0
                        }
                    });
        }

        internal static QuestFullUpdateMessage CreateTalkToSarahGreenePreviewMessage(Identity characterIdentity)
        {
            return CreateStanChainTipPreviewMessage(
                characterIdentity,
                TalkToSarahGreeneInstance,
                TalkToSarahGreeneShortInfo,
                TalkToSarahGreeneLongInfo);
        }

        internal static QuestFullUpdateMessage CreateFindTheThiefPreviewMessage(Identity characterIdentity)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                FindTheThiefInstance,
                FindTheThiefShortInfo,
                FindTheThiefLongInfo,
                244818,
                unchecked((int)0x78E0FC69));
        }

        internal static QuestFullUpdateMessage CreateDeliverDnaLockedArmorPreviewMessage(Identity characterIdentity)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                DeliverDnaLockedArmorInstance,
                DeliverDnaLockedArmorShortInfo,
                DeliverDnaLockedArmorLongInfo,
                158429,
                unchecked((int)0x78E0FC69));
        }

        internal static QuestFullUpdateMessage CreateSpeakToVernonGodfrayPreviewMessage(Identity characterIdentity)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                SpeakToVernonGodfrayInstance,
                SpeakToVernonGodfrayShortInfo,
                SpeakToVernonGodfrayLongInfo,
                244818,
                unchecked((int)0x78E0FC68));
        }

        internal static QuestFullUpdateMessage CreateHackingSkillsPreviewMessage(Identity characterIdentity)
        {
            // Capture 20260721-Vernon-Godfray #261: tip NPC CanbeAffected:78E0FC63 (not Vernon).
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                HackingSkillsInstance,
                HackingSkillsShortInfo,
                HackingSkillsLongInfo,
                11340,
                unchecked((int)0x78E0FC63));
        }

        internal static QuestFullUpdateMessage CreateGiveHackedTechnicalLibraryPreviewMessage(
            Identity characterIdentity)
        {
            // Capture 20260801-104528: icon=158429, tip NPC 78E0FC63,
            // Unknown6=1320 credits, Unknown8=2596 XP.
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                GiveHackedTechnicalLibraryInstance,
                GiveHackedTechnicalLibraryShortInfo,
                GiveHackedTechnicalLibraryLongInfo,
                158429,
                unchecked((int)0x78E0FC63),
                unknown6: 1320,
                unknown8: 2596);
        }

        internal static QuestFullUpdateMessage CreateCargoLiftingPreviewMessage(Identity characterIdentity)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                CargoLiftingInstance,
                "Cargo Lifting",
                "Cargo Lifting<BR><BR>Vernon mentioned that he would like to get his hands on the data from one of the "
                + "Shipping Manifest Terminals located in the industrial district of the shuttleport.<BR><BR>"
                + "<font color=\"#FF0000\">Mission Objective:<BR>"
                + "Open a dialog with the Shipping Manifest Terminal and apply the "
                + "<a href='itemref://87810/87810/1'>Hacker Tool</a> if access is denied.</font>",
                244818,
                unchecked((int)0x78E0FC63));
        }

        internal static QuestFullUpdateMessage CreateReturnToVernonGodfrayPreviewMessage(
            Identity characterIdentity)
        {
            // Capture 20260801-105429: tip short=Return to Vernon Godfray, icon=158429.
            // Completion reward (chip turn-in) is 2596 XP / 1360 credits.
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                ReturnToVernonGodfrayInstance,
                "Return to Vernon Godfray",
                "Return to Vernon Godfray<BR><BR>After finishing the hack job, return to Vernon and he might help you "
                + "with your ID problem.<BR><BR>"
                + "<font color=\"#FF0000\">Mission Objective:<BR>"
                + "Talk to Vernon Godfray and give him the "
                + "<a href='itemref://296572/296572/1'>Unprogrammed Identification Chip</a>.</font>",
                158429,
                unchecked((int)0x78E0FC63),
                unknown6: 1360,
                unknown8: 2596);
        }

        internal static QuestFullUpdateMessage CreateTalkToDoctorMasonPreviewMessage(
            Identity characterIdentity)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                TalkToDoctorMasonInstance,
                "Talk to Doctor Mason",
                "Talk to Doctor Mason<BR><BR><font color=\"#63ad63\">Identity Crisis:</font><BR>"
                + "In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. "
                + "Your mission is to create a fake ID Card to you can leave this place..<BR><BR>"
                + "After helping Vernon, he gave you a Blank ICC ID Chip. He said that Dr Mason would be able "
                + "to help you out further to imprint your DNA in to the chip.<BR><BR>"
                + "<font color=\"#FF0000\">Mission Objective:<BR>Talk to Doctor Mason.</font>",
                244818,
                unchecked((int)0x78E0FC68));
        }

        internal static QuestFullUpdateMessage CreateBuyNanoProgramsPreviewMessage(Identity characterIdentity)
        {
            // Capture 20260801-102913 QFU Mission:5574F01C tip preview:
            // Unknown6=1200 credits, Unknown8=2596 XP, MissionItemData=223373 QL25.
            // Completion grant XP/credits remain capture-backed separately (20260730-212921).
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, BuyNanoProgramsInstance);
            Identity tipNpcIdentity = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = unchecked((int)0x78E0FC65)
                                       };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = BuyNanoProgramsShortInfo,
                                   LongInfo = BuyNanoProgramsLongInfo,
                                   UnknownId1 = tipNpcIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1200,
                                   Unknown7 = 0,
                                   Unknown8 = 2596,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData =
                                       new[]
                                       {
                                           new MissionItemReward
                                           {
                                               LowId = CapturedAreteMarcoSpidaVendorContentProvider
                                                   .BuyNanoTipRewardItemId,
                                               HighId = CapturedAreteMarcoSpidaVendorContentProvider
                                                   .BuyNanoTipRewardItemId,
                                               Ql = CapturedAreteMarcoSpidaVendorContentProvider
                                                   .BuyNanoTipRewardQuality,
                                               Unknown = 0
                                           }
                                       },
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        private static QuestFullUpdateMessage CreateStanChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                missionInstance,
                shortInfo,
                longInfo,
                244818,
                unchecked((int)0x78E0FC63));
        }

        private static QuestFullUpdateMessage CreateSarahChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo,
            int missionIconId,
            int tipNpcInstance)
        {
            return CreateSarahChainTipPreviewMessage(
                characterIdentity,
                missionInstance,
                shortInfo,
                longInfo,
                missionIconId,
                tipNpcInstance,
                unknown6: 0,
                unknown8: 0);
        }

        private static QuestFullUpdateMessage CreateSarahChainTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo,
            int missionIconId,
            int tipNpcInstance,
            int unknown6,
            int unknown8,
            MissionItemReward[] missionItemData = null)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, missionInstance);
            Identity tipNpcIdentity = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = tipNpcInstance
                                       };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);
            MissionItemReward[] rewards = missionItemData ?? new MissionItemReward[0];

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = shortInfo,
                                   LongInfo = longInfo,
                                   UnknownId1 = tipNpcIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = unknown6,
                                   Unknown7 = 0,
                                   Unknown8 = unknown8,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = rewards,
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = missionIconId,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static QuestFullUpdateMessage CreateTradeskillNanoSensorPreviewMessage(Identity characterIdentity)
        {
            return CreateTradeskillTipPreviewMessage(
                characterIdentity,
                TradeskillNanoSensorInstance,
                TradeskillNanoSensorShortInfo,
                TradeskillNanoSensorLongInfo,
                11340);
        }

        internal static QuestFullUpdateMessage CreateTradeskillBasicBrainPreviewMessage(Identity characterIdentity)
        {
            return CreateTradeskillTipPreviewMessage(
                characterIdentity,
                TradeskillBasicBrainInstance,
                TradeskillBasicBrainShortInfo,
                TradeskillBasicBrainLongInfo,
                11340);
        }

        internal static QuestFullUpdateMessage CreateTradeskillPersonalizedBrainPreviewMessage(
            Identity characterIdentity)
        {
            return CreateTradeskillTipPreviewMessage(
                characterIdentity,
                TradeskillPersonalizedBrainInstance,
                TradeskillPersonalizedBrainShortInfo,
                TradeskillPersonalizedBrainLongInfo,
                11340);
        }

        internal static QuestFullUpdateMessage CreateTradeskillShowBrainPreviewMessage(Identity characterIdentity)
        {
            return CreateTradeskillTipPreviewMessage(
                characterIdentity,
                TradeskillShowBrainInstance,
                TradeskillShowBrainShortInfo,
                TradeskillShowBrainLongInfo,
                158429);
        }

        private static QuestFullUpdateMessage CreateTradeskillTipPreviewMessage(
            Identity characterIdentity,
            int missionInstance,
            string shortInfo,
            string longInfo,
            int missionIconId)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, missionInstance);
            Identity alexIdentity = new Identity
                                    {
                                        Type = IdentityType.CanbeAffected,
                                        Instance = unchecked((int)0x78E0FC61)
                                    };
            int expiry = (int)(TipClientClockBaseSeconds + TipMissionDurationSeconds);

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = shortInfo,
                                   LongInfo = longInfo,
                                   UnknownId1 = alexIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 0,
                                   Unknown7 = 0,
                                   Unknown8 = 0,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = expiry,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = missionIconId,
                                   Unknown20 = TipMissionDurationSeconds,
                                   Unknown21 = TipMissionDurationSeconds,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 105040,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 0,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateFlintAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, FlintInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = FlintInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateFlintQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, FlintInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static CharacterActionMessage CreateB199Action59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B199Instance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B199Instance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB199QuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B199Instance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestFullUpdateMessage CreateB199PreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B199Instance);
            Identity marcusIdentity = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = MarcusStoneInstance
                                      };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B199ShortInfo,
                                   LongInfo = B199LongInfo,
                                   UnknownId1 = marcusIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1040,
                                   Unknown7 = 0,
                                   Unknown8 = 2076,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1229076059,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 244818,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 104939,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        internal static CharacterActionMessage CreateB19AAction59Message(Identity characterIdentity)
        {
            return new CharacterActionMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = (CharacterActionType)59,
                       Unknown1 = 0,
                       Target = IdentityFromRaw(MissionIdentityType, B19AInstance),
                       Parameter1 = MissionIdentityType,
                       Parameter2 = B19AInstance,
                       Unknown2 = 0
                   };
        }

        internal static QuestMessage CreateB19AQuestDeleteMessage(Identity characterIdentity)
        {
            return new QuestMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 0,
                       Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete,
                       Unknown1 = 0,
                       Mission = IdentityFromRaw(MissionIdentityType, B19AInstance),
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        internal static QuestFullUpdateMessage CreateB19APreviewMessage(Identity characterIdentity)
        {
            Identity missionIdentity = IdentityFromRaw(MissionIdentityType, B19AInstance);
            Identity marcusIdentity = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = MarcusStoneInstance
                                      };

            return new QuestFullUpdateMessage
                   {
                       Identity = characterIdentity,
                       Unknown = 1,
                       Quests =
                           new[]
                           {
                               new Quest
                               {
                                   QuestId = missionIdentity,
                                   Unknown1 = 15,
                                   Unknown2 = 0,
                                   Unknown3 = 0,
                                   Unknown4 = 2,
                                   ShortInfo = B19AShortInfo,
                                   LongInfo = B19ALongInfo,
                                   UnknownId1 = marcusIdentity,
                                   Unknown5 = 6,
                                   Unknown6 = 1040,
                                   Unknown7 = 0,
                                   Unknown8 = 2076,
                                   Unknown9 = 1009,
                                   Unknown10 = 1009,
                                   MissionItemData = new MissionItemReward[0],
                                   Unknown11 = 1229076060,
                                   Unknown12 = 0,
                                   Unknown13 = 0,
                                   UnknownHash1 = string.Empty,
                                   Unknown14 = 0,
                                   Unknown15 = 0,
                                   Unknown16 = 0,
                                   Unknown17 = 0,
                                   Unknown18 = 0,
                                   UnknownId2 = characterIdentity,
                                   MissionIconId = 158429,
                                   Unknown20 = 0,
                                   Unknown21 = 0,
                                   QuestActions = new QuestActionInfo[0],
                                   PlayerIds = new[] { characterIdentity },
                                   UnknownArray1 = new[] { 85360450 },
                                   UnknownArray2 = new int[0],
                                   CharacterInfos = new CharacterInfo[0],
                                   Unknown22 = 6,
                                   PlayerIds2 = new[] { characterIdentity },
                                   Unknown23 = 0,
                                   Unknown24 = 104939,
                                   UnknownId3 = Identity.None,
                                   Unknown25 = 0,
                                   Unknown26 = 0,
                                   QuestIdentities = new QuestIdentity[0],
                                   Unknown27 = 7,
                                   FactionInfos = new Identity[0],
                                   Unknown28 = 1
                               }
                           }
                   };
        }

        private static Identity IdentityFromRaw(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }
    }
}
