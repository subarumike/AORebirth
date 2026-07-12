namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Playfields;

    #endregion

    internal sealed class CapturedSubwayContentProvider
    {
        public const int SubwayPlayfieldInstance = 127;

        private static readonly CapturedSubwaySpawnDefinition[] SpawnDefinitions =
        {
            CapturedSurveySpawn(DiscardedPet(0x794DF1E5, 5, 115, 184.843964f, 107.61483f, 240.569778f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x794E83C1, 7, 160, 195.351364f, 107.611687f, 290.974426f, 94, 32)),
            CapturedSurveySpawn(DiscardedPet(0x79528F6A, 9, 205, 171.851227f, 107.611687f, 304.098846f, 95, 40)),
            CapturedSurveySpawn(DiscardedPet(0x79528FDA, 8, 183, 188.99f, 107.611687f, 309.9072f, 94, 36)),
            CapturedSurveySpawn(DiscardedPet(0x795317D6, 5, 115, 178.220322f, 107.61483f, 247.394058f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x7953AA04, 10, 227, 346.527771f, 102.814827f, 161.956f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AA1B, 10, 227, 346.468719f, 102.814827f, 165.56929f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AA82, 10, 227, 349.01f, 102.814827f, 168.297592f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AC01, 6, 138, 149.800781f, 107.61483f, 251.29686f, 93, 28)),
            CapturedSurveySpawn(DiscardedPet(0x7953AD3C, 8, 183, 149.255112f, 107.61483f, 199.861237f, 94, 36)),
            CapturedSurveySpawn(DiscardedPet(0x7953AD5F, 10, 227, 200.48233f, 107.6164f, 161.475555f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AD6D, 10, 227, 267.8472f, 102.8164f, 164.076736f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AD6F, 10, 227, 268.905518f, 102.8164f, 166.401535f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AD74, 10, 227, 277.9136f, 102.8164f, 165.517181f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7953AF53, 6, 138, 158.790054f, 107.61483f, 235.160751f, 93, 28)),
            CapturedSurveySpawn(DiscardedPet(0x7953AF66, 5, 115, 158.817078f, 107.61483f, 246.372574f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x7953AF74, 5, 115, 185.507675f, 107.61483f, 241.627518f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x7953AF99, 6, 138, 181.53067f, 107.61483f, 249.831055f, 93, 28)),
            CapturedSurveySpawn(DiscardedPet(0x79557C09, 9, 205, 183.01f, 107.611687f, 308.6345f, 95, 40)),
            CapturedSurveySpawn(DiscardedPet(0x79557C26, 7, 160, 192.565231f, 107.611687f, 289.6804f, 94, 32)),
            CapturedSurveySpawn(DiscardedPet(0x79557C31, 5, 115, 174.194214f, 107.61483f, 242.166443f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x79557C8B, 10, 227, 286.2218f, 107.611687f, 285.7219f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x79557CA7, 8, 183, 161.97876f, 107.613258f, 301.466125f, 94, 36)),
            CapturedSurveySpawn(DiscardedPet(0x79557CAB, 10, 227, 281.3582f, 107.611687f, 284.467255f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x79557CAD, 10, 227, 288.673035f, 107.611687f, 276.390656f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7957E411, 10, 227, 201.890152f, 107.6164f, 164.699f, 95, 44)),
            CapturedSurveySpawn(DiscardedPet(0x7957E4A5, 6, 138, 144.8586f, 107.61483f, 251.138519f, 93, 28)),
            CapturedSurveySpawn(DiscardedPet(0x7957E4B1, 5, 115, 151.498718f, 107.61483f, 237.92157f, 93, 24)),
            CapturedSurveySpawn(DiscardedPet(0x7957E4BC, 8, 183, 156.301163f, 107.61483f, 233.5397f, 94, 36)),
            CapturedSurveySpawn(DisobedientBot(0x7953AA1E, 10, 227, 333.486145f, 102.414825f, 161.493332f, 95, 34)),
            CapturedSurveySpawn(DisobedientBot(0x7953AA81, 10, 227, 325.24176f, 102.814827f, 163.737274f, 95, 34)),
            CapturedSurveySpawn(DisobedientBot(0x7953AA8F, 10, 227, 337.210541f, 102.414825f, 160.9172f, 95, 34)),
            CapturedSurveySpawn(DisobedientBot(0x7953AB08, 10, 227, 334.099823f, 102.414825f, 166.305527f, 95, 34)),
            CapturedSurveySpawn(DisobedientBot(0x7953AD4B, 9, 205, 208.746964f, 107.6164f, 165.358978f, 95, 31)),
            CapturedSurveySpawn(DisobedientBot(0x7953AD61, 10, 227, 214.0725f, 107.6164f, 164.6418f, 95, 34)),
            CapturedSurveySpawn(DisobedientBot(0x7953AD69, 9, 205, 216.01f, 107.6164f, 162.708969f, 95, 31)),
            CapturedSurveySpawn(DisobedientBot(0x7953AF6F, 9, 205, 114.499268f, 107.61483f, 231.651047f, 95, 31)),
            CapturedSurveySpawn(DisobedientBot(0x7953AF98, 7, 160, 173.610947f, 107.61483f, 232.288391f, 94, 25)),
            CapturedSurveySpawn(DisobedientBot(0x7953AFA3, 6, 138, 179.514313f, 107.61483f, 232.11319f, 93, 22)),
            CapturedSurveySpawn(DisobedientBot(0x79557C66, 7, 160, 151.409119f, 107.61483f, 271.044f, 94, 25)),
            CapturedSurveySpawn(DisobedientBot(0x7957E40A, 10, 227, 211.504623f, 107.6164f, 166.472961f, 95, 34)),
            CapturedSurveySpawn(FilthFlea(0x795313FC, 5, 115, 147.950089f, 107.61483f, 229.4221f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953174B, 6, 138, 120.682472f, 107.61483f, 241.098831f, 24)),
            CapturedSurveySpawn(FilthFlea(0x79531752, 5, 115, 120.437515f, 107.61483f, 238.616013f, 21)),
            CapturedSurveySpawn(FilthFlea(0x79531754, 5, 115, 120.613022f, 107.61483f, 237.217636f, 21)),
            CapturedSurveySpawn(FilthFlea(0x795317F5, 7, 160, 158.915558f, 107.6164f, 162.843613f, 27)),
            CapturedSurveySpawn(FilthFlea(0x7953A9C2, 15, 393, 283.226f, 100.8164f, 212.817139f, 57)),
            CapturedSurveySpawn(FilthFlea(0x7953A9C6, 14, 360, 278.982574f, 100.8164f, 212.5821f, 53)),
            CapturedSurveySpawn(FilthFlea(0x7953AA0B, 15, 393, 316.3524f, 102.8164f, 218.6188f, 57)),
            CapturedSurveySpawn(FilthFlea(0x7953AA0C, 13, 327, 315.676147f, 102.8164f, 220.470123f, 49)),
            CapturedSurveySpawn(FilthFlea(0x7953AD2B, 6, 138, 152.821289f, 107.61483f, 203.99f, 24)),
            CapturedSurveySpawn(FilthFlea(0x7953AD2C, 5, 115, 148.600433f, 107.61483f, 224.30545f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AD2F, 8, 183, 145.374847f, 107.61483f, 199.427826f, 31)),
            CapturedSurveySpawn(FilthFlea(0x7953AD30, 7, 160, 149.195938f, 107.61483f, 213.897476f, 27)),
            CapturedSurveySpawn(FilthFlea(0x7953AD36, 8, 183, 146.856918f, 107.61483f, 201.203613f, 31)),
            CapturedSurveySpawn(FilthFlea(0x7953AD3E, 5, 115, 148.99f, 107.61483f, 196.137863f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AD70, 11, 261, 224.797775f, 107.6164f, 165.968567f, 41)),
            CapturedSurveySpawn(FilthFlea(0x7953AD71, 11, 261, 226.115967f, 107.6164f, 162.99f, 41)),
            CapturedSurveySpawn(FilthFlea(0x7953AD73, 10, 227, 224.226089f, 107.6164f, 163.8984f, 37)),
            CapturedSurveySpawn(FilthFlea(0x7953AD75, 11, 261, 231.024567f, 107.6164f, 163.936813f, 41)),
            CapturedSurveySpawn(FilthFlea(0x7953AD78, 10, 227, 248.081528f, 106.405754f, 164.442352f, 37)),
            CapturedSurveySpawn(FilthFlea(0x7953AEEA, 4, 93, 88.50346f, 115.615f, 300.2512f, 17)),
            CapturedSurveySpawn(FilthFlea(0x7953AEEE, 5, 115, 86.2133f, 111.615f, 270.391357f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AEFC, 4, 93, 100.99f, 107.61483f, 238.867691f, 17)),
            CapturedSurveySpawn(FilthFlea(0x7953AF04, 5, 115, 97.40637f, 107.61483f, 257.277435f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AF10, 5, 115, 86.80035f, 107.61483f, 250.369431f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AF18, 4, 93, 91.5352859f, 107.61483f, 248.860519f, 17)),
            CapturedSurveySpawn(FilthFlea(0x7953AF22, 6, 138, 101.782433f, 107.61483f, 236.890366f, 24)),
            CapturedSurveySpawn(FilthFlea(0x7953AF4A, 6, 138, 85.88043f, 107.61483f, 258.95575f, 24)),
            CapturedSurveySpawn(FilthFlea(0x7953AF57, 5, 115, 92.79191f, 107.61483f, 257.037323f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AFAA, 7, 160, 179.492691f, 107.61483f, 252.259949f, 27)),
            CapturedSurveySpawn(FilthFlea(0x7953AFAE, 5, 115, 176.862076f, 107.61483f, 249.52832f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AFC4, 7, 160, 182.377f, 107.61483f, 222.0669f, 27)),
            CapturedSurveySpawn(FilthFlea(0x7953AFC6, 5, 115, 190.181137f, 107.61483f, 224.268433f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953AFCC, 5, 115, 177.573273f, 107.61483f, 224.148026f, 21)),
            CapturedSurveySpawn(FilthFlea(0x7953A9E1, 13, 327, 330.578979f, 102.865f, 150.1263f, 49)),
            CapturedSurveySpawn(FilthFlea(0x7953A9E7, 11, 261, 328.6433f, 102.965f, 143.931885f, 41)),
            CapturedSurveySpawn(FilthFlea(0x7953A9EA, 11, 261, 325.99f, 102.8164f, 148.119644f, 41)),
            CapturedSurveySpawn(FilthFlea(0x7953A9FC, 11, 261, 327.13147f, 102.865f, 142.704468f, 41)),
            CapturedSurveySpawn(FilthFlea(0x79513A87, 12, 294, 351.975525f, 102.814827f, 141.408966f, 45)),
            CapturedSurveySpawn(FilthFlea(0x79513A8F, 12, 294, 351.4564f, 102.814827f, 148.9678f, 45)),
            CapturedSurveySpawn(FilthFlea(0x79513AAF, 13, 327, 348.571533f, 102.814827f, 138.478455f, 49)),
            CapturedSurveySpawn(FilthFlea(0x79513AC2, 13, 327, 350.350433f, 102.814827f, 144.813583f, 49)),
            CapturedSurveySpawn(FilthFlea(0x79545223, 13, 327, 325.3251f, 102.814827f, 183.530884f, 49)),
            CapturedSurveySpawn(FilthFlea(0x79545227, 11, 261, 324.01f, 102.814827f, 178.83403f, 41)),
            CapturedSurveySpawn(FilthFlea(0x79531120, 21, 592, 187.0416f, 73.3830261f, 88.03114f, 80)),
            CapturedSurveySpawn(FilthFlea(0x79531122, 21, 592, 187.2152f, 73.24139f, 109.886124f, 80)),
            CapturedSurveySpawn(FilthFlea(0x79545191, 19, 526, 160.99f, 81.21325f, 70.15537f, 72)),
            CapturedSurveySpawn(FilthFlea(0x7953AF6D, 19, 526, 121.509415f, 77.01481f, 126.348518f, 72)),
            CapturedSurveySpawn(FilthFlea(0x7953AF71, 21, 592, 125.111382f, 77.01481f, 128.979477f, 80)),
            CapturedSurveySpawn(FilthFlea(0x7953AF76, 20, 559, 123.632492f, 77.01481f, 126.585861f, 76)),
            CapturedSurveySpawn(FilthFlea(0x795451A4, 21, 592, 123.01f, 77.01481f, 127.967026f, 80)),
            CapturedSurveySpawn(Mugger(0x7953AA11, 8, 146, 291.3161f, 102.8164f, 250.824387f, 94, 30)),
            CapturedSurveySpawn(Mugger(0x7953AD6B, 10, 182, 264.127747f, 103.19651f, 163.2112f, 95, 36)),
            CapturedSurveySpawn(Mugger(0x795450D4, 5, 92, 167.8636f, 109.104828f, 255.636658f, 93, 20)),
            CapturedSurveySpawn(Mugger(0x795451FE, 10, 182, 228.215637f, 107.6164f, 163.445328f, 95, 36)),
            CapturedSurveySpawn(Mugger(0x79557F14, 10, 182, 292.5373f, 107.611687f, 298.02475f, 95, 36)),
            CapturedSurveySpawn(Mugger(0x7957E5C6, 9, 164, 152.437408f, 107.613258f, 297.01f, 95, 33)),
            CapturedSurveySpawn(Mugger(0x7957E5C7, 8, 146, 153.4413f, 107.613258f, 297.974335f, 94, 30)),
            CapturedSurveySpawn(Mugger(0x7957E5C8, 8, 146, 145.386154f, 107.613258f, 289.6806f, 94, 30)),
            CapturedSurveySpawn(Mugger(0x7957E5CA, 10, 182, 267.640045f, 107.611687f, 287.824371f, 95, 36)),
            CapturedSurveySpawn(Thief(0x7953AEA5, 5, 115, 72.7292557f, 115.61483f, 313.1308f, 93, 20, useSpawnAsPatrolStart: true, respawnDelaySeconds: 60.0)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AA4A, 10, 182, 198.0572f, 108.416405f, 191.596924f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD40, 6, 110, 148.6321f, 107.6164f, 189.491272f, 93, 18)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD48, 7, 128, 190.403168f, 107.6164f, 164.9011f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD49, 6, 110, 171.154053f, 107.6164f, 164.4986f, 93, 18)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD4A, 7, 128, 160.536346f, 107.6164f, 165.190842f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD4C, 7, 128, 163.605255f, 107.6164f, 167.144913f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD54, 8, 146, 198.14859f, 107.6164f, 163.834351f, 94, 23)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD58, 10, 182, 201.0314f, 107.6164f, 183.943253f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AD76, 10, 182, 282.375244f, 102.8164f, 166.22612f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AF49, 7, 128, 90.4653f, 107.61483f, 245.882523f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7953AFA1, 7, 128, 184.873016f, 107.61483f, 245.969681f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x79557CAC, 10, 182, 273.7663f, 107.611687f, 284.703522f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x7957405C, 7, 128, 166.46637f, 107.6164f, 165.103058f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x795743A7, 10, 182, 197.541138f, 108.416405f, 209.092392f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x795743A8, 10, 182, 199.9471f, 108.416405f, 193.514114f, 95, 27)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E02C, 7, 128, 169.272583f, 107.61483f, 244.71405f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E02E, 7, 128, 163.902328f, 107.6164f, 164.683487f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E123, 6, 110, 149.739487f, 107.61483f, 279.861847f, 93, 18)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E40E, 6, 110, 182.846771f, 107.6164f, 165.3118f, 93, 18)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E5BF, 7, 128, 165.985245f, 107.613258f, 305.1552f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E5C4, 7, 128, 153.280945f, 107.61483f, 277.751068f, 94, 20)),
            CapturedSurveySpawn(ViolentVagabond(0x7957E5C5, 6, 110, 151.613754f, 107.61483f, 280.145721f, 93, 18))
        };

        // Source: capture 20260709-164414 movement-packets.csv. These are complete
        // periodic NpcPath cycles, not the earlier partial samples that snapped when looped.
        private static readonly Dictionary<int, CapturedSubwayPatrolReplaySegment[]> PatrolReplaySegments =
            new Dictionary<int, CapturedSubwayPatrolReplaySegment[]>
            {
                {
                    // Source: completed Thief capture 20260710-205400,
                    // movement-packets.csv rows 602-964 plus the next-cycle boundary.
                    0x7953AEA5,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(4.548876, 79.989151f, 115.764999f, 315.675354f, 71.796402f, 115.61483f, 313.129456f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.366818, 72.9594345f, 115.61483f, 313.497711f, 70.0936584f, 115.61483f, 314.961914f, 24),
                        new CapturedSubwayPatrolReplaySegment(3.349639, 71.1996384f, 115.61483f, 314.249268f, 69.7715149f, 115.61483f, 320.24707f, 24),
                        new CapturedSubwayPatrolReplaySegment(3.334295, 70.0100021f, 115.61483f, 319.04303f, 71.3595734f, 115.61483f, 325.121643f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.331337, 71.0473251f, 115.61483f, 323.888733f, 73.746109f, 115.61483f, 325.728882f, 24),
                        new CapturedSubwayPatrolReplaySegment(8.916981, 72.5469437f, 115.61483f, 325.066711f, 86.765419f, 115.982338f, 322.630432f, 24),
                        new CapturedSubwayPatrolReplaySegment(7.115712, 85.6077423f, 115.614998f, 322.841034f, 95.6155396f, 115.42672f, 316.017395f, 24),
                        new CapturedSubwayPatrolReplaySegment(10.417090, 94.4051971f, 115.885956f, 316.858337f, 78.6982346f, 115.614662f, 315.49176f, 24)
                    }
                },
                {
                    0x7953AF18,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.665506, 90.9275284f, 107.61483f, 248.660339f, 93.7800903f, 107.61483f, 246.556122f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.450008, 93.2423325f, 107.61483f, 247.87915f, 96.4353943f, 107.61483f, 245.377533f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.433567, 95.7327881f, 107.61483f, 245.781525f, 96.4992371f, 107.61483f, 243.006943f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.65, 96.6621399f, 107.61483f, 244.05574f, 94.3061218f, 107.61483f, 240.678986f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.932947, 95.3061066f, 107.61483f, 241.413315f, 91.1917419f, 107.61483f, 239.323898f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.617, 92.3508301f, 107.61483f, 239.737793f, 88.4544373f, 107.61483f, 240.479248f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.433049, 89.2933578f, 107.61483f, 240.028229f, 86.9726868f, 107.61483f, 243.484451f, 25),
                        new CapturedSubwayPatrolReplaySegment(0.617011, 87.3437653f, 107.61483f, 242.336823f, 87.5973511f, 107.61483f, 246.433868f, 25),
                        new CapturedSubwayPatrolReplaySegment(1.149506, 87.2730255f, 107.61483f, 245.358917f, 91.8999939f, 108.604828f, 249.100006f, 25)
                    }
                },
                {
                    0x7953AF57,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.441025, 94.3362808f, 107.61483f, 257.132813f, 95.413147f, 108.601692f, 258.466431f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200505, 94.8347321f, 107.61483f, 257.172943f, 95.6919556f, 107.611687f, 256.584564f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.366001, 95.0244675f, 107.61483f, 257.222717f, 94.1281738f, 107.611687f, 256.459503f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200507, 95.1658936f, 107.61483f, 257.186981f, 94.1888733f, 107.611687f, 257.977692f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.949525, 95.1728973f, 107.61483f, 257.181f, 92.7790985f, 107.611687f, 258.242126f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.684136, 94.0233002f, 107.61483f, 257.701538f, 91.8856354f, 107.611687f, 256.911926f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.4331, 93.0863495f, 107.61483f, 257.547333f, 93.2575684f, 107.611687f, 255.791214f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.466668, 92.7739334f, 107.61483f, 257.126617f, 91.8856354f, 107.611687f, 256.911926f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.233, 92.6570053f, 107.61483f, 256.900421f, 92.7790985f, 107.611687f, 258.242126f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.900008, 92.5241089f, 107.61483f, 256.814789f, 94.1888733f, 107.611687f, 257.977692f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 93.0371857f, 107.61483f, 257.226532f, 94.1281738f, 107.611687f, 256.459503f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.833564, 93.3161469f, 107.61483f, 257.312469f, 95.6919556f, 107.611687f, 256.584564f, 24)
                    }
                },
                {
                    0x79531752,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.250007, 120.377983f, 107.61483f, 238.187988f, 120.357513f, 107.61483f, 238.598038f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.367001, 120.468063f, 107.61483f, 238.436417f, 119.140091f, 107.61483f, 237.279144f, 24),
                        new CapturedSubwayPatrolReplaySegment(2.199574, 120.261276f, 107.61483f, 238.25618f, 120.197006f, 107.61483f, 234.030853f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.883575, 120.148712f, 107.61483f, 235.243713f, 121.031387f, 107.61483f, 232.799698f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.65, 120.531502f, 107.61483f, 233.990005f, 121.690552f, 107.61483f, 232.025986f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 121.015503f, 107.61483f, 233.102234f, 121.300003f, 109.104828f, 231.699997f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.251002, 121.146011f, 107.61483f, 232.840668f, 121.690552f, 107.61483f, 232.025986f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.198999, 121.296318f, 107.61483f, 232.563324f, 121.031387f, 107.61483f, 232.799698f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.099945, 121.39901f, 107.61483f, 232.374069f, 120.197006f, 107.61483f, 234.030853f, 24),
                        new CapturedSubwayPatrolReplaySegment(2.199565, 120.925278f, 107.61483f, 232.96994f, 119.140091f, 107.61483f, 237.279144f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.950509, 119.64579f, 107.61483f, 235.990005f, 120.357513f, 107.61483f, 238.598038f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.683313, 119.875526f, 107.61483f, 237.319199f, 121.137146f, 107.61483f, 239.347321f, 24)
                    }
                },
                {
                    0x79531754,
                    new[]
                    {
                        new CapturedSubwayPatrolReplaySegment(0.750019, 122.448677f, 107.61483f, 236.743958f, 120.128296f, 107.61483f, 236.93544f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.883642, 121.360664f, 107.61483f, 236.610458f, 119.547424f, 107.61483f, 238.470001f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.899767, 120.38768f, 107.61483f, 237.447113f, 120.098579f, 109.104828f, 239.65303f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.233, 120.125732f, 107.61483f, 238.247757f, 119.547424f, 107.61483f, 238.470001f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.617008, 120.0289f, 107.61483f, 238.470566f, 120.128296f, 107.61483f, 236.93544f, 24),
                        new CapturedSubwayPatrolReplaySegment(1.116564, 119.850159f, 107.61483f, 238.367874f, 121.652603f, 107.61483f, 235.707397f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.417505, 120.783905f, 107.61483f, 236.876495f, 122.734451f, 107.61483f, 236.43454f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.665506, 121.274498f, 107.61483f, 236.591461f, 122.990768f, 107.61483f, 237.321808f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.0, 121.894073f, 107.61483f, 236.689957f, 122.526489f, 107.61483f, 238.292267f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.200001, 122.137306f, 107.61483f, 236.882767f, 122.990768f, 107.61483f, 237.321808f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.250007, 122.346909f, 107.61483f, 237.05751f, 122.734451f, 107.61483f, 236.43454f, 24),
                        new CapturedSubwayPatrolReplaySegment(0.683567, 122.601311f, 107.61483f, 237.150742f, 121.652603f, 107.61483f, 235.707397f, 24)
                    }
                }
            };

        public CapturedSubwaySpawnDefinition[] GetSpawnDefinitions()
        {
            var result = new CapturedSubwaySpawnDefinition[SpawnDefinitions.Length];
            Array.Copy(SpawnDefinitions, result, SpawnDefinitions.Length);
            return result;
        }

        public CapturedSubwayPatrolReplaySegment[] GetPatrolReplaySegments(int sourceInstance)
        {
            CapturedSubwayPatrolReplaySegment[] segments;
            if (!PatrolReplaySegments.TryGetValue(sourceInstance, out segments))
            {
                return new CapturedSubwayPatrolReplaySegment[0];
            }

            var result = new CapturedSubwayPatrolReplaySegment[segments.Length];
            Array.Copy(segments, result, segments.Length);
            return result;
        }

        public CapturedSubwayLootDefinition[] GetLootDefinitions()
        {
            // Sources: completed Subway corpse inventory captures.
            // Thief: 20260710-205400, inventory-updates.csv; the one-of-one observed corpse
            // contained one QL1 Stolen Handbag (297055/297055). Mission-state gating remains unknown.
            // Filth Flea: 20260709-210452 and 20260709-220439, inventory-updates.csv
            // correlated by enemy-combat.csv death sequence and enemy-dossier.json monsterData 17657.
            return new[]
            {
                new CapturedSubwayLootDefinition(
                    "Thief",
                    26092,
                    138,
                    297055,
                    297055,
                    1,
                    10000),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    234874,
                    234874,
                    1,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    103110,
                    103111,
                    6,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    101581,
                    101582,
                    6,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    110874,
                    110875,
                    6,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    101507,
                    101508,
                    6,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    202719,
                    202720,
                    14,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    234876,
                    234876,
                    1,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    101761,
                    101762,
                    9,
                    1250),
                new CapturedSubwayLootDefinition(
                    "Filth Flea",
                    17657,
                    138,
                    110192,
                    110193,
                    15,
                    1250)
            };
        }

        private static CapturedSubwaySpawnDefinition FirstLowerSectionSpawn(
            CapturedSubwaySpawnDefinition spawn)
        {
            spawn.ContentSection = "FirstLowerSection";
            return spawn;
        }

        private static CapturedSubwaySpawnDefinition CapturedSurveySpawn(
            CapturedSubwaySpawnDefinition spawn)
        {
            spawn.ContentSection = "Captured20260709Survey";
            return spawn;
        }

        private static CapturedSubwaySpawnDefinition FilthFlea(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int runSpeed = 22)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A096",
                "Filth Flea",
                17657,
                level,
                health,
                130,
                0,
                runSpeed,
                138,
                268964353,
                6,
                5,
                x,
                y,
                z,
                respawnDelaySeconds: 240.0);
        }

        private static CapturedSubwaySpawnDefinition DiscardedPet(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int monsterScale = 94,
            int runSpeed = 33)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A120",
                "Discarded Pet",
                17720,
                level,
                health,
                monsterScale,
                0,
                runSpeed,
                138,
                268980737,
                7,
                5,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition DisobedientBot(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int monsterScale = 94,
            int runSpeed = 33)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A120",
                "Disobedient Bot",
                17649,
                level,
                health,
                monsterScale,
                0,
                runSpeed,
                138,
                268964353,
                7,
                5,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition Mugger(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int monsterScale = 94,
            int runSpeed = 21)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Mugger",
                203734,
                level,
                health,
                monsterScale,
                40705,
                runSpeed,
                138,
                268964353,
                1,
                6,
                x,
                y,
                z);
        }

        private static CapturedSubwaySpawnDefinition Thief(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int monsterScale = 93,
            int runSpeed = 20,
            float? patrolX = null,
            float? patrolY = null,
            float? patrolZ = null,
            bool useSpawnAsPatrolStart = false,
            double? respawnDelaySeconds = null)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Thief",
                26092,
                level,
                health,
                monsterScale,
                40694,
                runSpeed,
                138,
                268964353,
                1,
                6,
                x,
                y,
                z,
                patrolX,
                patrolY,
                patrolZ,
                useSpawnAsPatrolStart,
                respawnDelaySeconds);
        }

        private static CapturedSubwaySpawnDefinition ViolentVagabond(
            int sourceInstance,
            int level,
            int health,
            float x,
            float y,
            float z,
            int monsterScale = 93,
            int runSpeed = 18)
        {
            return new CapturedSubwaySpawnDefinition(
                sourceInstance,
                "A051",
                "Violent Vagabond",
                203733,
                level,
                health,
                monsterScale,
                40676,
                runSpeed,
                3,
                268964353,
                1,
                6,
                x,
                y,
                z);
        }
    }

    internal sealed class CapturedSubwaySpawnDefinition
    {
        public CapturedSubwaySpawnDefinition(
            int sourceInstance,
            string templateHash,
            string name,
            int monsterData,
            int level,
            int health,
            int monsterScale,
            int headMesh,
            int runSpeed,
            int npcFamily,
            int characterFlags,
            int breed,
            int sex,
            float x,
            float y,
            float z,
            float? patrolX = null,
            float? patrolY = null,
            float? patrolZ = null,
            bool useSpawnAsPatrolStart = false,
            double? respawnDelaySeconds = null)
        {
            this.SourceInstance = sourceInstance;
            this.ContentSection = "CapturedPopulation";
            this.TemplateHash = templateHash;
            this.Name = name;
            this.MonsterData = monsterData;
            this.Level = level;
            this.Health = health;
            this.MonsterScale = monsterScale;
            this.HeadMesh = headMesh;
            this.RunSpeed = runSpeed;
            this.NpcFamily = npcFamily;
            this.CharacterFlags = characterFlags;
            this.Breed = breed;
            this.Sex = sex;
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.PatrolX = patrolX;
            this.PatrolY = patrolY;
            this.PatrolZ = patrolZ;
            this.UseSpawnAsPatrolStart = useSpawnAsPatrolStart;
            this.RespawnDelaySeconds = respawnDelaySeconds;
            this.Combat = CapturedSubwayCombatCatalog.For(name, monsterData);
        }

        public int SourceInstance { get; private set; }

        public string ContentSection { get; internal set; }

        public string TemplateHash { get; private set; }

        public string Name { get; private set; }

        public int MonsterData { get; private set; }

        public int Level { get; private set; }

        public int Health { get; private set; }

        public int MonsterScale { get; private set; }

        public int HeadMesh { get; private set; }

        public int RunSpeed { get; private set; }

        public int NpcFamily { get; private set; }

        public int CharacterFlags { get; private set; }

        public int Breed { get; private set; }

        public int Sex { get; private set; }

        public float X { get; private set; }

        public float Y { get; private set; }

        public float Z { get; private set; }

        public float? PatrolX { get; private set; }

        public float? PatrolY { get; private set; }

        public float? PatrolZ { get; private set; }

        public bool UseSpawnAsPatrolStart { get; private set; }

        public double? RespawnDelaySeconds { get; private set; }

        public CapturedEnemyCombatContract Combat { get; private set; }

        public bool HasPatrolWaypoint
        {
            get
            {
                return this.PatrolX.HasValue && this.PatrolY.HasValue && this.PatrolZ.HasValue;
            }
        }

        public bool HasRespawnDelay
        {
            get
            {
                return this.RespawnDelaySeconds.HasValue && this.RespawnDelaySeconds.Value > 0.0;
            }
        }
    }

    internal sealed class CapturedSubwayLootDefinition
    {
        public CapturedSubwayLootDefinition(
            string exactName,
            int monsterData,
            int npcFamily,
            int lowId,
            int highId,
            int quality,
            int observedBasisPoints)
        {
            this.ExactName = exactName;
            this.MonsterData = monsterData;
            this.NpcFamily = npcFamily;
            this.LowId = lowId;
            this.HighId = highId;
            this.Quality = quality;
            this.ObservedBasisPoints = observedBasisPoints;
        }

        public string ExactName { get; private set; }

        public int MonsterData { get; private set; }

        public int NpcFamily { get; private set; }

        public int LowId { get; private set; }

        public int HighId { get; private set; }

        public int Quality { get; private set; }

        public int ObservedBasisPoints { get; private set; }
    }

    internal sealed class CapturedSubwayPatrolReplaySegment
    {
        public CapturedSubwayPatrolReplaySegment(
            double delayAfterSeconds,
            float startX,
            float startY,
            float startZ,
            float endX,
            float endY,
            float endZ,
            byte moveMode)
        {
            this.DelayAfterSeconds = delayAfterSeconds;
            this.StartX = startX;
            this.StartY = startY;
            this.StartZ = startZ;
            this.EndX = endX;
            this.EndY = endY;
            this.EndZ = endZ;
            this.MoveMode = moveMode;
        }

        public double DelayAfterSeconds { get; private set; }

        public float StartX { get; private set; }

        public float StartY { get; private set; }

        public float StartZ { get; private set; }

        public float EndX { get; private set; }

        public float EndY { get; private set; }

        public float EndZ { get; private set; }

        public byte MoveMode { get; private set; }
    }
}
