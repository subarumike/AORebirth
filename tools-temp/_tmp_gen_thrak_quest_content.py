# Generate Thrak garden-key dialogue/quest content pack from capture 20260718-185306.
import json
import os

cap = "20260718-185306"
out_dir = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Content\Thrak\garden-key"
os.makedirs(os.path.join(out_dir, "dialogue"), exist_ok=True)
os.makedirs(os.path.join(out_dir, "quests"), exist_ok=True)


def opt(oid, idx, text, nxt):
    return {
        "Id": oid,
        "Index": idx,
        "Text": text,
        "TextEvidence": cap,
        "NextNodeId": nxt,
        "Conditions": [],
        "Actions": [],
    }


def node(nid, prompt, options):
    return {
        "Id": nid,
        "PromptText": prompt,
        "PromptTextConfidence": "captured-exact",
        "Options": options,
        "EnterActions": [],
    }


veronica_nodes = [
    node(
        "veronica_001",
        "The young woman wrinkles her forehead.\nThey can't be serious...\nYou take a quick look around. Yep, she's talking to you.\nI ask for an assistant... \nand they send me...\nVeronica looks at you with disbelief.\n...something looking like a science experiment sent here from Rubi-Ka!",
        [
            opt("v001_0", 0, "I am one of those science experiments.", "veronica_002"),
            opt("v001_1", 1, "I'm not anyone's assistant. Get lost.", "close"),
            opt("v001_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_002",
        "Ohh.\n\nDid I hurt your feelings, dear?\nYou can tell by the sound of her voice that she really doesn't care.\nI suspect you have the bracer Drake gave you? Let's have a look.\nVeronica seems to have conducted these readings for quite a while. As she quickly programs her wrist-attached DNA-Recompiler to receive the information from your bracer, you can't help wondering what they aim to accomplish with these experiments.",
        [
            opt("v002_0", 0, "Here is the Bracer.", "veronica_bracer_stub"),
            opt("v002_1", 1, "I don't have the Bracer.", "veronica_003"),
            opt("v002_2", 2, "I am not interested.", "close"),
            opt("v002_3", 3, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_bracer_stub",
        "Veronica studies the bracer briefly.",
        [
            opt("vbs_0", 0, "Continue", "veronica_003"),
            opt("vbs_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_003",
        "Veronica taps her foot impatiently as she waits for the DNA-Recompiler to reset.\nI don't have time for this...\nI found a strange and ancient-looking object today.\n\nI am not sure what it might have been used for, nor am I particularly interested in finding out.\nShe proudly lifts her head up and pulls her shoulders back in a well rehearsed pose to look important.\nI'm much too valuable for the project to be jeopardized as a consequence of sheer immature curiosity.\n\nYou on the other hand... are expendable.\n\nAnd since it doesn't look like I'm getting a new assistant anytime soon, you will just have to do.\nWith that, she starts rummaging around her backpack and digs up a strange item. You wonder whether you should take the job?",
        [
            opt("v003_0", 0, "What's the rush?", "veronica_004"),
            opt("v003_1", 1, "I have all the time in the world.", "close"),
            opt("v003_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_004",
        "You do that, and make sure to keep any information you find to yourself.\n\nI would suggest you search West of here. I have heard rumors of a settlement there. Find out anyone there shows interest in this Ancient Device and give it to him for inspection.\n\nReturn to me if you find something important enough to occupy my precious time.",
        [
            opt("v004_0", 0, "I'll see what I can do.", "veronica_005"),
            opt("v004_1", 1, "I am not interested.", "close"),
            opt("v004_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_005",
        "You do that, and make sure to keep any information you find to yourself.",
        [
            opt("v005_0", 0, "What kind of rumors have you heard?", "veronica_rumors"),
            opt("v005_1", 1, "What can you tell me of the symbols you seem to be researching here?", "veronica_symbols"),
            opt("v005_2", 2, "What can you tell me about the Brink?", "veronica_brink"),
            opt("v005_3", 3, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_rumors",
        "I have heard that a settlement has been found west of here.\n\nAnd that the settlement was supposed to be inhabited by powerful creatures, searching for a way to harvest the energies of nature.\n\nBut I haven't had time to explore that part of Nascence. I am sure you will.",
        [
            opt("vr_0", 0, "What can you tell me of the symbols you seem to be researching here?", "veronica_symbols"),
            opt("vr_1", 1, "What can you tell me about the Brink?", "veronica_brink"),
            opt("vr_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_symbols",
        "They refer to someone well versed in the past. His title would be, directly translated, Hypnagogic.\n\nThe one close in touch with the Divine entity is referred to as a Prophet.\n\nThese are all directly translated titles and my knowledge of the ancient alphabet is limited. Even though it is highly unlikely, keep in mind that there is a slight chance of error due to some phonetic variations that might have been lost in the translation.",
        [
            opt("vs_0", 0, "What can you tell me about the Brink?", "veronica_brink"),
            opt("vs_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "veronica_brink",
        "The Brink can be found North of here.\n\nBut since seismic readings from that area are not reliable, you would be wise to keep away.\n\nThe Brink is the very edge of these lands, but I have reached the conclusion that something is causing the edge to dissolve.\n\nI hope Drake can find a solution to this problem soon or we will be in serious danger out here.",
        [opt("vb_0", 0, "Goodbye", "close")],
    ),
]

prophet_nodes = [
    node(
        "prophet_001",
        "Greetings, child of Thrak.\nYou see a gleam of interest in the Prophet's eyes as he notices the ancient device you are carrying.",
        [
            opt("p001_0", 0, "I found this ancient device...", "prophet_trade_device"),
            opt("p001_1", 1, "I don't need any help from you.", "close"),
            opt("p001_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "prophet_trade_device",
        "Let me have a look.",
        [opt("ptd_0", 0, "(Continue after trade)", "prophet_002")],
    ),
    node(
        "prophet_002",
        "Yes. I have seen these before.",
        [
            opt("p002_0", 0, "I need to know more about this ancient item. Can you help me?", "prophet_003"),
            opt("p002_1", 1, "I don't need any help from you.", "close"),
            opt("p002_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "prophet_003",
        "We all have needs, Thenera.\n\nThe question is how much you are willing to sacrifice to have them satisfied.",
        [
            opt("p003_0", 0, "I have nothing to lose, what do you require?", "prophet_004"),
            opt("p003_1", 1, "I don't like the sound of this.", "close"),
            opt("p003_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "prophet_004",
        "Nothing to lose?\nYutt Thrak lowers his voice.\nWe will see about that.\n\nLook around you...\n\nWe were all chosen by the Sacred One. We now devote our lives to the cause and carry out His hallowed commands - whatever they may be.\n\n\nWe are the only ones who can show you the path to grandness. Those most dedicated of His followers may be granted entrance to His Garden and Sanctuary.\n\nOne may recieve incredible powers.\n\nOne may even evolve beyond human comprehension.\n\nBut one may also be destroyed in the blink of an eye, should the Divine decide you are not worthy.",
        [
            opt("p004_0", 0, "I am prepared to follow every command and reap the benefits of my hard work.", "prophet_005"),
            opt("p004_1", 1, "No, thanks. I think I should be leaving now.", "close"),
            opt("p004_2", 2, "Tell me more about that Garden.", "prophet_garden_info"),
            opt("p004_3", 3, "Goodbye", "close"),
        ],
    ),
    node(
        "prophet_garden_info",
        "Words, nothing but words.\n\nShow us proof of your devotion - commit yourself to the Sacred One!\n\nBring before me the mark revealing His existence here in Nascence.\n\nNow leave!",
        [opt("pgi_0", 0, "Goodbye", "close")],
    ),
    node(
        "prophet_005",
        "Words, nothing but words.\n\nShow us proof of your devotion - commit yourself to the Sacred One!\n\nBring before me the mark revealing His existence here in Nascence.\n\nNow leave!",
        [
            opt("p005_0", 0, "I have proof of Thraks Divine presence here in Nascence.", "prophet_trade_insignia"),
            opt("p005_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "prophet_trade_insignia",
        "Really?",
        [opt("pti_0", 0, "(Continue after trade)", "prophet_006")],
    ),
    node(
        "prophet_006",
        "Well done, Thenera. But you have much to prove before Thrak turns his attention your way.\n\nTravel to the statue raised in His glory and place the Insignia by its feet.\n\nYou will be granted passage to His Garden.\n\nThis is where you must continue your journey.\n\nSomeone versed in what has once been, may be able to tell you more about the Ancient Device.\n\nNow leave.\nStay in the shadows, Thenera and may Thrak keep prying eyes away from your path to glory.",
        [opt("p006_0", 0, "Goodbye", "close")],
    ),
    node(
        "prophet_need_insignia",
        "Bring before me the mark revealing His existence here in Nascence.",
        [
            opt("pni_0", 0, "I have proof of Thraks Divine presence here in Nascence.", "prophet_trade_insignia"),
            opt("pni_1", 1, "Goodbye", "close"),
        ],
    ),
]

hyp_nodes = [
    node(
        "hyp_001",
        "At first the Hypnagogic pretends not to see you, but then he notices the artifact.\nYou there!\nAs you approach him, he points at the ancient device.\nHow did you acquire that artifact?",
        [
            opt("h001_0", 0, "That's really none of your business, but make it worth my while and I might tell you.", "hyp_002"),
            opt("h001_1", 1, "It was found by a scientist close to Jobe research area.", "hyp_003"),
            opt("h001_2", 2, "Goodbye", "close"),
        ],
    ),
    node(
        "hyp_002",
        "How dare you?!\n\nYou will tell me right now or I will have you regret ever talking to me!",
        [
            opt("h002_0", 0, "It was found by a scientist close to Jobe research area.", "hyp_003"),
            opt("h002_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "hyp_003",
        "So it is true!\n\nAs it has been foretold, ancient Xan technology has been recovered and once again returned to the Devoted ones.\n\nGive me the artifact.",
        [opt("h003_0", 0, "(Open trade)", "hyp_trade_analyzer")],
    ),
    node(
        "hyp_trade_analyzer",
        "Give me the artifact.",
        [opt("hta_0", 0, "(Continue after trade)", "hyp_004")],
    ),
    node(
        "hyp_004",
        "Yes, this is a powerful artifact indeed.\n\nIt claims the soul of feeble creatures, if one knows how to release its dormant powers.\n\nFind out how utilize this Ancient Artifact and claim three souls in the name of Thrak.\nThe Hypnagogic unconsciously waves you away, whispering to himself.\nI must hurry! All the signs are in place. It is time.\n\nTime for us to act! Yes, I must let them know now.\nHe suddenly notices you still standing here.\nWhat are you waiting for, chosen one? Leave at once!\n Take this insignia of Thrak, it will aid us in our journey to glory.",
        [
            opt("h004_0", 0, "What signs are you talking about?", "hyp_005"),
            opt("h004_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "hyp_005",
        "The signs are all over.\n\nThey are showing us that the time has come. The time to set our grand plan in motion. Staged for centuries upon centuries.\n\nAnd there will be no mercy if we fail them. But the rewards when we succeed will be beyond comprehension!\nThe Hypnagogic turns around, busy preparing for the important events to follow.",
        [opt("h005_0", 0, "Goodbye", "close")],
    ),
    node(
        "hyp_return",
        "At first the Hypnagogic pretends not to see you, but then he notices the artifact.\nYou there!\nAs you approach him, he points at the ancient device.\nHow did you acquire that artifact?",
        [
            opt("hr_0", 0, "I claimed the three souls as you commanded.", "hyp_return_trade"),
            opt("hr_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "hyp_return_trade",
        "The Hypnagogic gives you a sharp look.\nIs that so?\n\nI did feel a massive surge of power, but I must inspect the Artifact before I can decide whether you deserve a reward.",
        [opt("hrt_0", 0, "(Open trade)", "hyp_return_after")],
    ),
    node(
        "hyp_return_after",
        "Very well, Thenera. The Artifact seems to be in perfect order.\n\nWe entrust you to keep it safe and out of reach from Aban, cursed be her name and her treacherous followers.\n\nYour reward, sanctified by Thrak, will be this sacred key. Use it to gain safe passage to this place, but use it with care.\n\nThrak will not look lightly upon misuse of such a powerful gift...\nThe Hypnagogic is muttering to himself, not paying you any attention.",
        [opt("hra_0", 0, "Goodbye", "close")],
    ),
]

silver_nodes = [
    node(
        "silver_001",
        "The creature looks at you with wide eyes.",
        [
            opt("s001_0", 0, "'Cover her eyes with the ancient device'", "silver_trade"),
            opt("s001_1", 1, "Goodbye", "close"),
        ],
    ),
    node(
        "silver_trade",
        "The creature stops and studies you for a couple of seconds as you look through your backpack. You reach out to cover her eyes with the ancient device.\nThe Silvertail starts to change shape as soon as you place the device close to her eyes.",
        [opt("st_0", 0, "Goodbye", "close")],
    ),
]

pack = {
    "Identity": {
        "Id": "thrak-garden-key-dialogue-20260718-185306",
        "Version": "capture-backed-1",
        "Source": "capture 20260718-185306",
    },
    "SourceCaptures": [cap],
    "Npcs": [
        {
            "Id": "scientist_veronica_787B54B2",
            "NpcIdentity": "SimpleChar:787B54B2",
            "Name": "Scientist Veronica Escobar",
            "RootNodeId": "veronica_001",
            "Aliases": [],
            "Nodes": veronica_nodes,
            "Conditions": [],
            "Actions": [],
        },
        {
            "Id": "prophet_yutt_78D280F6",
            "NpcIdentity": "SimpleChar:78D280F6",
            "Name": "Prophet Yutt Thrak",
            "RootNodeId": "prophet_001",
            "Aliases": [],
            "Nodes": prophet_nodes,
            "Conditions": [],
            "Actions": [],
        },
        {
            "Id": "hypnagogic_urga_lum_79758F3A",
            "NpcIdentity": "SimpleChar:79758F3A",
            "Name": "Hypnagogic Urga-Lum Thrak",
            "RootNodeId": "hyp_001",
            "Aliases": [],
            "Nodes": hyp_nodes,
            "Conditions": [],
            "Actions": [],
        },
        {
            "Id": "dreaming_silvertail",
            "NpcIdentity": "SimpleChar:797652A0",
            "Name": "Dreaming Silvertail",
            "RootNodeId": "silver_001",
            "Aliases": ["SimpleChar:797652F5", "SimpleChar:797652A7"],
            "Nodes": silver_nodes,
            "Conditions": [],
            "Actions": [],
        },
    ],
}

quests = {
    "Identity": {
        "Id": "thrak-garden-key-quests-20260718-185306",
        "Version": "capture-backed-1",
        "Source": "capture 20260718-185306",
    },
    "SourceCaptures": [cap],
    "Quests": [
        {
            "QuestId": "Mission:5556893A",
            "Title": "Ancient Device Inspection",
            "TitleConfidence": "decoded-from-QuestFullUpdate",
            "SourceNpcIdentity": "SimpleChar:787B54B2",
            "InitialStepId": "find_settlement",
            "Steps": [
                {
                    "StepId": "find_settlement",
                    "Name": "Find western settlement interested in the Ancient Device",
                    "Objectives": [
                        {
                            "ObjectiveId": "mission_5556893A_find",
                            "Type": "CapturedDialogueObjective",
                            "Description": "Search west of Veronica and show the Ancient Pattern Analyzer to interested parties.",
                            "TargetIdentity": "SimpleChar:78D280F6",
                            "RequiredCount": 1,
                            "Conditions": [],
                        }
                    ],
                    "Conditions": [],
                    "Actions": [],
                }
            ],
            "Conditions": [],
            "Actions": [],
            "UnresolvedFields": [],
        },
        {
            "QuestId": "Mission:55563C16",
            "Title": "Proof of Thrak",
            "TitleConfidence": "decoded-from-QuestFullUpdate",
            "SourceNpcIdentity": "SimpleChar:78D280F6",
            "InitialStepId": "bring_insignia",
            "Steps": [
                {
                    "StepId": "bring_insignia",
                    "Name": "Bring Insignia of Thrak to Prophet Yutt Thrak",
                    "Objectives": [
                        {
                            "ObjectiveId": "mission_55563C16_insignia",
                            "Type": "CapturedInventoryDeliveryObjective",
                            "Description": "Deliver Insignia of Thrak to Prophet Yutt Thrak.",
                            "TargetIdentity": "SimpleChar:78D280F6",
                            "RequiredCount": 1,
                            "Conditions": [],
                        }
                    ],
                    "Conditions": [],
                    "Actions": [],
                }
            ],
            "Conditions": [],
            "Actions": [],
            "UnresolvedFields": [],
        },
        {
            "QuestId": "Mission:55563C18",
            "Title": "Thrak Garden Passage",
            "TitleConfidence": "decoded-from-QuestFullUpdate",
            "SourceNpcIdentity": "SimpleChar:78D280F6",
            "InitialStepId": "enter_garden",
            "Steps": [
                {
                    "StepId": "enter_garden",
                    "Name": "Place Insignia at Thrak statue and enter His Garden",
                    "Objectives": [
                        {
                            "ObjectiveId": "mission_55563C18_garden",
                            "Type": "CapturedDialogueObjective",
                            "Description": "Use Insignia of Thrak on the Thrak statue, then speak with Hypnagogic Urga-Lum Thrak.",
                            "TargetIdentity": "SimpleChar:79758F3A",
                            "RequiredCount": 1,
                            "Conditions": [],
                        }
                    ],
                    "Conditions": [],
                    "Actions": [],
                }
            ],
            "Conditions": [],
            "Actions": [],
            "UnresolvedFields": [],
        },
        {
            "QuestId": "Mission:5556591A",
            "Title": "Claim Three Souls",
            "TitleConfidence": "decoded-from-QuestFullUpdate",
            "SourceNpcIdentity": "SimpleChar:79758F3A",
            "InitialStepId": "claim_souls",
            "Steps": [
                {
                    "StepId": "claim_souls",
                    "Name": "Release the power of the Ancient Artifact and claim three souls",
                    "Objectives": [
                        {
                            "ObjectiveId": "mission_5556591A_souls",
                            "Type": "CapturedKillObjective",
                            "Description": "Cover Dreaming Silvertail eyes with the favored Ancient Pattern Analyzer and claim three cursed souls.",
                            "TargetIdentity": "Dreaming Silvertail",
                            "RequiredCount": 3,
                            "Conditions": [],
                        }
                    ],
                    "Conditions": [],
                    "Actions": [],
                }
            ],
            "Conditions": [],
            "Actions": [],
            "UnresolvedFields": [],
        },
        {
            "QuestId": "Mission:5556893D",
            "Title": "Return the Artifact",
            "TitleConfidence": "decoded-from-QuestFullUpdate",
            "SourceNpcIdentity": "SimpleChar:79758F3A",
            "InitialStepId": "return_artifact",
            "Steps": [
                {
                    "StepId": "return_artifact",
                    "Name": "Return the charged artifact to Hypnagogic Urga-Lum Thrak",
                    "Objectives": [
                        {
                            "ObjectiveId": "mission_5556893D_return",
                            "Type": "CapturedInventoryDeliveryObjective",
                            "Description": "Trade the favored Ancient Pattern Analyzer back to Hypnagogic Urga-Lum Thrak for the sacred garden key.",
                            "TargetIdentity": "SimpleChar:79758F3A",
                            "RequiredCount": 1,
                            "Conditions": [],
                        }
                    ],
                    "Conditions": [],
                    "Actions": [],
                }
            ],
            "Conditions": [],
            "Actions": [],
            "UnresolvedFields": [],
        },
    ],
    "Links": [],
}

manifest = {
    "DialoguePacks": ["dialogue/thrak-garden-key.dialogue.json"],
    "QuestPacks": ["quests/thrak-garden-key.quests.json"],
}

with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
    json.dump(manifest, f, indent=2)
with open(
    os.path.join(out_dir, "dialogue", "thrak-garden-key.dialogue.json"),
    "w",
    encoding="utf-8",
    newline="\n",
) as f:
    json.dump(pack, f, indent=2, ensure_ascii=False)
with open(
    os.path.join(out_dir, "quests", "thrak-garden-key.quests.json"),
    "w",
    encoding="utf-8",
    newline="\n",
) as f:
    json.dump(quests, f, indent=2, ensure_ascii=False)

print("wrote", out_dir)
