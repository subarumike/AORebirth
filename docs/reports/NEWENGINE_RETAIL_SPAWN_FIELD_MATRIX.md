# Retail player spawn field matrix

Generated deterministically from the Legacy producer, NewEngine producer and required-stat contract. Legacy presence is implementation evidence, not a retail requirement. Source hashes and every entry are in the adjacent JSON.

| Stat | Legacy source/group | NewEngine group | DAO/data source | Retail required? | Current result | Action |
|---|---|---|---|---|---|---|
| Flags (0) | 255/G1 | 1 | LoadStats(0) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| MaxHealth (1) | 379/G2 | 2 | LoadStats(1) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Mass (2) | absent | 2 | LoadStats(2) | UNKNOWN - no retail necessity established | INTENTIONAL_DIFFERENCE | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AttackSpeed (3) | 538/G2 | 2 | LoadStats(3) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Breed (4) | 430/G2 | 2 | LoadStats(4) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Team (6) | 427/G2 | 2 | LoadStats(6) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| State (7) | 153/G1 | 1 | LoadStats(7) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapFlags (9) | 870/G4 | 4 | LoadStats(9) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ProfessionLevel (10) | 803/G3 | 3 | LoadStats(10) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PreviousHealth (11) | 460/G2 | 2 | LoadStats(11) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Mesh (12) | 418/G2 | 2 | LoadStats(12) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Strength (16) | 397/G2 | 2 | LoadStats(16) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Agility (17) | 394/G2 | 2 | LoadStats(17) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Stamina (18) | 391/G2 | 2 | LoadStats(18) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Intelligence (19) | 388/G2 | 2 | LoadStats(19) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Sense (20) | 385/G2 | 2 | LoadStats(20) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Psychic (21) | 382/G2 | 2 | LoadStats(21) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Health (27) | 376/G2 | 2 | LoadStats(27) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Side (33) | 469/G2 | 2 | LoadStats(33) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| DeadTimer (34) | 424/G2 | 2 | LoadStats(34) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| TitleLevel (37) | 688/G2 | 2 | LoadStats(37) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| AlienXP (40) | 793/G2 | 2 | LoadStats(40) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BeltSlots (45) | 821/G3 | 3 | LoadStats(45) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Fatness (47) | 812/G3 | 3 | LoadStats(47) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| InsuranceTime (49) | 858/G4 | 4 | LoadStats(49) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AggDef (51) | 412/G2 | 2 | LoadStats(51) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| XP (52) | 448/G2 | 2 | LoadStats(52) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| IP (53) | 451/G2 | 2 | LoadStats(53) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Level (54) | 445/G2 | 2 | LoadStats(54) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| LastXP (57) | 442/G2 | 2 | LoadStats(57) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Age (58) | 466/G2 | 2 | LoadStats(58) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Sex (59) | 433/G2 | 2 | LoadStats(59) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Profession (60) | 409/G2 | 2 | LoadStats(60) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Cash (61) | 406/G2 | 2 | LoadStats(61) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AlignmentClanTokens (62) | 403/G2,682/G2 | 2 | LoadStats(62) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Attitude (63) | 400/G2 | 2 | LoadStats(63) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| HairTexture (65) | 309/G1 | absent | LoadStats(65) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 66 (66) | 312/G1 | absent | LoadStats(66) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| HairColourRGB (67) | 315/G1 | absent | LoadStats(67) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| NumConstructedQuest (68) | 358/G2 | absent | LoadStats(68) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| MaxConstructedQuest (69) | 361/G2 | absent | LoadStats(69) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| ItemType (72) | 457/G2 | 2 | LoadStats(72) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| StrainOmniTokens (75) | 685/G2 | absent | LoadStats(75) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| CurrentMass (78) | 454/G2 | 2 | LoadStats(78) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Icon (79) | 415/G2 | 2 | LoadStats(79) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Race (89) | 815/G3 | 3 | LoadStats(89) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| ProjectileAC (90) | 715/G2 | 2 | LoadStats(90) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MeleeAC (91) | 712/G2 | 2 | LoadStats(91) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| EnergyAC (92) | 709/G2 | 2 | LoadStats(92) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ChemicalAC (93) | 706/G2 | 2 | LoadStats(93) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| RadiationAC (94) | 487/G2,703/G2 | 2 | LoadStats(94) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ColdAC (95) | 700/G2 | 2 | LoadStats(95) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PoisonAC (96) | 697/G2 | 2 | LoadStats(96) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| FireAC (97) | 694/G2 | 2 | LoadStats(97) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MartialArts (100) | 679/G2 | 2 | LoadStats(100) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MultiMelee (101) | 478/G2 | 2 | LoadStats(101) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| _1hBlunt (102) | 676/G2 | 2 | LoadStats(102) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| _1hEdged (103) | 673/G2 | 2 | LoadStats(103) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MeleeEnergy (104) | 670/G2 | 2 | LoadStats(104) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Skill2hEdged (105) | 667/G2 | 2 | LoadStats(105) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Piercing (106) | 664/G2 | 2 | LoadStats(106) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| _2hBlunt (107) | 661/G2 | 2 | LoadStats(107) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SharpObject (108) | 658/G2 | 2 | LoadStats(108) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Grenade (109) | 655/G2 | 2 | LoadStats(109) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| HeavyWeapons (110) | 652/G2 | 2 | LoadStats(110) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Bow (111) | 649/G2 | 2 | LoadStats(111) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Pistol (112) | 646/G2 | 2 | LoadStats(112) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Rifle (113) | 643/G2 | 2 | LoadStats(113) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MGSMG (114) | 640/G2 | 2 | LoadStats(114) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Shotgun (115) | 637/G2 | 2 | LoadStats(115) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AssaultRifle (116) | 634/G2 | 2 | LoadStats(116) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| VehicleWater (117) | 475/G2 | 2 | LoadStats(117) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MeleeInit (118) | 631/G2 | 2 | LoadStats(118) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| RangedInit (119) | 628/G2 | 2 | LoadStats(119) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PhysicalInit (120) | 625/G2 | 2 | LoadStats(120) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BowSpecialAttack (121) | 493/G2 | 2 | LoadStats(121) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SensoryImprovement (122) | 490/G2 | 2 | LoadStats(122) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| FirstAid (123) | 622/G2 | 2 | LoadStats(123) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Treatment (124) | 619/G2 | 2 | LoadStats(124) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MechanicalEngineering (125) | 616/G2 | 2 | LoadStats(125) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ElectricalEngineering (126) | 613/G2 | 2 | LoadStats(126) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MaterialMetamorphosis (127) | 610/G2 | 2 | LoadStats(127) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BiologicalMetamorphosis (128) | 607/G2 | 2 | LoadStats(128) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PsychologicalModification (129) | 604/G2 | 2 | LoadStats(129) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MaterialCreation (130) | 601/G2 | 2 | LoadStats(130) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SpaceTime (131) | 598/G2 | 2 | LoadStats(131) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| NanoPool (132) | 595/G2 | 2 | LoadStats(132) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| RangedEnergy (133) | 484/G2 | 2 | LoadStats(133) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MultiRanged (134) | 481/G2 | 2 | LoadStats(134) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| TrapDisarm (135) | 592/G2 | 2 | LoadStats(135) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Perception (136) | 589/G2 | 2 | LoadStats(136) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Adventuring (137) | 586/G2 | 2 | LoadStats(137) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Swimming (138) | 583/G2 | 2 | LoadStats(138) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| VehicleAir (139) | 505/G2 | 2 | LoadStats(139) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapNavigation (140) | 502/G2 | 2 | LoadStats(140) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Tutoring (141) | 580/G2 | 2 | LoadStats(141) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Brawl (142) | 577/G2 | 2 | LoadStats(142) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Riposte (143) | 574/G2 | 2 | LoadStats(143) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Dimach (144) | 571/G2 | 2 | LoadStats(144) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Parry (145) | 568/G2 | 2 | LoadStats(145) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SneakAttack (146) | 565/G2 | 2 | LoadStats(146) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| FastAttack (147) | 562/G2 | 2 | LoadStats(147) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Burst (148) | 496/G2 | 2 | LoadStats(148) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| NanoCInit (149) | 559/G2 | 2 | LoadStats(149) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| FlingShot (150) | 556/G2 | 2 | LoadStats(150) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AimedShot (151) | 553/G2 | 2 | LoadStats(151) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BodyDevelopment (152) | 550/G2 | 2 | LoadStats(152) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| DuckExp (153) | 547/G2 | 2 | LoadStats(153) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| DodgeRanged (154) | 544/G2 | 2 | LoadStats(154) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| EvadeClsC (155) | 541/G2 | 2 | LoadStats(155) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| RunSpeed (156) | 421/G2 | 2 | LoadStats(156) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| QuantumFT (157) | 535/G2 | 2 | LoadStats(157) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| WeaponSmithing (158) | 532/G2 | 2 | LoadStats(158) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Pharmaceuticals (159) | 529/G2 | 2 | LoadStats(159) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| NanoProgramming (160) | 526/G2 | 2 | LoadStats(160) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ComputerLiteracy (161) | 523/G2 | 2 | LoadStats(161) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Psychology (162) | 520/G2 | 2 | LoadStats(162) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Chemistry (163) | 517/G2 | 2 | LoadStats(163) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Concealment (164) | 514/G2 | 2 | LoadStats(164) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BreakingEntry (165) | 511/G2 | 2 | LoadStats(165) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| VehicleGround (166) | 508/G2 | 2 | LoadStats(166) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| FullAuto (167) | 499/G2 | 2 | LoadStats(167) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| NanoResist (168) | 273/G1 | absent | LoadStats(168) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| AlienLevel (169) | 787/G2 | 2 | LoadStats(169) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| CurrentMovementMode (173) | 809/G3 | 3 | LoadStats(173) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PrevMovementMode (174) | 806/G3 | 3 | LoadStats(174) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AlienNextXP (178) | 790/G2 | 2 | LoadStats(178) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MaxNCU (181) | 867/G4 | 4 | LoadStats(181) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Specialization (182) | 177/G1 | 1 | LoadStats(182) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| LastConcretePlayfieldInstance (191) | 279/G1 | 1 | LoadStats(191) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| InPlay (194) | 373/G2 | 2 | LoadStats(194) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SessionTime (198) | 327/G1 | 1 | LoadStats(198) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| RP (199) | 718/G2 | 2 | LoadStats(199) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| TeamSide (213) | 818/G3 | 3 | LoadStats(213) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| CurrentNano (214) | 275/G1,861/G4 | 1,4 | LoadStats(214) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| GmLevel (215) | 691/G2 | 2 | LoadStats(215) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MaxNanoEnergy (221) | 276/G1,864/G4 | 1 | LoadStats(221) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| Features (224) | 258/G1 | absent | LoadStats(224) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| InsurancePercentage (236) | 800/G3 | 3 | LoadStats(236) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ChangeSideCount (237) | 873/G4 | 4 | LoadStats(237) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbProjectileAC (238) | 828/G4 | 4 | LoadStats(238) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbMeleeAC (239) | 831/G4 | 4 | LoadStats(239) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbEnergyAC (240) | 834/G4 | 4 | LoadStats(240) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbChemicalAC (241) | 837/G4 | 4 | LoadStats(241) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbRadiationAC (242) | 840/G4 | 4 | LoadStats(242) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbColdAC (243) | 843/G4 | 4 | LoadStats(243) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbFireAC (244) | 849/G4 | 4 | LoadStats(244) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbPoisonAC (245) | 852/G4 | 4 | LoadStats(245) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| AbsorbNanoAC (246) | 846/G4 | 4 | LoadStats(246) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| TemporarySkillReduction (247) | 855/G4 | 4 | LoadStats(247) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MissionBits1 (256) | 297/G1 | 1 | LoadStats(256) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MissionBits2 (257) | 300/G1 | 1 | LoadStats(257) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PersonalResearchLevel (263) | 333/G1 | 1 | LoadStats(263) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| GlobalResearchLevel (264) | 336/G1 | 1 | LoadStats(264) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PersonalResearchGoal (265) | 339/G1 | 1 | LoadStats(265) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| GlobalResearchGoal (266) | 342/G1 | 1 | LoadStats(266) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| XPKillRange (275) | 370/G2 | 2 | LoadStats(275) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Members (300) | 351/G1 | 1 | LoadStats(300) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanUpkeep (303) | 303/G1 | absent | LoadStats(303) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| SavedXP (334) | 252/G1 | 1 | LoadStats(334) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| 348 (348) | 721/G2 | absent | LoadStats(348) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 349 (349) | 330/G1,367/G2 | absent | LoadStats(349) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| NextXP (350) | 439/G2 | 2 | LoadStats(350) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| NanoFocusLevel (355) | 174/G1 | 1 | LoadStats(355) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Scale (360) | 267/G1 | 1 | LoadStats(360) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| VisualProfession (368) | 270/G1 | 1 | LoadStats(368) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| LastSaveXP (372) | 436/G2 | 2 | LoadStats(372) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| Expansion (389) | 733/G2 | 2 | LoadStats(389) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| UnarmedTemplateInstance (418) | 156/G1 | 1 | LoadStats(418) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| CurrentState (423) | 463/G2 | 2 | LoadStats(423) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| WaitState (430) | 472/G2 | 2 | LoadStats(430) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ErrorCode (432) | 306/G1 | absent | LoadStats(432) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| MapOptions (470) | 282/G1 | 1 | LoadStats(470) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapsA (471) | 285/G1 | 1 | LoadStats(471) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapsB (472) | 288/G1 | 1 | LoadStats(472) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SocialStatus (521) | 189/G1,775/G2 | 1,2 | LoadStats(521) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ShadowBreed (532) | 183/G1 | 1 | LoadStats(532) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| 544 (544) | 318/G1 | absent | LoadStats(544) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 545 (545) | 321/G1 | absent | LoadStats(545) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| OTArmedForces (560) | 772/G2 | 2 | LoadStats(560) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanSentinels (561) | 769/G2 | 2 | LoadStats(561) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| OTMed (562) | 766/G2 | 2 | LoadStats(562) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanGaia (563) | 763/G2 | 2 | LoadStats(563) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| OTTrans (564) | 760/G2 | 2 | LoadStats(564) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanVanguards (565) | 757/G2 | 2 | LoadStats(565) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| GOS (566) | 754/G2 | 2 | LoadStats(566) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| OTFollowers (567) | 751/G2 | 2 | LoadStats(567) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| OTOperator (568) | 748/G2 | 2 | LoadStats(568) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| OTUnredeemed (569) | 745/G2 | 2 | LoadStats(569) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanDevoted (570) | 742/G2 | 2 | LoadStats(570) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanConserver (571) | 739/G2 | 2 | LoadStats(571) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ClanRedeemed (572) | 736/G2 | 2 | LoadStats(572) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| SK (573) | 724/G2 | 2 | LoadStats(573) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| LastSK (574) | 727/G2 | absent | LoadStats(574) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| NextSK (575) | 730/G2 | absent | LoadStats(575) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| PlayerOptions (576) | 192/G1 | 1 | LoadStats(576) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| LastPerkResetTime (577) | 186/G1 | 1 | LoadStats(577) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ShadowBreedTemplate (579) | 180/G1 | 1 | LoadStats(579) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ApartmentsAllowed (582) | 261/G1 | 1 | LoadStats(582) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| ApartmentsOwned (583) | 264/G1 | 1 | LoadStats(583) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapsC (585) | 291/G1 | 1 | LoadStats(585) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| MapsD (586) | 294/G1 | 1 | LoadStats(586) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| UnsavedXP (592) | 171/G1 | 1 | LoadStats(592) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| 594 (594) | 195/G1 | absent | LoadStats(594) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 595 (595) | 198/G1 | absent | LoadStats(595) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 596 (596) | 201/G1 | absent | LoadStats(596) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 597 (597) | 204/G1 | absent | LoadStats(597) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| PlayerID (607) | 778/G2 | 2 | LoadStats(607) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| InvadersKilled (615) | 159/G1,784/G2 | 1,2 | LoadStats(615) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| KilledByInvaders (616) | 162/G1,781/G2 | 1,2 | LoadStats(616) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| 617 (617) | 324/G1 | absent | LoadStats(617) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 618 (618) | 325/G1 | absent | LoadStats(618) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| 619 (619) | 326/G1 | absent | LoadStats(619) | UNKNOWN - no retail necessity established | MISSING_NEWENGINE | Unresolved retail necessity; no speculative emission |
| AccountFlags (660) | 165/G1 | 1 | LoadStats(660) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BattlestationSide (668) | 345/G1 | 1 | LoadStats(668) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| VP (669) | 168/G1 | 1 | LoadStats(669) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| BattlestationRep (670) | 348/G1 | 1 | LoadStats(670) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PaidPoints (672) | 364/G2 | 2 | LoadStats(672) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| VisualFlags (673) | 207/G1 | 1 | LoadStats(673) | YES - current spawn consumer/derivation | MATCH | Required persisted base and validated effective state |
| PVPDuelKills (674) | 210/G1 | 1 | LoadStats(674) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPDuelDeaths (675) | 213/G1 | 1 | LoadStats(675) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPProfessionDuelKills (676) | 216/G1 | 1 | LoadStats(676) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPProfessionDuelDeaths (677) | 219/G1 | 1 | LoadStats(677) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPRankedSoloKills (678) | 222/G1 | 1 | LoadStats(678) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPRankedSoloDeaths (679) | 225/G1 | 1 | LoadStats(679) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPRankedTeamKills (680) | 228/G1 | 1 | LoadStats(680) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPRankedTeamDeaths (681) | 231/G1 | 1 | LoadStats(681) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPSoloScore (682) | 234/G1 | 1 | LoadStats(682) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPTeamScore (683) | 237/G1 | 1 | LoadStats(683) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |
| PVPDuelScore (684) | 240/G1 | 1 | LoadStats(684) | UNKNOWN - no retail necessity established | MATCH | Emit only initialized state; omit absent optional tuple; reject sentinel |

## FullCharacter structural fields

- MsgVersion: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- InventorySlots: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- UploadedNanoIds: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown2: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown3: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown4: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- UnknownI2: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown5: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- UnknownI3: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown6: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Stats1: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Stats2: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Stats3: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Stats4: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown9: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown10: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown11: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown12: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
- Unknown13: existing AOtomation count/conditional codec; identity, inventory and nano arrays retain their DAO owners. Unknown sections retain the existing shape; semantics remain unproven.
